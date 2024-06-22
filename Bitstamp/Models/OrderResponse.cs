using System.Text.Json;
using System.Text.Json.Serialization;
using MilkerTools.Bitstamp.Misc.NumericStringConversion;

namespace MilkerTools.Bitstamp.Models;

public class OrderResponse
{
    /// <summary>
    /// Order ID.
    /// </summary>
    [JsonConverter(typeof(StringToLongConverter))]
    public long Id { get; set; }

    /// <summary>
    /// Market formatted as "BTC/USD".
    /// </summary>
    public required string Market { get; set; }

    public required DateTime Datetime { get; set; }

    [JsonConverter(typeof(OrderTypeConverter))]
    public OrderType Type { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public string? ClientOrderId { get; set; }
}

public class OrderTypeConverter : JsonConverter<OrderType>
{
    public override OrderType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string stringValue = reader.GetString()!;

        if (Enum.TryParse(typeof(OrderType), stringValue, out var enumValue))
        {
            return (OrderType)enumValue;
        }
        throw new JsonException($"Unable to convert \"{stringValue}\" to OrderType.");
    }

    public override void Write(Utf8JsonWriter writer, OrderType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(((int)value).ToString());
    }
}

public enum OrderType
{
    Buy = 0,
    Sell = 1
}
