using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Piano;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// The Unity shell for scoring: feeds a ScoringSession key presses (as
    /// song times) and the song clock, and writes each attempt to the
    /// headset as JSON. All judging is in ScoringSession; this only decides
    /// which presses count and when an attempt starts and ends.
    ///
    /// Timing: a press is timed from its own MIDI timestamp, converted to
    /// Unity realtime by MidiInput.ToRealtime, then to song time against the
    /// clock as it stands this frame. Runs after SongClock (execution order
    /// 100). Judging uses SongTime, never DisplaySongTime: the highway draws
    /// ahead by the display latency so a note is SEEN at the key line when
    /// it is due, and a press is then judged against when it was really due.
    /// Whatever latency is left after that (USB, anything the runtime's
    /// prediction misses) is a property of the system, measured once (slow-
    /// motion video), not learned from the player -- inputOffsetMs is
    /// where it goes. A steady mean error after that is the player.
    ///
    /// Attempts: one starts the first frame the clock plays with no attempt
    /// running; it ends when every note is judged (completed) or when the
    /// clock jumps back -- restart -- part-way (abandoned). Each attempt is
    /// one file in persistentDataPath/attempts/, rewritten if the app is
    /// backgrounded mid-attempt and again when it ends.
    ///
    /// Verifying (anchored calibration, until A): nothing is scored. Presses
    /// were always ignored then, but the clock -- started by the restart
    /// chord -- kept advancing the session, so every note went by as a MISS
    /// and its slab went slate. Now no attempt starts or advances while
    /// calibration.AwaitingConfirmation; one in progress when Verifying
    /// begins (a Middle C fix mid-song) is dropped like a restart, since the
    /// keys it was judged against are being re-checked. After A, a new
    /// attempt starts from that moment and scores only the notes still
    /// ahead (ScoringSession's scoreNotesFrom).
    ///
    /// Practice loop (F33): each pass is its own attempt, scoring only the
    /// bars in the loop and recording them as loopFromBar/loopToBar. A wrap is
    /// told apart from a restart by SongClock.Wraps: it finishes the pass
    /// rather than abandoning it. A pass nobody played a note in is not saved,
    /// so a headset left looping does not fill the attempts folder.
    ///
    /// Wait mode (F22): the session judges on pitch alone, and each frame this
    /// holds the clock at the next note not yet played. Switching wait mode
    /// while paused part-way drops the attempt like a restart and begins a new
    /// one from there, since an attempt is one mode or the other.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ScoreKeeper : MonoBehaviour
    {
        [SerializeField] private SongClock clock;
        [SerializeField] private SongTransport transport;
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private CurrentSong current;
        [Tooltip("Recorded as 'metronome' in each attempt: whether it clicked at any point.")]
        [SerializeField] private Metronome metronome;

        [Header("Timing windows, real ms either side")]
        [SerializeField] private float perfectMs = 40f;
        [SerializeField] private float greatMs = 80f;
        [SerializeField] private float goodMs = 150f;

        [Tooltip("Subtracted from every press, real ms. System latency left " +
                 "after display compensation, measured on the hardware -- not " +
                 "the player's mean error, which would hide their own rushing " +
                 "or dragging.")]
        [SerializeField] private float inputOffsetMs = 0f;

        [Tooltip("Log one [Score] line per judged note.")]
        [SerializeField] private bool logEachNote = true;

        public const string AttemptsFolder = "attempts";

        /// <summary>The attempt in progress or just finished; null before the
        /// first play and after a restart until play resumes.</summary>
        public ScoringSession Session { get; private set; }
        public bool AttemptSaved { get; private set; }
        public string LastSavedPath { get; private set; }

        /// <summary>Every judgement of whichever attempt is running, so views
        /// (slab and key colours) need not track which Session is current.
        /// noteIndex is an index into SongData.Notes.</summary>
        public event Action<JudgementResult> NoteJudged;
        /// <summary>Every wrong or extra press of the running attempt.</summary>
        public event Action<WrongNote> WrongNotePlayed;
        /// <summary>The attempt was dropped (restart or seek back) or a new
        /// one began: every note is unjudged again.</summary>
        public event Action AttemptReset;
        /// <summary>An attempt ended and was saved (completed or abandoned):
        /// the record as written, and the file it went to (null if the write
        /// failed). The results card listens.</summary>
        public event Action<AttemptRecord, string> AttemptEnded;

        /// <summary>Where attempt files go on this device.</summary>
        public static string AttemptsDirectory => Path.Combine(Application.persistentDataPath, AttemptsFolder);

        private SongData _song;
        private readonly List<(int pitch, double realtime)> _pending = new List<(int, double)>();
        private double _lastSongTime = double.NegativeInfinity;
        private int _lastWraps;
        private string _startedUtc;
        private bool _metronomeUsed;
        private string _path;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (transport == null) transport = FindAnyObjectByType<SongTransport>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (metronome == null) metronome = FindAnyObjectByType<Metronome>();
            if (current == null) current = FindAnyObjectByType<CurrentSong>();
            _song = current != null ? current.Song : null;
            PrepareClockFormatting();
        }

        /// <summary>The first local time on the headset took about 380 ms
        /// (measured 2026-11-05), which froze the first press of play when
        /// the first attempt's file name asked for it. Done on another thread
        /// at launch, nothing has to wait for it.</summary>
        private static void PrepareClockFormatting()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                DateTime.Now.ToString("yyyyMMdd-HHmmss");
                DateTime.UtcNow.ToString("o");
                Debug.Log($"[Score] clock formatting ready in {watch.ElapsedMilliseconds} ms");
            });
        }

        private void OnEnable()
        {
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn += OnNoteOn;
            if (current != null) current.SongChanged += OnSongChanged;
        }

        private void OnDisable()
        {
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn -= OnNoteOn;
            if (current != null) current.SongChanged -= OnSongChanged;
        }

        /// <summary>A different song or hand: whatever was running is over.</summary>
        private void OnSongChanged()
        {
            if (Session != null && !AttemptSaved && Session.JudgedCount + Session.WrongNotes.Count > 0)
                Save(AttemptRecord.Abandoned);
            Session = null;
            _pending.Clear();
            _song = current.Song;
            _lastSongTime = double.NegativeInfinity;
            AttemptReset?.Invoke();
        }

        private void OnNoteOn(int note, int velocity, double time)
        {
            if (clock == null || !clock.IsPlaying) return;
            // Mid-calibration, or the A that confirms the keys: not playing yet.
            if (calibration != null && (!calibration.IsPlacementValid || calibration.AwaitingConfirmation)) return;
            if (transport != null && transport.IsRestartChordNote(note) && !SongUses(note)) return;
            _pending.Add((note, MidiInput.Instance.ToRealtime(time)));
        }

        private void Update()
        {
            if (clock == null) return;
            Tick();
            // Read by the clock next frame. Always set, so nothing can leave
            // the clock held after the session that held it has gone.
            clock.HoldAt = Session != null && !AttemptSaved && Session.PitchOnly
                ? Session.NextDueTime
                : double.PositiveInfinity;
        }

        private void Tick()
        {
            double songTime = clock.SongTime;
            double tempo = clock.TempoMultiplier;

            // The loop wrapped: this pass is over. Presses still waiting were
            // made before the wrap, so they are timed from before it.
            int wraps = clock.Wraps - _lastWraps;
            _lastWraps = clock.Wraps;
            if (wraps > 0 && clock.Loop is LoopRange wrapped)
            {
                FinishPass(songTime + wraps * wrapped.PassLength, tempo);
                _lastSongTime = songTime;
            }

            // Restart (or any seek backwards): the attempt so far is over.
            if (songTime < _lastSongTime - 1e-6)
            {
                if (Session != null && !AttemptSaved && Session.JudgedCount + Session.WrongNotes.Count > 0)
                    Save(AttemptRecord.Abandoned);
                Session = null;
                _pending.Clear();
                AttemptReset?.Invoke();
            }
            _lastSongTime = songTime;

            if (calibration != null && calibration.AwaitingConfirmation)
            {
                _pending.Clear();
                if (Session != null && !AttemptSaved)
                {
                    bool progress = Session.JudgedCount + Session.WrongNotes.Count > 0;
                    if (progress) Save(AttemptRecord.Abandoned);
                    Debug.Log("[Score] keys awaiting confirmation: attempt " +
                              (progress ? "abandoned" : "dropped") + ", nothing scored until A");
                    Session = null;
                    AttemptReset?.Invoke();
                }
                return;
            }

            if (!clock.IsPlaying || tempo <= 0.0)
            {
                _pending.Clear();
                return;
            }

            if (Session != null && !AttemptSaved && transport != null && Session.PitchOnly != transport.WaitMode)
            {
                if (Session.JudgedCount + Session.WrongNotes.Count > 0) Save(AttemptRecord.Abandoned);
                Debug.Log($"[Score] wait mode {(transport.WaitMode ? "on" : "off")}: new attempt from here");
                Session = null;
                _pending.Clear();
            }

            if (Session == null) Begin(songTime, tempo);
            if (metronome != null && metronome.IsOn) _metronomeUsed = true;

            if (!AttemptSaved)
            {
                JudgePending(songTime, tempo);
                Session.Advance(songTime, tempo);

                if (Session.IsComplete) SaveFinished();
            }
            _pending.Clear();
        }

        private void JudgePending(double songTime, double tempo)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            foreach ((int pitch, double realtime) in _pending)
            {
                double press = songTime - (now - realtime) * tempo - inputOffsetMs / 1000.0 * tempo;
                // Lead-in noodling is not part of the attempt.
                if (press < Session.FirstNoteTime - NoteJudge.WindowSongSeconds(goodMs, tempo)) continue;
                Session.OnNoteOn(pitch, press, tempo);
            }
        }

        /// <summary>A finished attempt, unless it is a loop pass with nothing
        /// played in it, which is dropped.</summary>
        private void SaveFinished()
        {
            // No notes were left to score, as when the keys are confirmed after
            // the song has ended (a 0% card, 2026-11-05). Not an attempt.
            if (Session.ScorableCount == 0)
            {
                AttemptSaved = true;
                Debug.Log("[Score] no notes left to score: not an attempt");
                return;
            }
            if (clock.Loop.HasValue && Session.NotesHit == 0 && Session.WrongNotes.Count == 0)
            {
                AttemptSaved = true;
                Debug.Log("[Score] nothing played this pass: not saved");
                return;
            }
            Save(AttemptRecord.Completed);
        }

        /// <summary>The clock has just wrapped. Normally the pass already
        /// finished at its last note; if that note's window ran past the end
        /// of the bar, it is judged now.</summary>
        private void FinishPass(double songTimeBeforeWrap, double tempo)
        {
            if (Session != null && !AttemptSaved)
            {
                JudgePending(songTimeBeforeWrap, tempo);
                Session.Advance(double.PositiveInfinity, tempo);
                SaveFinished();
            }
            _pending.Clear();
            Session = null;
            AttemptReset?.Invoke();
        }

        /// <summary>Begins at the start of the song (the lead-in) after a
        /// restart, or part-way through when the keys were only just
        /// confirmed; either way only notes still hittable from now count.</summary>
        private void Begin(double songTime, double tempo)
        {
            PianoKeyLayout layout = calibration != null ? calibration.Layout : null;
            double scoreFrom = songTime - NoteJudge.WindowSongSeconds(goodMs, tempo);
            double scoreUntil = double.PositiveInfinity;
            if (clock.Loop is LoopRange loop)
            {
                scoreFrom = Math.Max(scoreFrom, loop.NotesFrom);
                scoreUntil = loop.NotesUntil;
            }
            Session = new ScoringSession(_song, new TimingWindows(perfectMs, greatMs, goodMs),
                                         layout != null ? (Func<int, bool>)layout.Contains : null,
                                         scoreFrom, scoreUntil,
                                         transport != null && transport.WaitMode,
                                         current != null ? current.Hand : PracticeHand.All);
            AttemptReset?.Invoke();
            Session.Judged += r => NoteJudged?.Invoke(r);
            Session.WrongNotePlayed += w => WrongNotePlayed?.Invoke(w);
            if (logEachNote)
            {
                Session.Judged += r => Debug.Log(
                    $"[Score] bar {r.bar} {MidiInput.NoteName(r.pitch)} {r.rating.ToUpperInvariant()}" +
                    (r.pressed ? $" {r.errorMs:+0;-0;0} ms" : ""));
                Session.WrongNotePlayed += w => Debug.Log(
                    $"[Score] bar {w.bar} WRONG {MidiInput.NoteName(w.pressedPitch)}" +
                    (w.expectedPitch >= 0 ? $" (expected {MidiInput.NoteName(w.expectedPitch)}, {w.errorMs:+0;-0;0} ms)" : " (nothing due)"));
            }

            AttemptSaved = false;
            _metronomeUsed = false;
            _startedUtc = DateTime.UtcNow.ToString("o");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _path = Path.Combine(AttemptsDirectory, $"{stamp}-{Slug(_song.Title)}.json");
            Debug.Log($"[Score] attempt started at song time {songTime:0.0}: '{_song.Title}', {Session.ScorableCount} notes, " +
                      $"windows {perfectMs:0}/{greatMs:0}/{goodMs:0} ms, offset {inputOffsetMs:0} ms, tempo {clock.TempoMultiplier:0.##}");
        }

        /// <summary>Backgrounded or closed mid-attempt: write what there is.
        /// The same file is rewritten if the attempt then goes on.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && Session != null && !AttemptSaved && Session.JudgedCount > 0) Write(AttemptRecord.Abandoned);
        }

        private void Save(string outcome)
        {
            AttemptSaved = true;
            AttemptRecord rec = Write(outcome);

            (double mean, double sd, int n) = Session.ErrorStats();
            BarStats worst = Session.WorstBar();
            Debug.Log($"[Score] attempt {outcome}: score {Session.Score}, accuracy {Session.Accuracy:0.0}%, " +
                      $"perfect {Session.Perfect} great {Session.Great} good {Session.Good} miss {Session.Miss}, " +
                      $"wrong {Session.WrongPitchCount} extra {Session.ExtraCount}, " +
                      $"mean error {mean:+0.0;-0.0;0} ms (sd {sd:0.0}, n {n})" +
                      (worst != null ? $", worst bar {worst.bar} ({worst.accuracy:0}%)" : "") +
                      $" -> {LastSavedPath ?? "not saved"}");

            if (rec != null) AttemptEnded?.Invoke(rec, LastSavedPath == _path ? _path : null);
        }

        private AttemptRecord Write(string outcome)
        {
            AttemptRecord rec = null;
            try
            {
                // The slowest tempo it was played at: a tempo change while
                // paused keeps the attempt going rather than restarting it.
                double tempo = double.IsInfinity(Session.LowestTempo) ? clock.TempoMultiplier : Session.LowestTempo;
                rec = Session.ToRecord(outcome, _startedUtc, DateTime.UtcNow.ToString("o"),
                                                     tempo, inputOffsetMs);
                rec.displayLatencyMs = clock.DisplayLatencySeconds * 1000.0;
                rec.metronome = _metronomeUsed;
                if (clock.Loop is LoopRange loop)
                {
                    rec.loopFromBar = loop.FromBar;
                    rec.loopToBar = loop.ToBar;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(_path, JsonUtility.ToJson(rec, true));
                LastSavedPath = _path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Score] attempt not saved to {_path}: {e.Message}");
            }
            return rec;
        }

        private bool SongUses(int pitch)
        {
            PracticeHand hand = current != null ? current.Hand : PracticeHand.All;
            foreach (SongNote n in _song.Notes) if (n.Pitch == pitch && n.BelongsTo(hand)) return true;
            return false;
        }

        private static string Slug(string title)
        {
            var chars = new List<char>();
            foreach (char c in title.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) chars.Add(c);
                else if (chars.Count > 0 && chars[chars.Count - 1] != '-') chars.Add('-');
            }
            return new string(chars.ToArray()).Trim('-');
        }
    }
}
