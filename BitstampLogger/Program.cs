using BitstampLogger;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using MilkerTools.Bitstamp;
using MilkerTools.Bitstamp.Models;
using System.Globalization;

Queue<DateTime> analysisQueue = new();

var settings = GetSettings();
if (settings == null)
{
    Console.WriteLine("I have no settings. You should configure some in appsettings.json. Bye.");
    Environment.Exit(1);
}
var bitstamp = new BitStamp(settings.Api);

var dbHealthWait = TimeSpan.FromMinutes(5);
DateTime? firstFailedHealthCheck = null;

Task? historyWorkTask = null;
var historyWorkDone = false;

DateTime? lastOhlcDataFetch = null;
var toBeAnalyzed = await GetUnanalyzedDataTimestamps();
if (toBeAnalyzed.Count > 0)
{
    toBeAnalyzed.ForEach(analysisQueue.Enqueue);
}

while (true)
{
    if (await PingInfluxDb())
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

async Task<bool> PingInfluxDb()
{
    var dbclient = new InfluxDBClient(GetClientOptions());
    return await dbclient.PingAsync();
}

async Task InitializeBucket()
{
    var options = GetClientOptions();
    using InfluxDBClient client = new(options);
    var bucketApi = client.GetBucketsApi();
    var orgId = (await client.GetOrganizationsApi().FindOrganizationsAsync(org: settings.InfluxDb.Org)).First().Id;

    var existingBucket = await bucketApi.FindBucketByNameAsync(settings.InfluxDb.Bucket);
    if (existingBucket != null)
    {
        await bucketApi.DeleteBucketAsync(existingBucket);
    }

    await bucketApi.CreateBucketAsync(settings.InfluxDb.Bucket, orgId);
}

async Task DoWork()
{
    var historyItemsCount = await PopulateHistoryOhlcData();
    if (historyItemsCount > 0) Thread.Sleep(CalculateWaitTime(historyItemsCount));

    if (lastOhlcDataFetch == null || lastOhlcDataFetch != null && (DateTime.Now - lastOhlcDataFetch.Value).TotalSeconds > (settings!.Market.Step / 2))
    {
        await PopulateRecentOhlcData();
        lastOhlcDataFetch = DateTime.Now;
    }

    await AnalyzeData();
}

int CalculateWaitTime(int ohlcCount) => Convert.ToInt16(Decimal.Round(ohlcCount * 0.15M));

async Task<int> PopulateHistoryOhlcData()
{
    var count = 0;
    var endTime = DateTime.Now;
    var earliestExistingTimestamp = await GetEarliestTimestamp("ohlc_data", "pair", settings!.Market.Pair) ?? endTime;
    var intendedEarliestTimestamp = endTime - (settings.MinimumHistoryDataSpan);

    var earliestOfTotal = DateTime.Now - (settings.MinimumHistoryDataSpan +
        GetTimespanOfPeriodValue(settings.Market.Step, Convert.ToInt16(settings.Analysis.GetLongestPeriod() + 5)));

    if (earliestExistingTimestamp >= endTime)
    {
        await WriteLineAsync("Need to log some historical OHLC data.");
        var requestRanges = GetRequestRanges(intendedEarliestTimestamp, earliestExistingTimestamp, settings.Market.Step);

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

            if (response!.Content?.Ohlc != null)
            {
                EnqueueManyIfNew(response.Content.Ohlc.Select(ohlc => ohlc.Timestamp.ToDateTime()), false);
            }

            PushOhlcDataToDb(response.Content!);
            await WriteLineAsync("Logged some OHLC data.");
        }

        await WriteLineAsync("Historical OHLC data logging finished.");
    }

     return count;
}

void EnqueueIfNew(DateTime item, bool recent, IEnumerable<DateTime>? existingTimestampsInDb)
{
    if (!analysisQueue.Contains(item))
    {
        if (recent)
        {
            var existingAnalysisItemTimestampsInDb = existingTimestampsInDb ?? GetTimestamps("analysis").Result;
            if (existingAnalysisItemTimestampsInDb.Contains(item)) return;
        }
        analysisQueue.Enqueue(item);
    }
}

void EnqueueManyIfNew(IEnumerable<DateTime> items, bool recent)
{
    var existingStamps = GetTimestamps("analysis").Result;
    foreach (var item in items)
    {
        EnqueueIfNew(item, recent, existingStamps);
    }
}

async Task<OhlcData> PopulateRecentOhlcData()
{
    List<Ohlc> allOhlc = [];

    var latestTimestamp = await GetLatestTimestamp("ohlc_data", "pair", settings!.Market.Pair) ?? DateTime.Now.AddSeconds(settings.Market.Step * -1);
    if (latestTimestamp <= DateTime.Now.AddSeconds(settings.Market.Step * -1))
    {
        await WriteLineAsync("Need to log some recent OHLC data.");
        var wholeRangeAsSteps = Convert.ToInt16(Math.Round((DateTime.Now - latestTimestamp).TotalSeconds / settings.Market.Step,0) + 1); // +1 just in case

        var requestRanges = GetRequestRanges(latestTimestamp, DateTime.Now, settings.Market.Step);
        var requests = requestRanges
            .Select(async range =>
            {
                await WriteLineAsync($"Requesting data from range {range.Start:dd.MM.yyyy HH:mm:ss} - {range.End:dd.MM.yyyy HH:mm:ss}");
                return await bitstamp!.GetOhlcData(settings.Market.Pair, settings.Market.Step, wholeRangeAsSteps, range.Start.ToUnixTimestamp()/1000, range.End.ToUnixTimestamp()/1000, true);
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
                EnqueueManyIfNew(response.Content.Ohlc.Select(ohlc => ohlc.Timestamp.ToDateTime()), false);
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
    if (!analysisQueue.TryDequeue(out var stamp))
    {
        return;
    }

    await AnalyzeOhlcItem(stamp);
    await WriteLineAsync($"Analyzed OHLC item from {stamp} (total {analysisQueue.Count} left)");
}

static async Task<InfluxOhlcData> GetOhlcDataFromInfluxDb(InfluxDbSettings influxDbSettings, DateTime startTimestamp, DateTime endTimestamp)
{
    var client = new InfluxDBClient(influxDbSettings.Uri.AbsoluteUri, influxDbSettings.Token);
    var api = client.GetQueryApi();

    var startTime = startTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    var endTime = endTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    var flux = $@"
            from(bucket: ""{influxDbSettings.Bucket}"")
            |> range(start: {startTime}, stop: {endTime})
            |> filter(fn: (r) => r._measurement == ""ohlc_data"")
            |> pivot(rowKey: [""_time""], columnKey: [""_field""], valueColumn: ""_value"")
            |> keep(columns: [""_time"", ""open"", ""low"", ""high"", ""close"", ""volume""])
        ";

    var ohlc = await api.QueryAsync<InfluxOhlc>(flux, influxDbSettings.Org);

    return new()
    {
        Pair = influxDbSettings.Bucket,
        Ohlc = ohlc
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
    client.GetWriteApi().WritePoint(pointData, settings.InfluxDb.Bucket, settings.InfluxDb.Org);
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

async Task<List<DateTime>> GetTimestamps(string measurement)
{
    var options = GetClientOptions();
    using InfluxDBClient client = new(options);
    string fluxQuery = $@"
from(bucket: ""{settings.InfluxDb.Bucket}"")
  |> range(start: {AnalysisHistoryBufferTimeSpan(true).ToFluxRange()})
  |> filter(fn: (r) => r._measurement == ""{measurement}"")
  |> keep(columns: [""_time""])
";
    
    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(fluxQuery, options.Org);
    var timestamps = tables
        .SelectMany(table => table.Records.Select(rec => rec.GetTimeInDateTime()!.Value))
        .ToList();

    return timestamps;
}

async Task<List<DateTime>> GetUnanalyzedDataTimestamps()
{
    var ohlcStamps = await GetTimestamps("ohlc_data");
    var analysisStamps = await GetTimestamps("analysis");
    return analysisStamps.Where(stamp => !ohlcStamps.Contains(stamp)).ToList();
}
async Task AnalyzeOhlcItem(DateTime timestamp)
{
    var earliestDateTimeOfRequiredHistoryData = timestamp - AnalysisHistoryBufferTimeSpan(false);

    await WriteLineAsync($"Getting history for analysis.");
    var getHistoryData = await GetOhlcDataFromInfluxDb(settings.InfluxDb, earliestDateTimeOfRequiredHistoryData, timestamp);
    if (getHistoryData.Ohlc == null || getHistoryData.Ohlc.Count < settings.Analysis.GetLongestPeriod())
    {
        await WriteLineAsync($"Not enough data for analysis. Skipping.");
        return;
    }

    await WriteLineAsync($"Analyzing data for {timestamp}...");
    var analysisData = Enrichment.AnalyzeLatestOhlc(getHistoryData, settings.Analysis, settings.Market.Pair, timestamp);
    PushAnalysisDataToDb(analysisData);
}

TimeSpan AnalysisHistoryBufferTimeSpan(bool includeHistory = true) => (includeHistory ? settings.MinimumHistoryDataSpan : TimeSpan.Zero) + GetTimespanOfPeriodValue(settings.Market.Step, Convert.ToInt16(settings.Analysis.GetLongestPeriod() + 5));


public record HistoryData(OhlcData Data, long StartTimestamp, long EndTimestamp);