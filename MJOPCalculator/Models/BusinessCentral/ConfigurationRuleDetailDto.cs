using System.Text.Json.Serialization;

public class ConfigurationRuleDetailDto
{
    [JsonPropertyName("Configuration_Line_No")]
    public int ConfigurationLineNo { get; set; }

    [JsonPropertyName("Component_No")]
    public string ComponentNo { get; set; } = "";
}