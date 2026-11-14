# PianoGlass — MIDI bridge and keyboard calibration

Drop-in scripts for days 3 and 4 of the week-one spike. Target: Unity 6, Meta
Quest 3, Meta XR All-in-One SDK.

## Files and where they go

Copy the `Assets` folder into your Unity project so the paths match exactly.

| File | Purpose |
|---|---|
| `Assets/Plugins/Android/MidiBridge.java` | Receives MIDI on Android and buffers it for Unity |
| `Assets/Scripts/Midi/MidiInput.cs` | Drains the bridge each frame, raises C# events |
| `Assets/Scripts/Midi/MidiDebugPanel.cs` | World-space HUD proving notes arrive |
| `Assets/Scripts/Midi/MidiEditorSimulator.cs` | Fake keyboard for editor work (editor-only) |
| `Assets/Scripts/Piano/PianoKeyLayout.cs` | Two points in, every key position out |
| `Assets/Scripts/Piano/KeyboardCalibration.cs` | Capture the two points, save and load them |
| `Assets/Scripts/Piano/PianoKeyHighlighter.cs` | Real key pressed, virtual key lights up |
| `Assets/Scripts/Piano/OvrCalibrationInput.cs.txt` | Trigger input for calibration. **Rename to .cs after installing the Meta XR SDK** |
| `MANIFEST-additions.txt` | The one manifest line to add by hand |

## The step everyone misses

Unity does not compile a `.java` file just because it is in the project.

1. Select `MidiBridge.java` in the Project window.
2. In the Inspector, under **Select platforms for plugin**, tick **Android**.
3. Click **Apply**.

Without this, the build succeeds, the app runs, and `MidiInput` reports
`bridge failed to load: ClassNotFoundException`. Nothing else about the failure
hints at the checkbox.

## Scene setup

1. Empty GameObject `MidiInput` → add `MidiInput` component.
2. Empty GameObject `Calibration` → add `KeyboardCalibration`.
   - `Key Count`: set to your keyboard's key count. The note range fills itself in.
   - `Pointer Tip`: an empty child of the right controller anchor, moved to the
     physical tip of the controller.
   - `Head Transform`: your centre eye anchor (found automatically if left empty).
3. Same or another GameObject → add `PianoKeyHighlighter`, assign the
   calibration reference and a material you created in the editor.
4. World-space Canvas with a TextMeshPro text → add `MidiDebugPanel`, assign the text.

## Using it

**Calibrate.** Touch the front edge of the lowest key with the controller tip,
pull the trigger. Do the same on the highest key. Every key appears. The
calibration is saved and reloads on next launch.

**Check it.** With `Show Idle Keys` on, the virtual keys should sit on your real
ones. If the derived key width is not near 23.5 mm, the console warns you — that
warning almost always means you touched the wrong two keys.

**Redo it.** Hold the left trigger and pull the right one.

Until the Meta XR SDK is installed, use the `Capture point` context menu on the
KeyboardCalibration component instead of the trigger — right-click the component
header in the Inspector. `OnCaptureButton()` is also public, so a UI button works.

## Reading notes in your own scripts

```csharp
void OnEnable()  => MidiInput.Instance.NoteOn += HandleNoteOn;
void OnDisable() => MidiInput.Instance.NoteOn -= HandleNoteOn;

void HandleNoteOn(int note, int velocity, double time)
{
    Debug.Log($"{MidiInput.NoteName(note)} at velocity {velocity}");
}
```

`MidiInput.Instance.IsHeld(note)` is what wait-mode needs.

## Things already handled for you

- **Note-on with velocity 0 is a note-off.** Most keyboards send these instead
  of real note-offs. Missed, every note sticks on forever.
- **Running status.** Keyboards omit the status byte on repeated messages.
- **Split messages and real-time bytes.** The parser is a proper state machine.
- **The port direction trap.** To receive from a keyboard you open its *output*
  port, not its input port.
- **Thread safety.** MIDI arrives on a Java thread; Unity objects are only
  touched on the main thread.

## If nothing arrives

Work down this list in order. Every item has caught someone before.

1. `adb shell pm list features | grep midi` — no `android.software.midi` means
   the standard route is closed and you need the UsbManager fallback.
2. `adb shell dumpsys usb` with the keyboard plugged in — if your keyboard is
   not listed, it is a cable, power or host-mode problem, not a code problem.
3. Did you tick **Android** on the .java file and press Apply?
4. Is the keyboard on its own power supply rather than drawing from the headset?
5. Check `adb logcat -s PianoGlassMidi` — the bridge logs every device it sees,
   opens, and rejects.
6. Are you testing a **standalone build**? Over Quest Link this runs on your
   PC's MIDI stack and proves nothing.

## Known limits

- One keyboard at a time. Enough for this project.
- Sustain pedal is parsed (`MidiMessage.IsSustainDown`) but not acted on.
- Black key positions are geometric midpoints. Add per-pitch-class nudges in
  `PianoKeyLayout.BlackKeyNudge` if your keyboard visibly disagrees.
- Calibration assumes the keyboard is level. True for a keyboard on a table.
