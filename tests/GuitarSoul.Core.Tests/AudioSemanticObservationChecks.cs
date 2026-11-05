using GuitarSoul.Core.AudioModels;

internal static class AudioSemanticObservationChecks
{
    public static void StrictSchemaKeepsUnknownAndRejectsClaimsOutsideTheContract()
    {
        const string unknown = """
            {"schemaVersion":1,"activity":"unknown","brightness":"unknown","density":"unknown","confidence":"unknown"}
            """;
        True(AudioSemanticObservation.TryParseStrict(unknown, out var observation, out _));
        Equal("unknown", observation!.Activity);

        Reject("""{"schemaVersion":1,"activity":"solo","brightness":"bright","density":"dense","confidence":"high"}""");
        Reject("""{"schemaVersion":1,"activity":"pulsed","brightness":"bright","density":"dense","confidence":0.8}""");
        Reject("""{"schemaVersion":1,"activity":"pulsed","brightness":"bright","density":"dense","confidence":"certain"}""");
        Reject("""{"schemaVersion":1,"activity":"pulsed","brightness":"bright","density":"dense","confidence":"high","instrument":"guitar"}""");
        Reject("""{"schemaVersion":2,"activity":"pulsed","brightness":"bright","density":"dense","confidence":"high"}""");
        Reject("""{"schemaVersion":1,"activity":"pulsed","brightness":"bright","confidence":"high"}""");
    }

    private static void Reject(string json) =>
        True(!AudioSemanticObservation.TryParseStrict(json, out _, out var error) && error.Length > 0);

    private static void True(bool condition)
    {
        if (!condition) throw new Exception("Expected condition to be true.");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}.");
    }
}
