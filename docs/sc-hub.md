# Hubs en SC-rapporten

Applicatieroutes: `/am/`, `/sc/` en `/f/`. De F-Hub bevat voorlopig alleen de gezamenlijke layout met eigen naam en geen functies. Er is geen hubwisselaar. Bestaande AM-routes blijven als alias werken; de navigatie gebruikt voortaan `/am/...`.

Deze routes zijn relatief aan de ingestelde `ASPNETCORE_PATHBASE`. Met de bestaande productieprefix `/dotnet/am-hub` is de SC-Hub dus `/dotnet/am-hub/sc/`. Publicatie direct op `/sc/`, `/am/` en `/f/` vereist een overeenkomstige reverse-proxy- en aanmeldconfiguratie; die wordt niet door deze codewijziging aangepast.

## Gegevens

De SC-Hub koppelt het e-mailadres uit dezelfde Entra-claims als de AM-kalender aan `SalesPersonCard.E_Mail`. Precies één niet-geblokkeerde kaart met een code is vereist, ook bij het bekijken van een andere coördinator. Het filter staat standaard op Mijn rapporten (de eigen code). Daarnaast kunnen MDM, RK, SH en VBR worden gekozen. De server valideert deze keuzelijst en filtert de BC-aanvraag en de ontvangen resultaten op de gekozen code. Wisselen van coördinator laadt opnieuw.

Daarna wordt `LVS_MainWorkOrderCard` bevraagd met:

```text
$filter=Status eq 'Completed' and KVT_Service_Coordinator eq '<eigen code>'
$select=No,Task_Code,Task_Description,Resource_Name,Start_Date,Status,KVT_Service_Coordinator,KVT_URL_Workorder_Report_PDF
```

Alle vervolgpagina's worden gevolgd, uitsluitend op hetzelfde endpoint en zonder redirects. De resultaten worden aanvullend gecontroleerd op status en coördinator, en ontdubbeld op werkordernummer. Er is geen datumfilter: ook oude uitgevoerde werkorders blijven zichtbaar.

Datum toont voorlopig `Start_Date`, standaard aflopend gesorteerd. Alle werkorders die aan de filters voldoen worden op één pagina getoond, zonder paginalimiet. Zoeken en sorteren gebruiken de geladen gegevens en doen geen nieuwe BC-aanvragen. Openen en Vernieuwen laden actuele BC-gegevens. Prerendering is voor het overzicht uitgeschakeld om dubbele aanvragen te voorkomen.

Het werkordernummer opent de detailpagina `/sc/werkorders/{nummer}`. Links staan taak, omschrijving, monteur, startdatum en coördinator; rechts staat de PDF-viewer. De desktopindeling gebruikt de resterende schermhoogte. Op mobiele schermen worden de gegevens boven het rapport geplaatst. De knop Open in SharePoint gebruikt de gevalideerde BC-link. Ontbrekende rapporten blijven zichtbaar met een melding. Deze versie wijzigt geen BC-statussen.

## PDF-weergave

De rapportpagina's worden met lokaal gehoste Mozilla PDF.js 6.5.438 gerenderd, zonder iframe of browserwerkbalk. De eigen bediening biedt Vorige/Volgende, paginabereik, zoomen en Hele pagina. Standaard passen de volledige pagina's binnen de beschikbare breedte én hoogte, zonder intern scrollen. Twee pagina's worden alleen getoond als beide op de beschikbare hoogte naast elkaar passen en er minimaal 920 pixels netto paginaruimte is. Daarbij worden de daadwerkelijke paginaverhoudingen meegenomen. De viewer meet zijn eigen ruimte, zodat het hubmenu, de werkordergegevens en de werkbalk worden meegerekend. Zowel breedte- als hoogtewijzigingen herschalen het rapport. Bij handmatig inzoomen kan scrollen nodig zijn; Hele pagina herstelt de passende weergave. Bij de laatste oneven pagina wordt geen lege tweede pagina toegevoegd. Bij omschakelen naar twee pagina's blijft de huidige pagina onderdeel van het getoonde paar.

Alleen de zichtbare één of twee pagina's worden gerenderd, met een begrensde canvasresolutie en een tekstlaag voor tekstselectie. De PDF wordt eenmaal per bezoek opgehaald bij het bestaande beveiligde endpoint; bladeren, zoomen en resizen blijven lokaal. De library en worker staan onder `wwwroot/lib/pdfjs`, inclusief licentie, lettertypen en aanvullende renderbestanden. Er is geen externe CDN of nieuwe SharePoint-toestemming nodig.

## SharePoint REST en aanmelden

De server haalt de PDF op via `GetFileByServerRelativePath(decodedUrl=@path)/$value`, met het pad uit `KVT_URL_Workorder_Report_PDF`. De URL moet HTTPS gebruiken en binnen de ingestelde site vallen; alleen PDF-paden worden geaccepteerd. OData-parameters worden gequote en geëncodeerd, ook bij spaties, apostroffen, procenttekens en hekjes in bestandsnamen. Redirects worden niet gevolgd. Het toegangstoken verlaat de server niet.

De Entra-appregistratie moet SharePoint **delegated** `AllSites.Read` hebben, inclusief de benodigde toestemming. De ingelogde gebruiker moet zelf leesrechten op het bestand hebben. De server vraagt de scope `https://kvtnl.sharepoint.com/AllSites.Read` aan. Graph-rechten vervangen deze SharePoint-rechten niet.

Voor tokenverwerving is een geldig client secret of certificaat nodig voor dezelfde appregistratie. Stel dit veilig in als `AzureAd:ClientSecret`, bijvoorbeeld via omgevingsvariabele `AzureAd__ClientSecret`, of gebruik de door Microsoft.Identity.Web ondersteunde `AzureAd:ClientCredentials`-certificaatconfiguratie. Geen geheime waarden in de repository opslaan. **Herstart de applicatie na het configureren van de credential.** Tokenverwerving wordt alleen bij de start ingeschakeld wanneer een credential aanwezig is; de bestaande aanmelding blijft zonder credential beschikbaar.

Bij ontbrekende credentials toont de detailpagina een configuratiemelding met Open in SharePoint als alternatief. Als toestemming, heraanmelding of een nieuwe tokensessie nodig is, verschijnt Aanmelden voor SharePoint. Deze start de OIDC-aanmelding en keert terug naar dezelfde detailpagina. Tokens worden server-side in het geheugen bewaard; na een herstart kan heraanmelden nodig zijn. Bij meerdere applicatie-instanties is een gedeelde token-cache nodig.

Optionele niet-geheime instellingen:

```json
"SharePointReports": {
  "SiteUrl": "https://kvtnl.sharepoint.com/sites/BCDocumentRepository",
  "MaxPdfMegabytes": 40
}
```

De PDF wordt pas opgehaald wanneer de detailpagina de viewer laadt. Het beveiligde endpoint `/sc/werkorders/{nummer}/rapport.pdf` bepaalt de werkorder en bestandslink opnieuw in BC. Het controleert de eigen medewerkerskaart, status Completed en toewijzing aan de eigen code of MDM/RK/SH/VBR; een willekeurige URL kan niet worden meegegeven. Het bestand wordt met een groottebegrenzing in het geheugen gelezen, op een PDF-header gecontroleerd en als `application/pdf` inline aangeboden met ondersteuning voor range requests. Geen permanente kopieën of gedeelde PDF-cache; responses bevatten `private, no-store`.

## Controle

```text
dotnet run --project tests/CoordinatorChecks -c Release -p:NuGetAudit=false
dotnet run --project tests/SharePointChecks -c Release -p:NuGetAudit=false
dotnet run --project tests/SharePointChecks -c Release --no-build -- --http
dotnet run --project tests/SharePointChecks -c Release --no-build -- --viewer
```

De controles gebruiken gesimuleerde BC- en SharePoint-responses en controleren identiteit, OData-filters, vervolgpagina's, oude achterstand, gebruikersscheiding, detailtoegang, REST-paden, PDF-inhoud, foutafhandeling, groottebegrenzing en ontbrekende credentials. De HTTP-controle start tijdelijk een lokale testserver en controleert aanmeldplicht, de PDF-response, range requests, foutstatussen en de URL-prefix. De viewercontrole gebruikt een synthetische PDF en Chromium via de bestaande Playwright-installatie, test één/twee pagina's, bladeren, een oneven laatste pagina, tekstlaag, resizen, zoomen, één PDF-aanvraag en foutmeldingen. Screenshots staan in `.artifacts/viewer`. Voor een volledige acceptatiecontrole blijven een echte Entra-aanmelding, BC-gegevens en SharePoint-toegang nodig.
