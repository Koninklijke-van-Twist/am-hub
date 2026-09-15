using AMHub.Models.Documents;

namespace AMHub.Services.Pdf;

public interface IOfferteHtmlRenderer
{
    Task<string> RenderAsync(
        OfferteDocumentModel model,
        CancellationToken cancellationToken = default);
}
