using System.Text;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Devices;
using GuitarSoul.Core.Companion;

if (args is ["--acoustic-report"])
{
    Console.WriteLine("Synthetic acoustic contrasts; no device capture or playback.");
    Console.WriteLine(AcousticContrastChecks.DiagnosticReport());
    return 0;
}

if (args is ["--arpeggio-report"])
{
    Console.WriteLine("Synthetic broken chords through the current detector; no device capture.");
    Console.WriteLine(ArpeggioProbe.Report());
    return 0;
}

if (args is ["--companion-report"])
{
    Console.WriteLine("Synthetic playing patterns through the current companion director; no device capture.");
    Console.WriteLine(CompanionRhythmProbe.Report());
    return 0;
}

if (args is ["--harmony-report"])
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(HarmonyChecks.PcmReport(),
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("Chord recording survives capture clock rewind and bounds long sessions explicitly", ChordReliabilityChecks.RewindAndRetention),
    ("Chord checkpoints round-trip atomically and preserve damaged files", ChordReliabilityChecks.DraftRoundTripAndCorruption),
    ("Chord loops require two complete passes and follow cyclic positions", ChordLoopChecks.ConfirmationAndFollowing),
    ("Chord loops handle duplicate roots and reject flicker and deviations", ChordLoopChecks.RepeatedChordsAndRejection),
    ("Chord idle reset follows actual sound, can be disabled and preserves one restored draft", ChordLoopChecks.IdleAndRestore),
    ("Game rejects silent, loopback and mixed player contributions", GameSliceChecks.NoInputNeverCredits),
    ("Game sustains participation through complete high windows with preview and release", GameSliceChecks.SustainsAndTiming),
    ("Game tolerates short gaps, exits long stops and deduplicates rejoining", GameSliceChecks.DropoutGraceAndRejoin),
    ("Game pause, seek, loop, stop and song change clear transient state", GameSliceChecks.TransportResets),
    ("Game quiet, reduced motion and input failure priorities are explicit", GameSliceChecks.PriorityAndFreshness),
    ("Player tracker rejects stale, invalid, clipped and previous-epoch input", GameSliceChecks.TrackerContract),
    ("Track arc is repeatable and relative, with zero highlights allowed", GameSliceChecks.ArcRepeatAndContrast),
    ("Game shadow state replay is deterministic", GameSliceChecks.ReplayDeterminism),
    ("Stage exploration separates listening and playing, persists and permits correction", StageCollectionChecks.SourcesAndPersistence),
    ("Stage collection preserves data on invalid edits, corruption and stale writes", StageCollectionChecks.FailuresPreserveCollection),
    ("Achievements reject stale, silent, uncertain and excluded input", StageAchievementChecks.EvidenceBoundaries),
    ("Achievements distinguish stable musical moments from feature flicker", StageAchievementChecks.DistinctMoments),
    ("Expanded artists require distinct musical events without inventing rests or dynamics", StageAchievementChecks.ExpandedArtistBoundaries),
    ("Synthetic PCM reaches automatic achievements through the real rhythm tracker", StageAchievementChecks.SyntheticAudioPipeline),
    ("Guitar evidence confirms a changing picked harmonic control and rejects non-guitar controls", GuitarEvidenceGateChecks.ConservativeEvidence),
    ("Guitar evidence resets on silence, no data, stale callbacks, clipping, calibration and review", GuitarEvidenceGateChecks.InvalidAndStaleEvidence),
    ("Unknown guitar evidence blocks only Jam and guitar-specific speech", GuitarEvidenceGateChecks.OnlyGuitarSpecificOutputsAreBlocked),
    ("Unknown guitar evidence blocks achievements while confirmed synthetic evidence survives", GuitarEvidenceGateChecks.AchievementAdmission),
    ("Earned achievements persist once and protect failed or concurrent writes", StageAchievementChecks.PersistenceAndFailures),
    ("Alternating bass and treble accents retain the full marked beat", MusicPulseChecks.AlternatingBassAndTrebleAccentsKeepTheFullBeat),
    ("Phrase fallback preserves ambiguity when both thirds are present", HarmonyChecks.PhraseRejectsConflictingThirds),
    ("Ongoing musical movement remains continuous across groove, engagement and guitar changes", ResponsivenessChecks.MotionContinuity),
    ("Stable triads distinguish roots and quality while uncertain sound stays unknown", HarmonyChecks.TriadsAndUnknown),
    ("Power chords keep a root without claiming a third", ProgressionChecks.PowerChordsKeepTheRootOnly),
    ("Triads keep major and minor when recorded", ProgressionChecks.TriadsKeepQuality),
    ("Degrees follow the chosen key and change with it", ProgressionChecks.DegreesFollowTheKey),
    ("Chord timeline keeps order, merges repeats and drops blips", ProgressionChecks.TimelineKeepsOrderAndDropsBlips),
    ("A played 1-5-6-4 power-chord phrase records as 1-5-6-4", ProgressionChecks.RecordedProgressionRunsOnRealPcm),
    ("A broken chord still names its chord and a scale line does not", ProgressionChecks.BrokenChordsStillNameTheChord),
    ("A key only relabels a recording that already exists", ProgressionChecks.KeyIsOnlyALabelOnTopOfTheRecording),
    ("The key is inferred from what was played, or not claimed at all", ProgressionChecks.KeyIsInferredFromWhatWasPlayed),
    ("Harmonic changes require context, survive chunks and clear silence", HarmonyChecks.ChangesAndChunking),
    ("Harmonic turns shape attention and joined phrase duration without overriding quiet", HarmonyChecks.MusicalContextChangesBehavior),
    ("Plucked PCM harmonic context changes actual companion behavior", HarmonyChecks.PcmToBehavior),
    ("Beat follows bass tactus over eighth-note subdivisions and clears silence", MusicPulseChecks.TactusAndSilence),
    ("Section contrast needs prior context and held tones do not create guitar cues", MusicPulseChecks.SectionContrast),
    ("New section highlights join promptly and yield to quiet", MusicPulseChecks.HighlightsJoinAndYield),
    ("Beat and section evidence survive arbitrary callback chunks", MusicPulseChecks.CallbackIndependence),
    ("Same clock responds differently to dense music, held notes and silence", ResponsivenessChecks.SameClockDifferentSound),
    ("Phrase changes interrupt current musical motion", ResponsivenessChecks.PhraseChangesInterruptTheCurrentPose),
    ("Relative intensity changes continuous engagement with personality differences", ResponsivenessChecks.RelativeIntensityAndPersonality),
    ("Repeated joined phrases need fresh musical evidence", ResponsivenessChecks.JoinedPhrasesNeedFreshEvidence),
    ("Quiet, silence and lost features release musical engagement", ResponsivenessChecks.QuietAndLostEvidence),
    ("Musical responses replay from evidence and reset", ResponsivenessChecks.MusicalReplay),
    ("Compressed repeated notes remain audible as articulation", AcousticContrastChecks.CompressedSameNoteRepeats),
    ("Adjacent notes retain articulation", AcousticContrastChecks.AdjacentNotes),
    ("Short hits over continuous backing remain audible as articulation", AcousticContrastChecks.BackingWithTransientHits),
    ("Dense articulation remains audible across rates and input levels", AcousticContrastChecks.HighDensityArticulation),
    ("True held tones do not manufacture articulation", AcousticContrastChecks.HeldTonesStayStill),
    ("Acoustic contrast responses are independent of gain and callback chunks", AcousticContrastChecks.ChunkAndGainIndependence),
    ("Listening distinguishes tone colour independently of gain and sample rate", ListeningFeatureChecks.ToneAndGain),
    ("Listening distinguishes sustained, flowing and animated articulation", ListeningFeatureChecks.Articulation),
    ("Listening gates noise, releases silence and holds brief changes", ListeningFeatureChecks.GateReleaseAndHold),
    ("Relative phrase dynamics require repeated articulation", ListeningFeatureChecks.RelativeDynamics),
    ("Listening observations are independent of callback chunking", ListeningFeatureChecks.ChunkIndependence),
    ("Personality changes gesture timing for the same input", PersonalityChecks.DirectorStyles),
    ("Tone changes attract attention while sustained notes settle motion", PersonalityChecks.DirectorToneAndSustain),
    ("Personality voice obeys evidence, silence, quiet and bounded display", PersonalityChecks.VoiceStylesAndBoundaries),
    ("Personality voice suppresses repetition and resets deterministically", PersonalityChecks.VoiceChangesAndReset),
    ("Short voice gaps preserve evidence while long stops and quiet clear progress", PersonalityChecks.VoicePhraseGaps),
    ("Voice confirms only the stable cues its words actually describe", PersonalityChecks.VoiceStableArticulation),
    ("Local-language prompt contains bounded facts without audio", CatLanguageChecks.PromptContainsFactsWithoutAudio),
    ("Local-language output rejects unsupported claims and reasoning", CatLanguageChecks.OutputGuardRejectsUnsupportedClaims),
    ("Local-language requests require reliable acoustic evidence", CatLanguageChecks.InvalidEvidenceNeverBuildsARequest),
    ("Local-language server starts on loopback and only returns guarded sentences", LocalLanguageChecks.GeneratesOnlyThroughTheOutputGuard),
    ("Local-language refusals fall back without killing a healthy server", LocalLanguageChecks.RefusedSentenceFallsBackWithoutKillingTheServer),
    ("A dead local server stops advertising itself as ready", LocalLanguageChecks.DeadServerStopsClaimingReady),
    ("Local-language startup timeout reports failure instead of readiness", LocalLanguageChecks.StartupTimeoutIsReportedAsFailure),
    ("An idle local server is released and starts again on demand", LocalLanguageChecks.IdleServerIsReleasedAndRestarted),
    ("Audio-model observations keep unknown and reject claims outside the strict schema", AudioSemanticObservationChecks.StrictSchemaKeepsUnknownAndRejectsClaimsOutsideTheContract),
    ("Real short plucks reach both personalities without losing phrase progress", ListeningPipelineChecks.ShortPlucksReachBothPersonalities),
    ("Real changing compressed notes reach distinct words and gesture timing", ListeningPipelineChecks.ChangingNotesReachVoiceAndDistinctBehavior),
    ("Sample-to-response pipeline clears silence and never jams on a held note", ListeningPipelineChecks.SilenceClearsAndSustainNeverJams),
    ("Compressed tone changes remain detectable across gain, sample rate and chunks", AudienceChecks.CompressedTone),
    ("Free rhythm gets an early expressive response and obeys quiet", AudienceChecks.ExpressiveResponse),
    ("Audience selects bounded phrases with opt-in, inhibition and cooldown", AudienceChecks.Selection),
    ("Audience selection resets on stale callbacks and caps long phrases", AudienceChecks.SelectionBoundaries),
    ("Synthetic compressed playing reaches expression and audience selection", AudienceChecks.Pipeline),
    ("Jam needs sustained rhythm and stays occasional", JamChecks.AdmissionAndCooldown),
    ("Jam yields to silence and quiet then restores listening", JamChecks.Interruption),
    ("Memory preserves names, provenance and links across reopen and rename", MemoryChecks.Persistence),
    ("Deleting a memory removes links and keeps audio", MemoryChecks.Delete),
    ("Cat-selected Riff keeps provenance and its audio attachment across reopen", MemoryChecks.CatRiffPersistence),
    ("Cat-selected Riff uses rollback-compatible SavedRiff storage", MemoryChecks.CatRiffRollbackCompatibility),
    ("Damaged and future memory formats are preserved", MemoryChecks.InvalidFiles),
    ("Memory rejects invalid user data without touching saved state", MemoryChecks.InvalidEdits),
    ("Stale memory writers cannot overwrite concurrent or damaged data", MemoryChecks.ConcurrentWrites),
    ("Invitations wait for quiet, require opt-in and consume once", MemoryChecks.Invitation),
    ("Invitations yield to quiet, playback, hidden pets and new activity", MemoryChecks.Interruption),
    ("New memories wait for a later session and invitations never confirm songs", MemoryChecks.Evidence),
    ("Director gives onset a complete notice and tolerates short pauses", CompanionDirectorChecks.NoticeAndPause),
    ("Director bounds engagement and leaves listening gaps", CompanionDirectorChecks.Cooldowns),
    ("Director does not reward sustained volume or manufacture rhythm", CompanionDirectorChecks.Sustain),
    ("Director settles into rest without applauding a stop", CompanionDirectorChecks.Silence),
    ("Quiet immediately overrides an in-flight gesture", CompanionDirectorChecks.Quiet),
    ("Director replay and reset are deterministic", CompanionDirectorChecks.Replay),
    ("Expression styles change attention and settling duration", CompanionDirectorChecks.Styles),
    ("Director rejects invalid time and bounds resume jumps", CompanionDirectorChecks.Timing),
    ("Equivalent low/high gain rhythms choose identical behavior", CompanionDirectorChecks.Gain),
    ("Rhythm follows 60 to 220 attacks/min across input levels", () =>
    {
        foreach (var bpm in new[] { 60d, 90, 120, 180, 220 })
        foreach (var gain in new[] { .025, .3 })
        {
            var tracker = new RhythmTracker(8000);
            var frame = tracker.Process(PulseTrain(8000, bpm, 10, gain));
            True(frame.OnsetCount >= 9);
            True(frame.TempoBpm.HasValue);
            Near(bpm, frame.TempoBpm!.Value, 3);
            True(frame.Confidence > .8);
        }
    }),
    ("Rhythm is independent of callback boundaries at 44.1 kHz", () =>
    {
        var samples = PulseTrain(44100, 120, 9, .2);
        var whole = new RhythmTracker(44100).Process(samples);
        var tracker = new RhythmTracker(44100);
        RhythmFrame chunks = RhythmFrame.Empty;
        for (int i = 0; i < samples.Length; i += 137)
            chunks = tracker.Process(samples.AsSpan(i, Math.Min(137, samples.Length - i)));
        Equal(whole, chunks);
    }),
    ("Sustains and low noise never manufacture a stable beat", () =>
    {
        var tracker = new RhythmTracker(8000);
        var sustain = Enumerable.Range(0, 80000).Select(i => (float)(.2 * Math.Sin(i * 2 * Math.PI * 220 / 8000))).ToArray();
        var held = tracker.Process(sustain);
        Equal(1L, held.OnsetCount);
        True(held.TempoBpm is null);
        var random = new Random(76);
        var noise = Enumerable.Range(0, 80000).Select(_ => (float)((random.NextDouble() - .5) * .001)).ToArray();
        Equal(0L, new RhythmTracker(8000).Process(noise).OnsetCount);
    }),
    ("Rhythm releases on silence and adapts to a new speed", () =>
    {
        var tracker = new RhythmTracker(8000);
        Near(120, tracker.Process(PulseTrain(8000, 120, 10, .2)).TempoBpm!.Value);
        var silent = tracker.Process(new float[24000]);
        True(silent.TempoBpm is null && silent.Confidence == 0);
        Near(90, tracker.Process(PulseTrain(8000, 90, 10, .2)).TempoBpm!.Value, 2);
    }),
    ("Irregular attacks do not claim a confident tempo", () =>
    {
        var tracker = new RhythmTracker(8000);
        RhythmFrame result = RhythmFrame.Empty;
        foreach (var duration in new[] { .31, .92, .44, 1.27, .36, .83, 1.15, .42, 1.3 })
        {
            var samples = new float[(int)(8000 * duration)];
            for (int i = 0; i < 160; i++) samples[i] = (float)(.2 * Math.Sin(i * 2 * Math.PI * 220 / 8000));
            result = tracker.Process(samples);
        }
        True(result.TempoBpm is null);
        True(result.Confidence < .6);
    }),
    ("Rhythm rejects invalid controls and sanitizes driver samples", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => new RhythmTracker(0));
        var tracker = new RhythmTracker(8000);
        Throws<ArgumentOutOfRangeException>(() => tracker.Process([], double.NaN));
        var result = tracker.Process(Enumerable.Repeat(float.NaN, 8000).ToArray());
        Equal(0L, result.OnsetCount);
        True(result.TempoBpm is null);
    }),
    ("Riff history preserves chronological complete frames through wrap and oversized writes", () =>
    {
        var ring = new AudioRingBuffer(3, 2);
        Equal(0, ring.Snapshot().Length);
        ring.Write([1, 2, 3, 4]);
        Bytes([1, 2, 3, 4], ring.Snapshot());
        var original = ring.Snapshot();
        ring.Write([5, 6, 7, 8]);
        Bytes([3, 4, 5, 6, 7, 8], ring.Snapshot());
        Bytes([1, 2, 3, 4], original);
        ring.Write([9, 10, 11, 12, 13, 14, 15, 16]);
        Bytes([11, 12, 13, 14, 15, 16], ring.Snapshot());
        ring.Write([17, 18]);
        Bytes([13, 14, 15, 16, 17, 18], ring.Snapshot());
        ring.Clear();
        Equal(0, ring.Count);
        ring.Write([21, 22]);
        Bytes([21, 22], ring.Snapshot());
        Throws<ArgumentException>(() => ring.Write([1]));
        Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(int.MaxValue, 8));
    }),
    ("Riff history matches last frames across many arbitrary callback sizes", () =>
    {
        var ring = new AudioRingBuffer(97, 8);
        var expected = new List<byte>();
        var random = new Random(735);
        for (int i = 0; i < 300; i++)
        {
            var block = new byte[random.Next(0, 150) * 8];
            random.NextBytes(block);
            ring.Write(block);
            expected.AddRange(block);
            if (expected.Count > 97 * 8) expected.RemoveRange(0, expected.Count - 97 * 8);
            Bytes(expected.ToArray(), ring.Snapshot());
        }
    }),
    ("Companion debounces brief transients and holds listening over gaps", () =>
    {
        var pet = new CompanionStateMachine();
        Equal(CompanionMood.Waiting, pet.Step(true, .3, .1).Mood);
        Equal(CompanionMood.Waiting, pet.Step(false, 0, .1).Mood);
        Equal(CompanionMood.Listening, pet.Step(true, .3, .25).Mood);
        Equal(CompanionMood.Listening, pet.Step(false, 0, .5).Mood);
        pet.Step(false, 0, 1);
        Equal(CompanionMood.Waiting, pet.Step(false, 0, .4).Mood);
        for (int i = 0; i < 15; i++) pet.Step(false, 0, 1);
        Equal(CompanionMood.Resting, pet.Current.Mood);
        Equal(CompanionMood.Listening, pet.Step(true, .3, .25).Mood);
    }),
    ("Companion excitement has a bounded release and resets on disconnect", () =>
    {
        var pet = new CompanionStateMachine();
        for (int i = 0; i < 1200; i++) pet.Step(true, .9, .1);
        Equal(CompanionMood.Excited, pet.Current.Mood);
        for (int i = 0; i < 20; i++) pet.Step(false, 0, .1);
        Equal(CompanionMood.Waiting, pet.Current.Mood);
        True(pet.Current.Energy < .11);
        pet.Reset();
        Equal(new CompanionState(CompanionMood.Waiting, 0), pet.Current);
        Throws<ArgumentOutOfRangeException>(() => pet.Step(true, double.NaN, .1));
        Throws<ArgumentOutOfRangeException>(() => pet.Step(true, .5, -1));
    }),
    ("Empty and silent samples produce finite inactive display-floor levels", () =>
    {
        var analyzer = new SignalAnalyzer();
        Equal(SignalFrame.Silence, analyzer.Process([]));
        Equal(SignalFrame.Silence, analyzer.Process(new float[1000]));
    }),
    ("RMS and peak represent normalized amplitude in mono and stereo", () =>
    {
        var analyzer = new SignalAnalyzer();
        var mono = analyzer.Process([0.5f, -0.5f]);
        Near(0.5, mono.Rms);
        Near(0.5, mono.Peak);
        Near(-6.020599913, mono.RmsDb);
        True(mono.IsActive && !mono.IsClipping);
        var stereo = analyzer.Process([0.5f, 0, -0.5f, 0], 2);
        Near(Math.Sqrt(0.125), stereo.Rms);
        Near(0.5, stereo.Peak);
    }),
    ("Noise and threshold settings gate real levels before the visual floor", () =>
    {
        var analyzer = new SignalAnalyzer { NoiseFloorDb = -80, ActivityThresholdDb = -70 };
        var weak = analyzer.Process([0.0004f]);
        Near(-60, weak.RmsDb);
        True(weak.IsActive);
        analyzer.NoiseFloorDb = -50;
        True(!analyzer.Process([0.0004f]).IsActive);
        analyzer.ActivityThresholdDb = -40;
        True(!analyzer.Process([0.005f]).IsActive);
        True(analyzer.Process([0.02f]).IsActive);
    }),
    ("Invalid samples cannot poison level math; overload is clipped safely", () =>
    {
        var analyzer = new SignalAnalyzer();
        var invalid = analyzer.Process([float.NaN, float.PositiveInfinity, float.NegativeInfinity]);
        Equal(SignalFrame.Silence, invalid);
        var frame = analyzer.Process([float.NaN, 2, -2, float.PositiveInfinity]);
        Near(1, frame.Peak);
        Near(Math.Sqrt(0.5), frame.Rms);
        True(frame.IsClipping);
        analyzer.Reset();
        Equal(SignalFrame.Silence, analyzer.CurrentFrame);
    }),
    ("Malformed channel buffers and invalid thresholds fail explicitly", () =>
    {
        var analyzer = new SignalAnalyzer();
        Throws<ArgumentOutOfRangeException>(() => analyzer.Process([], 0));
        Throws<ArgumentException>(() => analyzer.Process([0.1f, 0.2f, 0.3f], 2));
        Throws<ArgumentOutOfRangeException>(() => analyzer.NoiseFloorDb = double.NaN);
        Throws<ArgumentOutOfRangeException>(() => analyzer.ActivityThresholdDb = 10);
    }),
    ("Calibration requires three seconds of actual sample frames in stereo", () =>
    {
        var calibration = new CalibrationAccumulator(8000, 2);
        var pending = calibration.AddSamples(new float[2 * 8000]);
        Equal(CalibrationStatus.Collecting, pending.Status);
        Near(1.0 / 3, pending.Progress);
        Equal(pending, calibration.AddSamples([]));
        Equal(CalibrationStatus.Collecting, calibration.AddSamples(new float[2 * 15999]).Status);
        var complete = calibration.AddSamples([0, 0]);
        Equal(CalibrationStatus.Complete, complete.Status);
        Near(1, complete.Progress);
        Near(-90, complete.SuggestedNoiseFloorDb!.Value);
        Near(-60, complete.SuggestedActivityThresholdDb!.Value);
    }),
    ("Quiet noise calibration is independent of capture chunk boundaries", () =>
    {
        var samples = Enumerable.Range(0, 24000).Select(i => i % 2 == 0 ? 0.001f : -0.001f).ToArray();
        var contiguous = new CalibrationAccumulator(8000);
        var chunks = new CalibrationAccumulator(8000);
        var wholeResult = contiguous.AddSamples(samples);
        for (var offset = 0; offset < samples.Length; offset += 137)
            chunks.AddSamples(samples.AsSpan(offset, Math.Min(137, samples.Length - offset)));
        Equal(CalibrationStatus.Complete, chunks.Result.Status);
        Near(wholeResult.SuggestedNoiseFloorDb!.Value, chunks.Result.SuggestedNoiseFloorDb!.Value);
        Near(-50, chunks.Result.SuggestedActivityThresholdDb!.Value, 0.001);
    }),
    ("Loud guitar, quiet pluck changes, and corrupt input reject calibration", () =>
    {
        var calibration = new CalibrationAccumulator(8000);
        Equal(CalibrationStatus.Rejected, calibration.AddSamples([0.1f]).Status);
        True(calibration.Result.SuggestedActivityThresholdDb is null);
        Equal(CalibrationStatus.Rejected, calibration.AddSamples(new float[24000]).Status);
        calibration.Reset();
        Equal(CalibrationStatus.Collecting, calibration.AddSamples(new float[800]).Status);
        Equal(CalibrationStatus.Rejected, calibration.AddSamples(Enumerable.Repeat(0.004f, 400).ToArray()).Status);
        calibration.Reset();
        Equal(CalibrationStatus.Rejected, calibration.AddSamples([float.NaN]).Status);
        calibration.Reset();
        Equal(CalibrationStatus.Rejected, calibration.AddSamples(Enumerable.Repeat(0.02f, 400).ToArray()).Status);
    }),
    ("Calibration can restart cleanly after a rejected run", () =>
    {
        var calibration = new CalibrationAccumulator(8000);
        calibration.AddSamples([1]);
        calibration.Reset();
        Equal(CalibrationStatus.Collecting, calibration.Result.Status);
        Near(0, calibration.Result.Progress);
        Equal(CalibrationStatus.Complete, calibration.AddSamples(new float[24000]).Status);
    }),
    ("QME-10 profile separates capture smoke-test status from routing verification", () =>
    {
        var catalog = DeviceProfileCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "devices.json"));
        var profile = catalog.GetDisplayNameSuggestions("Microphone (SONICAKE USB)").Single();
        Equal(44100, profile.DocumentedSampleRate);
        Equal(16, profile.DocumentedBitsPerSample);
        Equal(2, profile.DocumentedChannels);
        True(!profile.IsHardwareVerified);
        True(profile.IsCaptureSmokeTested);
        Equal(0, catalog.GetDisplayNameSuggestions("Other brand interface").Count);
        Equal(0, catalog.GetDisplayNameSuggestions(null).Count);
    }),
    ("Malformed and unsupported profile data are rejected", () =>
    {
        Throws<InvalidDataException>(() => LoadJson("{bad"));
        Throws<InvalidDataException>(() => LoadJson("{\"schemaVersion\":2,\"profiles\":[]}"));
        Throws<InvalidDataException>(() => LoadJson("{\"schemaVersion\":1,\"profiles\":null}"));
        Throws<InvalidDataException>(() => LoadJson("{\"schemaVersion\":1,\"profiles\":[{}]}"));
        Throws<InvalidDataException>(() => LoadJson("{\"schemaVersion\":1,\"profiles\":[],\"forceSampleRate\":48000}"));
        Equal(0, LoadJson("{\"schemaVersion\":1,\"profiles\":[]}").Profiles.Count);
    })
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} checks passed.");
return failures == 0 ? 0 : 1;

static void True(bool condition)
{
    if (!condition) throw new Exception("Expected condition to be true.");
}

static void Bytes(byte[] expected, byte[] actual)
{
    if (!expected.AsSpan().SequenceEqual(actual)) throw new Exception("PCM history changed order, length, or content.");
}

static float[] PulseTrain(int rate, double bpm, int beats, double gain)
{
    var period = 60 / bpm;
    var samples = new float[(int)(rate * period * beats)];
    for (int i = 0; i < samples.Length; i++)
    {
        double phase = (i / (double)rate) % period;
        if (phase < .04) samples[i] = (float)(gain * Math.Sin(2 * Math.PI * 220 * i / rate) * Math.Exp(-phase * 30));
    }
    return samples;
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}.");
}

static void Near(double expected, double actual, double tolerance = 0.000001)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
        throw new Exception($"Expected {expected} ± {tolerance}, got {actual}.");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

static DeviceProfileCatalog LoadJson(string json)
{
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    return DeviceProfileCatalog.Load(stream);
}
