using System.Text.Json.Serialization;

namespace AMHub.Models.BusinessCentral;

public class ConfigurationRule
{
    [JsonPropertyName("Configuration_No")]
    public string? ConfigurationNo { get; set; }

    [JsonPropertyName("Configuration_Version_No")]
    public int ConfigurationVersionNo { get; set; }

    [JsonPropertyName("Line_No")]
    public int LineNo { get; set; }

    [JsonPropertyName("Job_Task_Type")]
    public string? JobTaskType { get; set; }

    [JsonPropertyName("Sub_Entity")]
    public string? SubEntity { get; set; }

    [JsonPropertyName("Sub_Entity_Description")]
    public string? SubEntityDescription { get; set; }

    [JsonPropertyName("Item_No")]
    public string? ItemNo { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("Selected")]
    public bool Selected { get; set; }

    [JsonPropertyName("Quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("Unit_Price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("Total_Price")]
    public decimal TotalPrice { get; set; }

    [JsonPropertyName("Component_No")]
    public string? ComponentNo { get; set; }

    public List<ConfigurationRuleDetail> Details { get; set; } = [];
}