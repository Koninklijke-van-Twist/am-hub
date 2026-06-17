using MJOP.Calculator.Models;

namespace MJOP.Calculator.Services;

public class MJOPCalculatorService
{
    /// <summary>
    /// Calculate a multi-year maintenance plan based on equipment specifications
    /// and the selected equipment model.
    /// </summary>
    public MaintenancePlan CalculatePlan(
        Equipment equipment,
        int years = 20,
        EquipmentModel? equipmentModel = null,
        HashSet<string>? excludedTaskNames = null,
        string? proefdraaiStand = null)
    {

        excludedTaskNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (equipmentModel == null)
        {
            throw new ArgumentNullException(nameof(equipmentModel),
                "Een EquipmentModel is verplicht om het onderhoudsplan te berekenen.");
        }

        var plan = new MaintenancePlan { Equipment = equipment };

        var maintenanceDefinitions = BuildMaintenanceDefinitions(equipmentModel);

        for (int year = equipment.ContractStartYear; year < equipment.ContractStartYear + years; year++)
        {
            var yearPlan = new YearPlan { Year = year };
            int equipmentAge = equipment.GetAgeInYear(year);
            int serviceYear = year - equipment.ContractStartYear + 1;

            foreach (var item in maintenanceDefinitions)
            {

                if (excludedTaskNames.Contains(item.Name))
                    continue;

                if (!ShouldPerformMaintenance(item, equipmentAge, serviceYear))
                    continue;

                decimal  occurrences;
                
                if (item.Name == "Proefdraaien")
                {
                    occurrences = CalculateProefdraaiOccurrences(item, equipmentAge, serviceYear, proefdraaiStand, excludedTaskNames);
                }
                else
                {
                    occurrences = CalculateOccurrences(item, equipmentAge, serviceYear);
                }

                if (occurrences <= 0)
                    continue;

                yearPlan.Items.Add(new MaintenanceItemPlan
                {
                    Name = item.Name,
                    CostPerOccurrence = item.CostPerOccurrence,
                    Occurrences = occurrences,
                    Frequency = GetFrequencyDescription(item)
                });
            }

            plan.YearPlans.Add(yearPlan);
        }

        return plan;
    }

    /// <summary>
    /// Maak onderhoudsdefinities op basis van het geselecteerde model.
    /// We nemen de union van alle keys uit kosten, frequenties en interval.
    /// </summary>
    
    
    public List<string> GetMaintenanceTaskNames(EquipmentModel equipmentModel)
        {
            return BuildMaintenanceDefinitions(equipmentModel)
                .Select(x => x.Name)
                .ToList();
        }

    private List<ModelMaintenanceDefinition> BuildMaintenanceDefinitions(EquipmentModel equipmentModel)
    {
        var itemNames = equipmentModel.CustomCosts.Keys
            .Union(equipmentModel.CustomFrequencies.Keys)
            .Union(equipmentModel.CustomInterval.Keys)
            .Distinct()
            .ToList();

        var result = new List<ModelMaintenanceDefinition>();

        foreach (var itemName in itemNames)
        {
            var cost = equipmentModel.CustomCosts.TryGetValue(itemName, out var itemCost)
                ? itemCost
                : 0m;

            var frequency = equipmentModel.CustomFrequencies.TryGetValue(itemName, out var itemFrequency)
                ? itemFrequency
                : 1m;

            var interval = equipmentModel.CustomInterval.TryGetValue(itemName, out var itemInterval)
                ? itemInterval
                : 1;

            result.Add(new ModelMaintenanceDefinition
            {
                Name = itemName,
                CostPerOccurrence = cost,
                FrequencyPerExecutionYear = frequency,
                IntervalYears = interval
            });
        }

        return result
            .OrderBy(x => GetSortOrder(x.Name))
            .ThenBy(x => x.Name)
            .ToList();
    }

    
    private bool ShouldPerformMaintenance(ModelMaintenanceDefinition item, int equipmentAge, int serviceYear)
    {
        // "0" Inspectie alleen in contractjaar 1
        if (item.Name == "\"0\" Inspectie")
        {
            return serviceYear == 1;
        }

        // Jaarlijkse taken
        if (item.IntervalYears <= 1)
        {
            return true;
        }

        int interval = item.IntervalYears;
        int ageAtContractStart = equipmentAge - serviceYear + 1;

        // Als het object bij contractstart al ouder is dan het interval:
        // meteen uitvoeren in jaar 1, daarna elke interval-jaren opnieuw
        if (ageAtContractStart > interval)
        {
            return (serviceYear - 1) % interval == 0;
        }

        // Anders gewoon op basis van bouwjaar / leeftijd
        return equipmentAge >= interval && equipmentAge % interval == 0;
    }

    /// <summary>
    /// Bepaalt hoe vaak een taak in het uitvoeringsjaar voorkomt.
    /// Bijvoorbeeld Proefdraaien = 11.
    /// </summary>
    private decimal CalculateOccurrences(ModelMaintenanceDefinition item, int equipmentAge, int serviceYear)
    {
        if (!ShouldPerformMaintenance(item, equipmentAge, serviceYear))
            return 0;

        return item.FrequencyPerExecutionYear;
    }

    private decimal CalculateProefdraaiOccurrences(ModelMaintenanceDefinition item, int equipmentAge, int serviceYear, string? proefdraaiStand, HashSet<string>? excludedTaskNames = null   )
    {
        int frequency = proefdraaiStand switch
        {
            "Maandelijks" => 12,
            "2-Maandelijks" => 6,
            "3-Maandelijks" => 4,
            _ => (int)item.FrequencyPerExecutionYear // default uit model
        };

        if (excludedTaskNames?.Contains("Jaarlijks onderhoud") != true)
        {
            frequency -= 1;
        }

        if (excludedTaskNames?.Contains("Halfjaarlijks onderhoud") != true)
        {
            frequency -= 1;
        }

        return frequency;

    }

    /// <summary>
    /// Menselijke omschrijving voor de frequentie.
    /// </summary>
    private string GetFrequencyDescription(ModelMaintenanceDefinition item)
    {
        if (item.Name == "\"0\" Inspectie")
            return "Eenmalig bij start contract";

        if (item.IntervalYears <= 1)
        {
            if (item.FrequencyPerExecutionYear == 1)
                return "Jaarlijks";

            return $"{item.FrequencyPerExecutionYear}x per jaar";
        }

        if (item.FrequencyPerExecutionYear == 1)
            return $"Elke {item.IntervalYears} jaar";

        return $"{item.FrequencyPerExecutionYear}x elke {item.IntervalYears} jaar";
    }

    /// <summary>
    /// Optioneel: vaste volgorde in je output houden.
    /// </summary>
    private int GetSortOrder(string itemName)
    {
        var order = new List<string>
        {
            "\"0\" Inspectie",
            "Jaarlijks Onderhoud",
            "Proefdraaien",
            "Brandstoftank Controle",
            "Smeerolie Analyse",
            "Brandstofanalyse",
            "Koelmedium Analyse",
            "Storingsdienst",
            "Belasten Loadbank",
            "E-Inspectie",
            "Waterslangen",
            "Accu's",
            "Injectoren",
            "Partiële Revisie"
        };

        var index = order.IndexOf(itemName);
        return index >= 0 ? index : int.MaxValue;
    }

    private sealed class ModelMaintenanceDefinition
    {
        public string Name { get; set; } = string.Empty;
        public decimal CostPerOccurrence { get; set; }
        public decimal FrequencyPerExecutionYear { get; set; }
        public int IntervalYears { get; set; }
    }
}
