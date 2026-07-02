using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class QuoteDetailsDto
{
    [JsonPropertyName("No")]
    public string QuoteNo { get; set; } = "";

    [JsonPropertyName("Sell_to_Customer_No")]
    public string Customer { get; set; } = "";

    [JsonPropertyName("Sell_to_Customer_Name")]
    public string CustomerName { get; set; } = "";

    [JsonPropertyName("LVS_Sell_to_Contact1")]
    public string ContactPerson { get; set; } = "";

    [JsonPropertyName("LVS_Your_Reference")]
    public string Reference { get; set; } = "";

    [JsonPropertyName("SellToEmail")]
    public string Email { get; set; } = "";

    [JsonPropertyName("Salesperson_Code")]
    public string SalespersonCode { get; set; } = "";

    
    
}