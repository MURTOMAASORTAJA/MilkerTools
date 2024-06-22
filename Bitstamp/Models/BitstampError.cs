using System.Text.Json.Serialization;
using System.Text.Json;

namespace MilkerTools.Models;

public class BitstampError
{
    [JsonConverter(typeof(ReasonConverter))]
    public Reason Reason { get; set; }
    public string? Error { get; set; }
    public string? Status { get; set; }
    public string? ResponseCode { get; set; }
}

public class Reason
{
    public List<string> All { get; set; }
}


public class ReasonConverter : JsonConverter<Reason>
{
    public override Reason Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Reason reason = new Reason();
        reason.All = new List<string>();

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "__all__")
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndArray)
                            {
                                break;
                            }

                            reason.All.Add(reader.GetString());
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.String)
                    {
                        reason.All.Add(reader.GetString());
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            reason.All.Add(reader.GetString());
        }

        return reason;
    }

    public override void Write(Utf8JsonWriter writer, Reason value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("__all__");

        if (value.All.Count == 1)
        {
            writer.WriteStringValue(value.All[0]);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var item in value.All)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}