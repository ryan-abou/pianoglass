using System;

namespace PianoGlass.Song
{
    /// <summary>
    /// A steady estimate of AudioSettings.dspTime "now".
    ///
    /// On Android dspTime only advances once per audio buffer, so a read in
    /// Update can be up to a whole buffer old -- and a metronome scheduled
    /// from it wanders by that much. The first headset video (2026-11-03)
    /// measured click-to-click gaps of 448-562 ms for a 500 ms beat; the
    /// audio itself is exact, the schedule was not.
    ///
    /// A stale read makes dsp - realtime smaller, never larger, so the
    /// largest offset seen recently is the freshest: the estimate is
    /// realtime + that maximum. Kept over a window of recent buffer steps so
    /// slow drift between the two clocks is followed; reset when the
    /// estimate and a fresh read disagree by more than <see cref="JumpSeconds"/>
    /// (audio restarted after the app was paused).
    /// </summary>
    public sealed class DspClockEstimator
    {
        public const double JumpSeconds = 0.25;

        private readonly double[] _offsets;
        private int _count, _next;
        private double _lastDsp = double.NaN;

        public DspClockEstimator(int window = 48)
        {
            _offsets = new double[Math.Max(1, window)];
        }

        public bool HasEstimate => _count > 0;

        /// <summary>Feed one read: <paramref name="realtime"/> (a steady clock,
        /// seconds) taken as close as possible to reading <paramref name="dspTime"/>.</summary>
        public void Observe(double realtime, double dspTime)
        {
            if (dspTime == _lastDsp) return;   // no new buffer: nothing fresh to learn
            _lastDsp = dspTime;
            double offset = dspTime - realtime;

            if (_count > 0 && Math.Abs(offset - MaxOffset()) > JumpSeconds)
            {
                _count = 0;
                _next = 0;
            }
            _offsets[_next] = offset;
            _next = (_next + 1) % _offsets.Length;
            if (_count < _offsets.Length) _count++;
        }

        /// <summary>The DSP time corresponding to <paramref name="realtime"/>;
        /// the last raw read until anything has been observed.</summary>
        public double Estimate(double realtime) =>
            _count > 0 ? realtime + MaxOffset() : (double.IsNaN(_lastDsp) ? realtime : _lastDsp);

        private double MaxOffset()
        {
            double max = double.NegativeInfinity;
            for (int i = 0; i < _count; i++) if (_offsets[i] > max) max = _offsets[i];
            return max;
        }
    }
}
