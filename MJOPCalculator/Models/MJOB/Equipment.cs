namespace MJOP.Calculator.Models;

/// <summary>
/// Represents equipment for which maintenance is planned
/// </summary>
public class Equipment
{
    public int BuildYear { get; set; }
    
    /// <summary>
    /// The year when the maintenance contract starts
    /// </summary>
    public int ContractStartYear { get; set; }
    
    /// <summary>
    /// Get the age of the equipment in a given year
    /// </summary>
    public int GetAgeInYear(int year)
    {
        return year - BuildYear;
    }
    
    /// <summary>
    /// Get the service years (years since contract start)
    /// </summary>
    public int GetServiceYearsInYear(int year)
    {
        return year - ContractStartYear;
    }
}
