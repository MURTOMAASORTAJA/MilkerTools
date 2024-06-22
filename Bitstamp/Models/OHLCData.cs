using System.Text.Json.Serialization;
using MilkerTools.Misc.NumericStringConversion;

namespace MilkerTools.Models;
public class OHLCData
{
    public required string Pair { get; set; }
    public required Ohlc[] Ohlc { get; set; }
}

public class Ohlc
{
    [JsonConverter(typeof(StringToLongConverter))]
    public long Timestamp { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Open { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal High { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Low { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Close { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Volume { get; set; }
}
