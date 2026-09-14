# PianoGlass

Mixed-reality piano trainer for **Meta Quest 2**. Notes fall onto the keys of a
real 88-key piano seen through passthrough; a USB MIDI keyboard provides
ground-truth input. Built by one student developer, ~7 hours a week.

The product claim is **feedback, not price**: a video cannot tell you that you
played D instead of D♯, or that bar 9 is where you keep falling apart. That is
what PianoGlass exists to do. Features that do not serve that claim are cut.

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
    KeyboardCalibration.cs            capture, nudge, persist (Meta-free)
    OvrCalibrationInput.cs            calibration's OVRInput code
    PianoKeyHighlighter.cs            real key pressed → virtual key lights
    CalibrationPanel.cs               in-headset calibration readout
  Scripts/Song/
    Song.cs                           SongNote + SongData, hard-coded 8-bar melody (own asmdef)
    SongClock.cs                      double songTime, tempo multiplier
  Scripts/Highway/
    NoteHighway.cs                    falling-note wall, position from (noteTime − songTime)
  Scripts/Transport/
    SongTransport.cs                  play/pause/restart + MIDI chord (Meta-free)
    OvrSongTransport.cs               transport's OVRInput code
  Tests/EditMode/                     PianoKeyLayout + SongClock, run headlessly
```

OVRInput now lives in two places, split the same way as calibration:
`OvrCalibrationInput.cs` and `OvrSongTransport.cs` are the only files that
touch it, and each is a thin adapter over a Meta-free component
(`KeyboardCalibration`, `SongTransport`) that does the actual work and is
what compiles and runs in the editor.

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

---

## Traps already hit — do not re-learn these

- **Unity does not compile a `.java` file until you select it in the Project
  window and tick Android under "Select platforms for plugin".** Toggle it off,
  Apply, on, Apply to force the `PluginImporter` block to serialize. Without
  it the build succeeds and fails at runtime with `ClassNotFoundException`.
- **A Screen Space - Overlay canvas is invisible in VR.** World Space, and
  scaled to ~0.001 or it is a 600-metre wall. Cost a full build cycle.
- **Unity hides file extensions in the Project window.** Rename `.cs.txt` files
  in Explorer, not in Unity.
- **Do not overwrite Meta's generated `AndroidManifest.xml`.** Add the one line
  `<uses-feature android:name="android.software.midi" android:required="false" />`
  by hand and leave the passthrough entries alone.
- **A MIDI note-on with velocity 0 is a note-off.** Most keyboards send these.
  Missed, every note sticks on forever.
- **To *receive* from a keyboard you open its OUTPUT port**, not its input port.
  Getting this backwards gives silence and no error.
- **Assign materials in the Inspector.** A runtime `Shader.Find` gets stripped
  from device builds and you see nothing, with no clue why.
- **`OVRInput.Button.PrimaryIndexTrigger` without a `Controller` argument**
  resolves against whichever controller is "active", and `Secondary...` may
  resolve to nothing. Always name `OVRInput.Controller.RTouch` / `LTouch`.
- **`FindFirstObjectByType` is deprecated in 6.6** — use `FindAnyObjectByType`.
- **`git commit -am` ignores untracked files.** Any new Unity asset — script,
  material, prefab, scene — needs `git add -A`. Check `git status` first.
- **Meta XR Audio SDK 205.0.0 has a runtime script that references editor-only
  APIs** and can block player builds with `CS0234 UnityEditor.GUID`. Restarting
  the editor cleared it once. If it returns, replace `com.meta.xr.sdk.all` with
  `com.meta.xr.sdk.core` in `Packages/manifest.json` — nothing here uses audio,
  platform, interaction or voice.

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

## Current state and what is next

Working and proven on device: MIDI input end to end, 88-key overlay registered
on the real piano to under half a percent of correct, calibration persisted with
runtime fine-adjust.

Next is the vertical slice — song data → song clock → falling-note highway → hit
detection → wait-mode — built thin and ugly, end to end, before any of it is
polished. The song clock must be a `double` of seconds with a tempo multiplier,
and note positions must derive from `(noteTime − songTime)`, never from frame
counts. Getting that wrong means rewriting everything above it.

Scoring is not a later feature: it is the study instrument. Build it into the
loop, and write every attempt to JSON on the headset before user testing starts.
