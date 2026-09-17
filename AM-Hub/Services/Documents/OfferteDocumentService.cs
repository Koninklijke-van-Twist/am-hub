using AMHub.Models.BusinessCentral;
using AMHub.Models.Documents;
using AMHub.Services.Customers;
using AMHub.Services.Documents;
using AMHub.Services.SalesQuotes;
using AMHub.Services.SalesPersons;

public class OfferteDocumentService : IOfferteDocumentService
{
    private readonly ISalesQuoteService _salesQuotes;
    private readonly IConfigurationRuleService _configurationRules;
    private readonly IAppCustomerService _customers;
    private readonly ISalesPersonService _salesPersons;

    public OfferteDocumentService(
        ISalesQuoteService salesQuotes,
        IConfigurationRuleService configurationRules,
        IAppCustomerService customers,
        ISalesPersonService salesPersons)
        {
            _salesQuotes = salesQuotes;
            _configurationRules = configurationRules;
            _customers = customers;
            _salesPersons = salesPersons;
        }

    public async Task<OfferteDocumentModel?> GetAsync(
        string offerteNummer,
        CancellationToken cancellationToken = default)
    {
        // Eerst offerte en configuratiematrix tegelijk ophalen.
        var quoteTask =
            _salesQuotes.GetByNumberAsync(
                offerteNummer,
                cancellationToken);

        var matrixTask =
            _configurationRules.GetMatrixAsync(
                offerteNummer,
                cancellationToken);

        // Deze keten wacht alleen op de offerte, niet op de configuratiematrix.
        async Task<(AppCustomerCard? Customer, SalesPersonCard? SalesPerson)> GetContactsAsync()
        {
            var quote = await quoteTask;
            if (quote is null)
                return (null, null);

            var customerTask =
            !string.IsNullOrWhiteSpace(quote.CustomerNumber)
                ? _customers.GetByNumberAsync(
                    quote.CustomerNumber,
                    cancellationToken)
                : Task.FromResult<AppCustomerCard?>(null);

            var salesPersonTask =
            !string.IsNullOrWhiteSpace(quote.SalespersonCode)
                ? _salesPersons.GetByCodeAsync(
                    quote.SalespersonCode,
                    cancellationToken)
                : Task.FromResult<SalesPersonCard?>(null);

            await Task.WhenAll(customerTask, salesPersonTask);
            return (await customerTask, await salesPersonTask);
        }

        var contactsTask = GetContactsAsync();
        // Wacht ook bij fouten op beide takken, zodat er geen aanvragen achterblijven.
        await Task.WhenAll(matrixTask, contactsTask);

        var quote = await quoteTask;
        var matrix = await matrixTask;
        if (quote is null)
            return null;

        var (customer, salesPerson) = await contactsTask;

        var model = new OfferteDocumentModel
            {
                OfferteNummer =
                    quote.Number ?? "",

                Datum =
                    quote.OrderDate
                    ?? DateOnly.FromDateTime(DateTime.Today),

                GeldigTot =
                    (quote.OrderDate
                        ?? DateOnly.FromDateTime(DateTime.Today))
                    .AddMonths(1),

                KlantNaam =
                    customer?.Name
                    ?? quote.CustomerName
                    ?? "",

                ContactPersoon =
                    quote.ContactName
                    ?? "",

                Email =
                    quote.ContactEmail
                    ?? "",

                Adres =
                    BuildAddress(
                        customer?.Address,
                        customer?.Address2),

                Postcode =
                    customer?.PostCode
                    ?? "",

                Plaats =
                    customer?.City
                    ?? "",

            AccountmanagerCode =
                    salesPerson?.Code ?? "",

            AccountmanagerNaam =
                    salesPerson?.Name ?? "",

                AccountmanagerFunctie =
                    salesPerson?.JobTitle ?? "",

                AccountmanagerEmail =
                    salesPerson?.Email ?? "",

                AccountmanagerTelefoon =
                    salesPerson?.PhoneNo ?? ""

        };

        foreach (var component in matrix.Components)
        {
            var documentComponent = new OfferteComponent
            {
                ComponentNo =
                    component.ComponentNo,

                Omschrijving =
                    component.ComponentDescription,

                Locatie =
                    component.LocationDescription
            };

            foreach (var task in matrix.Tasks)
            {
                if (!task.Prices.TryGetValue(
                        component.ComponentNo,
                        out var price) ||
                    price is null)
                {
                    continue;
                }

                documentComponent.Werkzaamheden.Add(
                    new OfferteWerkzaamheid
                    {
                        Code =
                            task.TaskCode,

                        Omschrijving =
                            task.Description,

                        // Voorlopig nog leeg zolang frequentie
                        // niet in je matrixmodel zit.
                        Frequentie = "",

                        Prijs =
                            price.Value,

                        Aantal = 1
                    });
            }

            model.Components.Add(
                documentComponent);
        }

        return model;
    }

    private static string BuildAddress(
        string? address,
        string? address2)
    {
        var parts = new[]
        {
            address,
            address2
        }
        .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(
            " ",
            parts);
    }
}
