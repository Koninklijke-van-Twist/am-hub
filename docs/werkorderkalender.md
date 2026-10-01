# Mijn werkorders

Pagina: `/werkorders` (productie: `/dotnet/am-hub/werkorders`).

## Accountmanager en gegevens

De server leest het e-mailadres uit de geauthenticeerde Entra-claims: email, preferred_username of upn. Iedere laadactie controleert `SalesPersonCard` op precies één overeenkomst met een geldige, niet-geblokkeerde code. Deze autorisatiecontrole wordt **niet gecachet**, ook niet bij terugbladeren. Ontbrekende, dubbele of geblokkeerde verkoperskaarten geven een melding voordat klant- of werkordergegevens worden gebruikt.

De eigen code is standaard geselecteerd. Gekoppelde gebruikers mogen ook AW, LL, FR, YB, NVD, NZ, FJ, MS en WM kiezen; de server valideert deze lijst. `AppCustomerCard.LVS_After_Sales_Person_Code` bepaalt de klantkoppeling. `Project_Manager` wordt daarvoor niet gebruikt.

Daarna worden de werkorders rechtstreeks uit `LVS_MainWorkOrderCard` opgehaald, met alleen de velden voor de kalender. `No` is het werkordernummer. Klant- en datumfilters en `Status ne 'Open'` worden in BC toegepast. Aanvullende lokale controles weren verkeerde klanten, ongeldige datums en lege/open statussen. Er is geen omweg via projecten of een tweede werkorderendpoint.

Begindatum `0001-01-01` is ongeldig. De einddatum is inclusief; de bovengrens van de periode is exclusief. Een lege/minimale einddatum betekent doorlopend. De kalender toont één week. De service ondersteunt perioden tot 42 dagen.

## Snelheid en actualiteit

- Eerste HTML verschijnt direct; BC-aanvragen starten na de eerste interactieve render. Prerendering veroorzaakt geen dubbele aanvraag.
- **Klant–accountmanagerkoppelingen** worden maximaal 24 uur in het servergeheugen bewaard, gedeeld tussen geautoriseerde gebruikers. De sleutel bevat het volledige klantqueryadres (inclusief bedrijf, manager en koppelveld), een hash van de BC-serviceaccountgegevens en de URL-limiet. Een herstart leegt deze cache. De cache bewaart maximaal 100 kleine lijsten of 100.000 klantnummers; grotere lijsten worden wel volledig opgehaald.
- Bij gegevens jonger dan vijf minuten is geen klantaanvraag nodig. Tussen vijf minuten en 24 uur wordt eerst de bestaande klantenlijst gebruikt om de werkorders te tonen. **Daarna** controleert de pagina de koppelingen op de achtergrond. Alleen bij een gewijzigde verzameling klantnummers wordt de zichtbare kalender opnieuw geladen, met behoud van dag, manager en waar mogelijk lijstpagina. Er draait geen timer: een nieuwe laadactie start de controle.
- Bij een lege of minstens 24 uur oude cache wordt eerst een actuele klantenlijst opgehaald. Een mislukte controle verlengt de bewaartermijn niet. Mislukt een achtergrondcontrole, dan blijft de al getoonde lijst staan met een melding; bij een mislukte eerste laadactie verschijnt een foutmelding.
- Gelijktijdige aanvragen voor dezelfde klantkoppeling delen één BC-controle. Een geannuleerde pagina stopt haar eigen wachttijd, niet de controle voor andere gebruikers. Volledig opgehaalde resultaten van deze gedeelde controle mogen in de cache worden opgeslagen. Fouten en gedeeltelijke klantenlijsten nooit.
- **Werkorderqueries** blijven maximaal 30 seconden bewaard binnen één Blazor-circuit, gescheiden op gebruikersmail en volledig queryadres. Datum, bedrijf en klantfilters zitten in die sleutel. Maximaal 128 queryresultaten en 50.000 rijen blijven per circuit bewaard.
- `Verversen` wist de werkordercache en wacht direct op een actuele klantenlijst, ongeacht de leeftijd. Een al lopende controle mag daarvoor worden gedeeld. Daarna worden de werkorders opnieuw opgehaald. Iedere laadactie controleert de accountmanagerautorisatie opnieuw, ook bij een volle gedeelde cache.
- Onafhankelijke klantgroepen worden met maximaal drie gelijktijdige aanvragen opgehaald. Vervolgpagina's binnen een groep blijven sequentieel. Te lange URL's worden in kleinere groepen gesplitst.
- Dagtotalen worden eenmaal per geladen week berekend. Agenda- en componentgroepen worden alleen opnieuw berekend als de selectie of gegevens wijzigen. De pagina toont 15 hoofdentiteiten per lijstpagina; alle bijbehorende componenten en orders blijven bij elkaar.
- Periode- en managerwissels annuleren oudere aanvragen. Timeout: 60 seconden per HTTP-request, 90 seconden per laadactie. Logs vermelden aantallen en duur per stap, zonder e-mailadressen, klantfilters of BC-responses.

Alle OData-vervolgpagina's worden verwerkt, alleen op hetzelfde endpoint. Redirects zijn uitgeschakeld. Klanten en werkorders worden ontdubbeld. Ongeldige vervolgpagina's en API-fouten geven een melding, nooit stilzwijgend onvolledige resultaten.

## Configuratie

Bestaande `BusinessCentral`- en `AzureAd`-instellingen blijven nodig. Optioneel in `appsettings.Local.json`:

```json
"WorkOrderCalendar": {
  "CustomerManagerField": "LVS_After_Sales_Person_Code",
  "MaxUrlLength": 7000,
  "MaxConcurrentRequests": 3,
  "CacheSeconds": 30
}
```

`CacheSeconds` betreft uitsluitend werkorders: 0 schakelt die cache uit, maximum 300. Klantkoppelingen gebruiken de vaste grenzen van vijf minuten en 24 uur. `MaxConcurrentRequests`: 1–6; verlaag bij BC-throttling. Alternatieve klantvelden zijn `Salesperson_Code` en `KVT_Service_coördinator`. Omgevingsvariabelen gebruiken bijvoorbeeld `WorkOrderCalendar__CacheSeconds`. `appsettings.Local.json` wordt als laatste geladen en heeft bij gelijke sleutels voorrang. Geen nieuwe secrets nodig.

BC moet `SalesPersonCard`, `AppCustomerCard` en `LVS_MainWorkOrderCard` met de gebruikte velden publiceren. De serviceaccount heeft leesrechten nodig. Live metadata, filters en responstijden moeten op de betreffende installatie worden gecontroleerd.

## Controle

`dotnet run --project tests/CalendarChecks -c Release` controleert mapping, klantisolatie, paginering, escaping, datums, nextLink-beveiliging, annulering, begrensde paralleliteit, managerkeuze, cacheverval, verversen en gebruikersscheiding zonder live BC. Aanvullende controles gebruiken een testklok voor exact vijf minuten/24 uur, gewijzigde/lege/ongewijzigde koppelingen, weergave vóór achtergrondcontrole, gedeelde controles, foutafhandeling en annulering van afzonderlijke wachtende gebruikers.

Controleer interactief: dag/week wisselen, vandaag, alle managers, bladeren door hoofdentiteiten, verversen, mobiel/tablet en snel wisselen tijdens laden. Een geblokkeerde verkoperskaart moet ook met een gevulde gegevenscache worden geweigerd.
