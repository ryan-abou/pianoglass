# PianoGlass

Mixed-reality piano trainer for **Meta Quest 2**. Notes fall onto the keys of a
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

## Environment — pinned, do not upgrade mid-project

| | |
|---|---|
| Unity | **6000.6.0f1** (URP 17.6.0) |
| Meta XR All-in-One SDK | **205.0.0** |
| OpenXR | **1.18.0** |
| Scripting | IL2CPP, **ARM64 only**, minSdk 26 |
| Graphics | Vulkan then GLES3, ASTC textures |
| Bundle ID | `com.ryanabourjeili.pianoglass` |

`adb` lives at
`C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools`
and is on PATH.

Application SpaceWarp is **deliberately off** — it reprojects at half rate and
smears thin, high-contrast geometry, which is exactly what falling notes, key
slabs and text are. The app is nowhere near GPU-bound.

Meta Platform SDK checklist items (Data Use Checkup, application ID) are
**deliberately ignored**. No entitlements, leaderboards or IAP.

---

## Building

Close the Unity editor first — it locks `Library/`, and a second instance on the
same project fails with a message about multiple instances rather than anything
about building.

```powershell
.\build.ps1                # release
.\build.ps1 -Development   # profiler + script debugging
.\build.ps1 -Install       # build, then adb install to the headset
```

The script exits with Unity's own exit code — 0 succeeded, 1 failed — and prints
every `[PianoGlassBuild]` line from the log, which is the whole story without
wading through Unity's tens of thousands of lines.

**Do not invoke Unity.exe directly with `&`.** `Unity.exe` is a Windows GUI
application, so PowerShell launches it and returns immediately without waiting.
You get your prompt back in two seconds, an empty `$LASTEXITCODE`, and a log
file that is still being written — which looks exactly like a failed build and
is not one. `build.ps1` uses `Start-Process -Wait -PassThru`, which blocks and
gives back a real exit code.

`BuildAndroidDevelopment` is the same thing with the profiler and script
debugging enabled. Use it while diagnosing, never for a build handed to a test
participant.

Install and inspect:

```powershell
adb install -r Builds\PianoGlass.apk
adb logcat -s PianoGlassMidi Unity
```

---

## Two ways to reach Unity — and they cannot both be live

Only one Unity process can hold this project at a time, because Unity locks
`Library/`. That single fact is what makes these mutually exclusive:

| | Editor state it needs | What it is for |
|---|---|---|
| **Unity MCP** (`Edit > Project Settings > AI > Unity MCP`) | **open** | reading the live editor |
| **`build.ps1`** | **closed** | producing and installing the APK |

They are not two ways to do the same job. One inspects a running editor; the
other ships a build. Neither replaces the headset.

**Check the state before acting. It is not knowable from the filesystem:**

```powershell
Get-Process Unity -ErrorAction SilentlyContinue
```

Output means the editor is open — MCP tools work, and `build.ps1` will fail
with a message about multiple instances that says nothing about building. No
output means it is closed — `build.ps1` works, and MCP calls will hang or
error. Guessing wrong costs a cycle, so confirm rather than assume.

**Use the MCP connection to look, not to change.** Reading the Console,
inspecting the hierarchy, checking what a component's fields are actually set
to, entering play mode. Every scene-state failure in week two — a scene that
was never wired, a build made before the wiring, a world-space Canvas left at
y = 0, which is floor height — was invisible on disk and obvious in the editor.

**Scene changes still go through `Assets/Editor/SceneSetup.cs`.** An MCP edit to
the scene produces an unreadable 60 KB YAML diff that exists on one machine
only. A `SceneSetup` change is reviewable C# that anyone can re-run. Use the
MCP to verify the result, not to make it.

**When one task needs both, the order is: inspect → close Unity → build →
reopen.** Never build first and change the scene afterwards. The APK will not
contain the scene changes, and the failure looks exactly like a code bug.

---

## Hardware realities that shape the design

**The Quest 2 has one USB-C port.** The MIDI keyboard and the PC cable compete
for it. Wireless adb (`adb tcpip 5555`, then `adb connect <ip>:5555`) is the
workaround but it drops whenever the headset sleeps and does not survive a
reboot; as of this writing it also times out, probably because the PC and
headset are on different subnets. The loop that always works: build over USB,
`adb install -r`, unplug, connect the keyboard, launch from **App Library →
Unknown Sources**.

**Quest 2 passthrough is monochrome and low-resolution.** You cannot rely on the
player seeing their real keys clearly. Published MR piano work hit the same wall
and fell back to rendering virtual keys. Design for a legible virtual overlay
registered on the real keyboard, not for a clear view of the keyboard itself.

**No depth sensor**, so no real occlusion — hands cannot correctly hide virtual
geometry. Keep the note highway above and behind the key plane so hands rarely
overlap it.

**Roughly half a Quest 3's GPU.** URP, no realtime shadows, unlit materials,
target 72 Hz.

---

## Controls — every one, today and in the hand-tracked menus

Keep this table current: add a row whenever a control is added, moved or
removed. Phase 4 (hand tracking, `docs/feature-evaluation.md`) moves most of
these off the controllers and into **Liquid Glass menus**. The design rule
from there: the controllers are set down on the piano, and hands either
play or poke.

### Today (2026-11-05)

Controllers always name `OVRInput.Controller.LTouch` / `RTouch`. Piano
chords are all built on the bottom key, A0, so that no song spends a note
on them and none can be played by accident.

| Control | Controller (today) | Piano chord | Where | Notes |
|---|---|---|---|---|
| Capture the next calibration point | right trigger | — | `OvrCalibrationInput` | Two touches, lowest then highest key |
| Start calibration over | B, or left trigger | — | `OvrCalibrationInput` | |
| Nudge left/right, nearer/further | right stick | — | `OvrCalibrationInput` | Rigid shift; it survives recalibration |
| Nudge up/down | left stick up/down | — | `OvrCalibrationInput` | Axis-locked against yaw |
| Turn the keyboard (yaw) | left stick left/right | — | `OvrCalibrationInput` | ±10° |
| Clear the nudge | right stick click | — | `OvrCalibrationInput` | Also cleared by a fresh calibration once the tip is measured |
| Measure the controller tip | left menu button (again = cancel) | — | `ControllerTipCalibration` | Written to the profile root |
| "Looks right" in Verifying | A | — | `OvrCalibrationInput` | Anchored mode; that A does not reach play/pause. Asked after launch, calibration, and 3 s or more away |
| Middle C fix: start / cancel | right grip | — | `OvrCalibrationInput` | Anchored mode; then touch Middle C and pull the right trigger |
| Play / pause | A | — | `OvrSongTransport` | |
| Restart from the top and play | left Y | A0 + A♯0 | `OvrSongTransport`, `SongTransport` | The chord works whenever calibrated, playing or not. Also how a practice loop is left |
| Start a practice loop | right controller ray + right trigger on the results card's "Loop bars" button | — | `ResultsCard` → `PracticeLoop` | The button shows when a bar is under 75%: that bar, and a neighbour under 90% |
| Choose bars to loop | right controller ray + right trigger on a bar pill | — | `ResultsCard`, `BarSelection` | One pill, then another for a range; the suggestion's button becomes "Loop bars 3–5". The same pill again, alone, clears it |
| Press a results card button (Retry, More/Less info, the suggestion's button) | right controller ray + right trigger | — | `OvrUiPointer`, `GlassButton` | Stopgap until hand poke (Phase 4). The ray shows only while a button is pressable; the trigger goes to calibration unless the ray is on a button |
| Tempo −5% / +5% | — | hold A0, press B0 / C1 | `SongTransport` | 25–200%, only while stopped or paused, saved |
| Metronome on/off | — | hold A0, press C♯1 | `SongTransport` → `Metronome` | Only while stopped or paused, saved |
| Wait mode on/off | — | hold A0, press D1 | `SongTransport` | Only while stopped or paused, saved. While paused part-way it starts a new attempt from there |
| Memory fade on/off | — | hold A0, press D♯1 | `SongTransport` | Only while stopped or paused, saved. Works on whole songs and in loops |
| Display mode (cycle) | left X | — | `OvrKeyOverlayInput` | FullOverlay / Translucent / ActiveKeysOnly / EdgeMarkers, saved |
| Passthrough on/off | left stick click | — | `OvrKeyOverlayInput` | A/B test tool |

Development tools that are not player controls:
- `DeviceChecklistRunner` (development builds) reads the flow's events and
  needs no input of its own.
- `OvrAnchorSpike` (Phase 3 harness) uses both grips. It is disabled in the
  scene and must stay disabled alongside the anchored-calibration flow.

### Target: hand-tracked Liquid Glass menus (Phase 4)

Every menu below is Liquid Glass (`GlassPanel` + `LiquidGlass.shader`,
values from `Assets/UI/GlassTokens.asset`):
- buttons are pills or circles, sitting slightly in front of their panel;
- nested rows follow the iOS grouped style, with radii concentric with
  their parent;
- every press depresses visibly.

| Menu | Holds | Shown when |
|---|---|---|
| **Song panel** (F12 + F24 merged), left end of the keyboard, angled toward the player | play/pause, restart, tempo −/+ with its value, metronome toggle, wait-mode toggle, loop worst bar, song picker button, settings button | Always, collapsible to a slim strip |
| **Results card** (F30/F32) | rank, accuracy, change vs last, timing, wrong notes, best streak, bar pills, one suggestion; buttons: retry, next song (hidden until a song list exists), more info (details panel) | When an attempt completes |
| **Settings** (S1, `PianoSettings`) | display mode, passthrough, hand colours (F13), due-key toggle (F18), metronome, memory fade, wait mode, auto-tempo on/off (Ryan, 2026-11-05: always on until these menus exist) | From the song panel |
| **Calibration panel** (today's status panel, restyled) | start calibration, "looks right", Middle C fix, nudge pad (6 arrows plus yaw ⟲/⟳, held to repeat), clear nudge, measure controller tip | During setup, and from Settings → Keyboard |
| **Song picker** (F38/F39) | song list with per-song history | From the song panel |

What stays off the menus, and why:
- **The status panel goes or shrinks (Ryan, 2026-11-05).** It carries a loop's
  pass and how the last one went only until these menus can show that.
- **The piano chords stay.** While playing, hands are on the keys, and a
  chord is faster than reaching for a panel. Each chord gets a matching
  button, so nothing is chord-only.
- **Calibration touches keep the controller for now.** The two-point
  capture and the Middle C touch need the controller's tip, and that is the
  measurement itself. A hand-tracked fingertip is not assumed to be accurate
  enough on a Quest 2 (tip calibration measured 1.58 mm; a white key is
  23.5 mm wide). Hands do everything else in calibration, and the two ideas
  below get measured in the hand-tracking spike.
- **Development tools stay on controllers:** passthrough on/off and the
  device checklist.

### Calibration ideas to measure in the hand-tracking spike (Phase 4)

Both replace the controller's two touches, and neither is built. Judge each
against controller calibration on the same piano: key width against 23.5 mm,
span against 119.9 cm, and the error at Middle C.

1. **A key on your finger (Ryan, 2026-11-04).** The app tracks your pointer
   finger and draws a virtual key in front of it. Move your hand until that
   key lines up with the real one, and confirm with a button on the other
   controller. Do this for A0, then C8. The eye does the aligning, so nothing
   has to touch the key, and only the moment of confirming needs steady
   tracking. To check first: whether a Quest 2 can track a hand and a
   controller at once. If it cannot, confirm with a pinch from the other
   hand, or with a press on the piano.
2. **Learn from key presses (Claude, 2026-11-04).** MIDI says exactly which
   key went down and when, so each press pairs a known key with a fingertip
   position. One press is noisy; many averaged may place the keyboard well,
   and could even correct small drift during play. To check first: how large
   the error is with the hand right against the keys, and whether it has a
   steady bias that averaging cannot remove.

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

The Quest 2 has no depth sensor, so *any* virtual slab over a key draws on
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

## Anchored calibration (development builds)

Status, per-phase device results and what is next live in
**`docs/anchored-calibration-status.md`** — update it after every phase.

The keyboard is stored relative to a spatial anchor created at calibration
time, not in tracking space: `piano_profiles.json` holds the anchor UUID
plus front-edge points and facing **in the anchor's local frame** — never a
world position, which every recenter invalidates. The layout is rebuilt as
"anchor pose × local geometry", so recenters, reboots and headset sleep need
no special handling. Proven on device in Phase 4 (localize ~100 ms on
relaunch, 134 ms after reboot, ~2 s after 10 min with the headset off; three
recenters of up to 1.1 m / 180° kept the keys on the piano).

- **Only in development builds.** `CalibrationFlowController` disables itself
  in Awake when `!Debug.isDebugBuild`, and release builds run the legacy
  `keyboard_calibration.json` path untouched. A release APK handed to a
  tester does *not* have anchored calibration.
- **All decisions are in Core** (`CalibrationFlow` decides,
  `AnchoredCalibrationSession` carries out the commands), tested headlessly
  against `EditorPianoAnchorService`. The MonoBehaviours only forward input.
  New behaviour goes in Core with a test first.
- **The headset's map can move an anchor** (86 mm after a day, 14-25 mm after
  the headset was off, 2026-11-05), with the anchor still reporting tracked. Recovery
  is one Middle C touch, up to 150 mm. See the status doc's "Anchor drift".
- **Rules the flow keeps:** nothing is created on launch; no failure path
  (timeout, failed load, failed save) ever erases an anchor or rewrites the
  profile; the old anchor is erased only after the profile naming the new
  one is on disk. An empty load is `NotFound` (anchor gone), not a failure.
- **Read-modify-write the profile file every time.** `ControllerTipCalibration`
  writes the tip fields into the same file; writing from a copy held since
  launch would wipe them.
- **The editor fake forgets its anchors when play mode stops,** so every
  editor run sees the saved anchor as gone and asks for a fresh calibration.
  That is expected, not a bug.
- **Grips are reserved for anchored-calibration corrections.**
  `OvrAnchorSpike` (Phase 3 harness) used both grips and is disabled in the
  scene by `Configure Anchored Calibration`; do not re-enable it alongside the
  flow.
- **Verifying is a real state, not a formality** (Phase 5). After every
  localize or calibration the keys are drawn and A means "looks right"
  (→ Ready, `lastVerifiedUtc`), *not* play — `SongTransport` ignores that one
  press. Right grip starts a Middle C fix (touch the front edge, trigger):
  a rigid anchor-local shift, vertical capped at 10 mm, refused over 150 mm. Ready
  also returns to Verifying after 3 s or more away (headset off).
- **All logs start with `[AnchorFlow]`**: `adb logcat -s Unity | findstr AnchorFlow`.

---

## Traps already hit — do not re-learn these

- **Unity does not compile a `.java` file until you select it in the Project
  window and tick Android under "Select platforms for plugin".** Toggle it off,
  Apply, on, Apply to force the `PluginImporter` block to serialize. Without
  it the build succeeds and fails at runtime with `ClassNotFoundException`.
- **A Screen Space - Overlay canvas is invisible in VR.** World Space, and
  scaled to ~0.001 or it is a 600-metre wall. Cost a full build cycle.
- **A world-space Canvas left at a fixed world position, y = 0, is at floor
  height in Quest tracking space** — unreadably low, and easy to mistake for
  a broken build rather than a placement bug. `CanvasCalibrationFollower`
  registers it on the calibrated keyboard instead, the same way `NoteHighway`
  registers the note wall; before calibration exists it falls back to a
  serialized height rather than whatever y the Canvas happened to be authored
  at.
- **A running adb server breaks headless Unity. Run `adb kill-server` before
  every batchmode run** — tests, a `SceneSetup` method via `-executeMethod`,
  and `build.ps1`. With an adb server already up, batchmode Unity exits
  `-1073740791` (`0xC0000409`) right after "Native extension for Android
  target not found", before any test or method runs — it looks like a code or
  scene bug and is neither. Unity's Android module starts that adb server
  itself and it outlives Unity, so the *second* headless run after anything
  Android-related (including `build.ps1 -Install`) is the one that crashes.
  The same lingering adb child is why `build.ps1`'s `Start-Process -Wait` can
  hang long after Unity has exited: `-Wait` waits for the whole process tree.
  `-PassThru` plus `$p.WaitForExit()` waits for Unity alone.
- **Unity hides file extensions in the Project window.** Rename `.cs.txt` files
  in Explorer, not in Unity.
- **Do not overwrite or hand-edit `Assets/Plugins/Android/AndroidManifest.xml`.**
  The checked-in file has no passthrough, anchor or MIDI entries: Meta's
  Gradle step (`OVRManifestPreprocessor`) injects passthrough and
  `USE_ANCHOR_API` into the *merged* manifest at build time, from
  `OculusProjectConfig.asset`. Check the APK (`aapt dump permissions`), not
  the file. There is **no** `android.software.midi` `uses-feature` line and
  none is needed — MIDI works on device without it, and with
  `required="false"` it only ever affected store filtering, never runtime
  behaviour. `MANIFEST-additions.txt` says what the file holds and how to
  check the merged APK.
- **A MIDI note-on with velocity 0 is a note-off.** Most keyboards send these.
  Missed, every note sticks on forever.
- **To *receive* from a keyboard you open its OUTPUT port**, not its input port.
  Getting this backwards gives silence and no error.
- **Assign materials in the Inspector.** A runtime `Shader.Find` gets stripped
  from device builds and you see nothing, with no clue why.
- **`OVRInput.Button.PrimaryIndexTrigger` without a `Controller` argument**
  resolves against whichever controller is "active", and `Secondary...` may
  resolve to nothing. Always name `OVRInput.Controller.RTouch` / `LTouch`.
- **The first `DateTime.Now` on the headset takes about 380 ms.** It froze the
  first press of play for a visible moment until `ScoreKeeper` began doing it
  on a background thread at launch (measured 2026-11-05: longest frame after
  that press went from 390 ms to 28 ms). Any other first-use cost on the main
  thread will look the same, and it looks like a graphics stall when it is not:
  profile before guessing. In a development build, set `Profiler.logFile`,
  `enableBinaryLog` and `enabled`, pull the `.raw`, and read the longest frame
  with `ProfilerDriver.LoadProfile` and `RawFrameDataView`: it names the method.
- **`FindFirstObjectByType` is deprecated in 6.6** — use `FindAnyObjectByType`.
- **`git commit -am` ignores untracked files.** Any new Unity asset — script,
  material, prefab, scene — needs `git add -A`. Check `git status` first.
- **Meta XR Audio SDK 205.0.0 has a runtime script that references editor-only
  APIs** and can block player builds with `CS0234 UnityEditor.GUID`. Restarting
  the editor cleared it once. If it returns, replace `com.meta.xr.sdk.all` with
  `com.meta.xr.sdk.core` **plus `com.meta.xr.sdk.interaction`** (same
  version, 205.0.0) in `Packages/manifest.json`. Nothing here uses audio,
  platform or voice. Interaction is needed once the hand-tracked UI (phase 4)
  exists, and dropping it would break that.
- **A camera with `Skybox` clear flags renders an opaque sky over
  everything, passthrough included — this is what the "black void, can't see
  my hands" report traced back to.** Enabling `OVRManager.isInsightPassthroughEnabled`
  and adding an `OVRPassthroughLayer` does nothing if the camera still clears
  to `Skybox`: the skybox material draws over the passthrough layer every
  frame regardless, and if that material happens to render as black, the
  result looks exactly like passthrough silently failing rather than like a
  camera setting. Needs **both** halves: `clearFlags = SolidColor` with
  background alpha `0`, **and** `RenderSettings.skybox = null` — either one
  alone still leaves something opaque in front of the passthrough feed. Also
  check the active URP asset's HDR flag and any `Global Volume` with active
  post-processing overrides before assuming the camera is the whole story:
  HDR and post-processing can each rewrite the camera's alpha channel on the
  way out, which breaks the same compositing even with clear flags and
  skybox both correct. (Not a problem in this project today — the Android
  quality tier's `Mobile_RPAsset` has HDR off, and the scene's `Global
  Volume` has post-processing disabled on the XR camera — but re-check both
  if passthrough ever goes transparent-but-black again after someone enables
  post-processing or switches render pipeline assets.)

---

## Working agreements

- **Pin the toolchain.** Twelve weeks is long enough that a Unity or SDK upgrade
  will cost more than it returns. Do not upgrade without a blocking reason.
- **Branch before large changes** so `main` stays demonstrably working:
  `git checkout -b highway`.
- **Record 30 seconds of video at every milestone.** Headset capture fails on
  the evening you need it, and footage cannot be recovered retroactively.
- **Keep a known-good APK.** `Milestones/` (one level above the project, outside
  the repo) holds dated builds. Reinstall one to tell whether a regression is in
  the code or the environment.
- **The editor simulator means most work needs no headset.** Song data, the song
  clock and MIDI parsing can all be verified from the desk.

---

## Git workflow

`main` is always the version that has passed on the headset. Everything else
happens on a branch. GitHub remote: `origin` = github.com/ryan-abou/pianoglass.
Commits only count on Ryan's GitHub contribution graph once they are on `main`,
and they keep their original commit dates.

1. **Start new work from main:** `git checkout main`, `git pull`, then
   `git checkout -b <short-name>` (e.g. `phase6-twopoint`).
2. **Commit whenever a step works, and commit often.** `git status`, then
   `git add -A`, then `git commit -m "<what changed>"`. Never `git commit -am`
   (it skips new files). Ryan wants a busy contribution graph, so split each
   feature into its real logical steps (pure maths + tests, shader,
   component, SceneSetup wiring, each headset tuning round, docs), one
   commit each, and push after each. Every commit must compile. No padding
   or empty commits.
3. **Push at the end of every session, even if nothing works yet.** First push
   of a branch: `git push -u origin <short-name>`; after that, `git push`.
   This is the only off-machine backup.
4. **Merge only after the headset test passes:** `git checkout main`,
   `git pull`, `git merge <short-name>`, `git push`. Keep the branch. If the
   test fails, stay on the branch, fix, retest.
5. **Record the result** in `docs/anchored-calibration-status.md` (or the
   relevant status doc) in the same commit as the merge or just before it.

When Ryan says "push", "merge this into main" or "start a new branch", follow
these steps exactly. Ask before force-pushing, rewriting history, or deleting a
branch. If a merge reports a conflict, stop and explain it rather than
resolving it silently.

---

## Current state and what is next

Working and proven on device: MIDI input end to end, 88-key overlay registered
on the real piano to under half a percent of correct, calibration persisted with
runtime fine-adjust, controller-tip calibration (1.58 mm RMS), and — in
development builds — anchored calibration that survives relaunch, recenter and
reboot (Phase 4, 2026-10-21), with a Verifying step and a Middle C one-point
fix (Phase 5, 2026-10-26). See `docs/anchored-calibration-status.md`.

Also working: the falling-note highway (8 mm gap so repeated notes read as
two), and — merged into main 2026-10-29 after a headset retest with the
filtered latency reading — scoring and display-latency compensation:

- **Scoring** (`Scripts/Scoring/`): `ScoringSession` (pure, tested) judges each
  press PERFECT/GREAT/GOOD/MISS at 40/80/150 real ms, names wrong pitches,
  keeps per-bar stats; `ScoreKeeper` feeds it MIDI presses timed from the
  bridge's own timestamps (`MidiInput.ToRealtime`), shows results on the
  panel and writes every attempt to
  `/sdcard/Android/data/com.ryanabourjeili.pianoglass/files/attempts/*.json`.
  Pull with `adb pull <that path>/. <dir>`; `[Score]` lines in logcat.
- **Display latency**: `SongClock.DisplaySongTime` = SongTime + display
  latency × tempo. Everything *drawn* uses it; *judging* uses `SongTime`.
  `OvrDisplayLatency` feeds it from OVRPlugin's private
  `GetPredictedDisplayTime()` (via reflection — it is not public in SDK 205),
  falling back to `XRDisplaySubsystem.TryGetMotionToPhoton`. Raw per-frame
  readings are noisy (15–44 ms, some 0, once −253 s), so they go through
  `DisplayLatencyFilter` (`Scripts/Song/`, pure, tested): readings outside
  (2 ms, 100 ms] are rejected and the last good value held; the rest are
  smoothed with a ~1 s time constant. `SongClock`'s clamp stays as a backstop.
- **Latency calibration is a system measurement, never the player's mean
  error** — a mean error would absorb the player's own rushing/dragging, which
  is feedback to show them. `ScoreKeeper.inputOffsetMs` stays 0 until measured
  on hardware (240 fps phone video: key down → virtual key lights).

First device data (7 attempts of Twinkle, 2026-10-29): 0 wrong pitches;
pooled mean error −7 ms (compensation looks right), spread ~40 ms, so ~⅔
PERFECT and ~90% accuracy is just that spread. Per-attempt mean drifts −28 to
+11 ms — no audible beat to lock onto. Rushing in bars 3 (−37 ms) and 7
(−18 ms).

**Highway grid (Phase 1, F16/F17): merged 2026-11-03** after four headset
rounds. The highway now leans back 60° (`NoteHighway.tiltDegrees = -60`,
set by `SceneSetup`) and starts 5 mm behind the keys (`depthOffset`; 20 mm
until the key depth was measured, then 15 mm, then 5 mm on 2026-11-03 once
FullOverlay photos showed no real key ends behind the virtual ones).
`HighwayGrid` draws, on exactly `NoteHighway.PlaneAxes()`: one thin lane line
per octave (each C's left edge, full keyboard width), faint beat lines, bar
lines brighter but not thicker, a dark bar-number tag left of the lowest key,
and a dark hit line on the runway side of the key line only.
`PianoGlass/Scene/Configure Highway Grid` destroys and re-adds the component
so code defaults are the source of truth, not stale serialized values.
**Headset photos:** Quest 2 captures never include passthrough, so the
grid is judged from phone photos taken through the lens.

**Key dimensions: merged 2026-11-03** (branch `black-key-geometry`, fronts,
lengths and sideways positions confirmed on the headset). Measured on Ryan's
piano: white key 147.64 mm, black key 94.45 mm long, black front 52.5 mm
behind the white front -- constants in `PianoKeyLayout`, absolute rather than
fractions of white depth. Black keys sit off-centre by the equal-slot model
(C-E top split in five, F-B in seven): C♯ −2.4 mm, D♯ +2.4, F♯ −3.4, G♯ 0,
A♯ +3.4. White key depth is a property of the piano, set in the scene by
`PianoGlass/Scene/Configure Key Dimensions`; neither calibration file
overrides it on load any more (`AnchoredCalibrationSession.ConfiguredWhiteKeyDepth`),
because every file saved before held the unmeasured 145 mm.

**Outcome colours (Phase 1, F19/F20): merged 2026-11-03** (branch
`phase1-outcome-colours`, three headset rounds). A hit slab fades to mint
`#3DDC97` over 120 ms; a missed one to slate `#5A636E`; a restart puts every
slab back to blue. A wrong or extra press turns its key bright coral
`#FF6B5B` at once, a dimmer steady coral while held -- **never green**, which
read as "that was fine" -- and on release fades out *as coral* over 300 ms
(blends to the idle colour only in FullOverlay/Translucent, where a key is
always drawn). All values in `OutcomeTint` (`Scripts/Song/`, pure, tested);
`ScoreKeeper` forwards `NoteJudged` / `WrongNotePlayed` / `AttemptReset`, and
`NoteHighway` and `PianoKeyHighlighter` only listen. Same branch: lit keys in
ActiveKeysOnly draw at 55% opacity on the translucent material so hands show
through. **Deliberately provisional:** colours, timings and alphas are to be
revisited after more people have tried them.

**Next, in order:**

Follow the four-phase build order in `docs/feature-evaluation.md` (Part 3):

1. **Highway looks finished**: ~~grid~~ (done); ~~hit/miss/wrong colours~~
   (done); ~~note names (F14)~~ (done, Batch A).
2. **Results, tempo, practice**: ~~tempo ±5%~~, ~~attempt schema v2~~ (both
   done, Batch A), ~~results card with bar heat strip~~, ~~metronome~~ (both
   done, Batch B), ~~loop worst bar~~, ~~auto-tempo~~, ~~wait mode~~ (all done,
   Batch C), memory fade (next round, branch `fade-song`).
3. **Real songs**: MIDI file loading, song picker, hand colours, one-hand
   practice, a free public-domain starter library, per-song history.
4. **Hand-tracked UI**: hand-tracking spike, settings model, one panel
   combining the dock and in-song controls.

Personal bests / XP / achievements come after that. Multiplayer is much later,
if ever.

---

## Batches (decided 2026-11-03)

Features now ship in **batches**: several per headset round instead of one.
The last features each drew one or two pieces of feedback and no bugs, and
the bottleneck is the 11–13 min build and the single headset, not writing
code. When Ryan says "do Batch A" (or B, C), follow this section.

### How a batch runs

1. **One branch per feature**, each started from the previous feature's
   branch when they touch the same files (`SongClock`, `SongTransport`,
   `ScoreKeeper`, `NoteHighway`), otherwise from `main`. Name them
   `batchA-<feature>`. Same commit rules as always: real logical steps
   (pure maths + tests, component, SceneSetup wiring, docs), each compiles,
   push after each.
2. **Do not parallelise with subagents or worktrees.** Every test run,
   SceneSetup run and build needs exclusive use of `Library/`; extra
   worktrees each need a full reimport. Write the features back to back.
3. **Run the EditMode tests after each feature** (headless command in the
   memory file / `adb kill-server` first).
4. **One integration branch for the build**: `batchA-test`, made by merging
   every feature branch of the batch. It is **never merged into main**. One
   development build of it, one install, one headset session.
5. **One combined checklist** for Ryan, grouped by feature, saying exactly
   what to look at and what "pass" means.
6. **Merge each passing feature branch into main separately** (in dependency
   order), then record the results in this file and
   `docs/feature-evaluation.md`. A feature that needs changes stays on its
   branch and goes into the next batch's round; it does not block the rest.
7. Tuning feedback from a round (colours, sizes, timings) becomes a small
   branch in the next batch.

Build housekeeping: an interrupted or failed build can leave
`ProjectSettings.asset` with an extra `preloadedAssets` entry and
`Assets/StreamingAssets/`, `Assets/Resources/PerformanceTestRun*` behind.
They are build-time leftovers: revert/delete them (keep the tracked
`Assets/StreamingAssets.meta`). A `clang++ ... failed due to signal`
(0xC0000005) during IL2CPP is a toolchain crash, not our code: retry once.

### Batch A — independent, mostly different files

**Done: all five passed one headset round and were merged into main on
2026-11-03** (branches `batchA-fastbuild`, `-notenames`, `-calibux`,
`-schema2`, `-tempo`). Changes from the plan, all agreed with Ryan:
- The schema version stays in the existing `schema` field (now 2)
  instead of a new `schemaVersion`: v1 files store `"schema": 1`, so
  they can be told apart, and `AttemptRecord.Parse` gives them the v1
  defaults (continuous, no loop, both hands, no metronome).
- Changing tempo while paused part-way keeps the song where it is. The
  attempt records the **slowest** tempo it was played at
  (`ScoringSession.LowestTempo`).
- Pressing A while the song is already playing starts an attempt that
  scores only the notes still ahead (`scoreNotesFrom`). An attempt that
  is running when Verifying begins (a Middle C fix mid-song) is saved
  as abandoned.
- A nudge clears a refused Middle C warning rather than recomputing it.
- `build.ps1` now runs `adb kill-server` first and waits for Unity
  alone (`WaitForExit`), not its lingering adb child.

1. **Faster development builds** (tooling, do first). In
   `Assets/Editor/BuildScript.cs`, for **development builds only**:
   IL2CPP code generation "Faster (smaller) builds"
   (`PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android,
   Il2CppCodeGeneration.OptimizeSize)`) and C++ compiler configuration
   `Debug` (`PlayerSettings.SetIl2CppCompilerConfiguration`). Set them for
   the build and **restore the previous values afterwards** so release builds
   and `ProjectSettings.asset` are untouched. Time one build before (the
   baseline is 690–810 s) and after; record both here. Check on the headset
   that it still holds 72 Hz.
   **Measured 2026-11-03:** before 690–810 s (release IL2CPP settings);
   after, a development build of `batchA-test` took **166 s** wall clock
   (Unity's own report 149 s), and **92 s** for the rebuild after a small
   script change. Holds 72 Hz on the headset (passed 2026-11-03).
2. **Note names on slabs (F14).** Pure helper (pitch → "C", "F♯"; whether a
   slab is tall enough for its label) with tests. In `NoteHighway`, one 3D
   TextMeshPro label per slab near its bottom, on `PlaneAxes()`; font
   material assigned by `SceneSetup`, never `Shader.Find`. No finger numbers.
   One label per slab is fine for Twinkle; pool them when MIDI loading (F38)
   arrives. Check the font has the ♯ glyph; fall back to "#" if not.
   Headset: legible at playing distance.
3. **Two calibration UX fixes** (from the grid tests): every note scores
   MISS while the flow is still in Verifying (until A) — `ScoreKeeper` should
   not start or advance an attempt while `calibration.AwaitingConfirmation`
   (and the slabs then don't go slate); and a thumbstick nudge does not
   re-run the Middle C check, leaving a stale "Middle C is N mm off" warning
   — clear or recompute it when the nudge changes. Anchored-calibration
   behaviour goes in Core with a test first.
4. **Tempo ±5% (F26)**, **25–200%** (Ryan, 2026-11-03; was 50–100%). **Control: a piano chord** (Ryan's choice,
   2026-11-03): **hold A0 (MIDI 21) and press B0 (23) for −5%, C1 (24) for
   +5%**, modelled on the existing A0 + A♯0 restart chord in
   `SongTransport`. Only acts while the song is stopped or paused (each
   attempt records one tempo); ignored while playing. Pure stepping/clamping
   rule with tests; `SongTransport` applies it to `SongClock.TempoMultiplier`;
   the status panel **always** shows the tempo ("Tempo 85%"), not only
   after a change; the value persists in PlayerPrefs. Above 100% the notes
   give less warning (3 s of song time is 1.5 s real at 200%) and judging
   windows stay in real ms, so nothing else needs changing. A0 alone
   must not trigger anything, and A0 + A♯0 must still restart.
5. **Attempt schema v2.** Nothing visible. Add in one change:
   `schemaVersion: 2`, `mode` ("continuous" / "wait"), `loopFromBar` /
   `loopToBar` (−1 = no loop), `hand` ("both"), `metronome` (bool). Tests
   that the existing v1 attempt files still load with sensible defaults.

### Batch B — needs nothing from A except tempo (for the card)

1. **Liquid Glass shader + shared colour tokens.** Hand-written URP unlit
   transparent shader like `HighwayGrid.shader`: SDF rounded corners, tint,
   rim brightest along the top, Fresnel sheen from the view angle. A
   ScriptableObject token asset (glass tint/alpha/rim/radius, text
   `#E9EEF2` / `#8A96A3`, the outcome colours from `OutcomeTint`). Material
   created and assigned in `SceneSetup`. Try it first on the status panel.
   Headset: readable over a bright window and a dark wall; 72 Hz.
   **Typeface: Outfit** (Ryan, 2026-11-04) on every glass surface; note names on the slabs keep LiberationSans. `Assets/Editor/GlassFont.cs` builds static Regular and Bold atlases from the TTFs in `Assets/UI/Fonts`, fixed instances of the variable font (its default is Thin, all Unity would draw). Outfit has no ♯ ▲ ▼, so those come from LiberationSans through the fallback table.
2. **Results card (F30) + bar heat strip (F32)**, built in that glass. A pure
   tested summary from the attempt record: headline accuracy, verdict,
   change vs the last attempt **at the same tempo and mode** (read from the
   attempts folder), timing as "▼ 37 ms late" (▲/▼ + ms, never colour
   alone), wrong-note count, one cell per bar on the score ramp with the
   worst bar outlined. World-space canvas registered on the keyboard
   (`CanvasCalibrationFollower`), built by `SceneSetup`; shows when an
   attempt ends, hides on play.
3. **Metronome (F27).** First measure the headset's audio output latency
   (phone video/audio: click vs key light). Then schedule clicks on
   `AudioSettings.dspTime` with `AudioSource.PlayScheduled`, from the
   `BeatGrid` beat times minus that latency; accent the first beat of each
   bar. **It clicks during the 3 s lead-in too** (Ryan, 2026-11-03 — this is
   the count-in coming back, by request). On/off toggle, recorded as
   `metronome` in the attempt. **Toggle: hold A0, press C♯1 (MIDI 25)**
   (Ryan, 2026-11-03), only while stopped or paused, saved in PlayerPrefs.
   Output latency (`SceneSetup.MetronomeOutputLatencyMs`) is 140 ms, measured
   on video: clicks against bar lines reaching the key line.

### Batch B status: merged into main 2026-11-05

All of Batch B, the results redesign and the first-press freeze fix passed on
the headset and were merged, in order: `batchB-devbuild`, `batchB-installfix`,
`batchB-glass`, `batchB-results`, `batchB-metronome`, `batchB-outfit`,
`results-redesign`, `first-play-hitch`. `batchB-test` stays unmerged. 451
EditMode tests pass.

- **Results redesign:** card, details panel, ray buttons, card and status panel
  taking turns all passed.
- **First-press freeze:** the longest frame after the first press of play went
  from 390 ms to 28 ms with the `ScoreKeeper` fix. The highway warm-up is gone
  and stays gone: on two fresh launches the longest stall near that press was
  4-5 frames (55-70 ms), about what a development build does mid-song, and Ryan
  saw it as smooth.
- **Release frame rate: 72 fps during songs** (two plays of Twinkle at 90% with
  the metronome on, about 80 s: every second 72/72, where the development build
  runs about 69). GPU about 45%, CPU and GPU both at level 2, 38 °C. A release
  build has anchored calibration off, so measure again if that ever ships.
- **Liquid Glass and Outfit:** Ryan's yes.
- **Metronome:** the 140 ms output latency holds. A 60 fps video of 13 lines
  reaching the key line had the click 17 ms early on average (sd 9; accented
  bars 18 ms early). The phone's own audio/video offset is known only to within
  about 33 ms, so no change. Clicks and lines drift about 13 ms apart over 7 s in
  that video, whose audio and video tracks differ in length by 19 ms, so it may be
  the recording; check it again on a long song once MIDI loading (F38) exists.

Later, not urgent: one stall of 55-85 ms (4-6 stale frames) 1-2 s after each
attempt ends, while the results card is up.

Older commits keep their Co-Authored-By lines: Ryan decided on 2026-11-05 not to
rewrite history. New commits leave them out.

### Batch C status (2026-11-05): five merged, fade still to be seen

Second headset round: `loop-weak-bar` and `calib-recover` passed and were merged.
The log shows the fade ladder never ran inside a loop that session, so
`batchC-fade` stays on its branch for the next round. In-song frame rate 68.7-70.4
fps in the development build, no exceptions from our code.

First headset round: loop, auto-tempo and wait mode passed and were merged into
main, in that order (`batchC-loop`, `batchC-autotempo`, `batchC-wait`). 36
attempts over ten minutes, mean 69.7 fps in the development build, no exceptions
from our code. `batchC-test` is the build and is never merged.

Decided while building, all confirmed by Ryan unless a branch changes it:

- **Starting and leaving a loop.** The results card's button starts it; left Y
  or the A0 + A♯0 chord leaves it, back to the full song. No results card
  between passes (it would cover the next one); the status panel shows the pass
  and how the last one went.
- **The card's range is the weak bar, plus a neighbour only if it was under 90%**
  (Ryan, 2026-11-05: why practise a bar already played well?). Bar 3 weak with
  good bars around it gives "Loop bar 3"; at most one bar either side. This
  replaces the plan's plain ±1. The count-in leads into the bar. Grouped pills on
  long songs keep the 4-bar phrase rule.
- **One bar of count-in** on every pass. A wrap keeps the frame's overshoot.
  Notes, due keys, grid lines and clicks all stop at the loop's end.
- **A pass nobody played in is not saved** and does not change the tempo.
- **Auto-tempo is always on in a loop and is not saved:** leaving the loop goes
  back to the tempo the player chose. It changes at the wrap, not at the last note.
- **Wait mode:** hold A0, press D1. Only the next note or chord is accepted,
  however early; a chord's keys may come in any order and need not be held
  together. Any other key is wrong and names what is awaited. Accuracy is right
  keys out of all keys. Schema 4 adds `correct`. The metronome pauses during a
  hold. Auto-tempo does nothing on a wait-mode pass.
- **Memory fade:** hold A0, press D♯1. Off by default and saved. On a whole song
  the ladder moves once per finished play-through and is saved per song, so a
  memorised song stays faint (Ryan asked for whole songs, the saving is Claude's
  call, 2026-11-05); an abandoned play-through does not move it. Every new loop
  has its own ladder, starting fully visible, and leaves the song's alone. The opacity is a ceiling on anything showing the note: slab, name and
  due key fade together, and the panel stops naming a note at a glimpse. The key's own
  coral on a wrong press is not faded, nor is a key the player holds.
- **The fade ladder (Ryan's steps and request, Claude's system, 2026-11-05):**
  100, 60, 30, 15, then a glimpse (15% for 0.6 s when a note appears, gone 0.5 s
  later), so notes always show at least briefly. It moves by keys right, not
  timing: 90%+ one step fainter, 70-89% stays, 40-69% one step back, under 40% or
  nothing played two steps back. Three mistakes in a row, or 5 s held at a note in
  wait mode, shows notes one step clearer for the rest of the pass, and a pass
  that needed help cannot go fainter. Tempo and fade never change together: tempo
  rises only at a glimpse and falls only at 100%. Values are in `MemoryFade`.

### Batch D (2026-11-05): built, headset round next

Ryan's request: no recalibrating after every headset removal, memory fade on whole
songs, and choosing your own bars. Branches, `batchD-test` is the build and is never
merged:
- `attempt-empty`: confirming the keys after a song had ended saved and showed a 0%
  attempt with no notes, which then also made the next "vs last" read +85%.
- `fade-song` (holds `batchC-fade`): whole-song fade, saved per song.
- `pick-bars`: tap the results card's bar pills to choose a loop.
- `calib-bias`: replaces clearing the nudge on recalibration, see the status doc.
- `anchor-trio`: two measuring anchors. Nothing visible changes.
- `press-probe`: logs fingertips against pressed keys. Nothing visible changes.

The last two only measure. Ryan would rather not use a marked controller spot on
the piano (idea 4 in the status doc), so that is kept for if nothing else works.

### Batch C — all build on the loop

1. **Loop worst bar ± 1 (F33).** Pure tested range rule from the worst bar,
   clamped to the song. `SongClock` jumps back to just before the range when
   it passes the end. `ScoreKeeper` today treats any backwards jump as a
   restart (abandoned); a loop wrap must instead save that pass as its own
   attempt with `loopFromBar`/`loopToBar`, judging only notes in the range.
   Slabs already reset on `AttemptReset`.
2. **Auto-tempo in the loop (F35).** Pure tested rule, shown on screen: after
   a pass, accuracy ≥ 90% → +5% (up to 100%: the goal is full speed, so
   auto-tempo never goes past it even though manual tempo can), < 70% → −5%
   (down to 25%), else hold.
3. **Wait mode (F22).** Pure tested gate: the clock holds at the next note
   until every pitch of it (chords included) is pressed. Pitch-only judging,
   no PERFECT/GREAT; attempt `mode: "wait"`, never compared with continuous
   attempts.
4. **Memory fade (F36).** Pure tested opacity (a ladder, see the status above);
   needs a transparent slab material assigned in `SceneSetup`.
   **A faded note still turns mint/slate once judged, at the opacity it already
   had (Ryan, 2026-11-05):** the outcome changes its colour only, never its
   opacity. A half-faded note shows half-visible mint or slate; a note whose
   glimpse has ended stays hidden.

After Batch C, Phase 3 starts with MIDI file loading (F38).

The beat-lines branch was dropped on 2026-10-30 because of how the lines
looked, not because of the idea. The grid is back in phase 1, and it must
look **exactly like the reference video**, nothing like that branch. The
visual target is written down in `docs/feature-evaluation.md` ("Highway grid:
visual target"). Only its timing maths (`BeatGrid.cs` and its tests, tag
`beat-lines-archive`) may be reused, and only if it helps.

Open, not urgent: measure the leftover input offset (video test above); tune
the 40/80/150 ms windows from more attempts; maybe a faster highway (0.5 m over
2 s instead of 3 s) compared by spread; rewrite this file's scope into
Now / Next / Later tiers (waiting on whether the 12-week timeline is a hard
deadline); `DeviceChecklist` step 4 never advances on device (dev tool only);
measure the seated vertical overlay error (`docs/anchored-calibration-status.md`).
The two calibration UX issues from the grid tests (stale Middle C warning
after a nudge; MISS while in Verifying) are scheduled in Batch A.

The song clock must stay a `double` of seconds with a tempo multiplier, and
note positions must derive from `(noteTime − songTime)`, never from frame
counts. Scoring is the study instrument: every attempt is written to JSON.
