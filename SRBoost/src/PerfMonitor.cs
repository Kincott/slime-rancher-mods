using System;
using System.Globalization;
using UnityEngine;

namespace SRBoost
{
    // Measures what the player actually gets, so tuning is based on numbers: every PerfLogSeconds it logs the
    // average FPS, the 1% low (average of the slowest 1% of frames) and the worst frame time. The first report
    // also says how the game runs physics, which matters for smoothness at high refresh rates.
    internal static class PerfMonitor
    {
        private const int Capacity = 20000; // enough for 30 s at well over 280 FPS

        private static readonly float[] frameMs = new float[Capacity];
        private static int count;
        private static float windowStart = -1f;
        private static bool physicsReported;
        private static int gcAtWindowStart;

        internal static void Tick()
        {
            int period = Main.Config.PerfLogSeconds;
            if (period <= 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (windowStart < 0f)
            {
                windowStart = now;
                gcAtWindowStart = GC.CollectionCount(0);
                return;
            }
            if (count < Capacity)
            {
                frameMs[count++] = Time.unscaledDeltaTime * 1000f;
            }
            if (now - windowStart < period)
            {
                return;
            }

            Report(now - windowStart);
            count = 0;
            windowStart = now;
            gcAtWindowStart = GC.CollectionCount(0);
        }

        private static void Report(float seconds)
        {
            if (!physicsReported)
            {
                physicsReported = true;
                Main.Log(string.Format(CultureInfo.InvariantCulture,
                    "physics step {0:0.###} s ({1:0} Hz), monitor {2} Hz",
                    Time.fixedDeltaTime, 1f / Time.fixedDeltaTime, Screen.currentResolution.refreshRate));
                // Incremental GC spreads collections over frames instead of one stop (enabled by
                // "gc-max-time-slice=3" in SlimeRancher_Data/boot.config); this confirms whether it is active.
                Main.Log(string.Format(CultureInfo.InvariantCulture,
                    "GC incremental={0}, time slice {1:0.0} ms",
                    UnityEngine.Scripting.GarbageCollector.isIncremental,
                    UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds / 1000000.0));
            }
            if (count == 0)
            {
                return;
            }

            float[] sorted = new float[count];
            Array.Copy(frameMs, sorted, count);
            Array.Sort(sorted); // ascending: slowest frames at the end

            int slowest = Math.Max(1, count / 100);
            float slowSum = 0f;
            for (int i = count - slowest; i < count; i++)
            {
                slowSum += sorted[i];
            }
            float lowFps = 1000f / (slowSum / slowest);
            float worstMs = sorted[count - 1];

            // Frames slower than 2x the median: the visible hitches.
            float median = sorted[count / 2];
            int hitches = 0;
            for (int i = count - 1; i >= 0 && sorted[i] > 2f * median; i--)
            {
                hitches++;
            }
            // Mono's garbage collector stops the game while it runs; if the GC count tracks the hitch count,
            // memory churn is the cause.
            int gcRuns = GC.CollectionCount(0) - gcAtWindowStart;

            Main.Log(string.Format(CultureInfo.InvariantCulture,
                "perf {0:0} s: avg {1:0} FPS, 1% low {2:0} FPS, worst frame {3:0.0} ms, {4} hitches (>2x median {5:0.0} ms), {6} GC runs",
                seconds, count / seconds, lowFps, worstMs, hitches, median, gcRuns));
        }
    }
}
