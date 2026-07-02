using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
}