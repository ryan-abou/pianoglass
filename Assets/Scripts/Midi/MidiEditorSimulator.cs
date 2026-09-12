using UnityEngine;

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
