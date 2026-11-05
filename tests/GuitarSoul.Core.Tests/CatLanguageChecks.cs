using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

internal static class CatLanguageChecks
{
    private static readonly ListeningFeatures Evidence = new(ToneColor.Warm,
        PlayingExpression.Sustained, .82) { Dynamics = DynamicContour.Steady };

    public static void PromptContainsFactsWithoutAudio()
    {
        var request = new CatSpeechRequest(Evidence, ExpressionStyle.Curious,
            "我想再听一会儿，这个长音暖暖的。", "上一句在这里。");
        var prompt = CatLanguage.BuildUserPrompt(request);
        Check(prompt.Contains("偏暖、偏温厚") && prompt.Contains("这个音还在延长"),
            "Prompt lost the bounded acoustic facts.");
        Check(prompt.Contains("好奇"), "Prompt lost personality.");
        Check(!prompt.Contains(request.Fallback), "Fallback wording should remain in the host instead of anchoring the model.");
        Check(!prompt.Contains(request.Previous!), "Previous wording should be compared by the guard, not used as a model anchor.");
        Check(!prompt.Contains("PCM") && !prompt.Contains("文件"), "Prompt should not ask for audio or files.");
    }

    public static void OutputGuardRejectsUnsupportedClaims()
    {
        var request = new CatSpeechRequest(Evidence, ExpressionStyle.Curious,
            "我想再听一会儿，这个长音暖暖的。", "我陪这个音慢慢走远。 ");
        Check(CatLanguage.TryAccept("这个暖暖的长音还在，我把耳朵往前凑了。", request, out var accepted) &&
            accepted.EndsWith('。'), "A short grounded cat sentence should be accepted.");
        Check(!CatLanguage.TryAccept("这个过载失真音色太摇滚了！", request, out _),
            "Unobserved effect and genre claims crossed the guard.");
        Check(!CatLanguage.TryAccept("你弹得太厉害了！", request, out _),
            "Performance scoring crossed the guard.");
        Check(!CatLanguage.TryAccept("<think>先分析</think>我听着这个音。", request, out _),
            "Reasoning text crossed the guard.");
        Check(!CatLanguage.TryAccept("我先听一会儿。然后再陪你。", request, out _),
            "More than one sentence crossed the guard.");
        Check(!CatLanguage.TryAccept(request.Fallback, request, out _),
            "An unchanged fallback should not be presented as generated variation.");

        var flowing = new CatSpeechRequest(new ListeningFeatures(ToneColor.Warm, PlayingExpression.Flowing, .8),
            ExpressionStyle.Reserved, "我听着这些音一个接着一个。");
        Check(!CatLanguage.BuildUserPrompt(flowing).Contains("偏暖"),
            "An articulation sentence must not inherit a tone claim it was not selected to describe.");
        Check(!CatLanguage.TryAccept("暖暖的长音还在延伸。", flowing, out _),
            "A generated sentence described cues outside the selected articulation evidence.");
    }

    public static void InvalidEvidenceNeverBuildsARequest()
    {
        var invalid = new CatSpeechRequest(ListeningFeatures.Empty, ExpressionStyle.Reserved, "我在听。 ");
        try { _ = CatLanguage.BuildUserPrompt(invalid); }
        catch (ArgumentException) { return; }
        throw new Exception("Empty evidence was accepted for model speech.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
