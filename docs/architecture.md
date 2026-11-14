# Architecture

Moved out of CLAUDE.md on 2026-11-07, word for word. Controls are in `docs/controls.md`, calibration in `docs/calibration-rounds.md`.

## What PianoGlass is

Mixed-reality piano trainer for **Meta Quest 3**. Notes fall onto the keys of a
real 88-key piano seen through passthrough; a USB MIDI keyboard provides
ground-truth input. Built by one student developer, ~7 hours a week.

The product claim is **feedback, not price**: a video cannot tell you that you
played D instead of D♯, or that bar 9 is where you keep falling apart. That is
what PianoGlass exists to do, and it comes first.

It also has to **look and feel finished**: a clean highway, readable results,
menus you can use with your hands. Every menu and panel uses the same
**Liquid Glass** style (translucent like the iPhone's Liquid Glass, bright
rim, rounded corners); the highway grid is the one exception and matches the
reference video instead. Polish that makes the app feel like a real
product earns its place alongside feedback. A feature is cut when it does
neither: it neither sharpens feedback nor makes the app look and feel
finished. The full list of what is in, deferred and cut, and the build order,
is in **`docs/feature-evaluation.md`** (decided 2026-10-30).

---

## Hardware realities that shape the design

**The Quest 3 is the target and the usability study headset.** Quest 2 is no
longer a target, but `quest2` stays in the manifest's supported devices.

**The Quest 3 has one USB-C port.** The MIDI keyboard and the PC cable compete
for it. Wireless adb (`adb tcpip 5555`, then `adb connect <ip>:5555`) is the
workaround but it drops whenever the headset sleeps and does not survive a
reboot; as of this writing it also times out, probably because the PC and
headset are on different subnets. The loop that always works: build over USB,
`adb install -r`, unplug, connect the keyboard, launch from **App Library →
Unknown Sources**.

**Quest 3 passthrough is in colour.** On the Quest 2 it was monochrome and
low-resolution, and you could not rely on the player seeing their real keys
clearly. Published MR piano work hit the same wall and fell back to rendering
virtual keys. The design stays a legible virtual overlay registered on the real
keyboard, not a clear view of the keyboard itself.

**The Quest 3 has a depth sensor, but PianoGlass does not use it for
occlusion, on purpose.** So hands still cannot hide virtual geometry. Keep the
note highway above and behind the key plane so hands rarely overlap it.

**Same performance budget until measured on a Quest 3.** URP, no realtime
shadows, unlit materials, target 72 Hz.

---

## Architecture

```
Assets/
  Plugins/Android/MidiBridge.java     Android MIDI → ring buffer → Unity
  Editor/BuildScript.cs               headless builds
  Scripts/Midi/
    MidiInput.cs                      singleton; drains bridge, raises events
    MidiDebugPanel.cs                 world-space HUD: service, device, notes
    MidiEditorSimulator.cs            fake keyboard for editor work
  Scripts/Piano/
    Core/PianoKeyLayout.cs            two points → every key's position (own asmdef, see below)
    Core/EdgeMarkerGeometry.cs        pure far-edge-bar maths for EdgeMarkers mode (own asmdef)
    KeyboardCalibration.cs            capture, nudge, persist (Meta-free)
    OvrCalibrationInput.cs            calibration's OVRInput code
    PianoKeyHighlighter.cs            real key pressed → virtual key lights; display modes (Meta-free)
    OvrKeyOverlayInput.cs             display mode + passthrough toggle OVRInput code
    CalibrationPanel.cs               in-headset calibration + transport readout
    CanvasCalibrationFollower.cs      status Canvas registered on the keyboard, not world space
    Core/AnchoredCalibrationSession.cs  anchored calibration end to end: CalibrationFlow + anchor I/O + profile writes (own asmdef, tested)
    Core/CalibrationFlow.cs           pure state machine: Launch/Localizing/FullCalibration/Verifying/Correcting/Ready/Suspended
    Core/AnchoredKeyboard.cs          yaw-only anchor frame, world <-> anchor-local, one-/two-point correction, plausibility
    Core/PianoProfileData.cs + PianoProfileSerializer.cs + PianoProfileEdits.cs
                                      piano_profiles.json schema v2: atomic writes, rename-aside on invalid, pure edits
    Core/PivotCalibration.cs          least-squares controller-tip offset for ControllerTipCalibration
    Core/EditorPianoAnchorService.cs  fake anchor runtime for the editor and tests (simulated recenter/relaunch)
    ControllerTipCalibration.cs       left menu: measure the controller tip; written to the profile root
    OvrPianoAnchorService.cs          IPianoAnchorService on OVRSpatialAnchor's async API (device only)
    CalibrationFlowController.cs      Unity shell for the session; KeyboardCalibration's placement owner (dev builds only)
    PianoAnchorManager.cs             LateUpdate: re-publishes the layout when the bound anchor moves > 1 mm / 0.1 deg
    OvrAnchorServiceRegistration.cs   hands the controller OvrPianoAnchorService on device (editor uses the fake)
  Scripts/Song/
    Song.cs                           SongNote + SongData, hard-coded 8-bar melody (own asmdef)
    SongClock.cs                      double songTime, tempo multiplier, pre-song lead-in
    BeatGrid.cs                       pure: which song times get beat/bar lines, bar numbers, octave lanes
    SlabGeometry.cs                   pure slab bottom/height maths, shared by highway + tests
    OutcomeTint.cs                    pure: outcome colours (mint/slate/coral) and their fade/pulse timing
    ActiveNoteSelector.cs             pure "is a note due soon" maths, shared by highlighter + tests
    DisplayLatencyFilter.cs           pure: rejects implausible display-latency readings, smooths ~1 s
  Scripts/Highway/
    NoteHighway.cs                    falling-note wall, position from (noteTime − DisplaySongTime); PlaneAxes() = the tilted plane
    HighwayGrid.cs                    grid on that plane: octave lanes, beat/bar lines, bar tags, hit line (one mesh,
                                      Shaders/HighwayGrid.shader, SDF rounded rects with a minimum on-screen width)
    OvrDisplayLatency.cs              feeds SongClock the runtime's render-to-display latency
  Scripts/Scoring/
    Core/ScoringSession.cs            pure judging: matching, misses, wrong notes, per-bar stats (own asmdef)
    Core/NoteJudge.cs + JudgementResult.cs + AccuracyCalculator.cs
                                      windows, ratings, the attempt JSON schema
    ScoreKeeper.cs                    Unity shell: MIDI → session, attempt files, restart = abandoned
  Scripts/Transport/
    SongTransport.cs                  play/pause/restart + MIDI chord + transport state (Meta-free)
    OvrSongTransport.cs               transport's OVRInput code
  Tests/EditMode/                     270 tests (layout, calibration, clock, slabs, scoring, latency filter, beat grid), run headlessly
```

OVRInput now lives in three places, split the same way as calibration:
`OvrCalibrationInput.cs`, `OvrSongTransport.cs` and `OvrKeyOverlayInput.cs`
are the only files that touch it, and each is a thin adapter over a
Meta-free component (`KeyboardCalibration`, `SongTransport`,
`PianoKeyHighlighter`) that does the actual work and is what compiles and
runs in the editor.

**Why MidiBridge is Java.** `android.media.midi.MidiReceiver` is an abstract
*class*, and Unity's `AndroidJavaProxy` can only implement *interfaces*. So the
receiving end must live in Java. It parses bytes on a background thread, pushes
packed ints into a ring buffer, and Unity drains it once per frame on the main
thread.

**Reading notes:**

```csharp
void OnEnable()  => MidiInput.Instance.NoteOn += HandleNoteOn;
void OnDisable() => MidiInput.Instance.NoteOn -= HandleNoteOn;
```

`MidiInput.Instance.IsHeld(note)` is what wait-mode needs.

**Calibration model.** Touching the front edge of the lowest and highest keys
fixes *scale* immediately — spacing comes from the distance between the points.
What it inherits is a constant offset between the controller's tracked origin
and its plastic tip. That offset translates the whole keyboard without
distorting it, which is why the thumbstick nudge is a rigid shift and why the
nudge deliberately **survives recalibration** (it is a property of the hardware;
the captured points are a property of where the keyboard is).

Sanity check: a full-size white key is **23.5 mm**, and an 88-key span is about
**119.9 cm** centre to centre. The layout warns if the derived width strays.

**The song clock's lead-in.** `SongClock.SongTime` starts at `-leadInSeconds`
(default 3), not 0 — set in `Awake()`, not only in `Restart()`, because the
very first `Play()` after the scene loads never calls `Restart()`. Without
this the first note's `StartTime` (0) is already within `leadTimeSeconds` of
due the instant play is pressed, so it never falls: the opening bars are
skipped entirely. `NoteHighway`'s clamp maths (now `SlabGeometry.VerticalExtent`)
need no special-casing for the resulting negative `SongTime` — bottom and top
are affine functions of it, so the same clamp that hides a not-yet-due note
hides one during the lead-in for free. The one consequence that does need
handling: a silent, empty wall during the countdown reads as a broken build,
which is why `CalibrationPanel` always shows transport state (Stopped /
Counting in / Playing / Finished) and `SongTime` to one decimal.

---

## Passthrough and the key overlay

**Passthrough configuration lives in `SceneSetup`, not the Inspector.**
`PianoGlass/Scene/Configure Passthrough` sets, on `[BuildingBlock] Camera Rig`:

- `OVRManager.isInsightPassthroughEnabled = true` (the component's own
  "Enable Passthrough" flag).
- An `OVRPassthroughLayer`, added if missing. `overlayType` is left alone —
  Meta XR SDK 205.0.0 made it `[Obsolete]`/`[HideInInspector]`: flexible
  layering is gone, and a passthrough layer is now always the single
  background layer under the eye buffer, i.e. always Underlay already.
- The main camera: `clearFlags = SolidColor`, background alpha forced to 0.
- `RenderSettings.skybox = null`.
- The project's `OVRProjectConfig.insightPassthroughSupport = Required` —
  this is the separate, project-level "Passthrough Support" flag Meta's
  build step reads to decide whether the generated `AndroidManifest.xml`
  gets the passthrough `uses-feature` entries. It is not readable from the
  `OVRManager` component in the scene; it lives in
  `Assets/Oculus/OculusProjectConfig.asset`.

Passthrough initialises **asynchronously**. Anything that must know it is
actually visible — not just requested — has to wait for
`OVRPassthroughLayer.PassthroughLayerResumed`, not assume success the frame
after enabling the layer. `OvrKeyOverlayInput` does this: toggling
passthrough on logs immediately (`passthrough requested on`), and logs again
when the layer confirms it (`passthrough resumed`).

**`PianoGlass/Scene/Configure Key Overlay`** wires `PianoKeyHighlighter` to
the scene's `SongClock`, creates/assigns a Transparent-surface material for
Translucent mode (`Assets/KeyOverlayTranslucent.mat`), and adds
`OvrKeyOverlayInput` next to it.

**Display modes** (`PianoKeyHighlighter.DisplayMode`, cycled with left X):

| Mode | At rest | Why |
|---|---|---|
| `FullOverlay` | all 88 keys, opaque | the original behaviour; baseline to compare the others against |
| `Translucent` | all 88 keys, alpha = `translucentAlpha` (0.25) | same coverage, see-through — needs the Transparent material above |
| `ActiveKeysOnly` (default) | nothing | only a key whose note is due within `activeKeyLookaheadSeconds` (0.5 s) or is actually held lights up, at `activeKeysAlpha` (0.55) so a hand shows through |
| `EdgeMarkers` | nothing | same "only when relevant" rule as `ActiveKeysOnly`, but each key shows as a thin bar across its far portion (`edgeMarkerDepthFraction` of key depth) instead of the full key, clear of where a resting finger touches |

PianoGlass does not use the Quest 3's depth sensor for occlusion, so *any* virtual slab over a key draws on
top of the player's hand regardless of passthrough. `FullOverlay` painting
all 88 keys at rest is what made a tester unable to see their own hands even
once passthrough is on — `ActiveKeysOnly`/`EdgeMarkers` exist to paint only
the key the player actually needs to find, which is why `ActiveKeysOnly` is
the shipped default rather than `FullOverlay`. The default only applies on
first launch: every mode change is saved to PlayerPrefs (by name) and the app
launches in the last mode chosen (confirmed on device 2026-10-29).

Controls, `OvrKeyOverlayInput.cs`, explicit `OVRInput.Controller.LTouch` as
always:

```
left X            cycle display mode
left stick click   toggle passthrough on/off (A/B it against the void)
```

Both read back from `PianoKeyHighlighter`/`OVRPassthroughLayer` and log one
combined line (`Debug.Log($"[KeyOverlay] ... mode={...} passthrough={...}")`)
on every change to either, so a full A/B session is readable afterwards from
`adb logcat -s Unity` without reconnecting a headset to ask "what was I
looking at."

---

