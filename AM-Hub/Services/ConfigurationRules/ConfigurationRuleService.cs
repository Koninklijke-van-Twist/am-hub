using AMHub.Models.BusinessCentral;
using AMHub.Models.ViewModels;
using AMHub.Services.BusinessCentral;

namespace AMHub.Services.ConfigurationRules;

public class ConfigurationRuleService : IConfigurationRuleService
{
    private readonly IBusinessCentralClient _bc;

    public ConfigurationRuleService(IBusinessCentralClient bc)
    {
        _bc = bc;
    }

    public Task<List<ConfigurationRule>> GetByConfigurationNumberAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default)
    {
        var escapedNumber = configurationNumber.Replace("'", "''");

        return _bc.GetAsync<ConfigurationRule>(
            "ConfigurationRules",
            new ODataQuery
            {
                Filter = $"Configuration_No eq '{escapedNumber}' and Selected eq true",
                OrderBy = "Line_No"
            },
            cancellationToken);
    }

    public Task<List<ConfigurationRuleDetail>> GetDetailsByConfigurationNumberAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default)
    {
        var escapedNumber = configurationNumber.Replace("'", "''");

        return _bc.GetAsync<ConfigurationRuleDetail>(
            "ConfigurationRulesDetails",
            new ODataQuery
            {
                Filter = $"Configuration_No eq '{escapedNumber}'",
                OrderBy = "Configuration_Line_No"
            },
            cancellationToken);
    }

    public async Task<List<ConfigurationRule>> GetWithDetailsAsync(
        string configurationNumber,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetByConfigurationNumberAsync(
                configurationNumber,
                cancellationToken);

        var linesNeedingDetails = rules
            .Where(rule => string.IsNullOrWhiteSpace(rule.ComponentNo))
            .Select(rule => rule.LineNo)
            .Distinct()
            .ToArray();

        var details = new List<ConfigurationRuleDetail>();
        var escapedNumber = configurationNumber.Replace("'", "''");

        // Beperk de querylengte en haal uitsluitend details voor regels zonder component op.
        foreach (var lines in linesNeedingDetails.Chunk(50))
        {
            var lineFilter = string.Join(" or ", lines.Select(line =>
                $"Configuration_Line_No eq {line}"));
            details.AddRange(await _bc.GetAsync<ConfigurationRuleDetail>(
                "ConfigurationRulesDetails",
                new ODataQuery
                {
                    Filter = $"Configuration_No eq '{escapedNumber}' and ({lineFilter})",
                    OrderBy = "Configuration_Line_No"
                },
                cancellationToken));
        }

        var detailsByLine = details
            .GroupBy(x => x.ConfigurationLineNo)
            .ToDictionary(
                x => x.Key,
                x => x.ToList());

        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.ComponentNo))
            {
                // Gebruik dezelfde componentstructuur voor de matrix en de offerte-PDF.
                rule.Details =
                [
                    new ConfigurationRuleDetail
                    {
                        ConfigurationNo = rule.ConfigurationNo,
                        ConfigurationVersionNo = rule.ConfigurationVersionNo,
                        ConfigurationLineNo = rule.LineNo,
                        ComponentNo = rule.ComponentNo,
                        SubEntity = rule.SubEntity
                    }
                ];
            }
            else if (detailsByLine.TryGetValue(
                rule.LineNo,
                out var ruleDetails))
            {
                rule.Details = ruleDetails;
            }
            else
            {
                rule.Details = [];
            }
        }

        return rules;
    }

    public async Task<ConfigurationMatrix> GetMatrixAsync(
    string configurationNumber,
    CancellationToken cancellationToken = default)
    {
        var rules = await GetWithDetailsAsync(
            configurationNumber,
            cancellationToken);

        var matrix = new ConfigurationMatrix();

        matrix.Components = rules
            .SelectMany(r => r.Details)
            .Where(d => !string.IsNullOrWhiteSpace(d.ComponentNo))
            .GroupBy(d => d.ComponentNo)
            .Select(g =>
            {
                var detail = g.First();

                return new ConfigurationComponentColumn
                {
                    ComponentNo = detail.ComponentNo ?? "",
                    SubEntity = detail.SubEntity ?? "",
                    ComponentDescription =
                        detail.ComponentDescription ?? "",
                    LocationDescription =
                        detail.ServiceLocationDescription ?? ""
                };
            })
            .ToList();

        var taskGroups = rules
    .Where(r =>
        !string.IsNullOrWhiteSpace(r.ItemNo) &&
        !string.IsNullOrWhiteSpace(r.Description))
    .GroupBy(r => r.ItemNo)
    .ToList();

        foreach (var group in taskGroups)
        {
            var firstRule = group.First();

            var row = new ConfigurationTaskRow
            {
                TaskCode = firstRule.ItemNo ?? "",
                Description = firstRule.Description ?? ""
            };

            foreach (var component in matrix.Components)
            {
                decimal? price = null;

                foreach (var rule in group)
                {
                    var detailForComponent = rule.Details
                        .FirstOrDefault(d =>
                            d.ComponentNo == component.ComponentNo);

                    if (detailForComponent is not null)
                    {
                        price = rule.UnitPrice;
                        break;
                    }
                }

                row.Prices[component.ComponentNo] = price;
            }

            matrix.Tasks.Add(row);
        }

        return matrix;
    }
}
