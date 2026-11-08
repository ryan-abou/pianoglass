using System;
using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>
    /// Where every beat and bar of a song falls in seconds, worked out once
    /// when the song loads. Pure, so the highway, the metronome, loops and the
    /// tests all share one answer.
    ///
    /// Beats are numbered from 0 = beat 1 of bar 1. Past either end of the
    /// list it carries on by itself: the lead-in runs backwards at the first
    /// tempo and time signature, and anything after the song runs on at the
    /// last ones. A one-tempo song is one beat in the list and everything else
    /// carried on from it, which is the same maths as the old single Bpm.
    /// </summary>
    public sealed class TempoMap
    {
        public const int DefaultMicrosPerQuarter = 500000; // 120 BPM, by the MIDI rules

        /// <summary>A longer song than this is a broken file, not music.</summary>
        public const int MaxBeats = 100000;

        private readonly struct Segment
        {
            public readonly long Tick;
            public readonly double Seconds;
            public readonly double SecondsPerTick;

            public Segment(long tick, double seconds, double secondsPerTick)
            {
                Tick = tick;
                Seconds = seconds;
                SecondsPerTick = secondsPerTick;
            }
        }

        private readonly Segment[] _segments;
        private readonly BeatLine[] _beats;
        private readonly double[] _barStarts;
        private readonly double _firstBeatSeconds;
        private readonly double _lastBeatSeconds;
        private readonly int _lastBeatsPerBar;

        /// <summary>The tempo at the start of the song.</summary>
        public float FirstBpm { get; }
        /// <summary>Beats in the first bar: the top number of its time signature.</summary>
        public int FirstBeatsPerBar { get; }
        /// <summary>The bottom number of the first time signature.</summary>
        public int FirstBeatUnit { get; }
        /// <summary>A time signature changed in the middle of a bar.</summary>
        public bool BarLinesMayBeOff { get; }
        /// <summary>The song ran past MaxBeats, so the list stops there.</summary>
        public bool TooLong { get; }

        /// <summary>"3/4".</summary>
        public string TimeSignatureText => $"{FirstBeatsPerBar}/{FirstBeatUnit}";

        /// <summary>False when the tempo is 0: there are no beats to find.</summary>
        public bool HasBeats => _firstBeatSeconds > 0.0;

        /// <summary>Song seconds in the first bar, at the first tempo.</summary>
        public double FirstBarSeconds => FirstBeatsPerBar * _firstBeatSeconds;

        private double LastBarSeconds => _lastBeatsPerBar * _lastBeatSeconds;

        private TempoMap(Segment[] segments, BeatLine[] beats, double[] barStarts, float firstBpm,
                         int firstBeatsPerBar, int firstBeatUnit, double firstBeatSeconds,
                         double lastBeatSeconds, int lastBeatsPerBar, bool barLinesMayBeOff, bool tooLong)
        {
            _segments = segments;
            _beats = beats;
            _barStarts = barStarts;
            FirstBpm = firstBpm;
            FirstBeatsPerBar = firstBeatsPerBar;
            FirstBeatUnit = firstBeatUnit;
            _firstBeatSeconds = firstBeatSeconds;
            _lastBeatSeconds = lastBeatSeconds;
            _lastBeatsPerBar = lastBeatsPerBar;
            BarLinesMayBeOff = barLinesMayBeOff;
            TooLong = tooLong;
        }

        /// <summary>One tempo and one time signature for the whole song.</summary>
        public static TempoMap Constant(float bpm, int beatsPerBar = 4)
        {
            int perBar = Math.Max(1, beatsPerBar);
            double beat = bpm > 0f ? 60.0 / bpm : 0.0;
            return new TempoMap(new[] { new Segment(0, 0.0, 0.0) },
                                new[] { new BeatLine(0.0, 1, 1) }, new[] { 0.0 },
                                bpm, perBar, 4, beat, beat, perBar, false, false);
        }

        /// <summary>
        /// The map for a MIDI file. Beats follow the bottom number of the time
        /// signature, so 6/8 has six eighth-note beats. Tempos and time
        /// signatures can come in any order; with no tempo at tick 0 the file
        /// starts at 120 BPM, and with no time signature at 0 it starts in 4/4,
        /// both by the MIDI rules.
        /// </summary>
        /// <param name="endTick">Where the last note ends. The list runs to
        /// the end of that bar.</param>
        public static TempoMap FromMidi(int ticksPerBeat, IEnumerable<MidiTempoChange> tempos,
                                        IEnumerable<MidiTimeSignature> timeSignatures, long endTick)
        {
            Segment[] segments = BuildSegments(ticksPerBeat, tempos);
            List<MidiTimeSignature> signatures = CleanTimeSignatures(timeSignatures);

            var beats = new List<BeatLine>();
            var barStarts = new List<double> { 0.0 };
            MidiTimeSignature sig = signatures[0];
            int nextSig = 1;
            long tick = 0;
            int beatInBar = 1, bar = 1;
            bool barLinesOff = false, tooLong = false;
            double firstBeatSeconds = BeatTicks(ticksPerBeat, sig) * segments[0].SecondsPerTick;

            while (true)
            {
                double seconds = Seconds(segments, tick);
                beats.Add(new BeatLine(seconds, beatInBar, bar));
                if (tick >= endTick && beatInBar == 1) break;
                if (beats.Count >= MaxBeats) { tooLong = true; break; }

                long next = tick + BeatTicks(ticksPerBeat, sig);
                bool sigDue = nextSig < signatures.Count && signatures[nextSig].Tick <= next;
                if (sigDue)
                {
                    long at = signatures[nextSig].Tick;
                    if (at != next || beatInBar != sig.Numerator) barLinesOff = true;
                    next = at;
                    sig = signatures[nextSig++];
                    bar++;
                    beatInBar = 1;
                    barStarts.Add(Seconds(segments, next));
                }
                else if (beatInBar >= sig.Numerator)
                {
                    bar++;
                    beatInBar = 1;
                    barStarts.Add(Seconds(segments, next));
                }
                else
                {
                    beatInBar++;
                }
                tick = next;
            }

            double lastBeatSeconds = BeatTicks(ticksPerBeat, sig) * SegmentAt(segments, tick).SecondsPerTick;
            float firstBpm = (float)(60.0 / (segments[0].SecondsPerTick * ticksPerBeat));
            return new TempoMap(segments, beats.ToArray(), barStarts.ToArray(), firstBpm,
                                signatures[0].Numerator, signatures[0].Denominator, firstBeatSeconds,
                                lastBeatSeconds, sig.Numerator, barLinesOff, tooLong);
        }

        private static long BeatTicks(int ticksPerBeat, MidiTimeSignature sig) =>
            Math.Max(1L, ticksPerBeat * 4L / sig.Denominator);

        private static Segment[] BuildSegments(int ticksPerBeat, IEnumerable<MidiTempoChange> tempos)
        {
            var sorted = new List<MidiTempoChange>(tempos);
            StableSort(sorted, (a, b) => a.Tick.CompareTo(b.Tick));

            double perTick(int micros) => micros / 1000000.0 / ticksPerBeat;
            var list = new List<Segment> { new Segment(0, 0.0, perTick(DefaultMicrosPerQuarter)) };
            foreach (MidiTempoChange t in sorted)
            {
                Segment last = list[list.Count - 1];
                if (t.Tick == last.Tick)
                {
                    list[list.Count - 1] = new Segment(last.Tick, last.Seconds, perTick(t.MicrosPerQuarter));
                    continue;
                }
                double seconds = last.Seconds + (t.Tick - last.Tick) * last.SecondsPerTick;
                list.Add(new Segment(t.Tick, seconds, perTick(t.MicrosPerQuarter)));
            }
            return list.ToArray();
        }

        /// <summary>In order, starting at tick 0, and with the ones that
        /// change nothing taken out, so a file that repeats its time signature
        /// doesn't count as changing it.</summary>
        private static List<MidiTimeSignature> CleanTimeSignatures(IEnumerable<MidiTimeSignature> timeSignatures)
        {
            var sorted = new List<MidiTimeSignature>(timeSignatures);
            StableSort(sorted, (a, b) => a.Tick.CompareTo(b.Tick));

            var list = new List<MidiTimeSignature> { new MidiTimeSignature(0, 4, 4) };
            foreach (MidiTimeSignature s in sorted)
            {
                MidiTimeSignature last = list[list.Count - 1];
                if (s.Tick == last.Tick)
                {
                    list[list.Count - 1] = s;
                    continue;
                }
                if (s.Numerator != last.Numerator || s.Denominator != last.Denominator) list.Add(s);
            }
            return list;
        }

        private static void StableSort<T>(List<T> list, Comparison<T> compare)
        {
            var indexed = new List<KeyValuePair<int, T>>(list.Count);
            for (int i = 0; i < list.Count; i++) indexed.Add(new KeyValuePair<int, T>(i, list[i]));
            indexed.Sort((a, b) =>
            {
                int c = compare(a.Value, b.Value);
                return c != 0 ? c : a.Key.CompareTo(b.Key);
            });
            for (int i = 0; i < list.Count; i++) list[i] = indexed[i].Value;
        }

        private static Segment SegmentAt(Segment[] segments, long tick)
        {
            int lo = 0, hi = segments.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (segments[mid].Tick <= tick) lo = mid;
                else hi = mid - 1;
            }
            return segments[lo];
        }

        private static double Seconds(Segment[] segments, long tick)
        {
            Segment s = SegmentAt(segments, tick);
            return s.Seconds + (tick - s.Tick) * s.SecondsPerTick;
        }

        /// <summary>Ticks to song seconds, across every tempo change.</summary>
        public double TicksToSeconds(long tick) => Seconds(_segments, tick);

        /// <summary>Beat k. 0 is the song's first beat, negative is the lead-in.</summary>
        public BeatLine Beat(long k)
        {
            if (k < 0)
                return new BeatLine(k * _firstBeatSeconds, BeatGrid.BeatInBar(k, FirstBeatsPerBar),
                                    BeatGrid.BarOf(k, FirstBeatsPerBar));
            if (k < _beats.Length) return _beats[k];

            BeatLine last = _beats[_beats.Length - 1];
            long beyond = k - (_beats.Length - 1);
            long fromBarStart = last.BeatInBar - 1 + beyond;
            int perBar = Math.Max(1, _lastBeatsPerBar);
            return new BeatLine(last.Time + beyond * _lastBeatSeconds, (int)(fromBarStart % perBar) + 1,
                                last.Bar + (int)(fromBarStart / perBar));
        }

        /// <summary>The first beat at or after this song time.</summary>
        public long FirstBeatAtOrAfter(double time)
        {
            if (!HasBeats) return 0;
            if (time <= 0.0) return (long)Math.Ceiling(time / _firstBeatSeconds);

            int n = _beats.Length;
            BeatLine last = _beats[n - 1];
            if (time > last.Time)
                return _lastBeatSeconds > 0.0
                    ? n - 1 + (long)Math.Ceiling((time - last.Time) / _lastBeatSeconds)
                    : n;

            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_beats[mid].Time >= time) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }

        /// <summary>1-based bar of a song time. Anything before 0 is bar 1.</summary>
        public int BarOf(double songTime)
        {
            if (songTime <= 0.0) return 1;

            // A hair of tolerance, so a note exactly on a bar line isn't put
            // in the bar before by rounding.
            const double eps = 1e-9;
            int n = _barStarts.Length;
            double lastStart = _barStarts[n - 1];
            if (songTime + eps >= lastStart)
            {
                double bar = LastBarSeconds;
                return bar > 0.0 ? n + (int)Math.Floor((songTime - lastStart) / bar + eps) : n;
            }

            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_barStarts[mid] <= songTime + eps) lo = mid;
                else hi = mid - 1;
            }
            return lo + 1;
        }

        /// <summary>Song time a 1-based bar starts at. Bar 0 and below are the
        /// lead-in, and bars past the end carry on at the last tempo.</summary>
        public double BarStartTime(int bar)
        {
            if (bar < 1) return (bar - 1) * FirstBarSeconds;
            int n = _barStarts.Length;
            if (bar <= n) return _barStarts[bar - 1];
            return _barStarts[n - 1] + (bar - n) * LastBarSeconds;
        }
    }
}
