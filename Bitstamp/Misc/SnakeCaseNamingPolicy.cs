using System.Text.Json;
namespace MilkerTools.Misc;

/// <summary>
/// Policy to convert property names to snake_case.
/// </summary>
public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }
        var snakeCaseName = new System.Text.StringBuilder();
        char previousChar = '\0';
        for (int i = 0; i < name.Length; i++)
        {
            if ((char.IsUpper(name[i]) || (char.IsNumber(name[i]) && !char.IsNumber(previousChar))) && i > 0)
            {
                snakeCaseName.Append('_');
            }
            snakeCaseName.Append(char.ToLower(name[i]));
            previousChar = name[i];
        }
        return snakeCaseName.ToString();
    }
}
