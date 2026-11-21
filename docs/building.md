# Building, testing and reaching Unity

Moved out of CLAUDE.md on 2026-11-07, word for word. CLAUDE.md keeps the short version.

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

## The in-headset test checklist (development builds)

Every post-build test checklist is also a file the headset shows on a Liquid Glass
panel, so it never has to be memorised. 5-8 items, most important first. Push a new
one at any time; no rebuild, and an open panel picks it up within a second:

```powershell
adb push checklist.md /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/checklist.md
```

The file is plain Markdown. The first `#` line is the title; each `- ` (or `1. `)
line is an item, and text after ` | ` is what pass looks like, drawn smaller:

```markdown
# Batch G round
- Launch, don't recenter for 15 s, screenshot | keys on the piano?
- Middle C fix with the bare tip | keys stay put, no nudge after
```

Hold the left grip, then press X to open or close it (it opens in front of you and
always starts hidden). Tick a row with the right controller ray and right trigger.
Ticks survive a relaunch (`checklist_ticks.json`) and are logged as `[Checklist]`
lines, so they sit on the logcat timeline next to everything else. Start the ticks
over: `adb shell rm /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/checklist_ticks.json`.

**Recording mode** (hold the left grip, then press Y) switches off every debug
overlay for milestone videos: the status panel (MIDI bridge, calibration readout)
and this checklist; turning it off brings back whatever was showing. It is remembered across launches,
so check you have turned it off before a test round: the calibration prompts are on
the status panel. Unity's "Development Build" watermark does not show in the headset.

All of it is compiled out of release builds (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`
in `Assets/Scripts/DevTools/`), so it can never reach a study participant.

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

