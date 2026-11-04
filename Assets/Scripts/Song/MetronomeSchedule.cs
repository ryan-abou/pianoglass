using System;
using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>One click to hand to AudioSource.PlayScheduled.</summary>
    public readonly struct MetronomeClick
    {
        /// <summary>AudioSettings.dspTime to start the clip at.</summary>
        public readonly double DspTime;
        /// <summary>The beat it sounds for, song seconds.</summary>
        public readonly double SongTime;
        /// <summary>First beat of a bar -- louder and higher.</summary>
        public readonly bool Accent;

        public MetronomeClick(double dspTime, double songTime, bool accent)
        {
            DspTime = dspTime; SongTime = songTime; Accent = accent;
        }
    }

    /// <summary>
    /// Which metronome clicks to schedule this frame, and when on the audio
    /// clock (F27). Pure, so the timing rules are tested headlessly; the
    /// Metronome component only owns the AudioSources.
    ///
    /// Beats are BeatGrid's, so a click lands on exactly the line the grid
    /// draws, and the bar lines are the accented clicks. The lead-in counts
    /// (song time from -leadIn to 0) -- the count-in, by request.
    ///
    /// Each frame, every beat due within the next LookaheadSeconds of real
    /// time that has not been scheduled yet is mapped from song time to the
    /// DSP clock: dspNow + (beat - songNow) / tempo - outputLatency. Taking
    /// the latency off means the sound leaves the speaker on the beat, not
    /// one buffer later. A beat whose start would already be in the past is
    /// skipped rather than played late. Reset after a pause or seek, since
    /// the clicks already handed out are stopped then.
    /// </summary>
    public sealed class MetronomeSchedule
    {
        public double LookaheadSeconds { get; }
        public double OutputLatencySeconds { get; set; }

        private double _lastScheduled = double.NegativeInfinity;
        private readonly List<BeatLine> _beats = new List<BeatLine>();

        public MetronomeSchedule(double lookaheadSeconds = 0.3, double outputLatencySeconds = 0.0)
        {
            LookaheadSeconds = Math.Max(0.0, lookaheadSeconds);
            OutputLatencySeconds = outputLatencySeconds;
        }

        /// <summary>Forget what was scheduled: after a pause, restart or seek.</summary>
        public void Reset() => _lastScheduled = double.NegativeInfinity;

        /// <summary>The clicks to schedule now, appended to <paramref name="into"/>
        /// (cleared first). <paramref name="earliest"/>/<paramref name="latest"/>
        /// bound the song in song seconds: -leadIn and the song's end.</summary>
        public int Collect(float bpm, int beatsPerBar, double earliest, double latest,
                           double songTime, double tempo, double dspNow, List<MetronomeClick> into)
        {
            into.Clear();
            if (tempo <= 0.0) return 0;

            BeatGrid.CollectVisible(bpm, beatsPerBar, earliest, latest, songTime, LookaheadSeconds * tempo, _beats);
            foreach (BeatLine b in _beats)
            {
                if (b.Time <= _lastScheduled + 1e-9) continue;
                _lastScheduled = b.Time;

                double dsp = DspTimeFor(b.Time, songTime, dspNow, tempo, OutputLatencySeconds);
                if (dsp < dspNow) continue; // too late to start on time: skip, never play late
                into.Add(new MetronomeClick(dsp, b.Time, b.IsBarLine));
            }
            return into.Count;
        }

        /// <summary>When, on the DSP clock, a clip must start for its sound to
        /// leave the speaker as the song reaches <paramref name="beatSongTime"/>.</summary>
        public static double DspTimeFor(double beatSongTime, double songTimeNow, double dspNow,
                                        double tempo, double outputLatencySeconds) =>
            dspNow + (beatSongTime - songTimeNow) / tempo - outputLatencySeconds;
    }
}
