using AMHub.Models.BusinessCentral;
using AMHub.Models.ViewModels;

public interface IConfigurationRuleService
{
    Task<List<ConfigurationRule>> GetByConfigurationNumberAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default);

    Task<List<ConfigurationRuleDetail>> GetDetailsByConfigurationNumberAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default);

    Task<List<ConfigurationRule>> GetWithDetailsAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default);
    Task<ConfigurationMatrix> GetMatrixAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default);
}