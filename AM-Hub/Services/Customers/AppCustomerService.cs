using AMHub.Services.Customers;
using AMHub.Models.BusinessCentral;
using AMHub.Services.BusinessCentral;

namespace AMHub.Services.Customers;

public class AppCustomerService : IAppCustomerService
{
    private readonly IBusinessCentralClient _bc;

    public AppCustomerService(IBusinessCentralClient bc)
    {
        _bc = bc;
    }

    public Task<AppCustomerCard?> GetForDocumentAsync(string customerNumber, CancellationToken cancellationToken = default) =>
        GetAsync(customerNumber, "No,Name,Address,Address_2,Post_Code,City", cancellationToken);

    public Task<AppCustomerCard?> GetByNumberAsync(
        string customerNumber,
        CancellationToken cancellationToken = default) => GetAsync(customerNumber,
            "No,Name,Name_2,Search_Name," +
            "Balance_LCY,Balance_Due_LCY,Credit_Limit_LCY," +
            "Blocked,Salesperson_Code,LVS_After_Sales_Person_Code," +
            "KVT_Service_coördinator," +
            "Address,Address_2,Post_Code,City,County," +
            "Country_Region_Code,Phone_No,MobilePhoneNo,E_Mail," +
            "Home_Page,Primary_Contact_No,ContactName," +
            "VAT_Registration_No,KVT_Chamber_Of_Commerce_No," +
            "Payment_Terms_Code,Payment_Method_Code,Currency_Code," +
            "Last_Date_Modified", cancellationToken);

    private async Task<AppCustomerCard?> GetAsync(string customerNumber, string select, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerNumber))
            return null;

        var escapedNumber = customerNumber.Replace("'", "''");

        var customers = await _bc.GetAsync<AppCustomerCard>(
            "AppCustomerCard",
            new ODataQuery
            {
                Filter = $"No eq '{escapedNumber}'",
                Top = 1,

                Select = select
            },
            cancellationToken);

        return customers.FirstOrDefault();
    }
}
