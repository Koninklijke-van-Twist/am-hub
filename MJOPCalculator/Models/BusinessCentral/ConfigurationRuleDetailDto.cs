using System.Text.Json.Serialization;


/// <summary>
/// Componenten in een configuratieregel van een offerte
/// </summary>
public class ConfigurationRuleDetailDto
{
    [JsonPropertyName("Configuration_Line_No")]
    public int ConfigurationLineNo { get; set; }

    [JsonPropertyName("Component_No")]
    public string ComponentNo { get; set; } = "";
}