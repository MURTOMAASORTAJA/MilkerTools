using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace MilkerTools.Misc.NumericStringConversion;
public class StringToLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return long.Parse(reader.GetString() ?? "", CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

public class StringToDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return decimal.Parse(reader.GetString() ?? "", CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

public class StringToIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return int.Parse(reader.GetString() ?? "", CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

public class BoolToStringConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string stringValue = reader.GetString()!;
        return stringValue switch
        {
            "true" => true,
            "false" => false,
            _ => throw new JsonException($"Invalid boolean string: {stringValue}")
        };
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        string stringValue = value ? "True" : "False";
        writer.WriteStringValue(stringValue);
    }
}
