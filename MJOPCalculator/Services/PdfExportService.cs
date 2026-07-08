using MJOP.Calculator.Models.BusinessCentral;
using System.Text;

namespace MJOP.Calculator.Services;

public class PdfExportService
{
    public string BuildQuoteHtml(QuoteDetailsDto quoteDetails, List<Component> components, List<string> taskTypes)
        {
            var culture = new System.Globalization.CultureInfo("nl-NL");

            string Encode(string value)
            {
                return System.Net.WebUtility.HtmlEncode(value ?? "");
            }

            var headerCells = new StringBuilder();

            foreach (var component in components)
            {
                headerCells.Append($"<th>{Encode(component.CustomerNo)}</th>");
            }

            var bodyRows = new StringBuilder();

            foreach (var taskType in taskTypes)
            {
                var rowCells = new StringBuilder();

                foreach (var component in components)
                {
                    var task = component.ComponentTasks
                        .FirstOrDefault(t => t.Type == taskType);

                    var priceText = task?.Price != null
                        ? $"€ {task.Price.Value.ToString("N2", culture)}"
                        : "-";

                    rowCells.Append($"<td>{Encode(priceText)}</td>");
                }

                bodyRows.Append($@"
                <tr>
                    <td>{Encode(taskType)}</td>
                    {rowCells}
                </tr>");
            }

            var totalCells = new StringBuilder();

            foreach (var component in components)
            {
                totalCells.Append($"<th>€ {component.TotalPrice.ToString("N2", culture)}</th>");
            }

            return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='utf-8'>
        <title>Offerte {Encode(quoteDetails.QuoteNo)}</title>

        <style>
            @page {{
                size: A4;
                margin: 0;
            }}

            * {{
                box-sizing: border-box;
            }}

            html,
            body {{
                margin: 0;
                padding: 0;
                width: 210mm;
                height: 297mm;
                font-family: Arial, Helvetica, sans-serif;
                color: #000;
                background: #fff;
                font-size: 9.2px;
                line-height: 1.15;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }}

            .page {{
                position: relative;
                width: 210mm;
                height: 297mm;
                padding: 22mm 17mm 14mm 24mm;
                overflow: hidden;
            }}

            .top-header {{
                display: flex;
                justify-content: space-between;
                align-items: flex-start;
                margin-bottom: 20mm;
            }}

            .logo {{
                width: 49mm;
                height: auto;
                display: block;
            }}

            .company-info {{
                display: flex;
                gap: 18mm;
                color: #0069b4;
                font-size: 6.4px;
                line-height: 1.25;
                white-space: nowrap;
                margin-top: 1mm;
            }}

            .company-info strong {{
                display: inline-block;
                width: 13mm;
                color: #00529b;
            }}

            .date-line {{
                margin-bottom: 7mm;
            }}

            .quote-details {{
                width: 100%;
                border-collapse: collapse;
                margin-bottom: 12mm;
                font-size: 9.2px;
                line-height: 1.15;
            }}

            .quote-details td {{
                padding: 1.5mm 0;
                vertical-align: top;
            }}

            .quote-details td.label {{
                width: 20mm;
                font-weight: bold;
                color: #00529b;
            }}

            .quote-details td.separator {{
                width: 3mm;
            }}

            .intro {{
                margin-bottom: 6mm;
                font-size: 9.2px;
                line-height: 1.15;
            }}

            table.lines {{
                width: 100%;
                border-collapse: collapse;
                margin: 6mm 0;
                font-size: 8.8px;
                line-height: 1.15;
            }}

            table.lines th {{
                background-color: #e6eef7;
                font-weight: bold;
                text-align: center;
            }}

            
            table.lines tbody tr:nth-child(odd) td {{
                background-color: #ffffff;
                color: #000000;
            }}

            table.lines tbody tr:nth-child(even) td {{
                background-color: #0099cc;
                color: #ffffff;
            }}


            table.lines td:last-child,
            table.lines th:last-child {{
                text-align: center;
                white-space: nowrap;
            }}

            .total-row th,
            .total-row td {{
                font-weight: bold;
                background-color: #dceaf7 !important;
            }}

            .note {{
                margin-top: 5mm;
                font-size: 8.7px;
                line-height: 1.18;
            }}

            .section-title {{
                margin-top: 11mm;
                margin-bottom: 8mm;
                font-weight: bold;
                font-size: 9px;
            }}

            .mjop-text {{
                font-size: 8.7px;
                line-height: 1.18;
                margin-bottom: 0;
            }}

            .footer {{
                position: absolute;
                left: 24mm;
                right: 24mm;
                bottom: 9mm;
                color: #0070c0;
                font-size: 5.8px;
                line-height: 1.25;
            }}

            .footer-main {{
                margin-bottom: 3mm;
            }}

            .footer-small {{
                font-size: 5.3px;
                line-height: 1.25;
            }}

            .blue-corner {{
                position: absolute;
                right: 0;
                bottom: 0;
                width: 0;
                height: 0;
                border-style: solid;
                border-width: 0 0 16mm 26mm;
                border-color: transparent transparent #0098d8 transparent;
            }}

            @media print {{
                html,
                body {{
                    width: 210mm;
                    height: 297mm;
                    overflow: hidden;
                }}

                .page {{
                    page-break-after: avoid;
                    page-break-inside: avoid;
                }}
            }}
        </style>
    </head>

    <body>
        <div class='page'>

            <div class='top-header'>
                <div class='logo-wrapper'>
                    <img class='logo' src='images/logo.png' />
                </div>

                <div class='company-info'>
                    <div>
                        Keerweer 62, 3316 KA Dordrecht<br />
                        Postbus 156, 3300 AD Dordrecht<br />
                        +31(0)78 - 632 66 00<br />
                        info@kvt.nl&nbsp;&nbsp;&nbsp;&nbsp; www.kvt.nl
                    </div>

                    <div>
                        <strong>IBAN</strong> NL98INGB0664086446<br />
                        <strong>BIC</strong> INGBNL2A<br />
                        <strong>KVK</strong> 23022060<br />
                        <strong>BTW/VAT</strong> NL001575429B01
                    </div>
                </div>
            </div>

            <div class='date-line'>
                Dordrecht, {DateTime.Now.ToString("d MMMM yyyy", culture)}
            </div>

            <table class='quote-details'>
                <tr>
                    <td class='label'>Aan</td>
                    <td class='separator'>:</td>
                    <td>{Encode(quoteDetails.CustomerName)}</td>
                </tr>
                <tr>
                    <td class='label'>Ter attentie van</td>
                    <td class='separator'>:</td>
                    <td>{Encode(quoteDetails.ContactPerson)}</td>
                </tr>
                <tr>
                    <td class='label'>Email</td>
                    <td class='separator'>:</td>
                    <td><span>{Encode(quoteDetails.Email)}</span></td>
                </tr>
                <tr>
                    <td class='label'>Behandeld door</td>
                    <td class='separator'>:</td>
                    <td>{Encode(quoteDetails.SalespersonCode)}</td>
                </tr>
                <tr>
                    <td class='label'>Betreft</td>
                    <td class='separator'>:</td>
                    <td>{Encode(quoteDetails.Reference)}</td>
                </tr>
                <tr>
                    <td class='label'>Offertenummer</td>
                    <td class='separator'>:</td>
                    <td>{Encode(quoteDetails.QuoteNo)}</td>
                </tr>
            </table>

            <p>Geachte relatie,</p>

            <div class='intro'>
                Hierbij ontvangt u ons voorstel voor de onderstaande werkzaamheden / diensten.
            </div>

            <table class='lines'>
                <thead>
                    <tr>
                        <th>Taak</th>
                        {headerCells}
                    </tr>
                </thead>
                <tbody>
                    {bodyRows}
                    <tr class='total-row'>
                        <td><strong>Totaal</strong></td>
                        {totalCells}
                    </tr>
                </tbody>
            </table>

            <div class='note'>
                <strong>Let op:</strong> Genoemde prijzen zijn exclusief BTW, tenzij anders vermeld.
                Eventuele aanvullende werkzaamheden worden uitsluitend uitgevoerd na overleg.
            </div>

            <div class='note'>
                <strong>Let op:</strong> Deze offerte is opgesteld op basis van de op dit moment bekende gegevens.
                Afwijkingen in configuratie of uitvoering kunnen invloed hebben op de uiteindelijke prijsstelling.
            </div>

            <div class='section-title'>
                Meer Jaren Onderhoud Plan (MJOP):
            </div>

            <div class='mjop-text'>
                Er kunnen onderdelen aanwezig zijn die aanvullend op het jaarlijks preventief onderhoud,
                periodiek gecontroleerd of preventief vervangen moeten worden.
            </div>

            <div class='footer'>
                <div class='footer-main'>
                    Koninklijke Van Twist is een ISO 9001, ISO 14001 en VCA** gecertificeerde organisatie.
                </div>

                <div class='footer-small'>
                    Op alle aanbiedingen en overeenkomsten inzake door ons te verrichten leveringen en/of diensten
                    zijn van toepassing de algemene verkoop- en leveringsvoorwaarden van Koninklijke van Twist.
                    Een exemplaar van deze voorwaarden is reeds in uw bezit en is beschikbaar op www.kvt.nl.
                    Uitdrukkelijk worden andersluidende voorwaarden afgewezen.
                </div>
            </div>

            <div class='blue-corner'></div>

        </div>
    </body>
    </html>";
        }
    }
