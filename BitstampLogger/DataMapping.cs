using InfluxDB.Client.Writes;
using MilkerTools.Bitstamp.Models;
namespace BitstampLogger;

public static class DataMapping
{
    public static List<PointData> ToPointData(this OhlcData ohlcData)
    {
        return ohlcData.Ohlc.Select(ohlc =>
        PointData
        .Measurement("ohlc_data")
        .Tag("pair", ohlcData.Pair.ToLower().Replace("/",""))
        .Field("open", ohlc.Open)
        .Field("high", ohlc.High)
        .Field("low", ohlc.Low)
        .Field("close", ohlc.Close)
        .Field("volume", ohlc.Volume)
        .Timestamp(ohlc.Timestamp, InfluxDB.Client.Api.Domain.WritePrecision.S))?.ToList() ?? [];
    }
}
