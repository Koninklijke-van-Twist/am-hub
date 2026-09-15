namespace AMHub.Services.BusinessCentral;

public interface IBusinessCentralClient
{
    Task<List<T>> GetAsync<T>(
        string webService,
        ODataQuery? query = null,
        CancellationToken cancellationToken = default);
}