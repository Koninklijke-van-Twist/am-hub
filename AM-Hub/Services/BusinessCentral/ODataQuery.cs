namespace AMHub.Services.BusinessCentral;

public class ODataQuery
{
    public string? Filter { get; set; }
    public string? Select { get; set; }
    public string? OrderBy { get; set; }
    public int? Top { get; set; }
    public string? Expand { get; set; }
}