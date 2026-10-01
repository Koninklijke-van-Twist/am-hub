using AMHub.Models.BusinessCentral;
using AMHub.Services.BusinessCentral;
using AMHub.Services.ConfigurationRules;
using AMHub.Services.Customers;
using AMHub.Services.SalesPersons;
using AMHub.Services.SalesQuotes;

static class DocumentChecks
{
    public static async Task RunAsync()
    {
        var client = new DocumentClient();
        var rules = new ConfigurationRuleService(client);
        // Rules cannot complete before details start: a sequential regression times out.
        var matrix = await rules.GetMatrixAsync("Q'1").WaitAsync(TimeSpan.FromSeconds(5));
        Check(matrix.Components.Count == 3, "Component fallback/deduplication changed");
        Check(matrix.Tasks.Count == 2 && matrix.Tasks[0].Description == "First description", "Task order changed");
        Check(matrix.Tasks[0].Prices["C1"] == 100m && matrix.Tasks[0].Prices["C2"] == 200m &&
            matrix.Tasks[0].Prices["C3"] is null, "First matching price or missing-price semantics changed");
        Check(matrix.Tasks[1].Prices["C3"] == 0m, "Zero price was lost");
        Check(client.RuleQuery!.Filter!.Contains("Q''1") && client.RuleQuery.Select!.Contains("Unit_Price"), "Rule projection/escaping changed");
        var document = await new OfferteDocumentService(new SalesQuoteService(client), rules,
            new AppCustomerService(client), new SalesPersonService(client)).GetAsync("Q'1");
        Check(document?.Totaal == 300m && document.Components.Count == 3, "Document total/components changed");
        Check(document!.Adres == "Street 1 Suite 2" && document.Postcode == "1234AB" && document.AccountmanagerNaam == "Test Manager", "Document contact fields missing");
        Check(client.CustomerQuery!.Select == "No,Name,Address,Address_2,Post_Code,City", "PDF requested unnecessary customer financial fields");
        Console.WriteLine("Document checks passed: parallel rules/details, first matching price, zero/missing prices, totals, contacts, narrow customer query.");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private sealed class DocumentClient : IBusinessCentralClient
    {
        private readonly TaskCompletionSource detailsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ODataQuery? RuleQuery { get; private set; }
        public ODataQuery? CustomerQuery { get; private set; }

        public async Task<List<T>> GetAsync<T>(string webService, ODataQuery? query = null, CancellationToken cancellationToken = default)
        {
            switch (webService)
            {
                case "ConfigurationRules":
                    RuleQuery = query;
                    await detailsStarted.Task.WaitAsync(cancellationToken);
                    return (List<T>)(object)new List<ConfigurationRule>
                    {
                        new() { LineNo = 1, ItemNo = "T1", Description = "First description", ComponentNo = "C1", UnitPrice = 100m },
                        new() { LineNo = 2, ItemNo = "T1", Description = "Second description", ComponentNo = "C2", UnitPrice = 200m },
                        new() { LineNo = 3, ItemNo = "T2", Description = "Free task", ComponentNo = "C3", UnitPrice = 0m }
                    };
                case "ConfigurationRulesDetails":
                    detailsStarted.TrySetResult();
                    return (List<T>)(object)new List<ConfigurationRuleDetail>
                    {
                        new() { ConfigurationLineNo = 1, ComponentNo = "C1", ComponentDescription = "Generator", ServiceLocationDescription = "Site" },
                        new() { ConfigurationLineNo = 2, ComponentNo = "C1" },
                        new() { ConfigurationLineNo = 2, ComponentNo = "C2" }
                    };
                case "SalesQuote":
                    return (List<T>)(object)new List<SalesQuote> { new() { Number = "Q'1", CustomerNumber = "Customer", SalespersonCode = "LL" } };
                case "AppCustomerCard":
                    CustomerQuery = query;
                    return (List<T>)(object)new List<AppCustomerCard> { new() { Name = "Customer", Address = "Street 1", Address2 = "Suite 2", PostCode = "1234AB", City = "City" } };
                case "SalesPersonCard":
                    return (List<T>)(object)new List<SalesPersonCard> { new() { Code = "LL", Name = "Test Manager" } };
                default: throw new Exception($"Unexpected endpoint: {webService}");
            }
        }
    }
}
