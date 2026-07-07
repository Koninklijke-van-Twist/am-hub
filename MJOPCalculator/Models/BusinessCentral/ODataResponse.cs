using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;
/// <summary>
/// generic wrapper class for deserializing responses from Business Central OData endpoints
/// </summary>
public class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
}