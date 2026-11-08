using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.Midi
{
    /// <summary>
    /// Fake keyboard for working in the Unity editor without deploying.
    /// Deliberately uses OnGUI buttons rather than keyboard input, so it works
    /// whether your project uses the old Input Manager, the new Input System,
    /// or both — one less thing to configure.
    ///
    /// Editor only: it compiles out of device builds entirely.
    /// </summary>
    public class MidiEditorSimulator : MonoBehaviour
    {
#if UNITY_EDITOR
        [Tooltip("Lowest note the on-screen buttons play. 60 = middle C.")]
        [SerializeField] private int firstNote = 60;
        [SerializeField] private int buttonCount = 13;
        [SerializeField] private bool showButtons = true;

        private void OnGUI()
        {
            if (!showButtons || MidiInput.Instance == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 720, 120), GUI.skin.box);
            GUILayout.Label("MIDI simulator (editor only) — click and hold a key");
            GUILayout.BeginHorizontal();

            for (int i = 0; i < buttonCount; i++)
            {
                int note = firstNote + i;
                bool held = MidiInput.Instance.IsHeld(note);

                GUI.backgroundColor = held ? Color.green
                                   : IsBlackKey(note) ? Color.gray : Color.white;

                GUILayout.Button(MidiInput.NoteName(note), GUILayout.Width(48), GUILayout.Height(48));

                // Rect-based press/release so notes behave like a real key.
                Rect r = GUILayoutUtility.GetLastRect();
                bool over = r.Contains(Event.current.mousePosition);
                bool mouseDown = Event.current.type == EventType.MouseDown && over;
                bool mouseUp = Event.current.type == EventType.MouseUp;

                if (mouseDown && !held) MidiInput.Instance.InjectNoteOn(note);
                if (mouseUp && held) MidiInput.Instance.InjectNoteOff(note);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---- Autoplay: presses your hand's notes at their song times ------

        private readonly List<(double time, int pitch, bool down)> _script =
            new List<(double, int, bool)>();
        private int _nextStep;
        private double _lastSongTime;
        private SongClock _clock;

        public bool IsAutoplaying { get; private set; }

        [ContextMenu("Autoplay my hand (perfect)")]
        private void AutoplayPerfect() => RestartThenAutoplay(0);

        [ContextMenu("Autoplay with 3 mistakes")]
        private void AutoplayMistakes() => RestartThenAutoplay(3);

        private void RestartThenAutoplay(int mistakes)
        {
            var transport = FindAnyObjectByType<SongTransport>();
            if (transport == null) return;
            transport.RestartAndPlay();
            StartAutoplay(mistakes);
        }

        /// <summary>Follows the song clock, so it keeps to the tempo setting.
        /// Use while the clock is running or about to.</summary>
        /// <param name="mistakes">Notes played one key too high instead.</param>
        /// <param name="skip">Your first notes to leave alone.</param>
        /// <param name="max">How many notes to play.</param>
        public void StartAutoplay(int mistakes = 0, int skip = 0, int max = int.MaxValue)
        {
            var current = FindAnyObjectByType<CurrentSong>();
            _clock = FindAnyObjectByType<SongClock>();
            if (current == null || _clock == null) return;

            var mine = new List<SongNote>();
            foreach (SongNote note in current.Song.Notes)
                if (note.BelongsTo(current.Hand)) mine.Add(note);
            mine.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            int end = max == int.MaxValue ? mine.Count : Mathf.Min(mine.Count, skip + max);
            int played = Mathf.Max(0, end - skip);
            var wrongAt = new HashSet<int>();
            for (int m = 1; m <= mistakes; m++) wrongAt.Add(skip + played * m / (mistakes + 1));

            _script.Clear();
            for (int i = skip; i < end; i++)
            {
                SongNote note = mine[i];
                int pitch = wrongAt.Contains(i) ? note.Pitch + (note.Pitch < 108 ? 1 : -1) : note.Pitch;
                _script.Add((note.StartTime, pitch, true));
                _script.Add((Mathf.Max((float)note.StartTime + 0.01f, (float)note.EndTime - 0.02f), pitch, false));
            }

            // Releases first, so a repeated key is up before it goes down again.
            _script.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.down.CompareTo(b.down));
            _nextStep = 0;
            _lastSongTime = _clock.SongTime;
            IsAutoplaying = _script.Count > 0;
            Debug.Log($"[Autoplay] {played} notes, {mistakes} mistakes");
        }

        private void Update()
        {
            if (!IsAutoplaying) return;
            if (_clock == null || MidiInput.Instance == null || _clock.SongTime < _lastSongTime - 1e-6)
            {
                IsAutoplaying = false;
                return;
            }

            _lastSongTime = _clock.SongTime;
            while (_nextStep < _script.Count && _script[_nextStep].time <= _lastSongTime + 1e-6)
            {
                (double _, int pitch, bool down) = _script[_nextStep++];
                if (down) MidiInput.Instance.InjectNoteOn(pitch);
                else MidiInput.Instance.InjectNoteOff(pitch);
            }
            if (_nextStep >= _script.Count) IsAutoplaying = false;
        }

        private static bool IsBlackKey(int note)
        {
            int pc = note % 12;
            return pc == 1 || pc == 3 || pc == 6 || pc == 8 || pc == 10;
        }

        [ContextMenu("Play C major scale")]
        private void PlayScale()
        {
            if (MidiInput.Instance == null) return;
            StartCoroutine(ScaleRoutine());
        }

        private System.Collections.IEnumerator ScaleRoutine()
        {
            int[] scale = { 0, 2, 4, 5, 7, 9, 11, 12 };
            foreach (int step in scale)
            {
                MidiInput.Instance.InjectNoteOn(firstNote + step);
                yield return new WaitForSeconds(0.18f);
                MidiInput.Instance.InjectNoteOff(firstNote + step);
                yield return new WaitForSeconds(0.04f);
            }
        }

        /// <summary>
        /// SongTransport's restart chord (notes 21 + 22, the bottom two keys
        /// of an 88) is below the on-screen button range by default, so
        /// there's no other way to press it without a real keyboard. This is
        /// that way -- fires the same NoteOn/NoteOff pair a real chord press
        /// would.
        /// </summary>
        [ContextMenu("Play restart chord (21 + 22)")]
        private void PlayRestartChord()
        {
            if (MidiInput.Instance == null) return;
            StartCoroutine(RestartChordRoutine());
        }

        private System.Collections.IEnumerator RestartChordRoutine()
        {
            const int a = 21, b = 22;
            MidiInput.Instance.InjectNoteOn(a);
            MidiInput.Instance.InjectNoteOn(b);
            yield return new WaitForSeconds(0.15f);
            MidiInput.Instance.InjectNoteOff(a);
            MidiInput.Instance.InjectNoteOff(b);
        }
#endif
    }
}
