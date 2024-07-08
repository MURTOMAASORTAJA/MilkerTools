using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;

namespace BitstampLogger;

public static class Analysis
{
    public static decimal CalculateSMA(List<InfluxOhlc> ohlcData, int period)
    {
    if (ohlcData.Count < period) throw new ArgumentException("Not enough data points for SMA calculation.");
    return ohlcData.Skip(ohlcData.Count - period).Take(period).Average(x => x.Close);
    }

    public static decimal CalculateEMA(List<InfluxOhlc> ohlcData, int period)
    {
        if (ohlcData.Count < period) throw new ArgumentException("Not enough data points for EMA calculation.");

        decimal multiplier = 2m / (period + 1);
        decimal previousEma = ohlcData.Take(period).Average(x => x.Close);

        for (int i = period; i < ohlcData.Count - 1; i++)
        {
            previousEma = (ohlcData[i].Close - previousEma) * multiplier + previousEma;
        }

        return (ohlcData.Last().Close - previousEma) * multiplier + previousEma;
    }

    public static decimal CalculateRSI(List<InfluxOhlc> ohlcData, int period)
    {
        if (ohlcData.Count < period + 1) throw new ArgumentException("Not enough data points for RSI calculation.");

        decimal gain = 0;
        decimal loss = 0;

        for (int i = ohlcData.Count - period; i < ohlcData.Count; i++)
        {
            var change = ohlcData[i].Close - ohlcData[i - 1].Close;
            if (change > 0)
                gain += change;
            else
                loss -= change;
        }

        if (loss == 0) return 100;
        decimal rs = gain / loss;
        return 100 - (100 / (1 + rs));
    }

    public static BollingerBandsValue CalculateBollingerBands(List<InfluxOhlc> ohlcData, int period, decimal multiplier)
    {
        if (ohlcData.Count < period) throw new ArgumentException("Not enough data points for Bollinger Bands calculation.");

        var sma = ohlcData.Skip(ohlcData.Count - period).Take(period).Average(x => x.Close);
        var squaredDiffs = ohlcData.Skip(ohlcData.Count - period).Take(period).Select(x => (x.Close - sma) * (x.Close - sma));
        var stdDev = (decimal)Math.Sqrt((double)squaredDiffs.Average());

        return new(sma + multiplier * stdDev, sma, sma - multiplier * stdDev);
    }

    public static MacdValue CalculateMACD(List<InfluxOhlc> ohlcData, int shortPeriod, int longPeriod, int signalPeriod)
    {
        if (ohlcData.Count < longPeriod) throw new ArgumentException("Not enough data points for MACD calculation.");

        var shortEma = CalculateEMA(ohlcData, shortPeriod);
        var longEma = CalculateEMA(ohlcData, longPeriod);
        var macd = shortEma - longEma;

        List<decimal> macdLine = ohlcData.Skip(longPeriod - signalPeriod).Select(x => CalculateEMA(ohlcData, shortPeriod) - CalculateEMA(ohlcData, longPeriod)).ToList();
        var signal = macdLine.Skip(macdLine.Count - signalPeriod).Take(signalPeriod).Average();
        var histogram = macd - signal;

        return new(macd, signal, histogram);
    }

    public static (decimal k, decimal d) CalculateStochasticOscillator(List<InfluxOhlc> ohlcData, int period)
    {
        if (ohlcData.Count < period) throw new ArgumentException("Not enough data points for Stochastic Oscillator calculation.");

        var latest = ohlcData.Last();
        var highestHigh = ohlcData.Skip(ohlcData.Count - period).Take(period).Max(x => x.High);
        var lowestLow = ohlcData.Skip(ohlcData.Count - period).Take(period).Min(x => x.Low);

        var k = 100 * (latest.Close - lowestLow) / (highestHigh - lowestLow);
        var d = ohlcData.Skip(ohlcData.Count - period).Take(period).Average(x => 100 * (x.Close - lowestLow) / (highestHigh - lowestLow));

        return (k, d);
    }

    public static decimal CalculateOBV(List<InfluxOhlc> ohlcData)
    {
        decimal obv = 0;
        decimal previousClose = ohlcData.First().Close;

        foreach (var ohlc in ohlcData)
        {
            if (ohlc.Close > previousClose)
                obv += ohlc.Volume;
            else if (ohlc.Close < previousClose)
                obv -= ohlc.Volume;

            previousClose = ohlc.Close;
        }

        return obv;
    }

    public static decimal CalculateCCI(List<InfluxOhlc> ohlcData, int period)
    {
        if (ohlcData.Count < period) throw new ArgumentException("Not enough data points for CCI calculation.");

        var latest = ohlcData.Last();
        var typicalPrice = (latest.High + latest.Low + latest.Close) / 3;
        var sma = ohlcData.Skip(ohlcData.Count - period).Take(period).Average(x => (x.High + x.Low + x.Close) / 3);
        var meanDeviation = ohlcData.Skip(ohlcData.Count - period).Take(period).Average(x => Math.Abs(((x.High + x.Low + x.Close) / 3) - sma));

        return (typicalPrice - sma) / (0.015m * meanDeviation);
    }

    public static IchimokuCloudValue CalculateIchimokuCloud(List<InfluxOhlc> ohlcData, int tenkanPeriod, int kijunPeriod, int senkouBPeriod)
    {
        if (ohlcData.Count < senkouBPeriod) throw new ArgumentException("Not enough data points for Ichimoku Cloud calculation.");

        var tenkanSen = (ohlcData.Skip(ohlcData.Count - tenkanPeriod).Max(x => x.High) + ohlcData.Skip(ohlcData.Count - tenkanPeriod).Min(x => x.Low)) / 2;
        var kijunSen = (ohlcData.Skip(ohlcData.Count - kijunPeriod).Max(x => x.High) + ohlcData.Skip(ohlcData.Count - kijunPeriod).Min(x => x.Low)) / 2;
        var senkouSpanA = (tenkanSen + kijunSen) / 2;
        var senkouSpanB = (ohlcData.Skip(ohlcData.Count - senkouBPeriod).Max(x => x.High) + ohlcData.Skip(ohlcData.Count - senkouBPeriod).Min(x => x.Low)) / 2;
        var chikouSpan = ohlcData[ohlcData.Count - tenkanPeriod].Close;

        return new(tenkanSen, kijunSen, senkouSpanA, senkouSpanB, chikouSpan);
    }

    public static decimal CalculateParabolicSAR(List<InfluxOhlc> ohlcData, decimal step, decimal maxStep)
    {
        if (ohlcData.Count < 2) throw new ArgumentException("Not enough data points for Parabolic SAR calculation.");

        decimal sar = ohlcData.First().Low;
        decimal ep = ohlcData.First().High;
        decimal af = step;

        for (int i = 1; i < ohlcData.Count; i++)
        {
            var previousSar = sar;

            if (ohlcData[i].High > ep)
            {
                ep = ohlcData[i].High;
                af = Math.Min(af + step, maxStep);
            }

            sar = previousSar + af * (ep - previousSar);

            if (sar > ohlcData[i].Low)
            {
                sar = ohlcData[i].Low;
                ep = ohlcData[i].Low;
                af = step;
            }
        }

        return sar;
    }

    public static decimal CalculateBOP(List<InfluxOhlc> ohlcData)
    {
        if (ohlcData.Count < 1) throw new ArgumentException("Not enough data points for BOP calculation.");

        var latest = ohlcData.Last();

        decimal priceRange = latest.High - latest.Low;
        if (priceRange == 0) return 0;

        return (latest.Close - latest.Open) / priceRange;
    }
}

/// <summary>
/// Contains all analysises of one OHLC item.
/// </summary>
public class AnalysisData
{
    public decimal Sma { get; set; }
    public decimal Ema { get; set; }
    public decimal Rsi { get; set; }
    public BollingerBandsValue BollingerBands { get; set; }
    public MacdValue Macd { get; set; }
    public (decimal k, decimal d) StochasticOscillator { get; set; }
    public decimal Obv { get; set; }
    public decimal Cci { get; set; }
    public IchimokuCloudValue IchimokuCloud { get; set; }
    public decimal ParabolicSar { get; set; }
    public decimal Bop { get; set; }
    public DateTime Timestamp { get; set; }
    public string Pair { get; set; }

}

public class AnalysisParameters
{
    public int SmaPeriod { get; set; } = 20; // 20 minutes
    public int EmaPeriod { get; set; } = 20; // 20 minutes
    public int RsiPeriod { get; set; } = 14; // 14 minutes
    public int BollingerBandsPeriod { get; set; } = 20; // 20 minutes
    public decimal BollingerBandsMultiplier { get; set; } = 2m; // Standard multiplier
    public int MacdShortPeriod { get; set; } = 12; // 12 minutes
    public int MacdLongPeriod { get; set; } = 26; // 26 minutes
    public int MacdSignalPeriod { get; set; } = 9; // 9 minutes
    public int StochasticOscillatorPeriod { get; set; } = 14; // 14 minutes
    public int ObvPeriod { get; set; } = 1; // OBV is typically calculated continuously
    public int CciPeriod { get; set; } = 20; // 20 minutes
    public int IchimokuCloudTenkanPeriod { get; set; } = 9; // 9 minutes
    public int IchimokuCloudKijunPeriod { get; set; } = 26; // 26 minutes
    public int IchimokuCloudSenkouBPeriod { get; set; } = 52; // 52 minutes
    public decimal ParabolicSarStep { get; set; } = 0.02m; // Standard step value
    public decimal ParabolicSarMaxStep { get; set; } = 0.2m; // Standard max step value

    public decimal GetLongestPeriod()
    {
        return new decimal[] { 
            SmaPeriod, 
            EmaPeriod, 
            RsiPeriod, 
            BollingerBandsPeriod, 
            MacdShortPeriod,
            MacdLongPeriod, 
            MacdSignalPeriod,
            StochasticOscillatorPeriod, 
            ObvPeriod,
            CciPeriod, 
            IchimokuCloudTenkanPeriod,
            IchimokuCloudKijunPeriod, 
            IchimokuCloudSenkouBPeriod 
        }.Max();
    }

}

public record struct BollingerBandsValue(decimal Upper, decimal Middle, decimal Lower);

public record struct MacdValue(decimal Macd, decimal Signal, decimal Histogram);

public record struct IchimokuCloudValue(decimal TenkanSen, decimal KijunSen, decimal SenkouSpanA, decimal SenkouSpanB, decimal ChikouSpan);