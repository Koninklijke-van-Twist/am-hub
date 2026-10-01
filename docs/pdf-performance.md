# Offerte-pdf: snelheid en controle

De pdf haalt altijd actuele documentgegevens uit Business Central op. Offerte en configuratiematrix lopen parallel; configuratieregels en details eveneens. Klant- en verkopergegevens starten zodra de offerte bekend is. Voor de klant worden uitsluitend naam en adres opgevraagd, zonder financiële velden. De matrix indexeert prijzen per taak/component en behoudt de eerste passende regel, inclusief nulprijzen.

Templates en base64-afbeeldingen worden gedeeld bewaard met een limiet van 64 MiB. Bestandsgrootte en wijzigingstijd worden bij iedere aanvraag gecontroleerd. Een gewijzigd template of foto wordt opnieuw ingelezen. De oorspronkelijke afbeeldingen en hun resolutie blijven behouden.

Chromium start op de achtergrond bij applicatiestart en wordt hergebruikt. Iedere render krijgt een geïsoleerde context. `SetContentAsync` wacht op `Load` en de footer wacht op geladen fonts; de extra netwerkstilte-wachttijd vervalt. Zie [Playwright SetContentAsync](https://playwright.dev/dotnet/docs/api/class-page#page-set-content). Annulering sluit de context en stopt het browserwerk.

Een SHA-256-hash van de volledige HTML bepaalt of een bestaande pdf kan worden hergebruikt. Veranderde bedragen, contactgegevens, teksten, afbeeldingen of CSS leiden tot een nieuwe render. Verse BC-data wordt ook bij een cachehit opgehaald. Gelijktijdige identieke renders worden samengevoegd. De pdf-cache is maximaal 32 MiB en bewaart bestanden maximaal 10 minuten; grote bestanden worden gewoon gegenereerd, maar passen mogelijk niet in de cache.

## Metingen

Lokale Release-test met dezelfde fictieve offerte, 12 componenten en 16 pdf-pagina's, zonder netwerkverkeer naar BC:

| Aanvraag | Voor | Na |
| --- | ---: | ---: |
| Eerste pdf, koude browser | 5374 ms | 4858 ms |
| Identieke pdf, tweede aanvraag | 2897 ms | 24 ms |
| Identieke pdf, derde aanvraag | 2691 ms | 22 ms |

De pdf voor en na is byte-identiek wanneer alleen `CreationDate` en `ModDate` buiten beschouwing worden gelaten. Dit controleert ook afbeeldingen, paginering en inhoud van het 16-pagina-document. Het vooraf starten van Chromium is niet meegerekend in de koude test hierboven.

De kalendercontrole met gesimuleerde BC-latentie van 40 ms per aanvraag mat 149 ms voor de eerste aanvraag en 48 ms bij herhaling. Klant-/werkorderaanvragen gingen van twee naar nul; de autorisatieaanvraag bleef actief. Dit is een simulatie, geen productiebenchmark. Werkelijke winst hangt af van BC-responstijd, hoeveelheid gegevens en cachehits.

## Herhalen

Vanuit de repositoryroot:

```text
dotnet run --project tests/CalendarChecks -c Release
dotnet run --project tests/PdfChecks -c Release -- --documents-only
dotnet run --project tests/PdfChecks -c Release -- optimized
```

De volledige pdf-test vereist de bij Microsoft.Playwright passende geïnstalleerde Chromium-browser en toestemming om een browserproces te starten. Voorbeeldbestanden staan in `.artifacts/pdf-checks/` (genegeerd door Git). Tests controleren parallel ophalen, prijsvoorrang, totalen, actuele documentdata, invalidatie, samenvoegen van gelijktijdige renders en annulering.

Productielogs scheiden voor pdf's `gegevens`, `HTML` en `PDF/cache` in milliseconden. Kalenderlogs tonen de duur per laadstap. Daarmee kunnen de resterende BC-wachttijden op de daadwerkelijke installatie worden gemeten.
