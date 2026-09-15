using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
namespace KiLupeDemo.Services;

public sealed record PredictionSettings
{
    public bool Enabled { get; init; }
    public bool OnlyCurrentSentence { get; init; } = true;
    public bool CompleteCalculations { get; init; } = true;
    public bool AllowInsertion { get; init; }
    public int PauseMilliseconds { get; init; } = 700;
    public Key AcceptKey { get; init; } = Key.Space;
    public ModifierKeys AcceptModifiers { get; init; } = ModifierKeys.Control | ModifierKeys.Alt;
    public string ExcludedProcesses { get; init; } = "";
    [JsonIgnore] public string ShortcutLabel => new KeyGesture(AcceptKey, AcceptModifiers).GetDisplayStringForCulture(System.Globalization.CultureInfo.CurrentCulture);
    [JsonIgnore] public HashSet<string> Exclusions => (ExcludedProcesses ?? "").Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(name => Path.GetFileNameWithoutExtension(name.Trim())).ToHashSet(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore] public bool IsValid => PauseMilliseconds is >= 300 and <= 5000
        && AcceptModifiers is (ModifierKeys.Control | ModifierKeys.Alt) or (ModifierKeys.Control | ModifierKeys.Shift)
        && !(AcceptKey == Key.L && AcceptModifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        && (AcceptKey == Key.Space || AcceptKey >= Key.A && AcceptKey <= Key.Z);

    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiLupe", "prediction.json");
    public static PredictionSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new();
        var settings = JsonSerializer.Deserialize<PredictionSettings>(File.ReadAllText(SettingsPath));
        return settings is { IsValid: true } ? settings : throw new InvalidDataException("Ungueltige Vorhersage-Einstellungen.");
    }
    public void Save()
    {
        if (!IsValid) throw new InvalidDataException("Ungueltige Vorhersage-Einstellungen.");
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, SettingsPath, true);
    }
}
