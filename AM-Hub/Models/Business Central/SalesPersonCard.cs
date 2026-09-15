using System.Text.Json.Serialization;

namespace AMHub.Models.BusinessCentral;

public class SalesPersonCard
{
    [JsonPropertyName("Code")]
    public string? Code { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Job_Title")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("Commission_Percent")]
    public decimal CommissionPercent { get; set; }

    [JsonPropertyName("Phone_No")]
    public string? PhoneNo { get; set; }

    [JsonPropertyName("KVT_Direct_Phone_No")]
    public string? DirectPhoneNo { get; set; }

    [JsonPropertyName("E_Mail")]
    public string? Email { get; set; }

    [JsonPropertyName("Next_Task_Date")]
    public DateOnly? NextTaskDate { get; set; }

    [JsonPropertyName("Privacy_Blocked")]
    public bool PrivacyBlocked { get; set; }

    [JsonPropertyName("Blocked")]
    public bool Blocked { get; set; }

    [JsonPropertyName("Global_Dimension_1_Code")]
    public string? GlobalDimension1Code { get; set; }

    [JsonPropertyName("Global_Dimension_2_Code")]
    public string? GlobalDimension2Code { get; set; }
}