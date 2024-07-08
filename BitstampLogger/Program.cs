using BitstampLogger;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using MilkerTools.Bitstamp;
using MilkerTools.Bitstamp.Models;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;

// get Settings from appsettings.json (and user secrets):

var settings = GetSettings();
if (settings == null)
{
    Console.WriteLine("I have no settings. You should configure some in appsettings.json. Bye.");
    Environment.Exit(1);
}
var bitstamp = new BitStamp(settings.Api);
var dbclient = new InfluxDBClient(GetClientOptions());

var dbHealthWait = TimeSpan.FromMinutes(5);
DateTime? firstFailedHealthCheck = null;

Task? historyWorkTask = null;
var historyWorkDone = false;

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
    var historyItemsCount = await PopulateHistoryOhlcData();
    Thread.Sleep(historyItemsCount);
    await AnalyzeData();
    await PopulateRecentOhlcData();
}

async Task<int> PopulateHistoryOhlcData()
{
    var count = 0;
    var earliestTimestamp = await GetEarliestTimestamp("ohlc_data", "pair", settings!.Market.Pair) ?? DateTime.Now;

    var endTime = DateTime.Now;
    var intendedEarliestTimestamp = endTime - (settings.MinimumHistoryDataSpan);

    var earliestOfTotal = DateTime.Now - (settings.MinimumHistoryDataSpan +
        GetTimespanOfPeriodValue(settings.Market.Step, Convert.ToInt16(settings.Analysis.GetLongestPeriod() + 5)));

    if (earliestTimestamp > endTime.AddSeconds(settings.Market.Step))
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

            count += response?.Content?.Ohlc.Length ?? 0;

            PushOhlcDataToDb(response.Content!);
            await WriteLineAsync("Logged some OHLC data.");
        }

        await WriteLineAsync("Historical OHLC data logging finished.");
    }

    return count;
}

async Task<OhlcData> PopulateRecentOhlcData()
{
    List<Ohlc> allOhlc = [];

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
            if (response.Content?.Ohlc != null)
            {
                allOhlc.AddRange(response.Content.Ohlc);
            }
            PushOhlcDataToDb(response.Content!);
            await WriteLineAsync("Logged some OHLC data.");
        }
        await WriteLineAsync("Recent OHLC data logging finished.");
    }

    return new OhlcData() 
    { 
        Pair = settings!.Market.Pair, 
        Ohlc = allOhlc.ToArray()
    };
}

async Task AnalyzeData()
{
    var unanalyzedTimestamps = await GetTimestampsOfUnanalyzedOhlcItems();
    if (unanalyzedTimestamps.Count > 0)
    {
        var timestamp = unanalyzedTimestamps.First();
        await WriteLineAsync($"Analyzing OHLC item from {timestamp}");
        await AnalyzeOhlcItem(timestamp);
        await WriteLineAsync($"Analyzed.");
    }
}

static async Task<InfluxOhlcData> GetOhlcDataFromInfluxDb(InfluxDbSettings influxDbSettings, DateTime startTimestamp, DateTime endTimestamp)
{
    var client = new InfluxDBClient(influxDbSettings.Uri.AbsoluteUri, influxDbSettings.Token);
    var api = client.GetQueryApi();

    var flux = $@"
            from(bucket: ""{influxDbSettings.Bucket}"")
            |> range(start: {startTimestamp:yyyy-MM-ddTHH:mm:ssZ}, stop: {endTimestamp:yyyy-MM-ddTHH:mm:ssZ})
            |> filter(fn: (r) => r._measurement == ""ohlc_data"")
            |> pivot(rowKey: [""_time""], columnKey: [""_field""], valueColumn: ""_value"")
            |> keep(columns: [""_time"", ""open"", ""low"", ""high"", ""close"", ""volume""])
        ";

    return new()
    {
        Pair = influxDbSettings.Bucket,
        Ohlc = await api.QueryAsync<InfluxOhlc>(flux, influxDbSettings.Org)
    };
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

void PushAnalysisDataToDb(AnalysisData data)
{
    using InfluxDBClient client = new(GetClientOptions());
    var pointData = data.ToPointData();
    client.GetWriteApi().WritePoint(pointData);
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

TimeSpan GetTimespanOfPeriodValue(int periodInSeconds, int periodValue) => TimeSpan.FromSeconds(periodInSeconds * periodValue);

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

async Task<List<DateTime>> GetTimestampsOfUnanalyzedOhlcItems()
{
    var options = GetClientOptions();
    using InfluxDBClient client = new(options);
    string fluxQuery = $@"
import ""array""
import ""join""

// Query OHLC data and add a dummy row if empty
ohlc = from(bucket: ""{settings.InfluxDb.Bucket}"")
  |> range(start: -1d)
  |> filter(fn: (r) => r._measurement == ""ohlc_data"")
  |> keep(columns: [""_time""])

dummy_ohlc = array.from(rows: [{{_time: time(v: 0)}}])
ohlc_with_dummy = union(tables: [ohlc, dummy_ohlc])

// Query analysis data and add a dummy row if empty
analysis = from(bucket: ""{settings.InfluxDb.Bucket}"")
  |> range(start: -1d)
  |> filter(fn: (r) => r._measurement == ""analysis"")
  |> keep(columns: [""_time""])

dummy_analysis = array.from(rows: [{{_time: time(v: 0)}}])
analysis_with_dummy = union(tables: [analysis, dummy_analysis])

// Perform the left join
joined = join.left(
  left: ohlc_with_dummy,
  right: analysis_with_dummy,
  on: (l, r) => l._time == r._time,
  as: (l, r) => ({{
    l with
    analysis_exists: exists r._time
  }})
)

// Filter for OHLC entries without corresponding analysis and remove dummy rows
result = joined
  |> filter(fn: (r) => r._time != time(v: 0) and not r.analysis_exists)
  |> keep(columns: [""_time""])

result";
    
    var earliestAnalyzableTimestamp = await GetEarliestTimestamp("ohlc_data", "pair", settings!.Market.Pair);

    if (earliestAnalyzableTimestamp == null)
    {
        return [];
    }

    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(fluxQuery, options.Org);
    var timestamps = tables
        .SelectMany(table => table.Records.Select(rec => rec.GetTimeInDateTime()!.Value))
        .Where(stamp => stamp >= earliestAnalyzableTimestamp)
        .ToList();

    return timestamps;
}



async Task AnalyzeOhlcItem(DateTime timestamp)
{
    var earliestDateTimeOfRequiredHistoryData = timestamp - AnalysisHistoryBufferTimeSpan();

    var getHistoryData = await GetOhlcDataFromInfluxDb(settings.InfluxDb, earliestDateTimeOfRequiredHistoryData, timestamp);
    var analysisData = Enrichment.AnalyzeLatestOhlc(getHistoryData, settings.Analysis);
    PushAnalysisDataToDb(analysisData);
}

TimeSpan AnalysisHistoryBufferTimeSpan() => settings.MinimumHistoryDataSpan + GetTimespanOfPeriodValue(settings.Market.Step, Convert.ToInt16(settings.Analysis.GetLongestPeriod() + 5));


public record HistoryData(OhlcData Data, long StartTimestamp, long EndTimestamp);