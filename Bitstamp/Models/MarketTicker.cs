using System.Text.Json.Serialization;
using MilkerTools.Bitstamp.Misc.NumericStringConversion;

namespace MilkerTools.Bitstamp.Models;

public class Ticker
{
    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Ask { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Bid { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal High { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Last { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Low { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Open { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Open24 { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal PercentChange24 { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Side { get; set; }

    [JsonConverter(typeof(StringToLongConverter))]
    public long Timestamp { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Volume { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Vwap { get; set; }
}
