using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PianoGlass.Midi;
using PianoGlass.Piano;
using PianoGlass.Scoring;
using PianoGlass.Song;
using PianoGlass.Transport;
using PianoGlass.UI;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// The whole song flow at the desk, with no headset or keyboard: the
    /// picker, a perfect run, wait mode, a time signature other than 4/4, your
    /// own files, and remembering the pick. One [SongCheck] line per step,
    /// ending with PASS or FAIL.
    ///
    /// Menu: PianoGlass/Checks/Run song check. It enters Play Mode by itself.
    /// With no editor open, RunHeadless does the same and exits with 0 or 1.
    ///
    /// Unity won't attach a script from an Editor folder to a scene object, so
    /// this steps its coroutines itself from the editor's update loop.
    /// </summary>
    public class SongCheckRunner
    {
        private const string StageKey = "PianoGlass.SongCheck.Stage";
        private const string HeadlessKey = "PianoGlass.SongCheck.Headless";

        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string GuessedFile = "zz-check-guessed.mid";
        private const string BrokenFile = "zz-check-broken.mid";
        private const float CheckTempo = 2f;
        private const float WaitLimitSeconds = 90f;

        private static string OwnFile(string name) => Path.Combine(CurrentSong.SongsFolder, name);

        [MenuItem("PianoGlass/Checks/Run song check")]
        public static void Run()
        {
            WriteOwnFiles();
            SessionState.SetInt(StageKey, 1);
            if (EditorApplication.isPlaying)
            {
                StartStage();
                return;
            }

            // A batchmode editor has no scene open at all.
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>For -executeMethod with no -quit.</summary>
        public static void RunHeadless()
        {
            SessionState.SetBool(HeadlessKey, true);
            Run();
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode) StartStage();
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (stage == 2) EditorApplication.delayCall += EditorApplication.EnterPlaymode;
                else Finish(stage == 3);
            }
        }

        private static SongCheckRunner _running;

        private static void StartStage()
        {
            _running?.Stop();
            _running = new SongCheckRunner();
            _running.Begin();
        }

        private void Begin()
        {
            Application.logMessageReceived += OnLog;
            _stack.Push(Steps());
            EditorApplication.update += Tick;
        }

        private void Stop()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
        }

        /// <summary>One step of whichever coroutine is running. A coroutine
        /// that hands back another runs that one first.</summary>
        private void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Stop();
                return;
            }

            while (_stack.Count > 0)
            {
                IEnumerator top = _stack.Peek();
                if (!top.MoveNext())
                {
                    _stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    _stack.Push(nested);
                    continue;
                }
                return;
            }
            Stop();
        }

        private static void WriteOwnFiles()
        {
            Directory.CreateDirectory(CurrentSong.SongsFolder);

            var low = new List<StarterSongWriter.Note>();
            var high = new List<StarterSongWriter.Note>();
            for (int beat = 0; beat < 16; beat++)
            {
                low.Add(new StarterSongWriter.Note(48, beat, 1.0));
                high.Add(new StarterSongWriter.Note(72, beat, 1.0));
            }

            byte[] bytes = StarterSongWriter.Encode(
                null, new[] { new StarterSongWriter.Tempo(0.0, 120), new StarterSongWriter.Tempo(8.0, 80) }, 4,
                ((string)null, low), ((string)null, high));
            File.WriteAllBytes(OwnFile(GuessedFile), bytes);
            File.WriteAllBytes(OwnFile(BrokenFile), new byte[] { 1, 2, 3, 4 });
        }

        private static void RemoveOwnFiles()
        {
            File.Delete(OwnFile(GuessedFile));
            File.Delete(OwnFile(BrokenFile));
        }

        private static void Finish(bool passed)
        {
            SessionState.SetInt(StageKey, 0);
            RemoveOwnFiles();
            if (SessionState.GetBool(HeadlessKey, false))
            {
                SessionState.SetBool(HeadlessKey, false);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        // ------------------------------------------------------------------

        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private readonly List<string> _log = new List<string>();
        private CurrentSong _current;
        private SongClock _clock;
        private SongTransport _transport;
        private ScoreKeeper _score;
        private ResultsCard _card;
        private SongPicker _picker;
        private KeyboardCalibration _calibration;
        private MidiEditorSimulator _simulator;
        private string _failed;

        private void OnLog(string message, string stack, LogType type) => _log.Add(message);

        private IEnumerator Steps()
        {
            yield return null;
            yield return null;
            FindScene();

            yield return SessionState.GetInt(StageKey, 1) == 2 ? Restored() : Sequence();

            if (_failed != null)
            {
                Debug.LogError($"[SongCheck] FAIL {_failed}");
                SessionState.SetInt(StageKey, 4);
                EditorApplication.ExitPlaymode();
            }
        }

        private void FindScene()
        {
            _current = UnityEngine.Object.FindAnyObjectByType<CurrentSong>();
            _clock = UnityEngine.Object.FindAnyObjectByType<SongClock>();
            _transport = UnityEngine.Object.FindAnyObjectByType<SongTransport>();
            _score = UnityEngine.Object.FindAnyObjectByType<ScoreKeeper>();
            _card = UnityEngine.Object.FindAnyObjectByType<ResultsCard>();
            _picker = UnityEngine.Object.FindAnyObjectByType<SongPicker>();
            _calibration = UnityEngine.Object.FindAnyObjectByType<KeyboardCalibration>();
            _simulator = UnityEngine.Object.FindAnyObjectByType<MidiEditorSimulator>();
            if (_simulator == null) _simulator = new GameObject("MidiSimulator").AddComponent<MidiEditorSimulator>();
        }

        private void Ok(string text) => Debug.Log($"[SongCheck] {text}");

        private bool Fail(string step, string why)
        {
            _failed = $"{step}: {why}";
            return false;
        }

        private bool Check(bool condition, string step, string why) => condition || Fail(step, why);

        private bool Logged(string part)
        {
            foreach (string line in _log)
                if (line.Contains(part)) return true;
            return false;
        }

        private IEnumerator WaitFor(Func<bool> condition, string step, string what)
        {
            float started = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - started > WaitLimitSeconds)
                {
                    Fail(step, $"timed out waiting for {what}");
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator Calibrate()
        {
            yield return WaitFor(() => _calibration.CurrentStage != KeyboardCalibration.Stage.Idle, "calibrate", "calibration to start");
            if (_failed != null) yield break;

            _calibration.Recalibrate();
            yield return WaitFor(() => _calibration.CurrentStage == KeyboardCalibration.Stage.AwaitingLowest, "calibrate", "the first touch");
            if (_failed != null) yield break;

            _calibration.PointerTip.position = new Vector3(-0.6f, 0.75f, 0.6f);
            _calibration.CapturePoint();
            yield return null;
            _calibration.PointerTip.position = new Vector3(0.6f, 0.75f, 0.6f);
            _calibration.CapturePoint();

            yield return WaitFor(() => _calibration.AwaitingConfirmation || !_calibration.NeedsPlayer, "calibrate", "the keys to be placed");
            if (_failed != null) yield break;
            if (_calibration.AwaitingConfirmation) _calibration.Confirm();
            yield return WaitFor(() => !_calibration.NeedsPlayer, "calibrate", "the keys to be confirmed");
        }

        private IEnumerator PlayThrough(string step, int mistakes = 0, int skip = 0, int max = int.MaxValue)
        {
            _transport.RestartAndPlay();
            yield return null;
            _simulator.StartAutoplay(mistakes, skip, max);
            yield return WaitFor(() => _score.Session != null && _score.AttemptSaved, step, "the attempt to end");
        }

        private IEnumerator Sequence()
        {
            // 1. Launch.
            yield return Calibrate();
            if (_failed != null) yield break;
            yield return WaitFor(() => _picker.IsShowing, "1 launch", "the picker to open");
            if (_failed != null) yield break;
            yield return null;
            if (!Check(Logged("[Songs] 3 built-in, 2 own"), "1 launch", "no '[Songs] 3 built-in, 2 own' line")) yield break;
            if (!Check(_current.Row != null, "1 launch", "nothing restored")) yield break;
            Ok($"1 launch: picker open on '{_current.Song.Title}'");

            if (_transport.WaitMode) _transport.ToggleWaitMode();
            _clock.TempoMultiplier = CheckTempo;

            // 2. Pick Ode to Joy, Right.
            _picker.PressHandForCheck(PracticeHand.Right);
            if (!Check(_picker.PressSongForCheck("Ode to Joy"), "2 pick", "no Ode to Joy row")) yield break;
            yield return null;
            if (!Check(Logged("[Songs] picked 'Ode to Joy' right, 16 bars, 4/4, 120 BPM"), "2 pick", "no picked line")) yield break;
            if (!Check(!_picker.IsShowing, "2 pick", "the picker stayed open")) yield break;
            if (!Check(!_clock.IsPlaying && _clock.SongTime < 0.0, "2 pick", "not stopped at the top")) yield break;
            Ok("2 pick: Ode to Joy, right hand");

            // 3. A perfect run.
            yield return PlayThrough("3 autoplay");
            if (_failed != null) yield break;
            if (!Check(Logged("'Ode to Joy', 62 notes"), "3 autoplay", "no attempt started with 62 notes")) yield break;
            yield return WaitFor(() => _card.IsShowing, "3 autoplay", "the results card");
            if (_failed != null) yield break;
            if (!Check(_card.Last.AccuracyText == "100%", "3 autoplay", $"results card says {_card.Last.AccuracyText}")) yield break;
            string json = File.ReadAllText(_score.LastSavedPath);
            if (!Check(json.Contains("\"hand\": \"right\""), "3 autoplay", "attempt file doesn't say right hand")) yield break;
            Ok("3 autoplay: 100%, attempt file says right hand");

            // 4. Wait mode holds at the fifth right-hand note.
            _clock.Pause();
            _transport.ToggleWaitMode();
            yield return PlayFirstNotes("4 wait", 4);
            if (_failed != null) yield break;
            yield return new WaitForSecondsRealtime(0.5f);
            double due = NthRightHandNote(4).StartTime;
            if (!Check(_clock.IsHolding && Math.Abs(_clock.SongTime - due) < 1e-6, "4 wait", $"not holding at {due}: at {_clock.SongTime}")) yield break;
            List<int> waiting = _score.Session.WaitingPitches();
            if (!Check(waiting.Count == 1 && waiting[0] == NthRightHandNote(4).Pitch, "4 wait", "waiting on the wrong key")) yield break;
            if (!Check(_score.Session.ScorableCount == 62, "4 wait", "the left hand is being waited for")) yield break;
            Ok("4 wait: holding at note 5 of the right hand");

            _simulator.StartAutoplay(0, 4);
            yield return WaitFor(() => _score.AttemptSaved, "4 wait", "the wait-mode attempt to end");
            if (_failed != null) yield break;
            yield return WaitFor(() => _card.IsShowing, "4 wait", "the results card");
            if (_failed != null) yield break;

            // 5. Songs button, then the Minuet's left hand.
            _card.PressSongsForCheck();
            yield return WaitFor(() => _picker.IsShowing, "5 minuet", "the picker to open from Songs");
            if (_failed != null) yield break;
            yield return null; // the card notices on its own next update
            yield return null;
            if (!Check(!_card.IsShowing, "5 minuet", "the card stayed up under the picker")) yield break;
            _picker.PressHandForCheck(PracticeHand.Left);
            if (!Check(_picker.PressSongForCheck("Minuet"), "5 minuet", "no Minuet row")) yield break;
            yield return null;
            if (!Check(_current.Hand == PracticeHand.Left, "5 minuet", "not the left hand")) yield break;
            if (!Check(_current.Song.BeatsPerBar == 3 && _current.Song.BarCount == 32, "5 minuet",
                       $"{_current.Song.BeatsPerBar} beats, {_current.Song.BarCount} bars")) yield break;
            _transport.ToggleWaitMode();
            yield return PlayThrough("5 minuet");
            if (_failed != null) yield break;
            if (!Check(_score.Session.ScorableCount == 75, "5 minuet", $"{_score.Session.ScorableCount} notes")) yield break;
            yield return WaitFor(() => _card.IsShowing, "5 minuet", "the results card");
            if (_failed != null) yield break;
            if (!Check(_card.Last.AccuracyText == "100%", "5 minuet", $"results card says {_card.Last.AccuracyText}")) yield break;
            if (!Check(Logged("3/4 at 120 BPM"), "5 minuet", "the grid never drew 3/4")) yield break;
            Ok("5 minuet: 3 beats a bar, 32 bars, 100% over 75 notes");

            // 6. Own files.
            _card.PressSongsForCheck();
            yield return WaitFor(() => _picker.IsShowing, "6 own files", "the picker");
            if (_failed != null) yield break;

            SongRow guessed = _current.Library.Find(SongLibrary.KeyFor(GuessedFile, false));
            SongRow broken = _current.Library.Find(SongLibrary.KeyFor(BrokenFile, false));
            if (!Check(guessed != null && guessed.Detail.Contains("Hands guessed"), "6 own files", "no Hands guessed row")) yield break;
            if (!Check(broken != null && !broken.CanLoad && broken.Detail == "Not a MIDI file", "6 own files", "broken row isn't greyed with its reason")) yield break;

            TempoMap map = guessed.Song.TempoMap;
            foreach (SongNote note in guessed.Song.Notes)
            {
                BeatLine line = map.Beat(map.FirstBeatAtOrAfter(note.StartTime - 1e-6));
                if (!Check(Math.Abs(line.Time - note.StartTime) < 1e-6, "6 own files", $"a bar line is off its note at {note.StartTime}")) yield break;
            }
            if (!Check(guessed.Song.BarCount == 4, "6 own files", $"{guessed.Song.BarCount} bars")) yield break;
            Ok("6 own files: guessed row, bar lines on the notes, broken row greyed");

            _picker.Close();
            yield return null;

            // 7. Stop and play again.
            SessionState.SetInt(StageKey, 2);
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator PlayFirstNotes(string step, int count)
        {
            _transport.RestartAndPlay();
            yield return null;
            _simulator.StartAutoplay(0, 0, count);
            yield return WaitFor(() => _score.Session != null && _score.Session.JudgedCount == count, step, $"{count} notes to be judged");
        }

        private SongNote NthRightHandNote(int n)
        {
            int seen = 0;
            foreach (SongNote note in _current.Song.Notes)
                if (note.BelongsTo(PracticeHand.Right) && seen++ == n) return note;
            throw new ArgumentOutOfRangeException(nameof(n));
        }

        private IEnumerator Restored()
        {
            yield return null;
            if (!Check(_current.Song.Title.Contains("Minuet") && _current.Hand == PracticeHand.Left, "7 restored",
                       $"came back as '{_current.Song.Title}' {_current.Hand}")) yield break;
            Ok("7 restored: the Minuet, left hand");
            Debug.Log("[SongCheck] PASS");
            SessionState.SetInt(StageKey, 3);
            EditorApplication.ExitPlaymode();
        }
    }
}
