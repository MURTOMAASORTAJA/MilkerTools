using BitstampLogger;
using InfluxDB.Client;
using MilkerTools.Models;

var settings = new Settings();


InfluxDBClientOptions GetClientOptions() => 
    new InfluxDBClientOptions(settings!.InfluxDb.Uri.ToString())
{
    Token = settings.InfluxDb.Token,
    Org = settings.InfluxDb.Org,
    Bucket = settings.InfluxDb.Bucket
};

void PushOhlcDataToDb(OHLCData data)
{
    using InfluxDBClient client = new(GetClientOptions());
    client.GetWriteApi().WriteMeasurement(data.ToPointData());
}



async Task<DateTime?> GetLatestTimestamp(string bucket, string org, string measurement, string tagKey, string tagValue)
{
    using InfluxDBClient client = new(GetClientOptions());
    string fluxQuery = $@"
from(bucket: ""{bucket}"")
  |> range(start: 0)
  |> filter(fn: (r) => r._measurement == ""{measurement}"" and r.{tagKey} == ""{tagValue}"")
  |> filter(fn: (r) => r._field == ""close"")
  |> last()
  |> keep(columns: [""_time""])";

    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(fluxQuery, org);
    var latestPoint = tables.SelectMany(table => table.Records).FirstOrDefault();
    return latestPoint?.GetTime()!.Value.ToDateTimeUtc();
}

async Task<DateTime?> GetEarliestTimestamp(string bucket, string org, string measurement, string tagKey, string tagValue)
{
    using InfluxDBClient client = new(GetClientOptions());
    string fluxQuery = $@"
from(bucket: ""{bucket}"")
  |> range(start: 0)
  |> filter(fn: (r) => r._measurement == ""{measurement}"" and r.{tagKey} == ""{tagValue}"")
  |> filter(fn: (r) => r._field == ""close"")
  |> first()
  |> keep(columns: [""_time""])";

    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(fluxQuery, org);
    var latestPoint = tables.SelectMany(table => table.Records).FirstOrDefault();
    return latestPoint?.GetTime()!.Value.ToDateTimeUtc();
}