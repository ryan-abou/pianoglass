using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using PianoGlass.Song;

namespace PianoGlass.Highway
{
    /// <summary>
    /// Tells SongClock how long this frame will take to reach the display,
    /// so everything drawn from the song is drawn where it will be when the
    /// player actually sees it -- the same prediction the runtime already
    /// makes for head pose. Without it every note reaches the key line one
    /// render-to-photon latency late, and a player who hits it exactly is
    /// judged late by the same amount.
    ///
    /// The latency is the runtime's own number: predicted display time minus
    /// now, both on OVRPlugin's clock, read fresh every frame; Unity's
    /// motion-to-photon figure if that is unavailable. Raw readings are
    /// noisy (0s, and once -253 s), so DisplayLatencyFilter rejects the
    /// implausible ones and smooths the rest over ~1 s before SongClock
    /// sees them.
    /// Nothing here is tuned by hand or learned from how anyone plays.
    ///
    /// Meta-specific code isolated in its own file, same split as
    /// OvrSongTransport: SongClock stays Meta-free and reads 0 in the editor.
    /// Runs before everything that draws (execution order -100).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SongClock))]
    public class OvrDisplayLatency : MonoBehaviour
    {
        [Tooltip("Seconds between [DisplayLatency] log lines, with the mean, " +
                 "min and max over that period. 0 = never.")]
        [SerializeField] private float logIntervalSeconds = 30f;

        [Tooltip("Smoothing time constant for the latency used to draw. ~1 s " +
                 "keeps the highway from jittering with per-frame timing.")]
        [SerializeField] private float smoothingSeconds = 1f;

        private SongClock _clock;
        private DisplayLatencyFilter _filter;
        private double _sum, _min = double.MaxValue, _max;
        private int _count;
        private float _nextLog;

        private enum Source { None, PredictedDisplayTime, MotionToPhoton }

        private Source? _source; // null until the first frame, so that one logs too
        private Func<double> _predictedDisplayTime;

        private void Awake()
        {
            _clock = GetComponent<SongClock>();
            _filter = new DisplayLatencyFilter(smoothingSeconds);

            // OVRPlugin.GetPredictedDisplayTime() exists in SDK 205 but is
            // private (checked by reflection against the installed package),
            // so it is bound once here. If a later SDK renames or strips it,
            // the fallback below takes over and the log says so.
            MethodInfo m = typeof(OVRPlugin).GetMethod("GetPredictedDisplayTime",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m != null && m.ReturnType == typeof(double))
                _predictedDisplayTime = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), m);
        }

        /// <summary>Real seconds from now until this frame is on the display,
        /// or false if the runtime can't say.</summary>
        private bool TryReadLatency(out double latency, out Source source)
        {
            latency = 0.0;
            source = Source.None;
            if (!OVRManager.isHmdPresent) return false;

            if (_predictedDisplayTime != null)
            {
                double predicted = _predictedDisplayTime();
                if (predicted > 0.0)
                {
                    latency = predicted - OVRPlugin.GetTimeInSeconds();
                    source = Source.PredictedDisplayTime;
                    return true;
                }
            }

            // Measured from pose sampling, which is later than Update, so it
            // reads a little short of what is needed -- better than nothing.
            XRDisplaySubsystem display = OVRManager.GetCurrentDisplaySubsystem();
            if (display != null && display.TryGetMotionToPhoton(out float m2p) && m2p > 0f)
            {
                latency = m2p;
                source = Source.MotionToPhoton;
                return true;
            }
            return false;
        }

        private void OnDisable()
        {
            if (_clock != null) _clock.DisplayLatencySeconds = 0.0;
        }

        private void Update()
        {
            // An unreadable frame is fed as 0, which the filter rejects like
            // any other implausible reading: the last good value is held.
            TryReadLatency(out double latency, out Source source);
            _clock.DisplayLatencySeconds = _filter.Add(latency, Time.unscaledDeltaTime);

            if (source != _source)
            {
                _source = source;
                Debug.Log($"[DisplayLatency] source: {source}" +
                          (source == Source.None ? " -- notes drawn at frame time, uncompensated" : ""));
            }

            // Stats are of the raw readings the filter accepted, so the log
            // shows how noisy the runtime is next to the smoothed value used.
            if (DisplayLatencyFilter.IsPlausible(latency))
            {
                _sum += latency;
                _count++;
                if (latency < _min) _min = latency;
                if (latency > _max) _max = latency;
            }

            if (logIntervalSeconds > 0f && Time.realtimeSinceStartup >= _nextLog)
            {
                _nextLog = Time.realtimeSinceStartup + logIntervalSeconds;
                string accepted = _count > 0
                    ? $"raw mean {_sum / _count * 1000.0:0.0} ms, min {_min * 1000.0:0.0}, max {_max * 1000.0:0.0}"
                    : "no plausible raw readings";
                Debug.Log($"[DisplayLatency] used {_clock.DisplayLatencySeconds * 1000.0:0.0} ms (smoothed); " +
                          $"{accepted} over {_filter.AcceptedCount} frames; rejected {_filter.RejectedCount}" +
                          (_filter.RejectedCount > 0 ? $" (last rejected {_filter.LastRejected * 1000.0:0.0} ms)" : ""));
                _sum = 0; _count = 0; _min = double.MaxValue; _max = 0;
                _filter.ResetCounts();
            }
        }
    }
}
