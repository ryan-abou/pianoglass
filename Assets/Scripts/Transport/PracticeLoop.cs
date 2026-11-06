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
        public int Pass => IsActive && clock != null ? clock.Wraps - _wrapsAtStart + 1 : 0;
        public bool IsActive => clock != null && clock.Loop.HasValue;
        public LoopRange? Range => clock != null ? clock.Loop : null;
        /// <summary>How the last saved pass went and what it did to the tempo
        /// (AutoTempo.Describe); null before one.</summary>
        public string LastPassLine { get; private set; }

        private int _wrapsAtStart;
        private int _lastWraps;
        private float _nextTempo = -1f;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (transport == null) transport = FindAnyObjectByType<SongTransport>();
            if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();
            if (resultsCard == null) resultsCard = FindAnyObjectByType<ResultsCard>();
        }

        private void OnEnable()
        {
            if (resultsCard != null) resultsCard.LoopRequested += StartLoop;
            if (scoreKeeper != null) scoreKeeper.AttemptEnded += OnAttemptEnded;
        }

        private void OnDisable()
        {
            if (resultsCard != null) resultsCard.LoopRequested -= StartLoop;
            if (scoreKeeper != null) scoreKeeper.AttemptEnded -= OnAttemptEnded;
        }

        public void StartLoop(int fromBar, int toBar)
        {
            if (clock == null || transport == null) return;
            if (!transport.StartLoop(fromBar, toBar)) return;
            _wrapsAtStart = _lastWraps = clock.Wraps;
            LastPassLine = null;
            _nextTempo = -1f;
            Debug.Log($"[Loop] looping {clock.Loop.Value.Label}");
        }

        private void OnAttemptEnded(AttemptRecord rec, string path)
        {
            if (!rec.IsLoop || rec.outcome != AttemptRecord.Completed) return;
            float tempo = clock.TempoMultiplier;
            // Wait mode has no timing, so its accuracy says nothing about tempo.
            bool wait = rec.mode == AttemptRecord.Wait;
            _nextTempo = wait ? tempo : AutoTempo.Next(tempo, rec.accuracy);
            LastPassLine = wait ? $"{rec.accuracy:0}% of keys right" : AutoTempo.Describe(tempo, _nextTempo, rec.accuracy);
            Debug.Log($"[Loop] pass {Pass}: {rec.accuracy:0.0}% -> {LastPassLine}");
        }

        private void Update()
        {
            if (clock == null) return;
            if (clock.Wraps == _lastWraps) return;
            _lastWraps = clock.Wraps;
            if (clock.Loop.HasValue && _nextTempo > 0f) clock.TempoMultiplier = _nextTempo;
            _nextTempo = -1f;
        }
    }
}
