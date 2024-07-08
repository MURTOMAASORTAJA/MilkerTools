namespace BitstampLogger;

public static class Enrichment
{
    public static AnalysisData AnalyzeLatestOhlc(InfluxOhlcData data, AnalysisParameters parameters)
    {
        var ohlcList = data.Ohlc;

        if (ohlcList.Count == 0) throw new ArgumentException("No OHLC data available.");

        return new AnalysisData
        {
            Sma = TryCalculate(() => Analysis.CalculateSMA(ohlcList, parameters.SmaPeriod)),
            Ema = TryCalculate(() => Analysis.CalculateEMA(ohlcList, parameters.EmaPeriod)),
            Rsi = TryCalculate(() => Analysis.CalculateRSI(ohlcList, parameters.RsiPeriod)),
            BollingerBands = TryCalculate(() => Analysis.CalculateBollingerBands(ohlcList, parameters.BollingerBandsPeriod, parameters.BollingerBandsMultiplier)),
            Macd = TryCalculate(() => Analysis.CalculateMACD(ohlcList, parameters.MacdShortPeriod, parameters.MacdLongPeriod, parameters.MacdSignalPeriod)),
            StochasticOscillator = TryCalculate(() => Analysis.CalculateStochasticOscillator(ohlcList, parameters.StochasticOscillatorPeriod)),
            Obv = TryCalculate(() => Analysis.CalculateOBV(ohlcList)),
            Cci = TryCalculate(() => Analysis.CalculateCCI(ohlcList, parameters.CciPeriod)),
            IchimokuCloud = TryCalculate(() => Analysis.CalculateIchimokuCloud(ohlcList, parameters.IchimokuCloudTenkanPeriod, parameters.IchimokuCloudKijunPeriod, parameters.IchimokuCloudSenkouBPeriod)),
            ParabolicSar = TryCalculate(() => Analysis.CalculateParabolicSAR(ohlcList, parameters.ParabolicSarStep, parameters.ParabolicSarMaxStep)),
            Bop = TryCalculate(() => Analysis.CalculateBOP(ohlcList))
        };
    }

    private static T TryCalculate<T>(Func<T> calculation)
    {
        try
        {
            return calculation();
        }
        catch
        {
            return default;
        }
    }
}
