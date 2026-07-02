using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class ComponentTask
{
    public string Type { get; set; } = "";
    public string Description {get; set; } = "";
    public decimal? Price { get; set; } = 0m;
    
}

