using System.Text.Json.Serialization;
using MilkerTools.Misc.NumericStringConversion;

namespace MilkerTools.Models.Requests;

public class LimitOrderRequest
{

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Amount { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal Price { get; set; }

    /// <summary>
    /// If the order gets executed, a new buy order will be placed, with "limit_price" as its price.
    /// </summary>
    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal LimitPrice { get; set; }

    /// <summary>
    /// Opens buy limit order which will be canceled at 0:00 UTC unless it already has been executed.
    /// </summary>
    [JsonConverter(typeof(BoolToStringConverter))]
    public bool? DailyOrder { get; set; }

    /// <summary>
    /// An Immediate-Or-Cancel (IOC) order is an order that must be executed immediately. 
    /// Any portion of an IOC order that cannot be filled immediately will be cancelled.
    /// </summary>
    [JsonConverter(typeof(BoolToStringConverter))]
    public bool? IocOrder { get; set; }

    /// <summary>
    /// A Fill-Or-Kill (FOK) order is an order that must be executed immediately in its entirety. 
    /// If the order cannot be immediately executed in its entirety, it will be cancelled.
    /// </summary>
    [JsonConverter(typeof(BoolToStringConverter))]
    public bool? FokOrder { get; set; }

    /// <summary>
    /// A Maker-Or-Cancel (MOC) order is an order that ensures it is not fully or partially filled when placed. 
    /// In case it would be, the order is cancelled.
    /// </summary>
    [JsonConverter(typeof(BoolToStringConverter))]
    public bool? MocOrder { get; set; }

    /// <summary>
    /// A Good-Till-Date (GTD) lets you select an expiration time up until which the order will be open. 
    /// Note that all GTD orders are cancelled at 00:00:00 UTC.
    /// </summary>
    [JsonConverter(typeof(BoolToStringConverter))]
    public bool GtdOrder { get; set; }

    /// <summary>
    /// Unix timestamp in milliseconds. Required in case of GTD order.
    /// </summary>
    [JsonConverter(typeof(StringToLongConverter))]
    public long? ExpireTime { get; set; }

    /// <summary>
    /// Client order ID set by the client for internal reference. 
    /// It should be unique, but there are no additional constraints or checks guaranteed on the field by Bitstamp.
    /// </summary>
    public string ClientOrderId { get; set; }
}

