using System.Text.Json.Serialization;
using MilkerTools.Misc.NumericStringConversion;
namespace MilkerTools.Models;
public class CurrencyBasicData
{
    public string Name { get; set; }
    public string Currency { get; set; }
    public string Type { get; set; }
    public string Symbol { get; set; }

    [JsonConverter(typeof(StringToIntConverter))]
    public int Decimals { get; set; }
    public string Logo { get; set; }

    [JsonConverter(typeof(StringToDecimalConverter))]
    public decimal AvailableSupply { get; set; }
    public string Deposit { get; set; }
    public string Withdrawal { get; set; }
}
