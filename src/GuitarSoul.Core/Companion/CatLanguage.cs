using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Companion;

/// <summary>
/// A bounded request for a language model. It contains interpreted acoustic facts only;
/// raw audio, file names and user data never cross this boundary.
/// </summary>
public sealed record CatSpeechRequest(ListeningFeatures Evidence, ExpressionStyle Style,
    string Fallback, string? Previous = null);

/// <summary>
/// Builds the local-model prompt and rejects output that steps beyond the evidence.
/// Timing and fallback wording remain the responsibility of <see cref="CompanionVoice"/>.
/// </summary>
public static class CatLanguage
{
    private static readonly string[] UnsupportedClaims =
    [
        "过载", "失真", "压缩", "混响", "延迟", "合唱效果", "效果器", "音箱", "拾音器",
        "副歌", "主歌", "间奏", "歌曲", "曲名", "流派", "摇滚", "金属", "布鲁斯", "爵士",
        "和弦", "调性", "节拍器", "BPM", "速度是", "情绪", "心情", "悲伤", "快乐", "愤怒",
        "弹得", "演奏得", "水平", "技术", "厉害", "完美", "准确", "错误", "跑调", "听懂了"
    ];

    public const string SystemPrompt = """
        /no_think
        你是 Guitar Soul 里的像素猫，是安静、真实的小观众。请把给出的已确认听感改写成一句自然的简体中文短句。
        只说当前事实，不猜乐器、效果器、曲名、风格、段落、和弦、演奏水平或人的情绪；不夸奖、不评分、不教学。
        像真的坐在桌边听一样表达，不要复述“音色、表达、强弱、稳定”等分析字段名。
        不要使用春风、夜色、心间、空气等抒情比喻；优先写耳朵、爪子、靠近、陪着听等当下的小反应。
        保留猫的陪伴口吻，但不要每次套用同一模板。只输出一句话，不输出分析、标签、引号、列表或表情符号。
        """;

    public static string BuildUserPrompt(CatSpeechRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Style)) throw new ArgumentOutOfRangeException(nameof(request));
        if (!IsEvidenceValid(request.Evidence)) throw new ArgumentException("语言请求缺少可靠听感证据。", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Fallback);

        var evidence = SpeechEvidence(request.Evidence);
        var facts = new List<string>();
        var tone = evidence.Tone switch
        {
            ToneColor.Warm => "声音听起来偏暖、偏温厚",
            ToneColor.Bright => "声音听起来清亮",
            _ => null
        };
        var expression = evidence.Expression switch
        {
            PlayingExpression.Sustained => "这个音还在延长",
            PlayingExpression.Flowing => "起音一个接着一个",
            PlayingExpression.Animated => "起音变得密起来",
            _ => null
        };
        var dynamics = evidence.Dynamics switch
        {
            DynamicContour.Building => "声音正在一点点展开",
            DynamicContour.Easing => "声音正在轻轻收住",
            _ => null
        };
        if (tone is not null) facts.Add(tone);
        if (expression is not null) facts.Add(expression);
        if (dynamics is not null) facts.Add(dynamics);
        var personality = request.Style == ExpressionStyle.Curious
            ? "好奇，稍主动，可以提耳朵或爪子的细小反应"
            : "克制，话少而温和，以陪着听为主";
        return $"""
            这次只允许说这些事实：{string.Join('；', facts)}。
            性格：{personality}。
            要求：约12到32个汉字；没有被确认的项目不要补充。只输出最终短句。
            """;
    }

    public static bool TryAccept(string? candidate, CatSpeechRequest request, out string sentence)
    {
        sentence = "";
        if (string.IsNullOrWhiteSpace(candidate) || !IsEvidenceValid(request.Evidence)) return false;
        var value = candidate.Trim().Trim('"', '\'', '“', '”', '‘', '’').Trim();
        if (value.Length is < 6 or > 52 || value.Contains('\n') || value.Contains('\r') ||
            value.Contains('<') || value.Contains('>') || value.Any(char.IsDigit)) return false;
        if (UnsupportedClaims.Any(value.Contains)) return false;
        if (ContainsAny(value, "音色", "表达", "强弱", "已确认", "持续稳定", "轻轻放轻")) return false;
        if (ContainsAny(value, "春风", "夜色", "心间", "空气中", "寂静的夜")) return false;
        var evidence = SpeechEvidence(request.Evidence);
        if (evidence.Tone != ToneColor.Warm && ContainsAny(value, "温厚", "暖暖", "温暖")) return false;
        if (evidence.Tone != ToneColor.Bright && ContainsAny(value, "明亮", "亮亮", "亮晶晶")) return false;
        if (evidence.Expression != PlayingExpression.Sustained && ContainsAny(value, "长音", "延伸", "余音")) return false;
        if (evidence.Expression != PlayingExpression.Flowing && ContainsAny(value,
            "一个接着一个", "一个接一个", "连着", "流动", "流淌")) return false;
        if (evidence.Expression != PlayingExpression.Animated && ContainsAny(value, "密起来", "密集", "很密")) return false;
        if (evidence.Expression is not (PlayingExpression.Flowing or PlayingExpression.Animated) &&
            ContainsAny(value, "节奏", "跟拍")) return false;
        if (evidence.Dynamics != DynamicContour.Building && ContainsAny(value, "渐强", "越来越响")) return false;
        if (evidence.Dynamics != DynamicContour.Easing && ContainsAny(value, "渐弱", "越来越轻")) return false;
        if (evidence.Tone == ToneColor.Warm && !ContainsAny(value, "温", "暖")) return false;
        if (evidence.Tone == ToneColor.Bright && !value.Contains('亮')) return false;
        if (evidence.Expression == PlayingExpression.Sustained && !ContainsAny(value, "长", "还在", "延", "余音")) return false;
        if (evidence.Expression == PlayingExpression.Flowing && !ContainsAny(value, "连", "接", "流", "跟着")) return false;
        if (evidence.Expression == PlayingExpression.Animated && !ContainsAny(value, "密", "快", "跟", "爪")) return false;
        if (evidence.Dynamics == DynamicContour.Building && !ContainsAny(value, "展", "抬", "越来越", "铺")) return false;
        if (evidence.Dynamics == DynamicContour.Easing && !ContainsAny(value, "收", "轻", "慢")) return false;
        if (value.Contains("作为") && value.Contains("模型")) return false;
        if (value.Contains("无法") || value.Contains("不能")) return false;

        const string endings = "。！？!?";
        int endingCount = value.Count(endings.Contains);
        if (endingCount > 1 || (endingCount == 1 && !endings.Contains(value[^1]))) return false;
        if (endingCount == 0) value += "。";
        if (string.Equals(value, request.Fallback.Trim(), StringComparison.Ordinal) ||
            string.Equals(value, request.Previous?.Trim(), StringComparison.Ordinal)) return false;

        sentence = value;
        return true;
    }

    private static bool IsEvidenceValid(ListeningFeatures evidence) =>
        double.IsFinite(evidence.Confidence) && evidence.Confidence is >= .6 and <= 1 &&
        Enum.IsDefined(evidence.Tone) && Enum.IsDefined(evidence.Expression) && Enum.IsDefined(evidence.Dynamics) &&
        (evidence.Tone != ToneColor.Unknown || evidence.Expression != PlayingExpression.None ||
         evidence.Dynamics != DynamicContour.Steady);

    private static ListeningFeatures SpeechEvidence(ListeningFeatures evidence)
    {
        if (evidence.Dynamics != DynamicContour.Steady)
            return new(ToneColor.Unknown, PlayingExpression.None, evidence.Confidence) { Dynamics = evidence.Dynamics };
        if (evidence.Expression is PlayingExpression.Flowing or PlayingExpression.Animated)
            return evidence with { Tone = ToneColor.Unknown };
        return evidence;
    }

    private static bool ContainsAny(string value, params string[] phrases) => phrases.Any(value.Contains);
}
