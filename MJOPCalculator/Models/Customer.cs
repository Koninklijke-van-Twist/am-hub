using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class Customer
{
    // Basisgegevens
    [JsonPropertyName("No")]
    public string No { get; set; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Name_2")]
    public string Name2 { get; set; } = "";

    [JsonPropertyName("Search_Name")]
    public string SearchName { get; set; } = "";

    [JsonPropertyName("Blocked")]
    public string Blocked { get; set; } = "";

    [JsonPropertyName("Privacy_Blocked")]
    public bool PrivacyBlocked { get; set; }

    [JsonPropertyName("Priority")]
    public int Priority { get; set; }


    // Accountmanagement / interne verantwoordelijkheid
    [JsonPropertyName("Salesperson_Code")]
    public string SalespersonCode { get; set; } = "";

    [JsonPropertyName("LVS_After_Sales_Person_Code")]
    public string AfterSalesPersonCode { get; set; } = "";

    [JsonPropertyName("KVT_Service_coördinator")]
    public string ServiceCoordinator { get; set; } = "";

    [JsonPropertyName("Responsibility_Center")]
    public string ResponsibilityCenter { get; set; } = "";

    [JsonPropertyName("Service_Zone_Code")]
    public string ServiceZoneCode { get; set; } = "";

    [JsonPropertyName("KVT_Customer_Group_Report_Ext")]
    public string CustomerGroupReport { get; set; } = "";


    // Factuuradres / hoofdadres
    [JsonPropertyName("Address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("Address_2")]
    public string Address2 { get; set; } = "";

    [JsonPropertyName("Post_Code")]
    public string PostCode { get; set; } = "";

    [JsonPropertyName("City")]
    public string City { get; set; } = "";

    [JsonPropertyName("County")]
    public string County { get; set; } = "";

    [JsonPropertyName("Country_Region_Code")]
    public string CountryRegionCode { get; set; } = "";


    // Bezoekadres
    [JsonPropertyName("LVS_Visit_Address")]
    public string VisitAddress { get; set; } = "";

    [JsonPropertyName("LVS_Visit_Post_Code")]
    public string VisitPostCode { get; set; } = "";

    [JsonPropertyName("LVS_Visit_City")]
    public string VisitCity { get; set; } = "";

    [JsonPropertyName("LVS_Visit_Country_Region_Code")]
    public string VisitCountryRegionCode { get; set; } = "";

    [JsonPropertyName("LVS_Visit_County")]
    public string VisitCounty { get; set; } = "";


    // Contactgegevens
    [JsonPropertyName("Phone_No")]
    public string PhoneNo { get; set; } = "";

    [JsonPropertyName("MobilePhoneNo")]
    public string MobilePhoneNo { get; set; } = "";

    [JsonPropertyName("E_Mail")]
    public string Email { get; set; } = "";

    

    [JsonPropertyName("Location_Code")]
    public string LocationCode { get; set; } = "";

    

    


    // Memo / overig
    [JsonPropertyName("LVS_gcuBLOBMemoMgt_GetMemoText_Rec_x002C__Rec_FieldNo_LVS_Memo")]
    public string Memo { get; set; } = "";

    
}