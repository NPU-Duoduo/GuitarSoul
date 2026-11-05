using System.Text.Json;

namespace GuitarSoul.Core.AudioModels;

public sealed record AudioSemanticObservation(
    string Activity,
    string Brightness,
    string Density,
    string Confidence)
{
    private static readonly HashSet<string> Activities =
        new(StringComparer.Ordinal) { "silence", "sustained", "pulsed", "mixed", "unknown" };
    private static readonly HashSet<string> BrightnessValues =
        new(StringComparer.Ordinal) { "dark", "balanced", "bright", "unknown" };
    private static readonly HashSet<string> DensityValues =
        new(StringComparer.Ordinal) { "sparse", "steady", "dense", "unknown" };
    private static readonly HashSet<string> ConfidenceValues =
        new(StringComparer.Ordinal) { "low", "medium", "high", "unknown" };
    private static readonly HashSet<string> Properties =
        new(StringComparer.Ordinal) { "schemaVersion", "activity", "brightness", "density", "confidence" };

    public const int SchemaVersion = 1;

    public static bool TryParseStrict(string json, out AudioSemanticObservation? observation, out string error)
    {
        observation = null;
        error = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Fail("The response is not a JSON object.", out error);
            foreach (var property in root.EnumerateObject())
                if (!Properties.Contains(property.Name)) return Fail($"Unexpected property: {property.Name}.", out error);
            if (root.GetRawText().Length > 512) return Fail("The response is too large.", out error);
            if (!ReadInteger(root, "schemaVersion", out int version) || version != SchemaVersion)
                return Fail("Unsupported or missing schemaVersion.", out error);
            if (!ReadChoice(root, "activity", Activities, out string activity, out error)) return false;
            if (!ReadChoice(root, "brightness", BrightnessValues, out string brightness, out error)) return false;
            if (!ReadChoice(root, "density", DensityValues, out string density, out error)) return false;
            if (!ReadChoice(root, "confidence", ConfidenceValues, out string confidence, out error)) return false;
            if (root.EnumerateObject().Count() != Properties.Count)
                return Fail("The response is missing a required property.", out error);
            observation = new AudioSemanticObservation(activity, brightness, density, confidence);
            return true;
        }
        catch (JsonException exception)
        {
            error = "Invalid JSON: " + exception.Message;
            return false;
        }
    }

    private static bool ReadInteger(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var element) &&
            element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool ReadChoice(
        JsonElement root,
        string name,
        HashSet<string> allowed,
        out string value,
        out string error)
    {
        value = "";
        error = "";
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return Fail($"Missing string property: {name}.", out error);
        value = element.GetString() ?? "";
        return allowed.Contains(value) || Fail($"Unsupported {name}: {value}.", out error);
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}

public sealed record AudioSemanticRequest(ReadOnlyMemory<float> MonoPcm, int SampleRate)
{
    public TimeSpan Duration => SampleRate > 0
        ? TimeSpan.FromSeconds((double)MonoPcm.Length / SampleRate)
        : TimeSpan.Zero;
}

public interface IAudioSemanticAnalyzer
{
    ValueTask<AudioSemanticObservation?> AnalyzeAsync(
        AudioSemanticRequest request,
        CancellationToken cancellationToken);
}
