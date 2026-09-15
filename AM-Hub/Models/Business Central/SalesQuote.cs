using System.Text.Json.Serialization;

namespace AMHub.Models.BusinessCentral;

public class SalesQuote
{
    [JsonPropertyName("Document_Type")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("No")]
    public string? Number { get; set; }

    [JsonPropertyName("Sell_to_Customer_No")]
    public string? CustomerNumber { get; set; }

    [JsonPropertyName("Sell_to_Customer_Name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("LVS_Job_Type")]
    public string? JobType { get; set; }

    [JsonPropertyName("LVS_Document_Status")]
    public string? DocumentStatus { get; set; }

    [JsonPropertyName("Order_Date")]
    public DateOnly? OrderDate { get; set; }

    [JsonPropertyName("Salesperson_Code")]
    public string? SalespersonCode { get; set; }

    [JsonPropertyName("KVT_Quote_Description")]
    public string? Description { get; set; }

    [JsonPropertyName("Sell_to_Contact")]
    public string? ContactName { get; set; }

    [JsonPropertyName("SellToPhoneNo")]
    public string? ContactPhone { get; set; }

    [JsonPropertyName("SellToMobilePhoneNo")]
    public string? ContactMobilePhone { get; set; }

    [JsonPropertyName("SellToEmail")]
    public string? ContactEmail { get; set; }
}