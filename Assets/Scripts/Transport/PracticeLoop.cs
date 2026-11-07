using UnityEngine;
using PianoGlass.Scoring;
using PianoGlass.Song;

namespace PianoGlass.Transport
{
    /// <summary>
    /// The practice loop as the player meets it (F33): the results card's
    /// "Loop bar 3" button starts it, the clock wraps by itself, and each
    /// pass is saved as its own attempt by ScoreKeeper. Restarting from the
    /// top (left Y, or the A0 + A#0 chord) is how a loop is left.
    ///
    /// This tracks what the panel shows: which pass, and how the last one
    /// went. The results card does not come up between passes, since it would
    /// cover the next one; the status panel carries the feedback.
    ///
    /// Auto-tempo (F35): after each saved pass AutoTempo picks the next pass's
    /// tempo, applied at the wrap so the rest of this pass keeps its speed. A
    /// pass nobody played in is not saved, so it changes nothing. These tempos
    /// are not saved: leaving the loop goes back to the tempo the player chose.
    ///
    /// Memory fade (F36): this moves the clock's MemoryFade ladder at each wrap
    /// and feeds it what happens during a pass; with it on, AutoTempo.NextWithFade
    /// keeps tempo and fade from changing together. Outside a loop the same rules
    /// move the song's own ladder once per play-through, and it is saved per song.
    ///
    /// After ScoreKeeper (100), which finishes a pass at the wrap if its last
    /// note's window ran past the end of the bar.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class PracticeLoop : MonoBehaviour
    {
        [SerializeField] private SongClock clock;
        [SerializeField] private SongTransport transport;
        [SerializeField] private ScoreKeeper scoreKeeper;
        [SerializeField] private ResultsCard resultsCard;

        /// <summary>1 for the first time through; counts up at every wrap.</summary>
        public int Pass => clock != null ? clock.LoopPass : 0;
        public bool IsActive => clock != null && clock.Loop.HasValue;
        public LoopRange? Range => clock != null ? clock.Loop : null;
        /// <summary>How the last saved pass went and what it did to the tempo
        /// (AutoTempo.Describe); null before one.</summary>
        public string LastPassLine { get; private set; }
        /// <summary>What the last whole play-through did to the song's fade.</summary>
        public string LastSongFadeLine { get; private set; }

        private int _lastWraps;
        private float _nextTempo = -1f;
        private bool _passPlayed;
        private double _passKeysRight;
        private float _heldFor;
        private string _songFadeKey;
        private string _cardLine;
        private readonly WaitRecall _recall = new WaitRecall();

        private const string SongFadePrefsPrefix = "PianoGlass.SongFade.";

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (transport == null) transport = FindAnyObjectByType<SongTransport>();
            if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();
            if (resultsCard == null) resultsCard = FindAnyObjectByType<ResultsCard>();
        }

        private void Start()
        {
            if (clock == null || transport == null || transport.Song == null) return;
            _songFadeKey = SongFadePrefsPrefix + transport.Song.Title;
            clock.SongFade = new MemoryFade(PlayerPrefs.GetInt(_songFadeKey, 0));
        }

        private void OnEnable()
        {
            if (resultsCard != null) resultsCard.LoopRequested += StartLoop;
            if (scoreKeeper != null)
            {
                scoreKeeper.AttemptEnded += OnAttemptEnded;
                scoreKeeper.NoteJudged += OnNoteJudged;
                scoreKeeper.WrongNotePlayed += OnWrongNote;
                scoreKeeper.AttemptReset += _recall.Reset;
            }
        }

        private void OnDisable()
        {
            if (resultsCard != null) resultsCard.LoopRequested -= StartLoop;
            if (scoreKeeper != null)
            {
                scoreKeeper.AttemptEnded -= OnAttemptEnded;
                scoreKeeper.NoteJudged -= OnNoteJudged;
                scoreKeeper.WrongNotePlayed -= OnWrongNote;
                scoreKeeper.AttemptReset -= _recall.Reset;
            }
        }

        public void StartLoop(int fromBar, int toBar)
        {
            if (clock == null || transport == null) return;
            if (!transport.StartLoop(fromBar, toBar)) return;
            _lastWraps = clock.Wraps;
            LastPassLine = null;
            _nextTempo = -1f;
            _passPlayed = false;
            _heldFor = 0f;
            Debug.Log($"[Loop] looping {clock.Loop.Value.Label}");
        }

        /// <summary>What moves the ladder for this attempt, and how to say it. Wait
        /// mode has no clock, so right keys alone can be had by trying them all;
        /// it counts what was played from memory.</summary>
        private (double percent, string words) FadeScore(AttemptRecord rec)
        {
            if (rec.mode == AttemptRecord.Wait)
            {
                _recall.Finish();
                if (_recall.Total > 0) return (_recall.Percent, $"{_recall.Clean} of {_recall.Total} from memory");
            }
            return (rec.KeysRightPercent, $"{rec.KeysRightPercent:0}% keys right");
        }

        /// <summary>A whole play-through moves the song's ladder. An abandoned
        /// one does not: it never reached the end to be judged.</summary>
        private void MoveSongFade(AttemptRecord rec)
        {
            MemoryFade fade = clock.SongFade;
            bool played = rec.notesHit + rec.wrongPitch + rec.extra > 0;
            (double score, string words) = FadeScore(rec);
            string after = fade.Describe(fade.StepAfter(score, played));
            LastSongFadeLine = played ? $"{words}: {after}" : $"nothing played: {after}";
            fade.EndPass(score, played);
            PlayerPrefs.SetInt(_songFadeKey, fade.Step);
            PlayerPrefs.Save();
            _cardLine = played ? LastSongFadeLine : after;
            Debug.Log($"[Fade] song: {LastSongFadeLine}");
        }

        private void OnAttemptEnded(AttemptRecord rec, string path)
        {
            if (rec.outcome != AttemptRecord.Completed) return;
            if (!rec.IsLoop)
            {
                if (clock.IsFading) MoveSongFade(rec);
                return;
            }
            float tempo = clock.TempoMultiplier;
            // Wait mode has no timing, so its accuracy says nothing about tempo.
            bool wait = rec.mode == AttemptRecord.Wait;
            _passPlayed = true;
            _passKeysRight = rec.KeysRightPercent;
            string fadeWords = "";
            if (clock.IsFading) (_passKeysRight, fadeWords) = FadeScore(rec);

            if (clock.IsFading)
            {
                MemoryFade fade = clock.Fade;
                _nextTempo = wait ? tempo : AutoTempo.NextWithFade(tempo, rec.accuracy, fade);
                LastPassLine = TempoStep.ToPercent(_nextTempo) != TempoStep.ToPercent(tempo)
                    ? AutoTempo.Describe(tempo, _nextTempo, rec.accuracy)
                    : $"{fadeWords}: {fade.Describe(fade.StepAfter(_passKeysRight, true))}";
            }
            else
            {
                _nextTempo = wait ? tempo : AutoTempo.Next(tempo, rec.accuracy);
                LastPassLine = wait ? $"{rec.accuracy:0}% of keys right" : AutoTempo.Describe(tempo, _nextTempo, rec.accuracy);
            }
            Debug.Log($"[Loop] pass {Pass}: {rec.accuracy:0.0}%, keys right {_passKeysRight:0.0}% -> {LastPassLine}");
        }

        private void OnNoteJudged(JudgementResult result)
        {
            if (clock == null || !clock.IsFading) return;
            if (result.IsHit) clock.Fade.OnNoteRight();
            else clock.Fade.OnMistake();
        }

        private void OnWrongNote(WrongNote wrong)
        {
            if (clock == null || !clock.IsFading) return;
            if (transport != null && transport.WaitMode)
            {
                _recall.NeededHelp();
                if (clock.Fade.ShownStep == MemoryFade.Glimpse)
                {
                    clock.Fade.Flash(clock.HoldAt, Time.unscaledTimeAsDouble);
                    return;
                }
            }
            clock.Fade.OnMistake();
        }

        /// <summary>The pass that just wrapped moves the ladder. A pass nobody
        /// played in has no record and leaves it where it is.</summary>
        private void MoveFade()
        {
            if (!clock.IsFading) return;
            MemoryFade fade = clock.Fade;
            if (!_passPlayed) LastPassLine = $"nothing played: {fade.Describe(fade.StepAfter(0.0, false))}";
            fade.EndPass(_passKeysRight, _passPlayed);
            Debug.Log($"[Loop] {MemoryFade.Label(fade.Step)}");
        }

        private void Update()
        {
            if (clock == null) return;

            // The card came up in the same event, in whichever order.
            if (_cardLine != null)
            {
                if (resultsCard != null && resultsCard.IsShowing) resultsCard.AddToSubLine(_cardLine);
                _cardLine = null;
            }

            // The hold moved on: a new stop to judge, and a new wait.
            if (clock.IsFading && transport != null && transport.WaitMode &&
                !double.IsPositiveInfinity(clock.HoldAt) && _recall.AtStop(clock.HoldAt))
                _heldFor = 0f;

            // Wait mode: nothing to press, nothing to see. Do not leave them there.
            if (clock.IsFading && clock.IsHolding)
            {
                bool glimpse = clock.Fade.ShownStep == MemoryFade.Glimpse;
                _heldFor += Time.unscaledDeltaTime;
                if (_heldFor >= (glimpse ? MemoryFade.GlimpseStuckSeconds : MemoryFade.StuckSeconds))
                {
                    _heldFor = 0f;
                    _recall.NeededHelp();
                    if (glimpse) clock.Fade.Reveal(clock.HoldAt, Time.unscaledTimeAsDouble);
                    else clock.Fade.AskForHelp();
                }
            }
            else _heldFor = 0f;

            if (clock.Wraps == _lastWraps) return;
            _lastWraps = clock.Wraps;
            if (clock.Loop.HasValue) MoveFade();
            if (clock.Loop.HasValue && _nextTempo > 0f) clock.TempoMultiplier = _nextTempo;
            _nextTempo = -1f;
            _passPlayed = false;
        }
    }
}
