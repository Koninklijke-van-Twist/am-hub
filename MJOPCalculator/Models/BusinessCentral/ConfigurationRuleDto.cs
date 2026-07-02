using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class ConfigurationRuleDto
{
    [JsonPropertyName("Configuration_No")]
    public string ConfigurationNo { get; set; } = "";

    [JsonPropertyName("Line_No")]
    public int LineNo { get; set; }

    [JsonPropertyName("Job_Task_Type")]
    public string JobTaskType { get; set; } = "";

    [JsonPropertyName("Sub_Entity")]
    public string SubEntity { get; set; } = "";

    [JsonPropertyName("Item_No")]
    public string ItemNo { get; set; } = "";

    [JsonPropertyName("Description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("Selected")]
    public bool Selected { get; set; }

    [JsonPropertyName("Quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("Unit_Price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("Line_Amount")]
    public decimal LineAmount { get; set; }

    public List<string> ComponentNos { get; set; } = new();
    
}