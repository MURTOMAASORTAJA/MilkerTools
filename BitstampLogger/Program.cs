using BitstampLogger;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using Microsoft.Extensions.Configuration;
using MilkerTools.Bitstamp;
using MilkerTools.Bitstamp.Models;

// get Settings from appsettings.json (and user secrets):

var settings = GetSettings();
var bitstamp = new BitStamp(settings.Api);
var dbclient = new InfluxDBClient(GetClientOptions());

var dbHealthWait = TimeSpan.FromMinutes(5);
DateTime? firstFailedHealthCheck = null;

while (true)
{
    if (await dbclient.PingAsync())
    {
        await DoWork();
    }
    else
    {
        if (firstFailedHealthCheck == null)
        {
            firstFailedHealthCheck = DateTime.Now;
            await WriteLineAsync($"Waiting for InfluxDB to wake up.");
            Thread.Sleep(3000);
            continue;
        }
        else
        {
            if (DateTime.Now - firstFailedHealthCheck > dbHealthWait)
            {
                await WriteLineAsync("Can't connect to InfluxDB. Bye.");
                Environment.Exit(1);
            }
            else
            {
                await Console.Out.WriteAsync(".");
                Thread.Sleep(3000);
                continue;
            }
        }
    }
    
    Thread.Sleep(new TimeSpan(0, 0, settings.Market.Step/2));
}

BitstampLogger.Settings GetSettings()
{
    var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<Program>();

    IConfiguration configuration = builder.Build();
    return configuration.Get<BitstampLogger.Settings>()!;
}

async Task DoWork()
{
    await PopulateHistoryOhlcData();
    await PopulateRecentOhlcData();
}

string StdoutTime() => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
async Task WriteLineAsync(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    await Console.Out.WriteAsync(StdoutTime());
    Console.ForegroundColor = ConsoleColor.White;
    await Console.Out.WriteLineAsync($" {text}");
    Console.ResetColor();
}

async Task WriteAsync(string text, ConsoleColor? color)
{
    if (color != null)
    {
        Console.ForegroundColor = color.Value;
    }

    await Console.Out.WriteAsync(text);

    if (color != null)
    {
        Console.ResetColor();
    }
}

async Task PopulateHistoryOhlcData()
{
    var earliestTimestamp = await GetEarliestTimestamp("ohlc_data", "pair", settings!.Market.Pair) ?? DateTime.Now;
    var intendedEarliestTimestamp = DateTime.Now - settings.MinimumHistoryDataSpan;

    if (earliestTimestamp > intendedEarliestTimestamp.AddSeconds(settings.Market.Step))
    {
        await WriteLineAsync("Need to log some historical OHLC data.");
        var requestRanges = GetRequestRanges(intendedEarliestTimestamp, earliestTimestamp, settings.Market.Step);
        foreach (var (Start, End) in requestRanges)
        {
            await WriteLineAsync($"Fetching OHLC from {Start:dd.MM.yyyy HH:mm:ss} - {End:dd.MM.yyyy HH:mm:ss}");
            var response = await bitstamp.GetOhlcData(settings.Market.Pair, settings.Market.Step, 1000, Start.ToUnixTimestamp()/1000, End.ToUnixTimestamp()/1000, true);
            if (!response.Success)
            {
                await Console.Out.WriteLineAsync();
                throw new Exception($"Response to an OHLC data request indicated failure: {response.Error?.Error ?? ""} {response.Error?.Status ?? ""}");
            }
            PushOhlcDataToDb(response.Content!);
            await WriteLineAsync("Logged some OHLC data.");
        }
        await WriteLineAsync("Historical OHLC data logging finished.");
    }
    Thread.Sleep(1000);
}

async Task PopulateRecentOhlcData()
{
    var latestTimestamp = await GetLatestTimestamp("ohlc_data", "pair", settings!.Market.Pair) ?? DateTime.Now.AddSeconds(settings.Market.Step * -1);
    if (latestTimestamp <= DateTime.Now.AddSeconds(settings.Market.Step * -1))
    {
        await WriteLineAsync("Need to log some recent OHLC data.");
        var requestRanges = GetRequestRanges(latestTimestamp, DateTime.Now, settings.Market.Step);
        var requests = requestRanges
            .Select(async range =>
            {
                await WriteLineAsync($"Requesting data from range {range.Start:dd.MM.yyyy HH:mm:ss} - {range.End:dd.MM.yyyy HH:mm:ss}");
                return await bitstamp!.GetOhlcData(settings.Market.Pair, settings.Market.Step, 1000, range.Start.ToUnixTimestamp()/1000, range.End.ToUnixTimestamp()/1000, true);
            });

        var responses = await Task.WhenAll(requests);
        foreach (var response in responses)
        {
            if (!response.Success)
            {
                throw new Exception($"Response to an OHLC data request indicated failure: {response.Error?.Error ?? ""} {response.Error?.Status ?? ""}");
            }
            PushOhlcDataToDb(response.Content!);
            await WriteLineAsync("Logged some OHLC data.");
        }
        await WriteLineAsync("Recent OHLC data logging finished.");
    }
    Thread.Sleep(1000);
}

(DateTime Start, DateTime End)[] GetRequestRanges(DateTime start, DateTime end, int step)
{
    List<(DateTime StartDate, DateTime EndDate)> ranges = [];

    DateTime currentStart = start;
    int maxItems = 1000;
    TimeSpan interval = TimeSpan.FromSeconds(step * maxItems);

    while (currentStart < end)
    {
        DateTime currentEnd = currentStart.Add(interval);
        if (currentEnd > end)
        {
            currentEnd = end;
        }
        ranges.Add((currentStart, currentEnd));
        currentStart = currentEnd;
    }

    return [.. ranges];
}

InfluxDBClientOptions GetClientOptions() =>
    new(settings!.InfluxDb.Uri.ToString())
    {
        Token = settings.InfluxDb.Token,
        Org = settings.InfluxDb.Org,
        Bucket = settings.InfluxDb.Bucket
    };

void PushOhlcDataToDb(OhlcData data)
{
    using InfluxDBClient client = new(GetClientOptions());
    var pointData = data.ToPointData();
    client.GetWriteApi().WritePoints(pointData);
}

async Task<DateTime?> GetLatestTimestamp(string measurement, string tagKey, string tagValue)
{
    var options = GetClientOptions();
    using InfluxDBClient client = new(GetClientOptions());
    string fluxQuery = $@"
from(bucket: ""{options.Bucket}"")
  |> range(start: 0)
  |> filter(fn: (r) => r._measurement == ""{measurement}"" and r.{tagKey} == ""{tagValue}"")
  |> filter(fn: (r) => r._field == ""close"")
  |> last()
  |> keep(columns: [""_time""])";

    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(fluxQuery, options.Org);
    var latestPoint = tables.SelectMany(table => table.Records).FirstOrDefault();
    return latestPoint?.GetTime()!.Value.ToDateTimeUtc();
}

async Task<DateTime?> GetEarliestTimestamp(string measurement, string tagKey, string tagValue)
{
    var options = GetClientOptions();
    using InfluxDBClient client = new(options);
    string fluxQuery = $@"
from(bucket: ""{options.Bucket}"")
  |> range(start: 0)
  |> filter(fn: (r) => r._measurement == ""{measurement}"" and r.{tagKey} == ""{tagValue}"")
  |> filter(fn: (r) => r._field == ""close"")
  |> first()
  |> keep(columns: [""_time""])";

    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(fluxQuery, options.Org);
    var latestPoint = tables.SelectMany(table => table.Records).FirstOrDefault();
    return latestPoint?.GetTime()!.Value.ToDateTimeUtc();
}