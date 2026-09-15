namespace AMHub.Services.Pdf.Templates;

public static class OfferteStaticContent
{
    public const string Introductie = """
    Hierbij ontvangt u ons een voorstel voor een “Service Onderhoudscontract”
    voor de noodstroominstallatie(s) op bovengenoemde locatie. Ons voorstel
    bestaat uit jaarlijks uit te voeren werkzaamheden, aangevuld met optionele
    diensten. In de bijlage worden deze diensten verder omschreven.
    """;

    public const string ZeroInspectie = """
    De noodstroominstallatie moet jaarlijks preventief onderhouden worden.
    Daarvoor zijn niet alleen de juiste materialen benodigd, maar moeten ook
    schema’s en certificaten aanwezig zijn. Daarnaast kunnen er unieke of
    bijzondere aanpassingen aan de installatie zijn gedaan, die voordat het
    eerste onderhoud uitgevoerd kan worden, bekend en voorbereid moeten zijn.

    Voor aanvang van een onderhoudsovereenkomst moet daarom bij voor KVT
    onbekende installaties een zogenaamde “0” inspectie worden uitgevoerd.
    """;

    public const string JaarlijksPreventiefOnderhoud = """
    De noodstroominstallatie moet jaarlijks preventief onderhouden worden.
    Wij kunnen deze werkzaamheden voor u uitvoeren en aansluitend kortstondig
    testen op bestaande gebouwbelasting of anders onbelast.
    """;

    public static class OfferteStaticSections
    {
        public static string RenderUitgangspunten()
        {
            return """
        <section class="document-section page-break">
            <h1>1. Uitgangspunten</h1>

            <h2>1.1 “0” inspectie</h2>
            <p>
                ...
            </p>

            <h2>1.2 Jaarlijks preventief onderhoud</h2>
            <p>
                ...
            </p>

            ...
        </section>
        """;
        }

        public static string RenderWerkzaamheden()
        {
            return """
        <section class="document-section page-break">
            <h1>
                3. Omschrijving werkzaamheden jaarlijks
                preventief onderhoud
            </h1>

            <ul>
                <li>Uitvoeren van een Last Minute Risico Analyse (VCA)</li>
                <li>Controleren van het oliepeil...</li>
                ...
            </ul>
        </section>
        """;
        }

        public static string RenderVeiligheidEnKwaliteit()
        {
            return """
        ...
        """;
        }

        public static string RenderVerkoopvoorwaarden()
        {
            return """
        ...
        """;
        }
    }
    // enzovoort
}