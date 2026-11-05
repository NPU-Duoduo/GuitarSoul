using System.IO;
using System.Text.Json;
using GuitarSoul.Audio.Windows;

namespace GuitarSoul.App;

// Save only after the user successfully starts this exact endpoint/channel.
// Friendly names are never used as identity and this record cannot start capture.
internal sealed record InputPreferences(string DeviceId, int Channel, int ChannelCount,
    AudioSourceKind SourceKind = AudioSourceKind.GuitarInput)
{
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Guitar Soul", "input-preferences.json");

    public AudioDeviceInfo? Resolve(IEnumerable<AudioDeviceInfo> devices,
        AudioSourceKind sourceKind = AudioSourceKind.GuitarInput) =>
        Enum.IsDefined(SourceKind) && SourceKind == sourceKind ? devices.FirstOrDefault(device =>
            device.Id == DeviceId && device.Channels == ChannelCount && Channel >= -1 && Channel < device.Channels) : null;

    public static InputPreferences? Load(string? path = null)
    {
        try
        {
            path ??= SettingsPath;
            return File.Exists(path) ? JsonSerializer.Deserialize<InputPreferences>(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
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
