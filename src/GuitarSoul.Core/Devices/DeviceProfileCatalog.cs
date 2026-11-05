using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuitarSoul.Core.Devices;

/// <summary>Documentation hints. These values must never override an endpoint's negotiated capture format.</summary>
public sealed record DeviceProfile
{
    public string Id { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string[] DisplayNameHints { get; init; } = [];
    public int DocumentedSampleRate { get; init; }
    public int DocumentedBitsPerSample { get; init; }
    public int DocumentedChannels { get; init; }
    public bool IsHardwareVerified { get; init; }
    /// <summary>Only confirms that a real endpoint opened and delivered samples through WASAPI.</summary>
    public bool IsCaptureSmokeTested { get; init; }
    public string[] Notes { get; init; } = [];
    public string SourceUrl { get; init; } = "";
}

public sealed record DeviceProfileDocument
{
    public int SchemaVersion { get; init; }
    public DeviceProfile[] Profiles { get; init; } = [];
}

public sealed class DeviceProfileCatalog
{
    private DeviceProfileCatalog(DeviceProfile[] profiles) => Profiles = Array.AsReadOnly(profiles);

    public IReadOnlyList<DeviceProfile> Profiles { get; }

    /// <summary>Throws InvalidDataException for malformed/untrusted configuration; callers can fall back to no hints.</summary>
    public static DeviceProfileCatalog Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static DeviceProfileCatalog Load(Stream stream)
    {
        DeviceProfileDocument document;
        try
        {
            document = JsonSerializer.Deserialize<DeviceProfileDocument>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            }) ?? throw new InvalidDataException("The profile document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Device profile JSON is invalid.", exception);
        }

        if (document.SchemaVersion != 1)
            throw new InvalidDataException("Unsupported device profile schema version.");
        if (document.Profiles is null || document.Profiles.Length > 200)
            throw new InvalidDataException("Device profile list must contain at most 200 items.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in document.Profiles)
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || !ids.Add(profile.Id))
                throw new InvalidDataException("Every profile needs a unique non-empty ID.");
            if (string.IsNullOrWhiteSpace(profile.Manufacturer) || string.IsNullOrWhiteSpace(profile.Model))
                throw new InvalidDataException($"Profile '{profile.Id}' needs manufacturer and model names.");
            if (profile.DisplayNameHints is null || profile.DisplayNameHints.Length == 0 ||
                profile.DisplayNameHints.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Profile '{profile.Id}' needs non-empty display-name hints.");
            if (profile.DocumentedSampleRate is < 8000 or > 384000 ||
                profile.DocumentedBitsPerSample is not (8 or 16 or 24 or 32) ||
                profile.DocumentedChannels is < 1 or > 32)
                throw new InvalidDataException($"Profile '{profile.Id}' has an invalid documented audio format.");
            if (profile.Notes is null || profile.Notes.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Profile '{profile.Id}' contains invalid notes.");
            if (!Uri.TryCreate(profile.SourceUrl, UriKind.Absolute, out var source) || source.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException($"Profile '{profile.Id}' needs an HTTPS documentation URL.");
        }
        return new DeviceProfileCatalog(document.Profiles);
    }

    /// <summary>
    /// Returns possible matches only. USB endpoint display names are not reliable hardware IDs;
    /// show these as suggestions and let every other device use generic capture without a profile.
    /// </summary>
    public IReadOnlyList<DeviceProfile> GetDisplayNameSuggestions(string? endpointDisplayName)
    {
        if (string.IsNullOrWhiteSpace(endpointDisplayName))
            return [];
        return Profiles.Where(profile => profile.DisplayNameHints.Any(hint =>
            endpointDisplayName.Contains(hint, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
