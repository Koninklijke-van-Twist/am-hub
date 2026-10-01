using AMHub.Models.BusinessCentral;

namespace AMHub.Services.Customers;

public interface IAppCustomerService
{
    Task<AppCustomerCard?> GetForDocumentAsync(
        string customerNumber,
        CancellationToken cancellationToken = default);

    Task<AppCustomerCard?> GetByNumberAsync(
        string customerNumber,
        CancellationToken cancellationToken = default);
}
