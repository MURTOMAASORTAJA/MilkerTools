using System.ComponentModel.DataAnnotations;
namespace MilkerTools.Models.Requests;

/// <summary>
/// https://www.bitstamp.net/api/#tag/Market-info/operation/GetOHLCData
/// 
/// GET https://www.bitstamp.net/api/v2/ohlc/{market_symbol}/
/// </summary>
public class OHLCRequest
{
    /// <summary>
    /// Timeframe in seconds.
    /// </summary>
    [AllowedValues(60, 180, 300, 900, 1800, 3600, 7200, 14400, 21600, 43200, 86400, 259200)]
    public required int Step { get; set; }

    /// <summary>
    /// Limit OHLC results.
    /// </summary>
    [Range(1, 1000)]
    public required int Limit { get; set; }

    /// <summary>
    /// Unix timestamp from when OHLC data will be started.
    /// </summary>
    public int? Start { get; set; }

    /// <summary>
    /// Unix timestamp to when OHLC data will be shown.
    /// If none from start or end timestamps are posted then 
    /// endpoint returns OHLC data to current unixtime. 
    /// If both start and end timestamps are posted, 
    /// end timestamp will be used.
    /// </summary>
    public int? End { get; set; }

    /// <summary>
    /// If set, results won't include current (open) candle.
    /// </summary>
    public bool? ExcludeCurrentCandle { get; set; }

    /// <summary>
    /// Allowed step values for <see cref="Step"/> property.
    /// </summary>
    public static int[] AllowedStepValues
    {
        get => new int[] { 60, 180, 300, 900, 1800, 3600, 7200, 14400, 21600, 43200, 86400, 259200 };
    }
}
