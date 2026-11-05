using UnityEngine;
using PianoGlass.Scoring;
using PianoGlass.Song;

namespace PianoGlass.Transport
{
    /// <summary>
    /// The practice loop as the player meets it (F33): the results card's
    /// "Loop bars 2-4" button starts it, the clock wraps by itself, and each
    /// pass is saved as its own attempt by ScoreKeeper. Restarting from the
    /// top (left Y, or the A0 + A#0 chord) is how a loop is left.
    ///
    /// This only tracks what the panel shows: which pass, and how the last
    /// one went. The results card does not come up between passes, since it
    /// would cover the next one; the status panel carries the feedback.
    /// </summary>
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
        /// <summary>Accuracy of the last saved pass of this loop; negative before one.</summary>
        public double LastAccuracy { get; private set; } = -1.0;

        private int _wrapsAtStart;

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
            _wrapsAtStart = clock.Wraps;
            LastAccuracy = -1.0;
            Debug.Log($"[Loop] looping {clock.Loop.Value.Label}");
        }

        private void OnAttemptEnded(AttemptRecord rec, string path)
        {
            if (!rec.IsLoop || rec.outcome != AttemptRecord.Completed) return;
            LastAccuracy = rec.accuracy;
            Debug.Log($"[Loop] pass {Pass}: {rec.accuracy:0.0}%");
        }
    }
}
