using System.IO;
using System.Text.Json;
using KiLupeDemo.Models;

namespace KiLupeDemo.Services;

public sealed record StartupConfigurationResult(AnalysisConfiguration Configuration, string? Warning);

public static class StartupConfigurationLoader
{
    public const string FileName = "kilupe.config.json";

    public static StartupConfigurationResult Load(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Die Konfiguration muss ein JSON-Objekt sein.");
            var configuration = AnalysisConfiguration.Default;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                configuration = property.Name switch
                {
                    "objectModel" => configuration with { ObjectModel = ReadEnum<ObjectModelKind>(property) },
                    "textCorrectionModel" => configuration with { TextCorrectionModel = ReadEnum<TextCorrectionModelKind>(property) },
                    "provider" => configuration with { Provider = ReadEnum<InferenceProviderKind>(property) },
                    _ => throw new JsonException($"Unbekannte Einstellung: {property.Name}")
                };
            }
            return new(configuration, null);
        }
        catch (FileNotFoundException)
        {
            return new(AnalysisConfiguration.Default, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new(AnalysisConfiguration.Default, null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new(AnalysisConfiguration.Default,
                $"Startkonfiguration konnte nicht geladen werden ({path}): {exception.Message} Standardwerte werden verwendet.");
        }
    }

    private static T ReadEnum<T>(JsonProperty property) where T : struct, Enum
    {
        if (property.Value.ValueKind == JsonValueKind.String)
        {
            var name = property.Value.GetString();
            if (Enum.GetNames<T>().Contains(name, StringComparer.OrdinalIgnoreCase))
                return Enum.Parse<T>(name!, ignoreCase: true);
        }
        throw new JsonException($"Ungueltiger Wert fuer {property.Name}. Erlaubt: {string.Join(", ", Enum.GetNames<T>())}.");
    }
}
