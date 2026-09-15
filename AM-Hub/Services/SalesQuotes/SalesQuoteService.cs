using AMHub.Models.BusinessCentral;
using AMHub.Services.BusinessCentral;

namespace AMHub.Services.SalesQuotes;

public class SalesQuoteService : ISalesQuoteService
{
    private readonly IBusinessCentralClient _bc;

    public SalesQuoteService(IBusinessCentralClient bc)
    {
        _bc = bc;
    }

    public Task<List<SalesQuote>> GetContractQuotesAsync(
        CancellationToken cancellationToken = default)
    {
        return _bc.GetAsync<SalesQuote>(
            "SalesQuote",
            new ODataQuery
            {
                Filter =
                    "Document_Type eq 'Quote' and LVS_Job_Type eq 'Contract'",

                Select =
                    "Document_Type,No,Sell_to_Customer_No," +
                    "Sell_to_Customer_Name,LVS_Job_Type," +
                    "LVS_Document_Status,Order_Date," +
                    "Salesperson_Code,KVT_Quote_Description"
            },
            cancellationToken);
    }

    public Task<List<SalesQuote>> GetForSalespersonAsync(
        string salespersonCode,
        CancellationToken cancellationToken = default)
    {
        var escapedCode = salespersonCode.Replace("'", "''");

        return _bc.GetAsync<SalesQuote>(
            "SalesQuote",
            new ODataQuery
            {
                Filter =
                    $"Salesperson_Code eq '{escapedCode}'",

                OrderBy = "Order_Date desc"
            },
            cancellationToken);
    }

    public async Task<SalesQuote?> GetByNumberAsync(
    string number,
    CancellationToken cancellationToken = default)
    {
        var escapedNumber = number.Replace("'", "''");

        var quotes = await _bc.GetAsync<SalesQuote>(
            "SalesQuote",
            new ODataQuery
            {
                Filter =
                    $"Document_Type eq 'Quote' and No eq '{escapedNumber}'",

                Top = 1
            },
            cancellationToken);

        return quotes.FirstOrDefault();
    }
}