using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class Component
{

[JsonPropertyName("No")]
    public string No { get; set; } = "";

    [JsonPropertyName("Description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("Description_2")]
    public string Description2 { get; set; } = "";

    [JsonPropertyName("Serial_No")]
    public string SerialNo { get; set; } = "";

    [JsonPropertyName("Status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("Main_Entity")]
    public string LocationNo { get; set; } = "";

    [JsonPropertyName("Main_Entity_Description")]
    public string LocationName { get; set; } = "";

    [JsonPropertyName("Manufacturer_Code")]
    public string ManufacturerCode { get; set; } = "";

    [JsonPropertyName("Manufacturer_Name")]
    public string ManufacturerName { get; set; } = "";

    [JsonPropertyName("Manufacturer_Model")]
    public string ManufacturerModel { get; set; } = "";

    [JsonPropertyName("Manufacturer_Item_No")]
    public string ManufacturerItemNo { get; set; } = "";

    [JsonPropertyName("Part_Item_No")]
    public string PartItemNo { get; set; } = "";

    [JsonPropertyName("Critical")]
    public bool Critical { get; set; }

    [JsonPropertyName("Date_of_Installation")]
    public DateTime DateOfInstallation { get; set; }

    [JsonPropertyName("Sell_to_Customer_No")]
    public string CustomerNo { get; set; } = "";

    [JsonPropertyName("Software_Version")]
    public string SoftwareVersion { get; set; } = "";

    [JsonPropertyName("Memo")]
    public string Memo { get; set; } = "";

    public List<ComponentTask> ComponentTasks { get; set; } = new();

    public decimal TotalPrice => ComponentTasks.Sum(x => x.Price ?? 0m);

    
}