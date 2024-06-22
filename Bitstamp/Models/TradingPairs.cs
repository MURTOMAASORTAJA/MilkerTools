namespace MilkerTools.Bitstamp.Models;
// GET https://www.bitstamp.net/api/v2/trading-pairs-info/

public class TradingPair
{
    public required string Name { get; set; }
    public required string UrlSymbol { get; set; }
    public required int BaseDecimals { get; set; }
    public required int CounterDecimals { get; set; }
    public required string MinimumOrder { get; set; }
    public required string Trading { get; set; }
    public required string InstantAndMarketOrders { get; set; }
    public required string Description { get; set; }
}