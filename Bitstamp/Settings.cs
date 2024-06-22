namespace MilkerTools.Bitstamp;

public class Settings
{
    public ApiSettings Api { get; set; } = new ApiSettings();
}

public class ApiSettings
{
    public string Key { get; set; } = "";
    public string Secret { get; set; } = "";
}
