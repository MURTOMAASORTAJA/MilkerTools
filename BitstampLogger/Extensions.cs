﻿using MilkerTools.Bitstamp.Models;

namespace BitstampLogger;

public static class Extensions
{
    public static long ToUnixTimestamp(this DateTime dateTime) => new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();

    public static DateTime ToDateTime(this long timestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
    }

    public static InfluxOhlc ToInfluxOhlc(this Ohlc ohlc)
    {
        return new()
        {
            Timestamp = ohlc.Timestamp.ToDateTime(),
            Open = ohlc.Open,
            High = ohlc.High,
            Low = ohlc.Low,
            Close = ohlc.Close,
            Volume = ohlc.Volume
        };
    }

    public static Ohlc ToOhlc(this InfluxOhlc ohlc)
    {
        return new()
        {
            Timestamp = ohlc.Timestamp.ToUnixTimestamp(),
            Open = ohlc.Open,
            High = ohlc.High,
            Low = ohlc.Low,
            Close = ohlc.Close,
            Volume = ohlc.Volume
        };
    }

    public static InfluxOhlcData ToInfluxOhlcData(this OhlcData ohlcData)
    {
        return new()
        {
            Ohlc = ohlcData.Ohlc.Select(ohlc => ohlc.ToInfluxOhlc()).ToList(),
            Pair = ohlcData.Pair
        };
    }

    public static OhlcData ToOhlcData(this InfluxOhlcData ohlcData)
    {
        return new()
        {
            Ohlc = ohlcData.Ohlc.Select(ohlc => ohlc.ToOhlc()).ToArray(),
            Pair = ohlcData.Pair
        };
    }

    public static string ToFluxRange(this TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds < 0)
        {
            throw new ArgumentException("TimeSpan cannot be negative.");
        }

        if (timeSpan.TotalDays >= 1)
        {
            return $"-{Math.Floor(timeSpan.TotalDays)}d{(timeSpan.Hours > 0 ? $" -{timeSpan.Hours}h" : "")}";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"-{Math.Floor(timeSpan.TotalHours)}h{(timeSpan.Minutes > 0 ? $" -{timeSpan.Minutes}m" : "")}";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return $"-{Math.Floor(timeSpan.TotalMinutes)}m{(timeSpan.Seconds > 0 ? $" -{timeSpan.Seconds}s" : "")}";
        }
        else
        {
            return $"-{Math.Floor(timeSpan.TotalSeconds)}s";
        }
    }
}
