using MilkerTools.Bitstamp;

namespace BitstampLogger;

public class Settings
{
    public ApiSettings Api { get; set; }
    public TimeSpan MinimumHistoryDataSpan { get; set; } = TimeSpan.FromDays(60);
    public InfluxDbSettings InfluxDb { get; set; }
    public MarketSettings Market { get; set; } = new();
    public AnalysisParameters Analysis { get; set; } = new();
}

public class InfluxDbSettings
{
    public Uri Uri { get; set; } = new Uri("http://localhost:8086");
    public string Token { get; set; }
    public string Org { get; set; }
    public string Bucket { get; set; }
}

public class MarketSettings
{
    public string Pair { get; set; } = "btcusd";
    public MarketLoggingSettings Logging { get; set; } = new();
    public int Step { get; set; } = 3600;
}

public class MarketLoggingSettings
{
    public string[] Measurements { get; set; } = ["ohlc_data"];
}
