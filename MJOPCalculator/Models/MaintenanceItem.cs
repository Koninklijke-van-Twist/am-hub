namespace MJOP.Calculator.Models;

/// <summary>
/// Represents a maintenance item with its frequency and cost
/// </summary>
public class MaintenanceItem
{
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Frequency per year (e.g., 1 = annually, 0.25 = every 4 years, 11 = 11 times per year)
    /// </summary>
    public decimal FrequencyPerYear { get; set; }
    
    /// <summary>
    /// Cost per occurrence
    /// </summary>
    public decimal CostPerOccurrence { get; set; }
    
    /// <summary>
    /// Calculate the annual cost of this maintenance item
    /// </summary>
    public decimal GetAnnualCost()
    {
        return FrequencyPerYear * CostPerOccurrence;
    }
}
