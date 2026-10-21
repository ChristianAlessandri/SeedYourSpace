using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

// Wraps the original GenerateCompleteStarSystem method with non-invasive hooks.
// Must be run in a graphics Player with the final scene's assets.
public sealed class UnityGenerationProfiler : MonoBehaviour
{
    [SerializeField] private StarSystemGenerator generator;
    [SerializeField] private UnityFrameSampler frameSampler;
    [SerializeField] private bool runOnStart = true;
    [SerializeField, Min(1)] private int seedCount = 60;
    [SerializeField, Min(1)] private int repetitions = 5;
    [SerializeField] private int firstSeed = 2000000;
    [SerializeField, Min(2)] private int settleFrames = 3;
    [SerializeField] private bool sampleFrames = true;

    private long start, logicBegin, logicEnd, skyBegin, skyEnd, buildBegin, buildEnd;
    private const double MillisPerSecond = 1000.0;
    private int gcDetected;

    private void Start() { if (runOnStart) StartCoroutine(Run()); }

    private void OnPhase(string phase)
    {
        long tick = Stopwatch.GetTimestamp();
        switch (phase)
        {
            case "logic_begin": logicBegin = tick; break;
            case "logic_end": logicEnd = tick; break;
            case "skybox_begin": skyBegin = tick; break;
            case "skybox_end": skyEnd = tick; break;
            case "build_begin": buildBegin = tick; break;
            case "build_end": buildEnd = tick; break;
        }
    }

    private static double Ms(long ticks)
    {
        return ticks * MillisPerSecond / Stopwatch.Frequency;
    }

    private static void WritePhase(SysBenchmarkCsv csv, string seed, int repeat,
        int planets, int moons, string phase, double elapsed)
    {
        csv.Write(seed, repeat, planets, moons, 1 + planets + moons,
                  phase, elapsed, "", "", "", "");
    }

    private static int MoonCount(StarSystemGenerator g)
    {
        int count = 0;
        foreach (PlanetData p in g.SystemPlanets) count += p.moons.Count;
        return count;
    }

    public IEnumerator Run()
    {
        if (generator == null || generator.enabled || generator.dioramaBuilder == null ||
            generator.dioramaBuilder.celestialPrefab == null)
        {
            UnityEngine.Debug.LogError("[SYS Profiler] You need StarSystemGenerator disabled, with VisualDioramaBuilder and a prefab assigned.");
            yield break;
        }
        if (Resources.Load<TextAsset>("generation_data_v1") == null ||
            Resources.Load<TextAsset>("markov_data_v1") == null)
        {
            UnityEngine.Debug.LogError("[SYS Profiler] JSON V1 files not found in Assets/Resources.");
            yield break;
        }
        if (sampleFrames && (frameSampler == null || !frameSampler.IsReady))
        {
            UnityEngine.Debug.LogError("[SYS Profiler] Set UnityFrameSampler and PixelizeRenderFeature to “Enabled” in the URP Renderer.");
            yield break;
        }
        if (Application.isEditor)
            UnityEngine.Debug.LogWarning("[SYS Profiler] The steps for the thesis require a Player build, not the Editor's Play Mode.");
        if (generator.OnSystemGenerated != null && generator.OnSystemGenerated.GetPersistentEventCount() > 0)
            UnityEngine.Debug.LogWarning("[SYS Profiler] Remove the persistent OnSystemGenerated UI listeners from the benchmark scene.");
        generator.algorithmVersion = 1;
        generator.benchmarkLogicOnly = false;
        int oldVsync = QualitySettings.vSyncCount;
        int oldTarget = Application.targetFrameRate;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        SysBenchmarkCsv.WriteEnvironment("unity");
        string profilePath = "", scatterPath = "";
        try
        {
            using (var profile = new SysBenchmarkCsv("unity_profile.csv",
                "seed", "repetition", "planet_count", "moon_count", "body_count",
                "phase", "elapsed_ms", "pixel_enabled", "pixel_scale", "fps", "frame_ms"))
            using (var scatter = new SysBenchmarkCsv("unity_generation_scatter.csv",
                "seed", "planet_count", "moon_count", "body_count", "logic_mean_ms", "build_mean_ms"))
            {
                // Warm-up outside the sample: initialization of JSON, factory, shader, and mesh.
                generator.GenerateCompleteStarSystem("1999999");
                for (int k = 0; k < settleFrames; k++) yield return null;
                // No CSV allocations during the timed period.
                generator.BenchmarkPhase = OnPhase;
                for (int i = 0; i < seedCount; i++)
                {
                    string seed = ((long)firstSeed + i).ToString(CultureInfo.InvariantCulture);
                    double logicSum = 0, buildSum = 0;
                    int planets = 0, moons = 0;
                    // Each seed has the same number of repetitions; no outliers are discarded.
                    for (int r = 0; r < repetitions; r++)
                    {
                        // The destruction of Unity objects is deferred until the end of the frame.
                        for (int k = 0; k < settleFrames; k++) yield return null;
                        int gcBefore = GC.CollectionCount(0);
                        logicBegin = logicEnd = skyBegin = skyEnd = buildBegin = buildEnd = 0;
                        start = Stopwatch.GetTimestamp();
                        generator.GenerateCompleteStarSystem(seed);
                        long stop = Stopwatch.GetTimestamp();
                        if (GC.CollectionCount(0) != gcBefore) gcDetected++;
                        if (logicBegin == 0 || logicEnd <= logicBegin ||
                            skyEnd < skyBegin || buildEnd <= buildBegin)
                            throw new InvalidOperationException("Missing hooks in the generator.");
                        double total = Ms(stop - start);
                        double skybox = Ms(skyEnd - skyBegin);
                        double logic = Ms(logicEnd - logicBegin) - skybox;
                        double build = Ms(buildEnd - buildBegin);
                        double other = total - logic - build;
                        if (logic < 0 || build < 0 || other < -0.05)
                            throw new InvalidOperationException("Inconsistent intervals: Check benchmark_hooks.patch.");
                        if (other < 0) other = 0;
                        planets = generator.SystemPlanets.Count;
                        moons = MoonCount(generator);
                        WritePhase(profile, seed, r, planets, moons, "logic", logic);
                        WritePhase(profile, seed, r, planets, moons, "build", build);
                        WritePhase(profile, seed, r, planets, moons, "other", other);
                        WritePhase(profile, seed, r, planets, moons, "total", total);
                        logicSum += logic;
                        buildSum += build;
                    }
                    scatter.Write(seed, planets, moons, 1 + planets + moons,
                        logicSum / repetitions, buildSum / repetitions);
                    // During the test frame, the content remains exactly the same.
                    if (i == 0 && sampleFrames)
                    {
                        for (int k = 0; k < 30; k++) yield return null;
                        yield return StartCoroutine(frameSampler.Sample(profile, seed,
                            repetitions - 1, planets, moons));
                    }
                    if ((i + 1) % 10 == 0)
                    {
                        profile.Flush(); scatter.Flush();
                        UnityEngine.Debug.Log("[SYS Profiler] completed " + (i + 1) + "/" + seedCount + " seed.");
                    }
                }
                profile.Flush(); scatter.Flush();
                profilePath = profile.PathName;
                scatterPath = scatter.PathName;
            }
        }
        finally
        {
            generator.BenchmarkPhase = null;
            QualitySettings.vSyncCount = oldVsync;
            Application.targetFrameRate = oldTarget;
        }
        UnityEngine.Debug.Log("[SYS Profiler] CSV: " + profilePath + " ; " + scatterPath +
                              ". Ripetitions with GC gen0: " + gcDetected);
    }
}
