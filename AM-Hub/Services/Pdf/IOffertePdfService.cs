namespace AMHub.Services.Pdf;

public interface IOffertePdfService
{
    Task<byte[]> GenerateAsync(
        string offerteNummer,
        CancellationToken cancellationToken = default);
}
