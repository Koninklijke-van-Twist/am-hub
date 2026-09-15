namespace AMHub.Models.ViewModels;

public class ConfigurationMatrix
{
    public List<ConfigurationComponentColumn> Components { get; set; } = [];
    public List<ConfigurationTaskRow> Tasks { get; set; } = [];
}

public class ConfigurationComponentColumn
{
    public string ComponentNo { get; set; } = string.Empty;
    public string ComponentDescription { get; set; } = string.Empty;
    public string SubEntity { get; set; } = string.Empty;
    public string LocationDescription { get; set; } = string.Empty;
}

public class ConfigurationTaskRow
{
    public string TaskCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Dictionary<string, decimal?> Prices { get; set; } = [];
}