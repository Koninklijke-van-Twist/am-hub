using System.Text.Json.Serialization;

namespace AMHub.Models.BusinessCentral;

public class ConfigurationRuleDetail
{
    [JsonPropertyName("Configuration_No")]
    public string? ConfigurationNo { get; set; }

    [JsonPropertyName("Configuration_Version_No")]
    public int ConfigurationVersionNo { get; set; }

    [JsonPropertyName("Configuration_Line_No")]
    public int ConfigurationLineNo { get; set; }

    [JsonPropertyName("Main_Entity")]
    public string? MainEntity { get; set; }

    [JsonPropertyName("Component_No")]
    public string? ComponentNo { get; set; }

    [JsonPropertyName("Sub_Entity")]
    public string? SubEntity { get; set; }

    [JsonPropertyName("KVT_Service_Loc_Description")]
    public string? ServiceLocationDescription { get; set; }

    [JsonPropertyName("KVT_Component_Description")]
    public string? ComponentDescription { get; set; }

    [JsonPropertyName("Qty_per_Main_Entity")]
    public decimal QuantityPerMainEntity { get; set; }
}