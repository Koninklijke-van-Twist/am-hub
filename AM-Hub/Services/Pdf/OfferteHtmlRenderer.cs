using AMHub.Models.Documents;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using System.Net;
using System.Text;

namespace AMHub.Services.Pdf;

public class OfferteHtmlRenderer : IOfferteHtmlRenderer
{
    private readonly IWebHostEnvironment _environment;

    private static readonly CultureInfo Nl =
        CultureInfo.GetCultureInfo("nl-NL");

    public OfferteHtmlRenderer(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> RenderAsync(
        OfferteDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        var templatesPath = Path.Combine(
            _environment.ContentRootPath,
            "Services",
            "Pdf",
            "Templates");

        var css = await File.ReadAllTextAsync(
            Path.Combine(
                templatesPath,
                "offerte.css"),
            cancellationToken);

        var staticContent = await File.ReadAllTextAsync(
            Path.Combine(
                templatesPath,
                "offerte-static.html"),
            cancellationToken);

        // KVT logo
        var logoPath = Path.Combine(
            templatesPath,
            "images",
            "kvtlogo.png");

        var kvtLogo =
            GetImageDataUri(logoPath);

        // Hero afbeelding
        var heroPath = Path.Combine(
            templatesPath,
            "images",
            "nsamonteurgespiegeld.png");

        var hero =
            GetImageDataUri(heroPath);

        var html = new StringBuilder();

        var accountmanagerPhoto =
            GetAccountmanagerPhoto(
                templatesPath,
                model.AccountmanagerCode);

        html.Append("""
        <!doctype html>
        <html lang="nl">
        <head>
            <meta charset="utf-8">
            <meta name="viewport"
                  content="width=device-width, initial-scale=1">

            <style>
        """);

        html.Append(css);

        // Paginamarges worden op iedere fysieke pagina herhaald, ook bij doorlopende tabellen.
        html.Append($$"""
        @page {
            @top-left {
                content: "";
                width: 45mm;
                margin-top: 14mm;
                background-image: url('{{kvtLogo}}');
                background-repeat: no-repeat;
                background-position: left top;
                background-size: 45mm auto;
            }
            @top-right {
                margin-top: 14mm;
                content: "{{CssString("Offerte " + model.OfferteNummer)}}";
                font-family: Arial, Helvetica, sans-serif;
                font-size: 10px;
                color: #5D6B78;
                vertical-align: top;
                text-align: right;
            }
        }
        @page :first {
            @top-left { content: none; background-image: none; }
            @top-right { content: none; }
        }
        """);

        html.Append("""
            </style>
        </head>

        <body>
        """);

        RenderCover(
            html,
            model,
            kvtLogo,
            hero,
            accountmanagerPhoto);



        RenderComponentSummary(
            html,
            model,
            kvtLogo);

        RenderMultiYearMaintenanceBudget(
            html,
            model,
            kvtLogo);

        RenderCorrectiveRatesAndTerms(
            html,
            model,
            kvtLogo);


        html.Append(staticContent);

        html.Append("""
        </body>
        </html>
        """);

        return html.ToString();
    }

    private static void RenderCover(
        StringBuilder html,
        OfferteDocumentModel model,
        string kvtLogo,
        string hero,
        string accountmanagerPhoto)
    {
        html.Append($$"""
        <div class="cover-footer-anchor">

        """);

        html.Append($$""""
            <footer class="cover-footer">

                <div class="cover-footer-left">

                    <p class="cover-footer-certification">
                        Koninklijke Van Twist is een ISO 9001, ISO 14001 en VCA**
                        gecertificeerde organisatie.
                    </p>

                    <p class="cover-footer-conditions">
                        Op alle aanbiedingen en overeenkomsten inzake door ons te
                        verrichten leveringen en/of diensten zijn van toepassing de
                        algemene verkoop- en leveringsvoorwaarden van Koninklijke van
                        Twist. Een exemplaar van deze voorwaarden is reeds in uw bezit
                        en is beschikbaar op www.kvt.nl. Uitdrukkelijk worden
                        andersluidende voorwaarden afgewezen.
                    </p>

                </div>

                <div class="cover-footer-right">

                    <div class="footer-contact-content">

                        <div class="footer-contact-photo">
                            {{AccountmanagerPhoto(accountmanagerPhoto)}}
                        </div>

                        <div class="footer-contact-title">
                            Uw contactpersoon
                        </div>

                        <div class="footer-contact-details">

                            <div class="footer-contact-name">
                                {{H(model.AccountmanagerNaam)}}
                            </div>

                            <div class="footer-contact-function">
                                {{H(model.AccountmanagerFunctie)}}
                            </div>

                            <div class="footer-contact-phone">
                                {{H(model.AccountmanagerTelefoon)}}
                            </div>

                            <div class="footer-contact-email">
                                {{H(model.AccountmanagerEmail)}}
                            </div>

                        </div>

                    </div>

                </div>

            </footer>
        </div>

"""");

        html.Append($$"""
        <section class="cover">
            <header class="company-header">

                <div class="company-header-logo">
                    {{ImageTag(
                        kvtLogo,
                        "Koninklijke Van Twist",
                        "company-logo")}}
                </div>

                <div class="company-header-contact">
                    <div>⌖ &nbsp; Keerweer 62, 3316 KA Dordrecht</div>
                    <div>✎ &nbsp; Postbus 156, 3300 AD Dordrecht</div>
                    <div>☎ &nbsp; +31(0)78 - 632 66 00</div>
                    <div>✉ &nbsp; info@kvt.nl &nbsp;&nbsp; ◉ &nbsp; www.kvt.nl</div>
                </div>

                <div class="company-header-details">

                    <div class="company-detail-row">
                        <strong>IBAN</strong>
                        <span>NL98INGB0664086446</span>
                    </div>

                    <div class="company-detail-row">
                        <strong>BIC</strong>
                        <span>INGBNL2A</span>
                    </div>

                    <div class="company-detail-row">
                        <strong>KVK</strong>
                        <span>23022060</span>
                    </div>

                    <div class="company-detail-row">
                        <strong>BTW/VAT</strong>
                        <span>NL001575429B01</span>
                    </div>

                </div>

            </header>

            <div
                class="hero"
                style="{{HeroStyle(hero)}}">

                <div class="hero-overlay">
                    
                    <div class="hero-small">
                        Op maat gemaakt voor
                    </div>

                    <h1>
                        {{H(model.KlantNaam)}}
                    </h1>



                    <p class="company-contact-rows">
                        <span>{{H(model.Adres)}} </span>         <span>{{H(model.ContactPersoon)}}</span>

                    </p>

                    <p class="company-contact-rows">
                        <span>{{H(model.Postcode)}} {{H(model.Plaats)}}</span>       <span>{{H(model.Email)}}</span>
                        

                    </p>

                </div>

            </div>

            <section class="cover-intro">

                <div>

                    <h2>
                        OFFERTE {{H(model.OfferteNummer)}}
                    </h2>

                    <h3>
                        Service onderhoudscontract
                    </h3>

                    <p>
                        Hierbij ontvangt u ons een voorstel voor een “Service Onderhoudscontract” voor de noodstroom- installatie(s) op de onderstaande locatie(s). Ons voorstel bestaat uit jaarlijks uit te voeren werkzaamheden, aangevuld met optionele diensten. In de bijlage worden deze diensten verder omschreven. Wanneer u verdere toelichting wilt, kunt u uiteraard contact met mij opnemen. Op basis van uw aanvraag en op basis van hetgeen ons momenteel bekend is, kan ik u het onderstaande aanbieden.
                    </p>

                </div>

            </section>

            <section class="investment-summary">

                <h3>
                    Samenvatting investering
                </h3>

                <table>

                    <thead>
                        <tr>
                            <th>
                                Omschrijving
                            </th>

                            <th class="money">
                                Totaalbedrag: {{Money(model.Totaal)}}
                            </th>
                        </tr>
                    </thead>

                    <tbody>
        """);

        foreach (var component in model.Components)
        {
            html.Append($$"""
                <tr>

                    <td>
                        <div>{{H(component.Omschrijving)}} <span class="investment-component-number">{{H(component.ComponentNo)}}</span></div>
                        <div class="investment-location">{{H(component.Locatie)}}</div>
                    </td>

                    <td class="money">
                        {{Money(component.Totaal)}}
                    </td>

                </tr>
                """);
        }



        html.Append("""
                    </tbody>
                </table>
            </section>
        </section>
        """);
    }

    private static void RenderComponentSummary(
    StringBuilder html,
    OfferteDocumentModel model,
    string kvtLogo)
    {
        html.Append($$"""
    <section class="document-chapter page-start">

        <h1 class="page-title">
            Taken per noodstroom installatie
        </h1>

        <p class="document-section">
            Wij kunnen u de volgende (verplichte) werkzaamheden aanbieden:
            Het jaarlijks preventief onderhoud en daarnaast de deelname aan de 24/7 storingsdienst.
            <br><br>
            In aanvulling op het jaarlijks onderhoud kunnen wij u additionele werkzaamheden aanbieden. Zo moet een bovengronds opgestelde brandstoftank jaarlijks geïnspecteerd worden, is het raadzaam de NSA minimaal maandelijks te controleren en op te starten en is het ook belangrijk om de NSA minimaal jaarlijks op (vol)last te testen. Wanneer u nog meer zekerheid wilt hebben over de (inwendige) staat van het NSA, en de smeerolie, het koelmedium of de brandstof frequent beoordeelt wilt hebben kunnen wij u (extra) analyses aanbieden. De monstername dient in combinatie met een regulier service bezoek uitgevoerd te kunnen worden. Het NSA staat continu paraat, en moet direct opstarten bij netuitval. Het NSA-besturingspaneel is hierin een cruciaal onderdeel. Dit paneel wordt tijdens het jaarlijks onderhoud beoordeeld op juist functioneren. Om nog meer zekerheid te garanderen adviseren wij het besturingspaneel en de elektrotechnische componenten op een rond de dieselmotor eenmaal per 4 jaar door een Elektrotechnische Engineer te laten beoordelen. Wij kunnen u daarom de volgende vaste taken en aanvullende opties aanbieden:
        </p>

        <div class="aggregate-summary-table">

            <div class="aggregate-summary-columns">

                <div>
                    Component / Omschrijving
                </div>

                <div>
                    Werkzaamheden
                </div>

                <div class="money">
                    Totaalbedrag
                </div>

            </div>
    """);

        var componentIndex = 1;

        foreach (var component in model.Components)
        {
            html.Append($$"""
        <section class="aggregate-summary-component">

            <div class="aggregate-summary-component-row">

                <div class="aggregate-summary-component-main">

                    <div class="aggregate-summary-number">
                        {{componentIndex}}
                    </div>

                    <div class="aggregate-summary-component-info">

                        <div class="aggregate-summary-component-title">
                            {{H(component.Omschrijving)}}
                            <span class="aggregate-summary-component-number">{{H(component.ComponentNo)}}</span>
                        </div>

                        <div class="aggregate-summary-location">
                            {{H(component.Locatie)}}
                        </div>

                    </div>

                </div>

                <div></div>

                <div class="aggregate-summary-component-total">
                    {{Money(component.Totaal)}}
                </div>

            </div>

            <div class="aggregate-summary-tasks">
        """);


            var taskOrder = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["GOH"] = 10,
                ["KOH"] = 20,
                ["PD"] = 30,
                ["PD2M"] = 40,
                ["LB100"] = 50,
                ["LB500"] = 60,
                ["LB700"] = 70,
                ["TC"] = 80,
                ["SA"] = 90,
                ["BA"] = 100,
                ["KWA"] = 110,
                ["PI4"] = 120,
                ["PI7"] = 130,
                ["EIN"] = 140
            };

            var orderedTasks =
                component.Werkzaamheden
                    .OrderBy(x =>
                        taskOrder.TryGetValue(x.Code, out var order)
                            ? order
                            : int.MaxValue)
                    .ThenBy(x => x.Code)
                    .ToList();

            for (var taskIndex = 0;
                 taskIndex < orderedTasks.Count;
                 taskIndex++)
            {
                var task = orderedTasks[taskIndex];

                var isLast =
                    taskIndex == orderedTasks.Count - 1;

                html.Append($$"""
                <div class="aggregate-summary-task-row">

                    <div class="aggregate-summary-tree">

                        <span class="aggregate-summary-branch">
                            {{(isLast ? "└─" : "├─")}}
                        </span>

                        <span class="aggregate-summary-task-number">
                            {{taskIndex + 1}}
                        </span>

                    </div>

                    <div class="aggregate-summary-task-description">
                        {{H(task.Omschrijving)}}
                    </div>

                    <div class="aggregate-summary-task-price">
                        {{Money(task.Totaal)}}
                    </div>

                </div>
                """);
            }

            html.Append("""
            </div>

        </section>
        """);

            componentIndex++;
        }

        html.Append($$"""
        </div>

        <section class="aggregate-summary-grand-total">

            <div>
                <strong>
                    TOTAALPRIJS
                </strong>

                <span>
                    (EXCL. BTW)
                </span>

                <div class="aggregate-summary-grand-total-subtitle">
                    Totaal voor alle componenten en werkzaamheden
                </div>
            </div>

            <div class="aggregate-summary-grand-total-price">
                {{Money(model.Totaal)}}
            </div>

        </section>

        <div class="notice">
            <strong>Let op:</strong> De installatie moet elke 4 jaar wettelijk verplicht SCIOS Scope 4d en/of 7C geïnspecteerd worden.
        </div>

        <div class="notice">
            <strong>Let op:</strong> De elektrotechnische inspectie betreft alleen het laagspanningsdeel. De generator- en vermogensschakelaars worden niet door ons geïnspecteerd of onderhouden.
        </div>

    </section>
    """);
    }

    private static string CssString(string value) =>
        string.Concat(value.Select(c => "\\" + ((int)c).ToString("X", CultureInfo.InvariantCulture) + " "));

    private static string H(
        string? value)
    {
        return WebUtility.HtmlEncode(
            value ?? "");
    }

    private static string Money(
        decimal value)
    {
        return value.ToString(
            "C2",
            Nl);
    }

    private static string Date(
        DateOnly value)
    {
        if (value == default)
            return "";

        return value.ToString(
            "dd MMMM yyyy",
            Nl);
    }

    private static string ImageTag(
        string dataUri,
        string alt,
        string cssClass = "logo-image")
    {
        if (string.IsNullOrWhiteSpace(dataUri))
        {
            return $"""
            <div class="logo-fallback">
                {H(alt)}
            </div>
            """;
        }

        return $"""
        <img
            class="{cssClass}"
            src="{dataUri}"
            alt="{H(alt)}">
        """;
    }

    private static string HeroStyle(
        string? hero)
    {
        if (string.IsNullOrWhiteSpace(hero))
            return string.Empty;

        return $"""
        background-image:
            linear-gradient(
                90deg,
                rgba(0, 82, 155, 0.98) 0%,
                rgba(0, 82, 155, 0.96) 30%,
                rgba(0, 82, 155, 0.82) 50%,
                rgba(0, 82, 155, 0.52) 72%,
                rgba(0, 82, 155, 0.22) 100%
            ),
            url('{hero}');
        """;
    }

    private static string GetImageDataUri(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            return "";
        }

        var extension =
            Path.GetExtension(path)
                .TrimStart('.')
                .ToLowerInvariant();

        var contentType =
            extension switch
            {
                "jpg" or "jpeg"
                    => "image/jpeg",

                "svg"
                    => "image/svg+xml",

                "webp"
                    => "image/webp",

                _ => "image/png"
            };

        var bytes =
            File.ReadAllBytes(path);

        return
            $"data:{contentType};base64," +
            Convert.ToBase64String(bytes);
    }

    private static string GetAccountmanagerPhoto(
        string templatesPath,
        string? accountmanagerCode)
    {
        if (string.IsNullOrWhiteSpace(accountmanagerCode))
            return "";

        var safeCode = new string(
            accountmanagerCode
                .Where(char.IsLetterOrDigit)
                .ToArray())
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(safeCode))
            return "";

        var imagePath = Path.Combine(
            templatesPath,
            "images",
            $"{safeCode}.jpg");

        return GetImageDataUri(imagePath);
    }

    private static string AccountmanagerPhoto(
        string? photo)
    {
        if (string.IsNullOrWhiteSpace(photo))
        {
            return """
                <div class="contact-photo-placeholder"></div>
                """;
        }

        return $"""
            <img
                class="contact-photo"
                src="{photo}"
                alt="Contactpersoon">
            """;
    }


    private static void RenderMultiYearMaintenanceBudget(
    StringBuilder html,
    OfferteDocumentModel model,
    string kvtLogo)
    {
        html.Append("""<section class="multi-year-maintenance-budget"></section>""");
    }

    private static string Budget(decimal? amount)
    {
        if (amount is null || amount == 0)
            return "€ -";

        return amount.Value.ToString(
            "C2",
            Nl);
    }


    private static void RenderCorrectiveRatesAndTerms(
    StringBuilder html,
    OfferteDocumentModel model,
    string kvtLogo)
    {
        html.Append($$"""
    <section class="document-chapter page-start corrective-chapter">

        <h1 class="page-title">
            Verrekentarieven correctieve werkzaamheden/storingen
        </h1>

        <p class="corrective-intro">
            De kosten voor extra uit te voeren werkzaamheden, waaronder het
            verhelpen van eerstelijns-storingen, worden op basis van nacalculatie
            aan u doorbelast. Wij hanteren hiervoor in 2026 de onderstaande tarieven:
        </p>

        <table class="corrective-rate-table">

            <thead>
                <tr>
                    <th>Omschrijving</th>
                    <th class="money">Tarief</th>
                </tr>
            </thead>

            <tbody>

                <tr>
                    <td>Servicemonteur</td>
                    <td class="money">€ 105,00</td>
                </tr>

                <tr>
                    <td>Sr. Service monteur / teamlead</td>
                    <td class="money">€ 135,00</td>
                </tr>

                <tr>
                    <td>Technical Support engineer (diagnostiek)</td>
                    <td class="money">€ 135,00</td>
                </tr>

                <tr>
                    <td>Servicemonteur OEM specialist</td>
                    <td class="money">€ 135,00</td>
                </tr>

                <tr>
                    <td>Inspecteur (SCIOS / E-Inspectie)</td>
                    <td class="money">€ 135,00</td>
                </tr>

                <tr>
                    <td>Commissioning engineer</td>
                    <td class="money">€ 135,00</td>
                </tr>

                <tr>
                    <td>Werkvoorbereider / Facturist</td>
                    <td class="money">€ 105,00</td>
                </tr>

                <tr>
                    <td>Planner / Coördinator</td>
                    <td class="money">€ 105,00</td>
                </tr>

                <tr>
                    <td>V&amp;G / KAM Coördinator</td>
                    <td class="money">€ 105,00</td>
                </tr>

                <tr>
                    <td>Voorrijkosten per keer</td>
                    <td class="money">€ 227,23</td>
                </tr>

                <tr>
                    <td>KM tarief servicewagen per kilometer</td>
                    <td class="money">€ 1,01</td>
                </tr>

                <tr>
                    <td>Weekendtoeslag / starttarief per order</td>
                    <td class="money">€ 209,45</td>
                </tr>

            </tbody>

        </table>

        <div class="corrective-note">
            <strong>
                Reisuren gelden als gewerkte uren / wettelijke slaapuren gelden als gewerkte uren.
            </strong>
        </div>

        <section class="corrective-subsection">

            <h2>
                Toeslagen buiten normale werkuren
            </h2>

            <ol class="corrective-surcharge-list">

                <li>
                    Normale werkuren, maandag t/m vrijdag van 08:15 tot 16:45 uur
                    <strong>100%</strong>
                </li>

                <li>
                    Werkdagen tussen 06:15 en 08:15 uur en tussen 16:45 en 18:45 uur
                    <strong>125%</strong>
                </li>

                <li>
                    Werkdagen tussen 18:45 en 24:00 uur en zaterdaguren, met een
                    minimum van 4 uur, tussen 06:15 en 24:00 uur
                    <strong>150%</strong>
                </li>

                <li>
                    Werkdagen en zaterdagen tussen 24:00 en 06:15 uur en zon- en
                    feestdagen, met een minimum van 4 uur.
                </li>

            </ol>

        </section>

        <div class="corrective-emergency">
            Telefoonnummer voor het melden van storingen (24/7/365):
            <strong>078 – 632 66 70</strong>
        </div>

        <section class="corrective-subsection">

            <h2>
                Algemene verkoop- en leveringsvoorwaarden
            </h2>

            <p>
                Op alle aanbiedingen en overeenkomsten inzake door ons te verrichten
                leveringen en/of diensten zijn van toepassing de KVT-verkoopvoorwaarden,
                tenzij anders overeengekomen. Een exemplaar van deze voorwaarden is
                reeds in uw bezit. Met deze aanbieding komen eerdere offertes voor deze
                werken te vervallen. Uitdrukkelijk worden andersluidende voorwaarden
                afgewezen. De KVT-Verkoopvoorwaarden worden als volgt aangevuld:
            </p>

            <dl class="corrective-terms">

                <dt>
                    Van toepassing zijn ook:
                </dt>

                <dd>
                    Koninklijke Van Twist Verkoopvoorwaarden Onderhoudscontract
                </dd>

                <dt>
                    Levering:
                </dt>

                <dd>
                    De onderhoudswerkzaamheden worden in overleg ingepland en uitgevoerd
                    binnen normale werkuren (maandag t/m vrijdag, 08:15 – 16:45 uur).
                </dd>

                <dt>
                    Prijsstelling:
                </dt>

                <dd>
                    Strikt nettoprijzen, exclusief btw. Gebaseerd op een ongehinderde
                    uitvoering.
                </dd>

                <dt>
                    Betalingsvoorwaarden:
                </dt>

                <dd>
                    Op rekening, echter betaling binnen 30 dagen na factuurdatum.
                </dd>

                <dt>
                    Annuleringkosten:
                </dt>

                <dd>
                    Indien u de geplande werkzaamheden binnen 48 uur voor de
                    uitvoeringsdatum annuleert, zijn wij genoodzaakt 50% van de geplande
                    arbeidsuren door te berekenen als meerwerk.

                    <br><br>

                    Daarnaast zullen we 2 uur à € 105,- per uur doorbelasten ter
                    compensatie van de ontstane planschade.

                    <br><br>

                    Eventuele extra kosten van onze onderaannemers zullen we 1 op 1
                    aan u doorbelasten.
                </dd>

                <dt>
                    Geldigheid offerte:
                </dt>

                <dd>
                    De prijzen in deze aanbieding zijn vast t/m 31 december 2026.
                </dd>

            </dl>

        </section>

        <section class="corrective-closing">

            <p>
                Zie voor verdere informatie, toelichting op onze prijzen, voorwaarden
                en verduidelijking de toegevoegde bijlagen.
            </p>

            <p>
                Wij gaan ervan uit u hiermee een passende aanbieding gedaan te hebben.
                Mocht u nog vragen hebben kunt u ons uiteraard bellen of mailen.
            </p>

            <div class="corrective-signature">

                <p>
                    Met vriendelijke groet,
                </p>

                <p>
                    Koninklijke Van Twist
                </p>

                <div class="corrective-accountmanager">

                    <strong>
                        {{H(model.AccountmanagerNaam)}}
                    </strong>

                    <span>
                        Accountmanager
                    </span>

                </div>

            </div>

        </section>

    </section>
    """);
    }

}
