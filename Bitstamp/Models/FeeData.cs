using System.Text.Json.Serialization;
using MilkerTools.Bitstamp.Misc.NumericStringConversion;

namespace MilkerTools.Bitstamp.Models;

public class FeeData
{
    public string CurrencyPair { get; set; }
    public Fees Fees { get; set; }
    public string Market { get; set; }
}

public class Fees
{
    /// <summary>
    /// "...when an order is filled immediately (Taker) <b>or at a later time (Maker)</b>..."
    /// </summary>
    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Maker { get; set; }

    /// <summary>
    /// "...<b>when an order is filled immediately (Taker)</b> or at a later time (Maker)..."
    /// </summary>
    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Taker { get; set; }
}

public static class FeeExtensions
{
    public static decimal MakerFeeFor<MarketRole>(this Fees fees, decimal amount) => amount * fees.Maker;
    public static decimal TakerFeeFor<MarketRole>(this Fees fees, decimal amount) => amount * fees.Taker;
}