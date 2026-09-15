using AMHub.Models.Documents;

namespace AMHub.Services.Documents;

public interface IOfferteDocumentService
{
    Task<OfferteDocumentModel?> GetAsync(
        string offerteNummer,
        CancellationToken cancellationToken = default);
}