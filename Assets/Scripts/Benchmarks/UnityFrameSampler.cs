using System;
using System.Collections;
using UnityEngine;

// Samples actual frames from the Player, not just the delta from the Update function.
// Assign the same Renderer Feature that is installed in the URP Renderer used by the camera.
public sealed class UnityFrameSampler : MonoBehaviour
{
    [SerializeField] private PixelizeRenderFeature pixelizeFeature;
    [SerializeField, Min(1)] private int warmupFrames = 60;
    [SerializeField, Min(1)] private int framesPerMode = 500;

    public bool IsReady
    {
        get { return pixelizeFeature != null && pixelizeFeature.settings != null &&
                     pixelizeFeature.isActive && pixelizeFeature.settings.pixelScale >= 1; }
    }

    public IEnumerator Sample(SysBenchmarkCsv csv, string seed, int repetition,
        int planetCount, int moonCount)
    {
        if (!IsReady)
            throw new InvalidOperationException("PixelizeRenderFeature assente, disattivata nel Renderer oppure pixelScale < 1.");
        int previousScale = pixelizeFeature.settings.pixelScale;
        bool previousEnabled = pixelizeFeature.settings.isEnabled;
        int bodyCount = 1 + planetCount + moonCount;
        int scale = pixelizeFeature.settings.pixelScale;
        var wait = new WaitForEndOfFrame();
        try
        {
            // Alternating order isn't necessary for a single configuration, but here the two
            // states are sampled on the same scene without regenerating it.
            for (int pass = 0; pass < 2; pass++)
            {
                bool enabled = pass == 1;
                pixelizeFeature.settings.isEnabled = enabled;
                for (int k = 0; k < warmupFrames; k++) yield return wait;
                for (int i = 0; i < framesPerMode; i++)
                {
                    yield return wait;
                    double ms = Time.unscaledDeltaTime * 1000.0;
                    if (ms <= 0.0 || double.IsNaN(ms) || double.IsInfinity(ms))
                        throw new InvalidOperationException("Frame time non valido.");
                    double fps = 1000.0 / ms;
                    csv.Write(seed, repetition, planetCount, moonCount, bodyCount,
                              "frame", "", enabled, scale, fps, ms);
                }
            }
        }
        finally
        {
            pixelizeFeature.settings.isEnabled = previousEnabled;
            pixelizeFeature.settings.pixelScale = previousScale;
        }
    }
}
