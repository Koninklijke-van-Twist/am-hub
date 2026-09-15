using AMHub.Models.BusinessCentral;

namespace AMHub.Services.SalesQuotes;

public interface ISalesQuoteService
{
    Task<List<SalesQuote>> GetContractQuotesAsync(
        CancellationToken cancellationToken = default);

    Task<List<SalesQuote>> GetForSalespersonAsync(
        string salespersonCode,
        CancellationToken cancellationToken = default);
    Task<SalesQuote?> GetByNumberAsync(
    string number,
    CancellationToken cancellationToken = default);
}