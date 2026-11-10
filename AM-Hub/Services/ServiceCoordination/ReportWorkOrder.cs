using System.Text.Json.Serialization;

namespace AMHub.Services.ServiceCoordination;

public sealed class ReportWorkOrder
{
    [JsonPropertyName("No")] public string Number { get; set; } = "";
    [JsonPropertyName("Task_Code")] public string TaskCode { get; set; } = "";
    [JsonPropertyName("Task_Description")] public string Description { get; set; } = "";
    [JsonPropertyName("Resource_Name")] public string ResourceName { get; set; } = "";
    [JsonPropertyName("Start_Date")] public DateOnly? Date { get; set; }
    [JsonPropertyName("Status")] public string Status { get; set; } = "";
    [JsonPropertyName("KVT_Service_Coordinator")] public string Coordinator { get; set; } = "";
    [JsonPropertyName("KVT_URL_Workorder_Report_PDF")] public string PdfUrl { get; set; } = "";
    [JsonIgnore] public string? SafePdfUrl => Uri.TryCreate(PdfUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;
}
