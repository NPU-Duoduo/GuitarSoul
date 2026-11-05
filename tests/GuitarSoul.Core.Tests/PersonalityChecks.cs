using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

internal static class PersonalityChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static readonly ListeningFeatures Warm = new(ToneColor.Warm, PlayingExpression.None, 1);
    private static readonly ListeningFeatures Bright = new(ToneColor.Bright, PlayingExpression.None, 1);
    private static readonly ListeningFeatures Animated = new(ToneColor.Bright, PlayingExpression.Animated, 1);
    private static readonly ListeningFeatures Held = new(ToneColor.Warm, PlayingExpression.Sustained, 1);
    private static CompanionPose Tick(CompanionDirector director, ListeningFeatures features, ExpressionStyle style,
        int index, bool quiet = false, bool steady = false, double drive = 0) =>
        director.Step(true, steady, index % 8 == 0, .05, quiet, style, drive, features);

    public static void DirectorStyles()
    {
        double First(ExpressionStyle style, ListeningFeatures features, CompanionAction action)
        {
            var director = new CompanionDirector(18);
            for (int i = 0; i < 1200; i++)
                if (Tick(director, features, style, i).Action == action) return (i + 1) * .05;
            throw new Exception($"{style} never reached {action} with {features}");
        }
        var curiousEngage = First(ExpressionStyle.Curious, Animated, CompanionAction.Engage);
        var reservedEngage = First(ExpressionStyle.Reserved, Animated, CompanionAction.Engage);
        Check(curiousEngage is > 0 and <= 1 && reservedEngage <= 1.5 && curiousEngage < reservedEngage,
            $"Same dense articulation should invite an earlier curious response: {curiousEngage}, {reservedEngage}");
        var curiousJam = First(ExpressionStyle.Curious, Animated, CompanionAction.Jam);
        var reservedJam = First(ExpressionStyle.Reserved, Animated, CompanionAction.Jam);
        Check(curiousJam >= 5 && reservedJam >= 8 && curiousJam < reservedJam,
            $"Both styles need sustained activity before their different Jam entrances: {curiousJam}, {reservedJam}");
        var flowing = Warm with { Expression = PlayingExpression.Flowing };
        Check(First(ExpressionStyle.Curious, flowing, CompanionAction.Groove) <
            First(ExpressionStyle.Reserved, flowing, CompanionAction.Groove), "Flowing notes lost style-dependent gentle motion");

        // Supplying no perception must preserve the existing deterministic behavior contract.
        var implicitEmpty = new CompanionDirector(9); var explicitEmpty = new CompanionDirector(9);
        for (int i = 0; i < 2400; i++)
        {
            bool active = i % 600 < 400;
            var old = implicitEmpty.Step(active, active, i % 10 == 0, .05, false, ExpressionStyle.Reserved, .5);
            var added = explicitEmpty.Step(active, active, i % 10 == 0, .05, false, ExpressionStyle.Reserved, .5, ListeningFeatures.Empty);
            Check(old == added, "Empty perception changes legacy caller behavior");
        }
    }

    public static void DirectorToneAndSustain()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector();
            for (int i = 0; i < 220; i++) Tick(director, Warm, style, i);
            Check(director.Current.Action == CompanionAction.Listen, "First timbre should not cause repeated notices");
            for (int i = 0; i < 12; i++)
                Check(Tick(director, Bright, style, i).Action == CompanionAction.Listen, "A short timbre flicker triggered attention");
            for (int i = 0; i < 20; i++) Tick(director, Warm, style, i);
            bool noticedChange = false;
            for (int i = 0; i < 90; i++) noticedChange |= Tick(director, Bright, style, i).Action == CompanionAction.Notice;
            Check(noticedChange, "A sustained tone change received no bounded notice");
            for (int i = 0; i < 100; i++) Tick(director, Bright, style, i);
            for (int i = 0; i < 800; i++)
                Check(Tick(director, Bright, style, i).Action == CompanionAction.Listen, "Unchanged timbre repeatedly restarted attention");

            director.Reset();
            for (int i = 0; i < 200 && director.Current.Action != CompanionAction.Engage; i++) Tick(director, Animated, style, i);
            Check(director.Current.Action == CompanionAction.Engage, "Fixture never entered an active gesture");
            var heldBright = Held with { Tone = ToneColor.Bright };
            for (int i = 0; i < 80; i++) Tick(director, heldBright, style, i, steady: true, drive: .9);
            for (int i = 0; i < 200; i++)
                Check(Tick(director, heldBright, style, i, steady: true, drive: .9).Action == CompanionAction.Listen,
                    "A reliable held note should quietly listen despite stale beat/drive evidence");
            for (int i = 0; i < 500; i++)
            {
                var pose = Tick(director, Animated, style, i, quiet: true, steady: true, drive: .9);
                Check(pose.Action == CompanionAction.Listen && pose.Weight == 0, "Quiet failed to suppress feature-driven actions");
            }
        }
    }

    public static void VoiceStylesAndBoundaries()
    {
        var phrases = new Dictionary<ExpressionStyle, string>();
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var voice = new CompanionVoice();
            int starts = 0; double first = 0, visible = 0; string? previous = null;
            for (int i = 0; i < 2400; i++)
            {
                var phrase = voice.Step(Held, true, false, style, .05);
                if (phrase is not null)
                {
                    visible += .05;
                    Check(voice.Source == Held, "Visible voice lost its real evidence");
                    if (previous is null) { starts++; first = (i + 1) * .05; phrases[style] = phrase; }
                }
                else Check(voice.Source == ListeningFeatures.Empty, "Invisible voice retained evidence");
                previous = phrase;
            }
            Check(starts == 1, "Identical ongoing sound should get one observation, not repeated chatter");
            double wait = style == ExpressionStyle.Curious ? 4 : 6;
            Check(first >= wait && first < wait + .1 && visible is >= 4.4 and <= 5.1,
                "A voice observation bypassed initial listening or overstayed its short display");
        }
        Check(phrases[ExpressionStyle.Curious] != phrases[ExpressionStyle.Reserved], "Styles delivered identical phrasing");

        foreach (var reason in new[] { "silence", "quiet", "invalid", "unknown", "resume", "style" })
        {
            var voice = new CompanionVoice();
            for (int i = 0; i < 100; i++) voice.Step(Held, true, false, ExpressionStyle.Curious, .05);
            Check(voice.Current is not null, "Fixture must interrupt a visible sentence");
            var source = reason == "invalid" ? Held with { Confidence = double.NaN } : reason == "unknown" ? ListeningFeatures.Empty : Held;
            Check(voice.Step(source, reason != "silence", reason == "quiet", reason == "style" ? ExpressionStyle.Reserved : ExpressionStyle.Curious,
                reason == "resume" ? 5 : reason == "silence" ? .7 : .05) is null &&
                voice.Source == ListeningFeatures.Empty, $"{reason} failed to immediately clear voice and evidence");
            for (int i = 0; i < 1000; i++)
                Check(voice.Step(Held, true, false, ExpressionStyle.Curious, .05) is null, "Resume replayed an unchanged prior observation");
        }
    }

    public static void VoiceChangesAndReset()
    {
        var changing = new CompanionVoice();
        for (int i = 0; i < 90; i++) changing.Step(Warm, true, false, ExpressionStyle.Curious, .05);
        Check(changing.Current is not null, "Fixture never spoke the initial warm tone");
        for (int i = 0; i < 42; i++) changing.Step(Bright, true, false, ExpressionStyle.Curious, .05);
        Check(changing.Current is null && changing.Source == ListeningFeatures.Empty,
            "A stable new cue left an obsolete tone observation visible");
        var voice = new CompanionVoice();
        string? first = null;
        for (int i = 0; i < 120; i++)
        {
            var phrase = voice.Step(Warm, true, false, ExpressionStyle.Curious, .05);
            first ??= phrase;
        }
        Check(first is not null, "Stable initial tone was never observed");
        // Call every frame even after the first phrase; no time advances from mere observation.
        for (int i = 0; i < 100; i++) voice.Step(Warm, true, false, ExpressionStyle.Curious, .05);
        for (int i = 0; i < 180; i++)
            Check(voice.Step(Bright, true, false, ExpressionStyle.Curious, .05) is null, "Tone changes bypassed speech cooldown");
        string? changed = null;
        for (int i = 0; i < 500; i++)
        {
            var phrase = voice.Step(Bright, true, false, ExpressionStyle.Curious, .05);
            if (phrase is not null) changed = phrase;
        }
        Check(changed is not null && changed != first, "A stable changed tone was never acknowledged after cooldown");

        var building = Bright with { Dynamics = DynamicContour.Building };
        string? dynamic = null;
        for (int i = 0; i < 700; i++)
        {
            var phrase = voice.Step(building, true, false, ExpressionStyle.Curious, .05);
            if (phrase is not null)
            {
                dynamic = phrase;
                Check(voice.Source.Dynamics == DynamicContour.Building, "Dynamic description has wrong evidence");
            }
        }
        Check(dynamic is not null && dynamic != changed, "A stable dynamic contour cannot get its own observation");
        voice.Reset();
        Check(voice.Current is null && voice.Source == ListeningFeatures.Empty, "Reset retained a sentence");
        for (int i = 0; i < 60; i++) Check(voice.Step(Warm, true, false, ExpressionStyle.Curious, .05) is null, "Reset skipped initial listening");
        for (int i = 0; i < 30; i++) voice.Step(Warm, true, false, ExpressionStyle.Curious, .05);
        Check(voice.Current == first, "Reset cannot replay the same real listening evidence deterministically");
        foreach (var invalid in new[] { -.1, double.NaN, double.PositiveInfinity })
        {
            bool rejected = false;
            try { voice.Step(Warm, true, false, ExpressionStyle.Curious, invalid); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid voice time was accepted");
        }
    }

    public static void VoicePhraseGaps()
    {
        var voice = new CompanionVoice();
        bool spoke = false;
        double evidence = 0;
        string? previous = null;
        for (int i = 0; i < 200; i++)
        {
            bool sounding = i % 10 < 6;
            if (sounding) evidence += .05;
            var phrase = voice.Step(sounding ? Warm : ListeningFeatures.Empty, sounding, false, ExpressionStyle.Curious, .05);
            if (!sounding) Check(phrase is null || phrase == previous, "A silent note gap started a new voice observation");
            if (phrase is not null)
            {
                spoke = true;
                Check(evidence >= 4, "Phrase gaps counted as active evidence");
            }
            previous = phrase;
        }
        Check(spoke, "Short pluck gaps continually erased the listening evidence");
        voice.Reset();
        for (int i = 0; i < 60; i++) voice.Step(Warm, true, false, ExpressionStyle.Curious, .05);
        for (int i = 0; i < 20; i++) voice.Step(ListeningFeatures.Empty, false, false, ExpressionStyle.Curious, .05);
        for (int i = 0; i < 60; i++)
            Check(voice.Step(Warm, true, false, ExpressionStyle.Curious, .05) is null, "A long stop joined two short phrases into one observation");
        voice.Reset();
        for (int i = 0; i < 60; i++) voice.Step(Warm, true, false, ExpressionStyle.Curious, .05);
        voice.Step(Warm, true, true, ExpressionStyle.Curious, .05);
        for (int i = 0; i < 60; i++)
            Check(voice.Step(Warm, true, false, ExpressionStyle.Curious, .05) is null, "Quiet preserved a pending sentence for immediate replay");

        voice.Reset();
        for (int i = 0; i < 90; i++) voice.Step(Held, true, false, ExpressionStyle.Curious, .05);
        var displayed = voice.Current;
        Check(displayed is not null, "Gap display fixture never spoke");
        for (int i = 0; i < 6; i++)
            Check(voice.Step(ListeningFeatures.Empty, false, false, ExpressionStyle.Curious, .05) == displayed && voice.Source == Held,
                "A normal detached-note gap erased a readable observation or its evidence");
        for (int i = 0; i < 9; i++) voice.Step(ListeningFeatures.Empty, false, false, ExpressionStyle.Curious, .05);
        Check(voice.Current is null && voice.Source == ListeningFeatures.Empty, "A long silence retained old words");
    }

    public static void VoiceStableArticulation()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        foreach (var expression in new[] { PlayingExpression.Flowing, PlayingExpression.Animated })
        {
            var voice = new CompanionVoice();
            int starts = 0;
            double visible = 0;
            string? previous = null;
            for (int i = 0; i < 800; i++)
            {
                var feature = (i % 8 < 4 ? Warm : Bright) with { Expression = expression };
                var words = voice.Step(feature, true, false, style, .05);
                if (words is not null)
                {
                    visible += .05;
                    if (previous is null)
                    {
                        starts++;
                        Check(voice.Source == feature, "Articulation words lost their actual starting evidence");
                    }
                    Check(voice.Source.Expression == expression, "Articulation words described the wrong phrase");
                }
                previous = words;
            }
            Check(starts == 1 && visible is >= 4.4 and <= 5.1,
                $"{style} {expression} words were reset or repeated by unrelated tone changes: {starts}, {visible}");
        }

        var dynamicVoice = new CompanionVoice();
        bool heardDynamics = false;
        var toneVoice = new CompanionVoice();
        for (int i = 0; i < 200; i++)
        {
            var feature = (i % 8 < 4 ? Warm : Bright) with
            {
                Expression = i % 8 < 4 ? PlayingExpression.Flowing : PlayingExpression.Animated,
                Dynamics = DynamicContour.Building
            };
            heardDynamics |= dynamicVoice.Step(feature, true, false, ExpressionStyle.Curious, .05) is not null;
            Check(toneVoice.Step(i % 8 < 4 ? Warm : Bright, true, false, ExpressionStyle.Curious, .05) is null,
                "An unstable tone produced words that require confirmed tone evidence");
        }
        Check(heardDynamics, "A stable dynamic contour was erased by changing notes or articulation");
    }
}
