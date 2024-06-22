using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MilkerTools.Bitstamp.Misc;
using MilkerTools.Bitstamp.Models;
using MilkerTools.Bitstamp.Models.Requests;

namespace MilkerTools.Bitstamp;
public partial class BitStamp
{
    private ApiSettings Settings { get; set; }
    private HttpClient Client { get; set; }
    private JsonSerializerOptions ResponseJsonOptions { get; set; }
    private JsonSerializerOptions RequestJsonOptions { get; set; }
    private readonly long nonce;

    public BitStamp(ApiSettings settings)
    {
        Settings = settings;
        var handler = new HttpClientHandler()
        {
            Credentials = new NetworkCredential(Settings.Key, Settings.Secret)
        };

        Client = new(handler)
        {
            BaseAddress = new Uri("https://www.bitstamp.net/api/v2/")
        };

        ResponseJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new SnakeCaseNamingPolicy()
        };

        RequestJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new PascalCaseNamingPolicy()
        };
        nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// <para>
    /// Gets trading pairs.
    /// </para>
    /// <seealso href="https://www.bitstamp.net/api/#tag/Market-info/operation/GetTradingPairs"/>
    /// </summary>
    public async Task<List<TradingPair>> GetTradingPairsAsync()
    {
        var response = await Client.GetAsync("trading-pairs-info/");
        var content = await response.Content.ReadAsStringAsync();
        var tradingPairs = JsonSerializer.Deserialize<List<TradingPair>>(content, ResponseJsonOptions);
        return tradingPairs ?? [];
    }

    /// <summary>
    /// Returns OHLC (Open High Low Close) data.
    /// </summary>
    /// <param name="marketSymbol">Market symbol.</param>
    /// <param name="step">Timeframe in seconds.</param>
    /// <param name="limit">Limit OHLC results.</param>
    /// <param name="start">Unix timestamp from when OHLC data will be started.</param>
    /// <param name="end">
    /// Unix timestamp to when OHLC data will be shown.
    /// If none from start or end timestamps are posted 
    /// then endpoint returns OHLC data to current unixtime. 
    /// If both start and end timestamps are posted, end timestamp 
    /// will be used.</param>
    /// <param name="excludeCurrentCandle">If set, results won't include current (open) candle.</param>
    public async Task<BitstampResponse<OhlcData>> GetOhlcData(
        string marketSymbol,

        // hours:                                  1     2      4      6     12     24      48
        // minutes:     1    2    5   15    30    60   120    240    360    720   1440    4320
        [AllowedValues(60, 180, 300, 900, 1800, 3600, 7200, 14400, 21600, 43200, 86400, 259200)]
        int step,

        [Range(1, 1000)]
        int limit,

        long? start = null,
        long? end = null,
        bool? excludeCurrentCandle = false)
    {
        var url = $"ohlc/{marketSymbol}/?step={step}&limit={limit}";
        if (start != null)
        {
            url += $"&start={start}";
        }
        if (end != null)
        {
            url += $"&end={end}";
        }
        if (excludeCurrentCandle != null)
        {
            url += $"&exclude_current_candle={excludeCurrentCandle}";
        }
        var response = await Client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        try
        {
            var dataNode = JsonNode.Parse(content)!["data"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(dataNode))
            {
                throw new JsonException();
            }
            var ohlcData = Deserialize<OhlcData>(dataNode, ResponseJsonOptions);
            return ohlcData!;
        }
        catch (JsonException)
        {
            var error = JsonSerializer.Deserialize<BitstampError>(content);
            return new BitstampErrorResponse<OhlcData>(error!);
        }
    }

    public async Task<BitstampResponse<Ticker>> GetTicker(string marketSymbol)
    {
        var response = await Client.GetAsync($"ticker/{marketSymbol}/");
        var ticker = await ReadResponse<Ticker>(response.Content, ResponseJsonOptions);
        return ticker!;
    }

    public async Task<BitstampResponse<FeeData>> GetFees(string marketSymbol)
    {
        var response = await SendAuthenticatedRequest(HttpMethod.Post, "", $"fees/trading/{marketSymbol}/");
        var feeData = await ReadResponse<FeeData>(response.Content, ResponseJsonOptions);
        return feeData!;
    }

    public async Task<BitstampResponse<List<FeeData>>> GetFees()
    {
        var response = await SendAuthenticatedRequest(HttpMethod.Post, "", "fees/trading/");
        var feeData = await ReadResponse<List<FeeData>>(response.Content, ResponseJsonOptions);
        return feeData!;
    }

    /// <summary>
    /// Create a buy limit order in the exchange.
    /// </summary>
    /// <param name="marketSymbol">Format is btceur, first one being what you buy, and the second what you buy with.</param>
    public async Task<BitstampResponse<OrderResponse>> BuyLimitOrder(string marketSymbol, LimitOrderRequest buyLimitOrder)
    {
        var casingCorrectedJson = JsonSerializer.Serialize(buyLimitOrder, ResponseJsonOptions);
        var propertyDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(casingCorrectedJson);
        var formContent = new FormUrlEncodedContent(propertyDictionary!.Where(kvp => kvp.Value != null).Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()!)));
        var response = await SendAuthenticatedRequest(HttpMethod.Post, "", $"buy/{marketSymbol}/", formContent);
        return await ReadResponse<OrderResponse>(response.Content, ResponseJsonOptions);
    }

    private static async Task<BitstampResponse<T>> ReadResponse<T>(HttpContent responseContent, JsonSerializerOptions options)
    {
        var responseString = await responseContent.ReadAsStringAsync();
        return Deserialize<T>(responseString, options);
    }

    private static BitstampResponse<T> Deserialize<T>(string json, JsonSerializerOptions options)
    {
        try
        {
            var response = JsonSerializer.Deserialize<T>(json, options)!;
            return new BitstampResponse<T>(response);
        }
        catch (JsonException)
        {
            try
            {
                var error = JsonSerializer.Deserialize<BitstampError>(json, options)!;
                return new BitstampErrorResponse<T>(error);
            }
            catch (Exception)
            {

                throw;
            }
        }
    }

    private async Task<HttpResponseMessage> SendAuthenticatedRequest(HttpMethod method, string query, string url, HttpContent? content = null)
    {
        var contentStr = content != null ? (await content?.ReadAsStringAsync() ?? null): null;
        string nonce = Guid.NewGuid().ToString("D").ToLower();
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string contentType = contentStr != null ? "application/x-www-form-urlencoded" : string.Empty;
        //string contentType = contentStr != null ? "www-form-urlencoded" : string.Empty;
        string version = "v2";
        var uriBuilder = new UriBuilder(new Uri(Client.BaseAddress, url));
        uriBuilder.Query = query;
        var fullUri = uriBuilder.Uri;
        string host = fullUri.Host;
        string path = fullUri.AbsolutePath;

        // Construct the message to sign
        string message = $"BITSTAMP {Settings.Key}{method.ToString().ToUpper()}{host}{path}{query}{contentType}{nonce}{timestamp}{version}{contentStr}";
        string signedSignature = CreateSignature(message);
        var request = new HttpRequestMessage(method, fullUri);

        if (content != null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Content = content;
        }

        request.Headers.Add("X-Auth", $"BITSTAMP {Settings.Key}");
        request.Headers.Add("X-Auth-Signature", signedSignature);
        request.Headers.Add("X-Auth-Nonce", nonce);
        request.Headers.Add("X-Auth-Timestamp", timestamp);
        request.Headers.Add("X-Auth-Version", version);
        //if (!string.IsNullOrEmpty(contentType))
        //{
        //    request.Headers.Add("Content-Type", contentType);
        //}

        var response = await Client.SendAsync(request);
        return response;
    }


    private string CreateSignature(string message)
    {
        using var hmac = CreateHmac();
        var messageBytes = Encoding.ASCII.GetBytes(message);
        var hashMessage = hmac.ComputeHash(messageBytes);
        return BitConverter.ToString(hashMessage).Replace("-", "").ToUpper();
    }

    private HMACSHA256 CreateHmac()
    {
        var keyByte = Encoding.ASCII.GetBytes(Settings.Secret);
        return new HMACSHA256(keyByte);
    }


    private void DoAuthStuff((HttpResponseMessage Message, string Timestamp, string Body) response)
    {
        
        var serverSignature = response.Message.Headers.GetValues("X-Server-Auth-Signature").FirstOrDefault();
        var responseContentType = response.Message.Content.Headers.ContentType?.MediaType ?? "";
        var stringToSign = nonce + response.Timestamp + responseContentType + response.Body;

    }
}
