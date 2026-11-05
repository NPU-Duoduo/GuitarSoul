using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

namespace GuitarSoul.App;

/// <summary>Exports the actual desktop renderer; preview clocks and inputs never start audio capture.</summary>
internal static class RockerGallery
{
    private const int CatSize = 224, Fps = 12;
    private static readonly GuitarStyle[] Guitars = [GuitarStyle.Stratocaster, GuitarStyle.LesPaul];
    private static readonly string[] Skins = ["白猫", "三花", "橘猫", "狸花"];
    private static readonly string[] Moods = ["等待", "倾听", "兴奋", "休息", "一起弹"];
    private static readonly string[] Actions = ["等待", "注意到起音", "倾听", "轻微跟拍", "短暂投入", "慢慢收拍", "整理自己", "休息", "一起弹"];
    private static readonly Brush Background = Brush("#EDE9DF"), Panel = Brush("#253A31"), Ink = Brush("#323D35"), Paper = Brush("#FFF9ED");

    public static void Render(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var stem = Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath));
        RenderMain(fullPath);
        RenderStates(stem + "-states.png");
        RenderInstruments(stem + "-guitars.png");
        RenderResponsiveness(stem + "-responsiveness.png");
        RenderMotion(stem + ".gif", directed: false);
        var transitions = RenderMotion(stem + "-behavior.gif", directed: true);
        var checks = Validate();
        var report = new
        {
            Description = "Actual WPF PixelCat renderer; deterministic synthetic gestures; no audio captured.",
            MainSheet = "2 guitar styles × 4 skins, additional Jam pose",
            StateSheet = "2 guitar styles × (4 unchanged moods + new Jam pose); light and dark backgrounds",
            InstrumentSheet = "Isolated Stratocaster and Les Paul silhouettes at 10× cell scale",
            ResponsivenessSheet = "Synthetic evidence only; actual WPF pixels at default 0.7 motion amount; engagement 0.35/0.90 at matched pulse tails; " + stem + "-responsiveness.png",
            RasterExport = "224 DIP render at 96 DPI; additional 144 DPI bitmap export validation (not a monitor-DPI test)",
            Motion = "12-second gesture showcase; 16-second real CompanionDirector simulation after 42 seconds of synthetic steady-input pre-roll; 12 fps; decoded delays verified",
            SystemAnimationEnabled = SystemParameters.ClientAreaAnimation,
            Passed = checks.Count(x => x.Passed), Total = checks.Count,
            Checks = checks,
            DirectedTransitions = transitions
        };
        File.WriteAllText(stem + ".json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllLines(stem + ".txt", new[]
        {
            $"Rocker renderer checks: {report.Passed}/{report.Total}.",
            report.Description, report.MainSheet, report.StateSheet, report.InstrumentSheet, report.ResponsivenessSheet, report.RasterExport, report.Motion,
            "Reduced motion and zero amount tested across time, pulse, and gesture weight, including a moving-to-settle transition.",
            "Bounds checks inspect transparent edges, content area, and full nontransparent extent; animated frames checked at 224 and 168 DIP.",
            "Jam frames also check that every rendered color stays aligned to the cat's shared integer-pixel grid."
        }.Concat(checks.Where(x => !x.Passed).Select(x => "FAIL: " + x.Name)).Concat(transitions));
        if (checks.Any(x => !x.Passed))
            throw new InvalidOperationException("Rocker gallery checks failed; inspect " + stem + ".json");
    }

    private static void RenderMain(string path)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, 1024, 704));
            Label(dc, "Guitar Soul / 猫咪摇滚乐手", 26, 20, 25, Ink);
            Label(dc, "同一只伙伴 · 两把吉他 · 四种毛色", 27, 59, 15, Ink);
            for (int guitar = 0; guitar < Guitars.Length; guitar++)
            {
                var y = 98 + guitar * 294;
                Label(dc, GuitarName(Guitars[guitar]), 27, y, 19, Ink);
                for (int skin = 0; skin < 4; skin++)
                {
                    var x = 20 + skin * 248;
                    dc.DrawRectangle(Panel, null, new Rect(x, y + 32, 240, 250));
                    dc.DrawImage(Cat(Guitars[guitar], (CatSkin)skin, CompanionMood.Excited, .1, 1, jamming: true), new Rect(x + 8, y + 28, 224, 224));
                    Label(dc, Skins[skin], x + 20, y + 246, 15, Paper);
                }
            }
        }
        SavePng(RenderVisual(visual, 1024, 704), path);
    }

    public static void RenderBeatReplay(string beforePath, string afterPath, string outputPath)
    {
        using var before = JsonDocument.Parse(File.ReadAllText(beforePath));
        using var after = JsonDocument.Parse(File.ReadAllText(afterPath));
        if (before.RootElement.GetProperty("SourceSha256").GetString() != after.RootElement.GetProperty("SourceSha256").GetString())
            throw new InvalidDataException("Beat comparison requires identical source audio.");
        var a = before.RootElement.GetProperty("behavior").GetProperty("styles")[0].GetProperty("trajectory");
        var b = after.RootElement.GetProperty("behavior").GetProperty("styles")[0].GetProperty("trajectory");
        if (a.GetArrayLength() != b.GetArrayLength()) throw new InvalidDataException("Trace lengths differ.");
        for (int i = 0; i < a.GetArrayLength(); i++)
            if (a[i].GetProperty("seconds").GetDouble() != b[i].GetProperty("seconds").GetDouble())
                throw new InvalidDataException("Trace sample times differ.");
        string path = Path.GetFullPath(outputPath);
        string poster = Path.ChangeExtension(path, ".png"), report = Path.ChangeExtension(path, ".render.json");
        string stopFrame = Path.ChangeExtension(path, ".stop.png");
        if (File.Exists(path) || File.Exists(poster) || File.Exists(report) || File.Exists(stopFrame)) throw new IOException("Preserve existing replay artifacts.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var left = NewCat(GuitarStyle.Stratocaster, CatSkin.Snow);
        using var right = NewCat(GuitarStyle.Stratocaster, CatSkin.Snow);
        PixelCat[] cats = [left, right];
        JsonElement[] traces = [a, b];
        var encoder = new GifBitmapEncoder();
        int rowIndex = 0, differentFrames = 0;
        double first = a[0].GetProperty("seconds").GetDouble();
        double end = a[a.GetArrayLength() - 1].GetProperty("seconds").GetDouble();
        int frames = (int)Math.Floor((end - first) * Fps) + 1;
        for (int frame = 0; frame < frames; frame++)
        {
            double time = first + frame / (double)Fps;
            while (rowIndex + 1 < a.GetArrayLength() && a[rowIndex + 1].GetProperty("seconds").GetDouble() <= time) rowIndex++;
            var visual = new DrawingVisual();
            byte[]? previousPixels = null;
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Panel, null, new Rect(0, 0, 560, 360));
                Label(dc, $"同一音频 · 原版 / 修正 · {time:F2}s", 20, 14, 18, Paper);
                Label(dc, "实际猫绘制器离线重放 · 无声 · 非现场截图", 20, 43, 13, Paper);
                for (int col = 0; col < 2; col++)
                {
                    var row = traces[col][rowIndex];
                    var action = Enum.Parse<CompanionAction>(row.GetProperty("action").GetString()!);
                    double? bpm = row.GetProperty("beatBpm").ValueKind == JsonValueKind.Number ? row.GetProperty("beatBpm").GetDouble() : null;
                    var rhythm = new RhythmFrame(row.GetProperty("seconds").GetDouble(), row.GetProperty("lastOnsetSeconds").GetDouble(),
                        row.GetProperty("onsetCount").GetInt64(),
                        row.GetProperty("articulationBpm").ValueKind == JsonValueKind.Number ? row.GetProperty("articulationBpm").GetDouble() : null,
                        row.GetProperty("articulationConfidence").GetDouble())
                    { BeatBpm = bpm, BeatConfidence = row.GetProperty("beatConfidence").GetDouble(), BeatAnchorSeconds = row.GetProperty("beatAnchorSeconds").GetDouble() };
                    var cat = cats[col];
                    cat.PreviewSeconds = time; cat.PreviewAudioAdvance = time - rhythm.AudioSeconds;
                    cat.SetRhythm(rhythm, 1);
                    cat.SetPose(new(action, ActionMood(action), row.GetProperty("weight").GetDouble())
                    { Engagement = row.GetProperty("engagement").GetDouble(), HasMusicalEvidence = true });
                    var bitmap = RenderVisual(cat, CatSize, CatSize);
                    var pixels = Pixels(bitmap);
                    if (col == 0) previousPixels = pixels;
                    else if (!pixels.AsSpan().SequenceEqual(previousPixels)) differentFrames++;
                    dc.DrawImage(bitmap, new Rect(col * 280 + 28, 70, CatSize, CatSize));
                    bool locked = row.GetProperty("beatLocked").GetBoolean();
                    Label(dc, (col == 0 ? "原版" : "修正") + (locked ? $" · {bpm:F1} BPM" : " · 尚未锁拍"), col * 280 + 26, 295, 16, Paper);
                    Label(dc, Actions[(int)action], col * 280 + 26, 321, 13, Paper);
                }
            }
            var output = RenderVisual(visual, 560, 360);
            encoder.Frames.Add(BitmapFrame.Create(output));
            if (frame == 8 * Fps) SavePng(output, poster);
            if (frame == 31 * Fps) SavePng(output, stopFrame);
        }
        if (differentFrames == 0) throw new InvalidOperationException("Pulse change did not reach visible cat pixels.");
        SaveTimedAnimation(encoder, path, clearFrames: true);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            scope = "Actual WPF PixelCat renderer, recorded acoustic/director states, deterministic render clock. 100 ms trace sampling, 12 fps GIF. No live capture, playback, dispatcher latency or user-experience validation.",
            sourceSha256 = before.RootElement.GetProperty("SourceSha256").GetString(),
            beforeCoreSha256 = before.RootElement.GetProperty("coreSha256").GetString(),
            afterCoreSha256 = after.RootElement.GetProperty("coreSha256").GetString(),
            frames, fps = Fps, first, end, differentFrames
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void RenderMotionComparison(string beforePath, string afterPath, string outputPath)
    {
        using var before = JsonDocument.Parse(File.ReadAllText(beforePath));
        using var after = JsonDocument.Parse(File.ReadAllText(afterPath));
        if (before.RootElement.GetProperty("SourceSha256").GetString() != after.RootElement.GetProperty("SourceSha256").GetString())
            throw new InvalidDataException("Comparison requires the same source audio.");
        var a = before.RootElement.GetProperty("behavior").GetProperty("styles")[0].GetProperty("trajectory");
        var b = after.RootElement.GetProperty("behavior").GetProperty("styles")[0].GetProperty("trajectory");
        if (a.GetArrayLength() != b.GetArrayLength()) throw new InvalidDataException("Trajectory lengths differ.");
        var selected = new List<int>();
        foreach (var index in Enumerable.Range(0, a.GetArrayLength())
            .OrderByDescending(i => b[i].GetProperty("weight").GetDouble() - a[i].GetProperty("weight").GetDouble()))
        {
            if (b[index].GetProperty("weight").GetDouble() - a[index].GetProperty("weight").GetDouble() < .3) break;
            if (selected.Any(i => Math.Abs(a[i].GetProperty("seconds").GetDouble() - a[index].GetProperty("seconds").GetDouble()) < 1)) continue;
            selected.Add(index);
            if (selected.Count == 4) break;
        }
        if (selected.Count == 0) throw new InvalidDataException("No movement differences to render.");
        selected.Sort();
        const int width = 1024, height = 660;
        var visual = new DrawingVisual();
        int changedImages = 0;
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            Label(dc, "持续演奏中的动作衔接", 24, 16, 25, Ink);
            Label(dc, "上：0.7.5　下：0.7.6 · 同音频/同采样点/固定拍点 · 生产绘制，非现场截图", 24, 54, 14, Ink);
            for (int col = 0; col < selected.Count; col++)
            {
                int index = selected[col];
                byte[]? prior = null;
                for (int row = 0; row < 2; row++)
                {
                    var data = row == 0 ? a[index] : b[index];
                    if (a[index].GetProperty("seconds").GetDouble() != b[index].GetProperty("seconds").GetDouble()
                        || a[index].GetProperty("action").GetString() != b[index].GetProperty("action").GetString())
                        throw new InvalidDataException("Comparison changed action or time.");
                    var action = Enum.Parse<CompanionAction>(data.GetProperty("action").GetString()!);
                    var pose = new CompanionPose(action, ActionMood(action), data.GetProperty("weight").GetDouble())
                    { Engagement = data.GetProperty("engagement").GetDouble(), HasMusicalEvidence = true };
                    var bitmap = PoseCat(GuitarStyle.Stratocaster, CatSkin.Snow, pose, 1);
                    var pixels = Pixels(bitmap);
                    if (row == 0) prior = pixels;
                    else if (!pixels.AsSpan().SequenceEqual(prior)) changedImages++;
                    int x = 20 + col * 248, y = 92 + row * 278;
                    dc.DrawRectangle(Panel, null, new Rect(x, y, 240, 266));
                    dc.DrawImage(bitmap, new Rect(x + 8, y, 224, 224));
                    Label(dc, $"{data.GetProperty("seconds").GetDouble():F2}s · {Actions[(int)action]}", x + 10, y + 223, 13, Paper);
                    Label(dc, $"动作权重 {pose.Weight:F2}", x + 10, y + 245, 12, Paper);
                }
            }
        }
        if (changedImages != selected.Count) throw new InvalidOperationException("Changed weights did not reach visible pixels.");
        var path = Path.GetFullPath(outputPath);
        if (File.Exists(path)) throw new IOException("Preserve the existing comparison image.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SavePng(RenderVisual(visual, width, height), path);
    }

    private static void RenderStates(string path)
    {
        const int width = 1224, height = 660;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            Label(dc, "原有四种状态 + 新增「一起弹」", 24, 20, 24, Ink);
            Label(dc, "拿起吉他是一个独立动作，原有等待、倾听、兴奋和休息形象保持原样。", 24, 58, 15, Ink);
            for (int row = 0; row < 2; row++)
            {
                var guitar = Guitars[row]; var skin = row == 0 ? CatSkin.Calico : CatSkin.Ginger;
                var y = 102 + row * 272;
                var light = row % 2 == 1;
                for (int mood = 0; mood < 5; mood++)
                {
                    int x = 16 + mood * 240;
                    dc.DrawRectangle(light ? Paper : Panel, null, new Rect(x, y, 232, 250));
                    dc.DrawImage(Cat(guitar, skin, mood == 4 ? CompanionMood.Excited : (CompanionMood)mood, .08, 1, jamming: mood == 4), new Rect(x + 4, y - 3, 224, 224));
                    Label(dc, $"{GuitarName(guitar)} · {Skins[(int)skin]}", x + 14, y + 209, 12, light ? Ink : Paper);
                    Label(dc, Moods[mood], x + 14, y + 229, 12, light ? Ink : Paper);
                }
            }
        }
        SavePng(RenderVisual(visual, width, height), path);
    }

    private static void RenderInstruments(string path)
    {
        const int width = 960, height = 680;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            Label(dc, "两把琴 / 轮廓与配色", 24, 18, 23, Ink);
            Label(dc, "上排只看轮廓，下排为同一像素稿的配色。均为实际持琴角度，10 倍放大。", 24, 56, 14, Ink);
            for (int i = 0; i < Guitars.Length; i++)
            {
                int x = 20 + i * 470;
                dc.DrawRectangle(Paper, null, new Rect(x, 95, 450, 260));
                dc.PushTransform(new TranslateTransform(x, -130));
                dc.PushTransform(new ScaleTransform(10, 10));
                PixelGuitar.DrawInstrumentPreview(dc, Guitars[i], silhouette: true);
                dc.Pop(); dc.Pop();
                Label(dc, GuitarName(Guitars[i]), x + 20, 326, 16, Ink);
                dc.DrawRectangle(Panel, null, new Rect(x, 370, 450, 280));
                dc.PushTransform(new TranslateTransform(x, 155));
                dc.PushTransform(new ScaleTransform(10, 10));
                PixelGuitar.DrawInstrumentPreview(dc, Guitars[i]);
                dc.Pop(); dc.Pop();
                Label(dc, GuitarName(Guitars[i]), x + 20, 610, 16, Paper);
            }
        }
        SavePng(RenderVisual(visual, width, height), path);
    }

    private static void RenderResponsiveness(string path)
    {
        const int width = 1464, height = 696;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            Label(dc, "连续投入 / 同一时刻的声音证据改变动作", 24, 18, 24, Ink);
            Label(dc, "合成输入 · 实际 WPF 像素渲染 · 未采集音频 · 默认动态 0.7 · 同组脉冲与时间完全相同", 24, 56, 15, Ink);
            Label(dc, SystemParameters.ClientAreaAnimation ? "观察身体、琴颈与扫弦手腕停留的长短；强弱并非把动作改成另一套形象。" : "当前系统关闭了动画，所有对照遵从系统设置保持静止。", 24, 81, 14, Ink);
            for (int row = 0; row < 2; row++)
            for (int column = 0; column < 6; column++)
            {
                int group = column / 2;
                double engagement = column % 2 == 0 ? .35 : .9;
                double pulse = row == 0 ? .65 : .4;
                var action = group == 0 ? CompanionAction.Engage : CompanionAction.Jam;
                var guitar = group == 2 ? GuitarStyle.LesPaul : GuitarStyle.Stratocaster;
                int x = 16 + column * 240, y = 123 + row * 272;
                dc.DrawRectangle(Panel, null, new Rect(x, y, 232, 256));
                dc.DrawImage(PoseCat(guitar, CatSkin.Calico, MusicalPose(action, engagement), pulse), new Rect(x + 4, y - 4, CatSize, CatSize));
                Label(dc, group == 0 ? "身体投入" : "一起弹 / " + GuitarName(guitar), x + 14, y + 208, 13, Paper);
                Label(dc, $"投入 {engagement:F2} · 脉冲 {pulse:F2}", x + 14, y + 231, 13, Paper);
            }
            Label(dc, "上排：收拍中段的身体与琴颈；下排：更靠后的扫弦手腕。图片验证可见响应，不代表已验证曲风或情绪识别。", 24, 674, 13, Ink);
        }
        SavePng(RenderVisual(visual, width, height), path);
    }

    private static List<string> RenderMotion(string path, bool directed)
    {
        using var strat = NewCat(GuitarStyle.Stratocaster, CatSkin.Calico);
        using var lesPaul = NewCat(GuitarStyle.LesPaul, CatSkin.Ginger);
        PixelCat[] cats = [strat, lesPaul];
        var director = new CompanionDirector(24);
        // Exercise the production Jam cooldown without accelerating it or making the preview unnecessarily long.
        if (directed)
            for (int frame = 0; frame < 42 * Fps; frame++)
                director.Step(true, true, frame % 6 == 0, 1d / Fps, false, ExpressionStyle.Curious);
        int frames = (directed ? 16 : 12) * Fps;
        var encoder = new GifBitmapEncoder();
        var transitions = new List<string>();
        CompanionAction? previous = null;
        for (int frame = 0; frame < frames; frame++)
        {
            double time = frame / (double)Fps;
            bool active = time < 9;
            CompanionPose pose = directed
                ? director.Step(active, active, active && frame % 6 == 0, 1d / Fps, false, ExpressionStyle.Curious)
                : ShowcasePose(time);
            double pulse = (directed ? active : pose.Action is CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam)
                ? Math.Exp(-(time % .5) / .16) : 0;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Panel, null, new Rect(0, 0, 448, 300));
                Label(dc, directed ? (active ? "模拟输入 / 已持续弹奏 42 秒" : "模拟输入 / 安静收拍") : "动作预览 / " + Actions[(int)pose.Action], 18, 12, 15, Paper);
                for (int i = 0; i < cats.Length; i++)
                {
                    var cat = cats[i]; cat.PreviewSeconds = time; cat.PreviewPulse = pulse;
                    cat.SetPose(pose); cat.UpdateLayout();
                    dc.DrawImage(RenderVisual(cat, CatSize, CatSize), new Rect(i * CatSize, 35, CatSize, CatSize));
                    Label(dc, GuitarName(Guitars[i]), i * CatSize + 22, 257, 15, Paper);
                    Label(dc, Actions[(int)pose.Action], i * CatSize + 22, 279, 12, Paper);
                }
            }
            encoder.Frames.Add(BitmapFrame.Create(RenderVisual(visual, 448, 300)));
            if (previous != pose.Action) transitions.Add($"{time.ToString("F2", CultureInfo.InvariantCulture)}s {pose.Action}");
            previous = pose.Action;
        }
        SaveTimedAnimation(encoder, path);
        return transitions;
    }

    private static CompanionPose ShowcasePose(double time) => time switch
    {
        < 1.5 => new(CompanionAction.Idle, CompanionMood.Waiting, 0),
        < 2.4 => new(CompanionAction.Notice, CompanionMood.Listening, .8),
        < 4 => new(CompanionAction.Listen, CompanionMood.Listening, 0),
        < 7 => new(CompanionAction.Groove, CompanionMood.Listening, 1),
        < 9 => new(CompanionAction.Jam, CompanionMood.Excited, 1),
        < 10.4 => new(CompanionAction.Settle, CompanionMood.Listening, Math.Clamp(1 - (time - 9) / 1.4, 0, 1)),
        _ => new(CompanionAction.Rest, CompanionMood.Resting, 0)
    };

    private sealed record Check(string Name, bool Passed);
    private static List<Check> Validate()
    {
        var checks = new List<Check>();
        foreach (var guitar in Guitars)
        foreach (var skin in Enum.GetValues<CatSkin>())
        {
            foreach (var action in Enum.GetValues<CompanionAction>())
            foreach (bool reduced in new[] { true, false })
            {
                using var cat = NewCat(guitar, skin);
                // Seed a moving pose before disabling motion, so Settle cannot pass only because its cache was empty.
                cat.PreviewSeconds = .01; cat.PreviewPulse = 1;
                cat.SetPose(new(CompanionAction.Jam, CompanionMood.Excited, 1));
                _ = RenderVisual(cat, CatSize, CatSize);
                cat.ReducedMotion = reduced; cat.MotionAmount = reduced ? 1 : 0;
                var pose = new CompanionPose(action, ActionMood(action), 1);
                cat.SetPose(pose); cat.PreviewSeconds = 0; cat.PreviewPulse = 1;
                var first = Pixels(RenderVisual(cat, CatSize, CatSize));
                bool equal = true;
                foreach (double time in new[] { .37, 1.2, 6.98 })
                {
                    cat.SetPose(pose with { Weight = .15 }); cat.PreviewSeconds = time; cat.PreviewPulse = 0;
                    equal &= first.AsSpan().SequenceEqual(Pixels(RenderVisual(cat, CatSize, CatSize)));
                }
                checks.Add(new($"{guitar}/{skin}/{action}/{(reduced ? "reduced motion" : "zero motion")}: identical across time, pulse and weight", equal));
            }
            foreach (var mood in Enum.GetValues<CompanionMood>())
            {
                bool clear = true;
                foreach (int size in new[] { 224, 168 })
                foreach (double time in new[] { 0d, .12, .35, .51, 1.2, 6.98 })
                {
                    var bitmap = Cat(guitar, skin, mood, time, Math.Exp(-(time % .5) / .16), size: size);
                    clear &= WithinBounds(bitmap);
                }
                checks.Add(new($"{guitar}/{skin}/{mood}: visible content and >=2px transparent margin at 224/168 DIP across 6 phases", clear));
                var hiDpi = Cat(guitar, skin, mood, .12, 1, dpi: 144);
                checks.Add(new($"{guitar}/{skin}/{mood}: 144 DPI raster remains inside bounds", WithinBounds(hiDpi) && hiDpi.PixelWidth == 336));
            }
            bool jamBounds = true;
            foreach (int size in new[] { 224, 168 })
            foreach (double time in new[] { 0d, .12, .35, .51, 1.2, 6.98 })
                jamBounds &= WithinBounds(Cat(guitar, skin, CompanionMood.Excited, time, Math.Exp(-(time % .5) / .16), size: size, jamming: true));
            checks.Add(new($"{guitar}/{skin}/Jam: visible content and >=2px transparent margin at 224/168 DIP across 6 phases", jamBounds));
            checks.Add(new($"{guitar}/{skin}/Jam: 144 DPI raster remains inside bounds", WithinBounds(Cat(guitar, skin, CompanionMood.Excited, .12, 1, dpi: 144, jamming: true))));
            bool pixelGrid = true;
            foreach (int size in new[] { 224, 168 })
            foreach (double pulse in new[] { 0d, 1d })
                pixelGrid &= PixelGridAligned(Cat(guitar, skin, CompanionMood.Excited, .12, pulse, size: size, jamming: true));
            checks.Add(new($"{guitar}/{skin}/Jam: all visible edges use the cat's integer-pixel grid at 224/168 DIP", pixelGrid));
            if (SystemParameters.ClientAreaAnimation)
            {
                var first = Pixels(Cat(guitar, skin, CompanionMood.Excited, .05, 1, jamming: true));
                var second = Pixels(Cat(guitar, skin, CompanionMood.Excited, .35, 0, jamming: true));
                checks.Add(new($"{guitar}/{skin}: Jam visibly responds to pulse", !first.AsSpan().SequenceEqual(second)));
            }
        }
        foreach (var skin in Enum.GetValues<CatSkin>())
        foreach (var mood in Enum.GetValues<CompanionMood>())
        {
            var strat = Pixels(Cat(GuitarStyle.Stratocaster, skin, mood, 0, 1));
            var lesPaul = Pixels(Cat(GuitarStyle.LesPaul, skin, mood, 0, 1));
            var original = Pixels(Cat(GuitarStyle.None, skin, mood, 0, 1));
            checks.Add(new($"{skin}/{mood}: original state identical with None, Stratocaster and Les Paul", strat.AsSpan().SequenceEqual(lesPaul) && strat.AsSpan().SequenceEqual(original)));
        }
        foreach (var skin in Enum.GetValues<CatSkin>())
        {
            var strat = Pixels(Cat(GuitarStyle.Stratocaster, skin, CompanionMood.Excited, 0, 1, jamming: true));
            var lesPaul = Pixels(Cat(GuitarStyle.LesPaul, skin, CompanionMood.Excited, 0, 1, jamming: true));
            var excited = Pixels(Cat(GuitarStyle.Stratocaster, skin, CompanionMood.Excited, 0, 1));
            checks.Add(new($"{skin}/Jam: guitar choices produce different pixels", !strat.AsSpan().SequenceEqual(lesPaul)));
            checks.Add(new($"{skin}/Jam: additional state differs from existing Excited", !strat.AsSpan().SequenceEqual(excited) && !lesPaul.AsSpan().SequenceEqual(excited)));
        }
        foreach (var guitar in Guitars)
        for (int a = 0; a < 4; a++)
        for (int b = a + 1; b < 4; b++)
            checks.Add(new($"{guitar}: {Skins[a]} and {Skins[b]} remain distinct",
                !Pixels(Cat(guitar, (CatSkin)a, CompanionMood.Excited, 0, 1, jamming: true)).AsSpan()
                    .SequenceEqual(Pixels(Cat(guitar, (CatSkin)b, CompanionMood.Excited, 0, 1, jamming: true)))));
        if (SystemParameters.ClientAreaAnimation)
        {
            // Simulate the production delay budget: an attack detected by the audio
            // thread and rendered one 50 ms behavior tick later must still move pixels
            // at the default motion amount, and quiet frames must not inherit it.
            using var onsetCat = NewCat(GuitarStyle.None, CatSkin.Snow);
            onsetCat.MotionAmount = .7; onsetCat.ReducedMotion = false;
            onsetCat.SetPose(new(CompanionAction.Notice, CompanionMood.Listening, .1));
            onsetCat.PreviewSeconds = 0; onsetCat.PreviewOnsetAge = .05;
            var onsetFrame = Pixels(RenderVisual(onsetCat, CatSize, CatSize));
            onsetCat.PreviewOnsetAge = .5;
            // Preview fields do not run the live animation timer in this offscreen check.
            onsetCat.InvalidateVisual();
            var decayFrame = Pixels(RenderVisual(onsetCat, CatSize, CatSize));
            onsetCat.PreviewOnsetAge = null;
            onsetCat.InvalidateVisual();
            var quietFrame = Pixels(RenderVisual(onsetCat, CatSize, CatSize));
            checks.Add(new("Onset visible after one 50 ms dispatch tick", !onsetFrame.AsSpan().SequenceEqual(quietFrame)));
            checks.Add(new("Onset returns to baseline after its brief envelope", decayFrame.AsSpan().SequenceEqual(quietFrame)));
        }
        ValidateMusicalResponsiveness(checks);
        return checks;
    }

    private static void ValidateMusicalResponsiveness(List<Check> checks)
    {
        foreach (var guitar in Guitars)
        foreach (var skin in Enum.GetValues<CatSkin>())
        foreach (int size in new[] { 224, 168 })
        {
            string name = $"{guitar}/{skin}/{size} DIP";
            if (SystemParameters.ClientAreaAnimation)
            {
                foreach (var action in new[] { CompanionAction.Groove, CompanionAction.Engage })
                {
                    var weak = Pixels(PoseCat(guitar, skin, MusicalPose(action, .35), .65, size));
                    var strong = Pixels(PoseCat(guitar, skin, MusicalPose(action, .9), .65, size));
                    checks.Add(new($"{name}/{action}: musical engagement changes body pixels at default motion", !weak.AsSpan().SequenceEqual(strong)));
                }
                var lightWrist = Pixels(PoseCat(guitar, skin, MusicalPose(CompanionAction.Jam, .35), .4, size));
                var strongWrist = Pixels(PoseCat(guitar, skin, MusicalPose(CompanionAction.Jam, .9), .4, size));
                checks.Add(new($"{name}/Jam: stronger evidence sustains the strumming wrist after the body tap has ended", !lightWrist.AsSpan().SequenceEqual(strongWrist)));
                var lightNeck = PoseCat(guitar, skin, MusicalPose(CompanionAction.Jam, .35), .65, size);
                var strongNeck = PoseCat(guitar, skin, MusicalPose(CompanionAction.Jam, .9), .65, size);
                int softHeight = NeckHeightRelativeToStrap(lightNeck), strongHeight = NeckHeightRelativeToStrap(strongNeck);
                checks.Add(new($"{name}/Jam: stronger evidence raises the neck relative to the body anchor, rather than only moving the whole cat",
                    softHeight != int.MinValue && strongHeight != int.MinValue && strongHeight == softHeight - Math.Min(size / 40, size / 42)));
            }
            foreach (var action in new[] { CompanionAction.Groove, CompanionAction.Engage, CompanionAction.Jam })
            {
                var weak = MusicalPose(action, .35) with { HasMusicalEvidence = false };
                var strong = weak with { Engagement = .9 };
                bool legacySame = true;
                foreach (double pulse in new[] { .4, .65, 1 })
                    legacySame &= Pixels(PoseCat(guitar, skin, weak, pulse, size)).AsSpan()
                        .SequenceEqual(Pixels(PoseCat(guitar, skin, strong, pulse, size)));
                checks.Add(new($"{name}/{action}: no musical evidence preserves the legacy preview regardless of engagement", legacySame));
            }
            bool inside = true, aligned = true;
            foreach (double engagement in new[] { .35, .9 })
            foreach (double pulse in new[] { 0d, .4, .65, 1 })
            {
                var bitmap = PoseCat(guitar, skin, MusicalPose(CompanionAction.Jam, engagement), pulse, size);
                inside &= WithinBounds(bitmap); aligned &= PixelGridAligned(bitmap);
            }
            checks.Add(new($"{name}/musical Jam: default motion stays in bounds and on the integer pixel grid", inside && aligned));
        }

        foreach (var action in new[] { CompanionAction.Groove, CompanionAction.Engage, CompanionAction.Jam, CompanionAction.Settle })
        foreach (bool reduced in new[] { true, false })
        {
            using var cat = NewCat(GuitarStyle.Stratocaster, CatSkin.Calico);
            cat.MotionAmount = .7; cat.PreviewSeconds = 0; cat.PreviewPulse = 1;
            cat.SetPose(MusicalPose(CompanionAction.Jam, .9));
            _ = RenderVisual(cat, CatSize, CatSize);
            cat.ReducedMotion = reduced; cat.MotionAmount = reduced ? .7 : 0;
            cat.SetPose(MusicalPose(action, .35));
            var first = Pixels(RenderVisual(cat, CatSize, CatSize));
            bool equal = true;
            foreach (double time in new[] { .37, 1.2, 6.98 })
            {
                cat.PreviewSeconds = time; cat.PreviewPulse = 0;
                cat.SetPose(MusicalPose(action, .9) with { Weight = .15 });
                equal &= first.AsSpan().SequenceEqual(Pixels(RenderVisual(cat, CatSize, CatSize)));
            }
            checks.Add(new($"musical {action}/{(reduced ? "reduced" : "zero")} motion: engagement and a cached moving pose cannot bypass motion preferences", equal));
        }

        var director = new CompanionDirector(24);
        var music = new ListeningFeatures(ToneColor.Bright, PlayingExpression.Animated, 1);
        CompanionPose moving = default;
        for (int frame = 0; frame < 240; frame++)
            moving = director.Step(true, true, frame % 5 == 0, .05, false, ExpressionStyle.Curious, .9, music);
        var quiet = director.Step(true, true, true, .05, true, ExpressionStyle.Curious, .9, music);
        using (var quietCat = NewCat(GuitarStyle.Stratocaster, CatSkin.Calico))
        {
            quietCat.MotionAmount = .7; quietCat.PreviewSeconds = 0; quietCat.PreviewPulse = 1;
            quietCat.SetPose(moving); _ = RenderVisual(quietCat, CatSize, CatSize);
            quietCat.ReducedMotion = true; quietCat.SetPose(quiet);
            var first = Pixels(RenderVisual(quietCat, CatSize, CatSize));
            quietCat.PreviewSeconds = 6.98; quietCat.PreviewPulse = 0; quietCat.InvalidateVisual();
            var second = Pixels(RenderVisual(quietCat, CatSize, CatSize));
            var baseline = Pixels(PoseCat(GuitarStyle.Stratocaster, CatSkin.Calico, new(CompanionAction.Listen, CompanionMood.Listening, 0), 0, reduced: true));
            checks.Add(new("quiet mode immediately overrides actual musical engagement, removes the guitar and holds the listening baseline",
                moving.HasMusicalEvidence && quiet.Action == CompanionAction.Listen && !quietCat.IsJamming && first.AsSpan().SequenceEqual(second) && first.AsSpan().SequenceEqual(baseline)));
        }

        if (!SystemParameters.ClientAreaAnimation) return;
        foreach (var action in new[] { CompanionAction.Engage, CompanionAction.Jam })
        {
            using var cat = NewCat(GuitarStyle.LesPaul, CatSkin.Ginger);
            cat.MotionAmount = .7; cat.PreviewSeconds = 0; cat.PreviewPulse = null;
            var weak = MusicalPose(action, .35);
            var strong = MusicalPose(action, .9);
            byte[] At(CompanionPose pose, double age)
            {
                cat.PreviewOnsetAge = age; cat.SetPose(pose);
                return Pixels(RenderVisual(cat, CatSize, CatSize));
            }
            var baseline = At(strong, 10);
            var early = At(strong, .05);
            var middle = At(strong, .25);
            var softMiddle = At(weak, .25);
            bool settles = baseline.AsSpan().SequenceEqual(At(strong, .6)) && baseline.AsSpan().SequenceEqual(At(weak, .6));
            checks.Add(new($"musical {action}: real onset age remains visible after a 50 ms dispatch and distinguishes engagement at 250 ms",
                !early.AsSpan().SequenceEqual(baseline) && !middle.AsSpan().SequenceEqual(softMiddle)));
            checks.Add(new($"musical {action}: the real onset envelope returns body and guitar to baseline without a continuing metronome", settles));
        }
    }

    private static CompanionPose MusicalPose(CompanionAction action, double engagement) =>
        new(action, ActionMood(action), 1) { HasMusicalEvidence = true, Engagement = engagement };

    private static int NeckHeightRelativeToStrap(BitmapSource bitmap)
    {
        var data = Pixels(bitmap);
        int neck = bitmap.PixelHeight, strap = bitmap.PixelHeight;
        for (int y = 0; y < bitmap.PixelHeight; y++)
        for (int x = 0; x < bitmap.PixelWidth; x++)
        {
            int offset = (y * bitmap.PixelWidth + x) * 4;
            if (data[offset + 3] != 255) continue;
            // The renderer's solid shoulder strap (#51404A) anchors the body; the fingerboard
            // (#594237) anchors the neck. Comparing their relative height removes
            // the whole-body sway and bob from this independently visible check. The
            // scarf's upper row is covered by the nodding face, so it is not a stable anchor.
            if (data[offset] == 0x4A && data[offset + 1] == 0x40 && data[offset + 2] == 0x51)
                strap = Math.Min(strap, y);
            if (x >= bitmap.PixelWidth / 2 && data[offset] == 0x37 && data[offset + 1] == 0x42 && data[offset + 2] == 0x59)
                neck = Math.Min(neck, y);
        }
        return neck == bitmap.PixelHeight || strap == bitmap.PixelHeight ? int.MinValue : neck - strap;
    }

    private static RenderTargetBitmap PoseCat(GuitarStyle guitar, CatSkin skin, CompanionPose pose, double pulse, int size = CatSize, bool reduced = false)
    {
        using var cat = NewCat(guitar, skin, size);
        cat.MotionAmount = .7; cat.ReducedMotion = reduced; cat.PreviewSeconds = 0; cat.PreviewPulse = pulse;
        cat.SetPose(pose);
        return RenderVisual(cat, size, size);
    }

    private static CompanionMood ActionMood(CompanionAction action) => action switch
    {
        CompanionAction.Rest => CompanionMood.Resting,
        CompanionAction.Engage or CompanionAction.Jam => CompanionMood.Excited,
        CompanionAction.Idle or CompanionAction.Groom => CompanionMood.Waiting,
        _ => CompanionMood.Listening
    };

    private static bool WithinBounds(BitmapSource bitmap)
    {
        var data = Pixels(bitmap);
        int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
        int minX = width, minY = height, maxX = -1, maxY = -1, visible = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (data[(y * width + x) * 4 + 3] == 0) continue;
            visible++; minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        return visible > width * height * .08 && minX >= 2 && minY >= 2 && maxX < width - 2 && maxY < height - 2;
    }

    private static bool PixelGridAligned(BitmapSource bitmap)
    {
        var data = Pixels(bitmap);
        int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
        int pixel = Math.Min(width / 40, height / 42);
        int left = (width - 40 * pixel) / 2, top = (height - 42 * pixel) / 2;
        for (int row = 0; row < 42; row++)
        for (int column = 0; column < 40; column++)
        {
            int first = ((top + row * pixel) * width + left + column * pixel) * 4;
            for (int y = 0; y < pixel; y++)
            for (int x = 0; x < pixel; x++)
            {
                int current = ((top + row * pixel + y) * width + left + column * pixel + x) * 4;
                for (int channel = 0; channel < 4; channel++)
                    if (data[current + channel] != data[first + channel]) return false;
            }
        }
        return true;
    }

    private static PixelCat NewCat(GuitarStyle guitar, CatSkin skin, int size = CatSize)
    {
        var cat = new PixelCat { Width = size, Height = size, Guitar = guitar, Skin = skin, MotionAmount = 1 };
        cat.Measure(new Size(size, size)); cat.Arrange(new Rect(0, 0, size, size));
        return cat;
    }

    private static RenderTargetBitmap Cat(GuitarStyle guitar, CatSkin skin, CompanionMood mood, double time, double pulse, int size = CatSize, double dpi = 96, bool jamming = false)
    {
        using var cat = NewCat(guitar, skin, size);
        cat.Mood = mood; cat.IsJamming = jamming; cat.PreviewSeconds = time; cat.PreviewPulse = pulse; cat.UpdateLayout();
        return RenderVisual(cat, size, size, dpi);
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    private static RenderTargetBitmap RenderVisual(Visual visual, int width, int height, double dpi = 96)
    {
        if (visual is UIElement element) element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width * dpi / 96), (int)(height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static void SaveTimedAnimation(GifBitmapEncoder encoder, string path, bool clearFrames = false)
    {
        using var encoded = new MemoryStream(); encoder.Save(encoded);
        var bytes = encoded.ToArray();
        int headerLength = 13 + ((bytes[10] & 0x80) != 0 ? 3 * (1 << ((bytes[10] & 7) + 1)) : 0);
        int cursor = headerLength, controls = 0, images = 0;
        // WPF drops timing metadata while encoding. Preserve image blocks, then add verified GIF delays.
        while (cursor < bytes.Length)
        {
            byte marker = bytes[cursor++];
            if (marker == 0x3b) break;
            if (marker == 0x21)
            {
                byte label = bytes[cursor++];
                if (label == 0xf9)
                {
                    if (bytes[cursor] != 4) throw new InvalidDataException("Invalid GIF graphic control.");
                    int delay = FrameDelay(controls++);
                    bytes[cursor + 2] = (byte)delay; bytes[cursor + 3] = 0;
                    // Full-frame replay must not accumulate earlier cat positions
                    // where WPF's GIF palette marks a pixel transparent.
                    if (clearFrames) bytes[cursor + 1] = (byte)((bytes[cursor + 1] & ~0x1c) | 0x08);
                }
            }
            else if (marker == 0x2c)
            {
                byte flags = bytes[cursor + 8]; cursor += 9;
                if ((flags & 0x80) != 0) cursor += 3 * (1 << ((flags & 7) + 1));
                cursor++; images++;
            }
            else throw new InvalidDataException("Unexpected GIF block.");
            int blockSize;
            do { blockSize = bytes[cursor++]; cursor += blockSize; } while (blockSize != 0);
        }
        if (controls != encoder.Frames.Count || images != controls) throw new InvalidDataException("GIF timing is incomplete.");
        using var timed = new MemoryStream();
        timed.Write(bytes, 0, headerLength);
        timed.Write(new byte[] { 0x21, 0xff, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 });
        timed.Write(bytes, headerLength, bytes.Length - headerLength); timed.Position = 0;
        var decoder = new GifBitmapDecoder(timed, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count != controls) throw new InvalidDataException("GIF lost frames.");
        for (int frame = 0; frame < controls; frame++)
            if (decoder.Frames[frame].Metadata is not BitmapMetadata metadata ||
                !Equals(metadata.GetQuery("/grctlext/Delay"), (ushort)FrameDelay(frame)))
                throw new InvalidDataException("GIF frame delay did not survive decoding.");
        File.WriteAllBytes(path, timed.ToArray());
    }

    private static int FrameDelay(int frame) => (int)Math.Round((frame + 1) * 100d / Fps) - (int)Math.Round(frame * 100d / Fps);
    private static string GuitarName(GuitarStyle guitar) => guitar == GuitarStyle.LesPaul ? "Les Paul" : "Stratocaster";
    private static Brush Brush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; brush.Freeze(); return brush;
    }
    private static void Label(DrawingContext dc, string text, double x, double y, double size, Brush brush) =>
        dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), size, brush, 1), new Point(x, y));
}
