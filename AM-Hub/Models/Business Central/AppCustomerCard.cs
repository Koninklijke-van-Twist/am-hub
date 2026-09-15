using System.Text.Json.Serialization;

namespace AMHub.Models.BusinessCentral;

public class AppCustomerCard
{
    [JsonPropertyName("No")]
    public string? No { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Name_2")]
    public string? Name2 { get; set; }

    [JsonPropertyName("Search_Name")]
    public string? SearchName { get; set; }

    [JsonPropertyName("Balance_LCY")]
    public decimal BalanceLcy { get; set; }

    [JsonPropertyName("Balance_Due_LCY")]
    public decimal BalanceDueLcy { get; set; }

    [JsonPropertyName("Credit_Limit_LCY")]
    public decimal CreditLimitLcy { get; set; }

    [JsonPropertyName("Blocked")]
    public string? Blocked { get; set; }

    [JsonPropertyName("Salesperson_Code")]
    public string? SalespersonCode { get; set; }

    [JsonPropertyName("LVS_After_Sales_Person_Code")]
    public string? AfterSalesPersonCode { get; set; }

    [JsonPropertyName("KVT_Service_coördinator")]
    public string? ServiceCoordinator { get; set; }

    [JsonPropertyName("Address")]
    public string? Address { get; set; }

    [JsonPropertyName("Address_2")]
    public string? Address2 { get; set; }

    [JsonPropertyName("Post_Code")]
    public string? PostCode { get; set; }

    [JsonPropertyName("City")]
    public string? City { get; set; }

    [JsonPropertyName("County")]
    public string? County { get; set; }

    [JsonPropertyName("Country_Region_Code")]
    public string? CountryRegionCode { get; set; }

    [JsonPropertyName("Phone_No")]
    public string? PhoneNo { get; set; }

    [JsonPropertyName("MobilePhoneNo")]
    public string? MobilePhoneNo { get; set; }

    [JsonPropertyName("E_Mail")]
    public string? Email { get; set; }

    [JsonPropertyName("Home_Page")]
    public string? HomePage { get; set; }

    [JsonPropertyName("Primary_Contact_No")]
    public string? PrimaryContactNo { get; set; }

    [JsonPropertyName("ContactName")]
    public string? ContactName { get; set; }

    [JsonPropertyName("VAT_Registration_No")]
    public string? VatRegistrationNo { get; set; }

    [JsonPropertyName("KVT_Chamber_Of_Commerce_No")]
    public string? ChamberOfCommerceNo { get; set; }

    [JsonPropertyName("Payment_Terms_Code")]
    public string? PaymentTermsCode { get; set; }

    [JsonPropertyName("Payment_Method_Code")]
    public string? PaymentMethodCode { get; set; }

    [JsonPropertyName("Currency_Code")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("Last_Date_Modified")]
    public DateOnly? LastDateModified { get; set; }
}