using InfluxDB.Client.Core;

namespace BitstampLogger;

public class InfluxOhlcData
{
    public List<InfluxOhlc> Ohlc { get; set; }
    public string Pair { get; set; }
}

public class InfluxOhlc
{
    [Column(IsTimestamp = true)]
    public DateTime Timestamp { get; set; }

    [Column("open")]
    public decimal Open { get; set; }

    [Column("high")]
    public decimal High { get; set; }

    [Column("low")]
    public decimal Low { get; set; }

    [Column("close")]
    public decimal Close { get; set; }

    [Column("volume")]
    public decimal Volume { get; set; }

}
