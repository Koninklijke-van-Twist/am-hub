namespace AMHub.Models.Documents;

public class OfferteDocumentModel
{
    public string OfferteNummer { get; set; } = "";
    public DateOnly Datum { get; set; }
    public DateOnly GeldigTot { get; set; }

    public string KlantNaam { get; set; } = "";
    public string ContactPersoon { get; set; } = "";
    public string Email { get; set; } = "";
    public string Adres { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Plaats { get; set; } = "";

    public string AccountmanagerCode { get; set; } = "";
    public string AccountmanagerNaam { get; set; } = "";
    public string AccountmanagerFunctie { get; set; } = "";
    public string AccountmanagerEmail { get; set; } = "";
    public string AccountmanagerTelefoon { get; set; } = "";

    public List<OfferteComponent> Components { get; set; } = [];

    public decimal OverigeWerkzaamheden { get; set; }

    public decimal Totaal =>
        Components.Sum(x => x.Totaal) + OverigeWerkzaamheden;
}

public class OfferteComponent
{
    public string ComponentNo { get; set; } = "";
    public string Omschrijving { get; set; } = "";
    public string Locatie { get; set; } = "";

    public List<OfferteWerkzaamheid> Werkzaamheden { get; set; } = [];

    public decimal Totaal =>
        Werkzaamheden.Sum(x => x.Totaal);
}

public class OfferteWerkzaamheid
{
    public string Code { get; set; } = "";
    public string Omschrijving { get; set; } = "";
    public string Frequentie { get; set; } = "";
    public int Aantal { get; set; }
    public decimal Prijs { get; set; }

    public decimal Totaal => Aantal * Prijs;
}
