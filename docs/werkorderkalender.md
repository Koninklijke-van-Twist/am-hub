# Mijn werkorders

Pagina: `/werkorders` (productie: `/dotnet/am-hub/werkorders`).

## Automatische accountmanagerkoppeling

Geen handmatige mapping nodig. De server leest het e-mailadres uit de geauthenticeerde Entra-claims: email, daarna preferred_username, daarna upn (inclusief mapped claimtypes). Vervolgens wordt SalesPersonCard gefilterd op E_Mail, met alleen Code,E_Mail,Blocked,Privacy_Blocked. Alle vervolgpagina's worden verwerkt. Alleen precies één overeenkomst met een geldige, niet-geblokkeerde verkoperscode is toegestaan. Geen overeenkomst, dubbele overeenkomst of ontbrekend e-mailadres geeft een begrijpelijke melding zonder klant- of werkorderrequests. De eigen code wordt server-side opgezocht. Ingelogde gebruikers met een geldige verkoperskaart mogen daarnaast kiezen uit AW, LL, FR, YB, NVD, NZ, FJ, MS en WM. De server valideert deze keuzelijst; het e-mailadres wordt nooit vanuit de browser overgenomen.

De gevonden Code wordt gebruikt voor AppCustomerCard.LVS_After_Sales_Person_Code. Oude WorkOrderCalendar:Users-configuratie wordt niet meer gebruikt en kan verwijderd worden. Houd E_Mail in BC gelijk aan het aanmeldadres; hoofdlettergevoeligheid van het OData-filter hangt af van de BC-installatie. Teruggegeven adressen worden aanvullend hoofdletterongevoelig gecontroleerd.

## Configuratie

Bestaande BusinessCentral:BaseUrl, Company, Username, Password en AzureAd blijven nodig. Geen nieuwe secrets. Optioneel in appsettings.Local.json:

```json
"WorkOrderCalendar": {
  "CustomerManagerField": "LVS_After_Sales_Person_Code",
  "MaxUrlLength": 7000,
  "MaxConcurrentRequests": 3
}
```

Environment-equivalenten: WorkOrderCalendar__CustomerManagerField en WorkOrderCalendar__MaxUrlLength. appsettings.Local.json wordt als laatste geladen en heeft bij gelijke sleutels voorrang. Bewaar dit bestand buiten wwwroot en Git en geef www-data leesrechten.

Alternatieve klantvelden: Salesperson_Code en KVT_Service_coördinator. Geen keuze voor gebruikers. Autorisatie verloopt via klanten, niet via Project_Manager.

## Ophalen en kalender

Kaarten, details en zoeken gebruiken het werkordernummer uit `KVT_Maintenance_Workorder_No`, niet het projectnummer. `AppWerkorders.No` levert via deze koppeling `Status`, `Main_Entity`, `Component_No` en `Component_Description`. Deze velden worden in dezelfde gegroepeerde aanvraag opgehaald en moeten op de OData-page zijn gepubliceerd. Open, lege en ontbrekende statussen worden uitgesloten. De klantkoppeling blijft uit AppProjecten komen. De planning komt uitsluitend uit AppWerkorders.Start_Date en End_Date. Het datumfilter staat op AppWerkorders; AppProjecten selecteert en filtert geen projectdatums meer. Daardoor kunnen bij een periodewissel meer projecten worden opgehaald, maar worden werkorders niet onterecht door afwijkende projectdatums uitgesloten.

De eerste HTML toont direct de pagina met laadmelding. OData start pas na het eerste interactieve renderen (`OnAfterRenderAsync`), zodat prerendering niet op BC wacht en niet nogmaals dezelfde gegevens ophaalt. Onafhankelijke querygroepen worden met maximaal drie tegelijk opgehaald, zowel voor projecten als gekoppelde werkorders. Paginering binnen een groep blijft sequentieel. `WorkOrderCalendar__MaxConcurrentRequests` kan dit tussen 1 en 6 instellen; verlaag dit bij BC-throttling. Annulering en het afwijzen van verouderde resultaten blijven actief. Zoekresultaten en de gesorteerde agenda worden eenmaal per render berekend. Geen gedeelde gegevenscache of verouderde autorisatiegegevens.

De kalender toont dagaantallen met een gepagineerde lijst (15 per pagina), een zoekveld en statusfilter. Doorlopende werkorders staan standaard uit en kunnen worden aangevinkt. Zoeken omvat ook hoofdentiteit en componentvelden.

Na SalesPersonCard volgt AppCustomerCard (alleen No), daarna AppProjecten met alleen benodigde velden. Alle endpoints volgen nextLink uitsluitend op hetzelfde endpoint; HTTP-redirects zijn uitgeschakeld. Normaal één werkorderquery plus vervolgpagina's. Te lange gecodeerde URL's worden in groepen klanten opgesplitst. Klanten en werkorders worden ontdubbeld. MaxUrlLength geldt ook voor nextLink. Limieten en API-fouten geven een melding, nooit stilzwijgend onvolledige resultaten.

Begindatum 0001-01-01 wordt uitgesloten. Einddatum is inclusief, periodebovengrens exclusief. Doorlopende orders worden begrensd tot de zichtbare zes weken en expliciet als doorlopend aangegeven. Geen caching. Periodewissels annuleren verouderde requests. Timeout: 60 seconden per HTTP-request, 90 seconden per periode. Logs bevatten geen e-mailadressen, klantfilters of BC-responses.

## Starten en testen

1. Configureer BC/AzureAd en controleer E_Mail op de verkoperskaart. De lokale Entra-callback moet toegestaan zijn.
2. `dotnet run --project AM-Hub --launch-profile http`
3. Open `http://localhost:5257/werkorders` en meld aan.
4. Test maanden, vandaag, details, tabletbreedte, doorlopende orders en snel wisselen van periode.
5. Test verschillende gebruikers, ontbrekende/dubbele BC-e-mailadressen en geblokkeerde verkopers.
6. `dotnet run --project tests/CalendarChecks -c Release` voert backendcontroles zonder live BC uit.

BC moet SalesPersonCard, AppCustomerCard en AppProjecten en de genoemde velden publiceren. De serviceaccount heeft leesrechten nodig. Metadata, datumtypes en live resultaten moeten op de installatie worden bevestigd. NextLink naar een ander endpoint wordt geweigerd. Veel doorlopende orders kunnen veel kalenderregels opleveren.

## Accountmanagerkeuze

Standaard staat de keuze op de eigen code uit SalesPersonCard via het aanmeldadres. De vaste NVD-testuitzondering is verwijderd. Met de keuzelijst kunnen gekoppelde, ingelogde gebruikers de klanten en werkorders van AW, LL, FR, YB, NVD, NZ, FJ, MS en WM bekijken. Bij wisselen wordt de vorige aanvraag geannuleerd en worden oude resultaten gewist. De keuze blijft actief bij maandwissels; bij opnieuw openen van de pagina start deze weer op de eigen code. Dit vervangt de eerdere beperking tot uitsluitend eigen klanten.