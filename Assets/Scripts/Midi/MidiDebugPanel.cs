using System.Text;
using TMPro;
using UnityEngine;

namespace PianoGlass.Midi
{
    /// <summary>
    /// The day-three deliverable: a world-space panel that proves MIDI is
    /// reaching the headset. Put this on a world-space Canvas parented to your
    /// camera rig, about 1 metre in front of you.
    ///
    /// Keep it in the project after day three — when something stops working
    /// mid-term, this panel tells you in two seconds whether the keyboard or
    /// your game code is at fault.
    /// </summary>
    public class MidiDebugPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private int historyLines = 12;

        private readonly StringBuilder _sb = new StringBuilder(512);
        private readonly System.Collections.Generic.Queue<string> _history
            = new System.Collections.Generic.Queue<string>();

        private void Reset() => text = GetComponentInChildren<TMP_Text>();

        private void OnEnable()
        {
            if (MidiInput.Instance == null)
            {
                Debug.LogWarning("[MidiDebugPanel] No MidiInput in the scene.");
                return;
            }
            MidiInput.Instance.NoteOn += OnNoteOn;
            MidiInput.Instance.NoteOff += OnNoteOff;
        }

        private void OnDisable()
        {
            if (MidiInput.Instance == null) return;
            MidiInput.Instance.NoteOn -= OnNoteOn;
            MidiInput.Instance.NoteOff -= OnNoteOff;
        }

        private void OnNoteOn(int note, int velocity, double time)
            => Push($"<color=#A9C67C>ON </color> {MidiInput.NoteName(note),-4} n={note,-3} vel={velocity,-3} {time:0.00}s");

        private void OnNoteOff(int note, double time)
            => Push($"<color=#7F8A74>OFF</color> {MidiInput.NoteName(note),-4} n={note,-3}          {time:0.00}s");

        private void Push(string line)
        {
            _history.Enqueue(line);
            while (_history.Count > historyLines) _history.Dequeue();
        }

        private void Update()
        {
            if (text == null || MidiInput.Instance == null) return;

            var midi = MidiInput.Instance;

            _sb.Clear();
            _sb.AppendLine("<b>PianoGlass — MIDI bridge</b>");
            _sb.AppendLine($"service:   {(midi.IsSupported ? "<color=#A9C67C>available</color>" : "<color=#E5948A>NOT AVAILABLE</color>")}");
            _sb.AppendLine($"status:    {midi.Status}");
            _sb.AppendLine($"held:      {FormatHeld(midi)}");
            _sb.AppendLine($"notes rcv: {midi.TotalNotesReceived}");
            _sb.AppendLine();

            foreach (var line in _history) _sb.AppendLine(line);

            text.text = _sb.ToString();
        }

        private static string FormatHeld(MidiInput midi)
        {
            if (midi.HeldNotes.Count == 0) return "—";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var n in midi.HeldNotes) parts.Add(MidiInput.NoteName(n));
            parts.Sort();
            return string.Join(" ", parts);
        }
    }
}
