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

    public static PointData ToPointData(this AnalysisData analysisData)
    {
        return 
            PointData
            .Measurement("analysis")
            .Tag("pair", analysisData.Pair)
            .Field("sma", analysisData.Sma)
            .Field("ema", analysisData.Ema)
            .Field("rsi", analysisData.Rsi)
            .Field("bollinger_bands_lower", analysisData.BollingerBands.Lower)
            .Field("bollinger_bands_upper", analysisData.BollingerBands.Upper)
            .Field("macd", analysisData.Macd.Macd)
            .Field("macd_signal", analysisData.Macd.Signal)
            .Field("macd_histogram", analysisData.Macd.Histogram)
            .Field("stochastic_oscillator_k", analysisData.StochasticOscillator.k)
            .Field("stochastic_oscillator_d", analysisData.StochasticOscillator.d)
            .Field("obv", analysisData.Obv)
            .Field("cci", analysisData.Cci)
            .Field("ichimoku_cloud_tenkan_sen", analysisData.IchimokuCloud.TenkanSen)
            .Field("ichimoku_cloud_kijun_sen", analysisData.IchimokuCloud.KijunSen)
            .Field("ichimoku_cloud_senkou_span_a", analysisData.IchimokuCloud.SenkouSpanA)
            .Field("ichimoku_cloud_senkou_span_b", analysisData.IchimokuCloud.SenkouSpanB)
            .Field("ichimoku_cloud_chikou_span", analysisData.IchimokuCloud.ChikouSpan)
            .Field("parabolic_sar", analysisData.ParabolicSar)
            .Field("bop", analysisData.Bop)
            .Timestamp(analysisData.Timestamp, InfluxDB.Client.Api.Domain.WritePrecision.S);

    }
}
