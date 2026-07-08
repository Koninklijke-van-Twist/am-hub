using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

/// <summary>
/// offerte details zoals opgehaald uit Business Central
/// </summary>

public class QuoteDetailsDto
{
    [JsonPropertyName("No")]
    public string QuoteNo { get; set; } = "";
    
    [JsonPropertyName("KVT_Quote_Description ")]
    public string Description { get; set; } = "";

    [JsonPropertyName("Sell_to_Customer_No")]
    public string CustomerNo { get; set; } = "";

    [JsonPropertyName("Sell_to_Customer_Name")]
    public string CustomerName { get; set; } = "";

    [JsonPropertyName("LVS_Sell_to_Contact1")]
    public string ContactPerson { get; set; } = "";

    [JsonPropertyName("LVS_Your_Reference")]
    public string Reference { get; set; } = "";

    [JsonPropertyName("SellToEmail")]
    public string Email { get; set; } = "";

    [JsonPropertyName("SellToPhoneNo")]
    public string Phone { get; set; } = "";

    [JsonPropertyName("Salesperson_Code")]
    public string SalespersonCode { get; set; } = "";

    [JsonPropertyName("LVS_Document_Status")]
    public string DocumentStatus { get; set; } = "";

    
    
}