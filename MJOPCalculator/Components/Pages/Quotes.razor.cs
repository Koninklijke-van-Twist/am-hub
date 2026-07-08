using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MJOP.Calculator.Models;
using MJOP.Calculator.Models.BusinessCentral;
using MJOP.Calculator.Services;

namespace MJOP.Calculator.Components.Pages;

public partial class Quotes : ComponentBase
{
    [Parameter]
    public string? OfferteNo { get; set; }

    private bool HasSelectedQuote => !string.IsNullOrWhiteSpace(OfferteNo);
    private Customer customer = new();
    private string configurationNo = "SQ12604147";
    private string error = "";
    private bool hasSearched = false;
    private bool isLoading = false;

    private List<QuoteDetailsDto> quoteOverviewItems = new();
    private QuoteDetailsDto quoteDetails = new();
    private List<Component> components = new();
    private List<string> taskTypes = new();
    private List<ConfigurationRuleDto> configurationRules = new();
    private List<ConfigurationRuleDetailDto> configurationRulesDetails = new();

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private BusinessCentralApiService BusinessCentralApiService { get; set; } = default!;

    [Inject]
    private PdfExportService PdfExportService { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(OfferteNo))
        {
            configurationNo = OfferteNo;
            await LoadConfigurationDataAsync();
        }
        else
        {
            _ = GetquoteOverviewItemsAsync();
        }
    }

    private async Task SearchQuoteAsync()
    {
        Navigation.NavigateTo(
            $"{Navigation.BaseUri}offertes/{Uri.EscapeDataString(configurationNo)}",
            replace: true);
    }

    private void OpenQuote(string quoteNo)
    {
        Navigation.NavigateTo($"{Navigation.BaseUri}offertes/{Uri.EscapeDataString(quoteNo)}");
    }

    private async Task LoadConfigurationDataAsync()
    {
        hasSearched = true;
        isLoading = true;
        error = "";
        ResetQuoteData();

        try
        {
            await GetConfigurationRulesAsync();
            await GetConfigurationRulesDetailsAsync();
            await GetQuoteDetailsAsync();
            await GetComponentsAsync();
            await GetCustomersAsync();

            AttachTasksToComponents();
            AttachComponentNosToConfigurationRules();
        }
        catch (Exception ex)
        {
            ResetQuoteData();
            error = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task GetConfigurationRulesAsync()
    {
        configurationRules = await BusinessCentralApiService.GetBCFilteredDataAsync<ConfigurationRuleDto>(
            "ConfigurationRules",
            $"Configuration_No eq '{configurationNo}' and Selected eq true and Job_Task_Type eq 'Posting'");
    }

    private async Task GetConfigurationRulesDetailsAsync()
    {
        configurationRulesDetails = await BusinessCentralApiService.GetBCFilteredDataAsync<ConfigurationRuleDetailDto>(
            "ConfigurationRulesDetails",
            $"Configuration_No eq '{configurationNo}'");
    }

    private async Task GetquoteOverviewItemsAsync()
    {
        quoteOverviewItems = await BusinessCentralApiService.GetBCFilteredDataAsync<QuoteDetailsDto>(
            "SalesQuote",
            $"Document_Type eq 'Quote' and Salesperson_Code eq 'NZ'");
    }

    private async Task GetQuoteDetailsAsync()
    {
        var quoteDetailsList = await BusinessCentralApiService.GetBCFilteredDataAsync<QuoteDetailsDto>(
            "SalesQuote",
            $"No eq '{configurationNo}'");

        quoteDetails = quoteDetailsList.FirstOrDefault() ?? new QuoteDetailsDto();
    }

    private async Task GetCustomersAsync()
    {
        var customerList = await BusinessCentralApiService.GetBCFilteredDataAsync<Customer>(
            "AppCustomerCard",
            $"No eq '{quoteDetails.CustomerNo}'");

        customer = customerList.FirstOrDefault() ?? new Customer();
    }

    private async Task GetComponentsAsync()
    {
        components = new();

        var componentNos = configurationRulesDetails
            .Select(detail => detail.ComponentNo)
            .Where(componentNo => !string.IsNullOrWhiteSpace(componentNo))
            .Distinct()
            .ToList();

        foreach (var componentNo in componentNos)
        {
            var safeComponentNo = componentNo.Replace("'", "''");

            var componentData = await BusinessCentralApiService.GetBCFilteredDataAsync<Component>(
                "AppComponentCard",
                $"No eq '{safeComponentNo}'");

            var component = componentData.FirstOrDefault();

            if (component != null)
            {
                component.ComponentTasks = new();
                components.Add(component);
            }
            else
            {
                components.Add(new Component
                {
                    No = componentNo,
                    ComponentTasks = new()
                });
            }
        }
    }

    private void AttachComponentNosToConfigurationRules()
    {
        foreach (var rule in configurationRules)
        {
            rule.ComponentNos = configurationRulesDetails
                .Where(detail => detail.ConfigurationLineNo == rule.LineNo)
                .Select(detail => detail.ComponentNo)
                .ToList();
        }
    }

    private void AttachTasksToComponents()
    {
        foreach (var component in components)
        {
            var lineNumbersForThisComponent = configurationRulesDetails
                .Where(detail => detail.ComponentNo == component.No)
                .Select(detail => detail.ConfigurationLineNo)
                .Distinct()
                .ToHashSet();

            component.ComponentTasks = configurationRules
                .Where(rule => lineNumbersForThisComponent.Contains(rule.LineNo))
                .OrderBy(rule => rule.LineNo)
                .Select(rule => new ComponentTask
                {
                    Type = rule.ItemNo,
                    Description = rule.Description,
                    Price = rule.UnitPrice > 0 ? rule.UnitPrice : null
                })
                .ToList();
        }

        taskTypes = components
            .SelectMany(component => component.ComponentTasks)
            .Select(task => task.Type)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct()
            .ToList();
    }

    private async Task PrintPdf()
    {
        if (!components.Any() || !taskTypes.Any())
        {
            error = "Haal eerst configuratiegegevens op.";
            return;
        }

        var html = PdfExportService.BuildQuoteHtml(quoteDetails, components, taskTypes);
        await JS.InvokeVoidAsync("exportHtmlToPrint", html);
    }

    private async Task DownloadPdf()
    {
        if (!components.Any() || !taskTypes.Any())
        {
            error = "Haal eerst configuratiegegevens op.";
            return;
        }

        var html = PdfExportService.BuildQuoteHtml(quoteDetails, components, taskTypes);
        await JS.InvokeVoidAsync("exportHtmlToDownload", html, $"Offerte_{configurationNo}.pdf");
    }

    private void ResetQuoteData()
    {
        components = new();
        taskTypes = new();
        configurationRules = new();
        configurationRulesDetails = new();
    }

    private static string FormatCurrency(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString("N2", CultureInfo.GetCultureInfo("nl-NL"))
            : "-";
    }
}
