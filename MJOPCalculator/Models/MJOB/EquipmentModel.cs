namespace MJOP.Calculator.Models;

/// <summary>
/// Represents a specific equipment model type with custom maintenance costs
/// </summary>
public class EquipmentModel
{
    public int Id { get; set; }
    
    public string Brand { get; set; } = string.Empty;
    
    public string ModelName { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom maintenance costs for this model. Key = maintenance item name, Value = cost
    /// </summary>
    public Dictionary<string, decimal> CustomCosts { get; set; } = new();
    
    /// <summary>
    /// Custom maintenance frequencies for this model. Key = maintenance item name, Value = frequency per year
    /// </summary>
    /// 
    public Dictionary<string, decimal> CustomFrequencies { get; set; } = new();
    
    /// <summary>
    /// Custom maintenance intervals for this model. 
    /// </summary>
    /// 
    public Dictionary<string, int> CustomInterval { get; set; } = new();
    
   
    public decimal? GetCustomCost(string itemName)
    {
        if (CustomCosts.TryGetValue(itemName, out var cost))
        {
            return cost;
        }
        return null;
    }
    public decimal? GetCustomFrequency(string itemName)
    {
        if (CustomFrequencies.TryGetValue(itemName, out var frequency))
        {
            return frequency;
        }
        
        return null;
    }
    public decimal? GetCustomInterval(string itemName)
    {
        if (CustomInterval.TryGetValue(itemName, out var interval))
        {
            return interval;
        }
        
        return null;
    }
}
