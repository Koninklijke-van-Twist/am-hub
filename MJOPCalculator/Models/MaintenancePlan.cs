namespace MJOP.Calculator.Models;

/// <summary>
/// Represents a year's maintenance plan with all scheduled items and costs
/// </summary>
public class YearPlan
{
    public int Year { get; set; }
    
    public List<MaintenanceItemPlan> Items { get; set; } = new();
    
    public decimal TotalCost => Items.Sum(x => x.TotalCost);
    
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Represents a maintenance item within a specific year's plan
/// </summary>
public class MaintenanceItemPlan
{
    public string Name { get; set; } = string.Empty;
    
    public decimal CostPerOccurrence { get; set; }
    
    public decimal Occurrences { get; set; }
    
    public decimal TotalCost => CostPerOccurrence * Occurrences;
    
    public string Frequency { get; set; } = string.Empty;
}

/// <summary>
/// Represents a complete multi-year maintenance plan
/// </summary>
public class MaintenancePlan
{
    public Equipment Equipment { get; set; } = new();
    
    public List<YearPlan> YearPlans { get; set; } = new();
    
    public int TotalYears => YearPlans.Count;
    
    public decimal TotalCost => YearPlans.Sum(x => x.TotalCost);
    
    public decimal AverageCostPerYear => TotalYears > 0 ? TotalCost / TotalYears : 0;
}
