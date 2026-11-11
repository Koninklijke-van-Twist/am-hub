namespace AMHub.Services.BusinessCentral;

public static class BusinessCentralRecordUrl
{
    public static string Build(int page, string number, string field = "No.")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        var filter = $"'{field.Replace("'", "''")}' IS '{number.Replace("'", "''")}'";
        return "https://kvtmd365.kvt.nl/KVTMDLive_AAD/?company=Koninklijke%20van%20Twist&page="
            + page + "&filter=" + Uri.EscapeDataString(filter).Replace("'", "%27");
    }
}
