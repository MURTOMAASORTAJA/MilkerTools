namespace BitstampLogger;

public class Settings
{
    public TimeSpan MinimumHistoryDataSpan { get; set; } = TimeSpan.FromDays(60);
    public InfluxDbSettings InfluxDb { get; set; }
}

public class InfluxDbSettings
{
    public Uri Uri { get; set; } = new Uri("http://localhost:8086");
    public string Token { get; set; }
    public string Org { get; set; }
    public string Bucket { get; set; }
}
