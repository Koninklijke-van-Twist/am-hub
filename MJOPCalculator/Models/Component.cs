using System.Text.Json.Serialization;

namespace MJOP.Calculator.Models.BusinessCentral;

public class Component
{
    public string ComponentNo { get; set; } = "";


    public List<ComponentTask> ComponentTasks { get; set; } = new();

    public decimal TotalPrice => ComponentTasks.Sum(x => x.Price ?? 0m);

    
}