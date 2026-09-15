using AMHub.Models.BusinessCentral;
using AMHub.Services.BusinessCentral;

namespace AMHub.Services.SalesPersons;

public class SalesPersonService : ISalesPersonService
{
    private readonly IBusinessCentralClient _bc;

    public SalesPersonService(
        IBusinessCentralClient bc)
    {
        _bc = bc;
    }

    public async Task<SalesPersonCard?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var escapedCode =
            code.Replace("'", "''");

        var salesPersons =
            await _bc.GetAsync<SalesPersonCard>(
                "SalesPersonCard",
                new ODataQuery
                {
                    Filter =
                        $"Code eq '{escapedCode}'",

                    Top = 1,

                    Select =
                        "Code,Name,Job_Title," +
                        "Commission_Percent," +
                        "Phone_No,KVT_Direct_Phone_No," +
                        "E_Mail,Next_Task_Date," +
                        "Privacy_Blocked,Blocked," +
                        "Global_Dimension_1_Code," +
                        "Global_Dimension_2_Code"
                },
                cancellationToken);

        return salesPersons.FirstOrDefault();
    }
}