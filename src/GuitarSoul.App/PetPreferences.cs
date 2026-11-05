using System.IO;
using System.Text.Json;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

namespace GuitarSoul.App;

internal sealed record PetPreferences(CatSkin Skin = CatSkin.Snow, double MotionAmount = .7, bool ReducedMotion = false,
    bool QuietMode = false, ExpressionStyle Expression = ExpressionStyle.Reserved, bool MemoryInvitations = false,
    GuitarStyle Guitar = GuitarStyle.Stratocaster, bool AutoRiffs = false, KeySignature Key = default,
    bool AutoKey = true, bool LocalLanguage = true, int ChordResetSeconds = 15)
{
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Guitar Soul", "pet-preferences.json");
    public static PetPreferences Load(string? path = null)
    {
        path ??= SettingsPath;
        try
        {
            if (!File.Exists(path)) return new();
            var text = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<PetPreferences>(text) ?? new();
            // A file written before automatic keys existed carries a key, but the default value
            // is not a choice. Keep honouring a key the player actually picked; leave the rest
            // on inference so the new behaviour is not hidden behind an old default.
            bool legacyChoice = false;
            using (var document = JsonDocument.Parse(text))
            {
                var root = document.RootElement;
                legacyChoice = root.TryGetProperty(nameof(Key), out _) && !root.TryGetProperty(nameof(AutoKey), out _)
                    && value.Key != KeySignature.Default;
            }
            return value with
            {
                Skin = Enum.IsDefined(value.Skin) ? value.Skin : CatSkin.Snow,
                MotionAmount = double.IsFinite(value.MotionAmount) ? Math.Clamp(value.MotionAmount, 0, 1) : .7,
                Expression = Enum.IsDefined(value.Expression) ? value.Expression : ExpressionStyle.Reserved,
                Guitar = value.Guitar is GuitarStyle.Stratocaster or GuitarStyle.LesPaul ? value.Guitar : GuitarStyle.Stratocaster,
                Key = value.Key with { Tonic = ((value.Key.Tonic % 12) + 12) % 12 },
                AutoKey = value.AutoKey && !legacyChoice,
                ChordResetSeconds = ChordIdleReset.ValidSeconds(value.ChordResetSeconds)
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(string? path = null)
    {
        path = Path.GetFullPath(path ?? SettingsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
