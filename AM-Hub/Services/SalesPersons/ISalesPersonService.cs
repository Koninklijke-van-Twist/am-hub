using AMHub.Models.BusinessCentral;

namespace AMHub.Services.SalesPersons;

public interface ISalesPersonService
{
    Task<SalesPersonCard?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
}