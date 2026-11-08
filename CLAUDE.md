# PianoGlass

Mixed-reality piano trainer for **Meta Quest 2**. Notes fall onto the keys of a
real 88-key piano seen through passthrough; a USB MIDI keyboard provides
ground-truth input. Built by one student developer, ~7 hours a week.

The product claim is **feedback, not price**: a video cannot tell you that you
played D instead of D♯, or that bar 9 is where you keep falling apart. It also
has to **look and feel finished**: every menu and panel is **Liquid Glass**
(the highway grid is the one exception). A feature that does neither is cut.

Where things live:
- `docs/feature-evaluation.md`: what is in, deferred and cut, and the build order.
- `docs/architecture.md`: product, hardware, code layout, passthrough, key overlay.
- `docs/controls.md`: every control. **Update it whenever a control changes.**
- `docs/building.md`: full build notes, environment, the MCP vs `build.ps1` rule.
- `docs/traps.md`: the full text of every trap listed below.
- `docs/anchored-calibration-status.md` (update after every phase) and
  `docs/calibration-rounds.md`: anchored calibration, its rules and rounds.
- `docs/history.md`: batch plans, how a batch runs, every round's results.
- `docs/glossary.md`: plain meanings of technical words, with PianoGlass examples.
- Board: https://github.com/users/ryan-abou/projects/2 (To do, top to bottom).

## Environment — pinned, do not upgrade mid-project

| | |
|---|---|
| Unity | **6000.6.0f1** (URP 17.6.0) |
| Meta XR All-in-One SDK | **205.0.0** |
| OpenXR | **1.18.0** |
| Scripting | IL2CPP, **ARM64 only**, minSdk 26 |
| Graphics | Vulkan then GLES3, ASTC textures |
| Bundle ID | `com.ryanabourjeili.pianoglass` |

`adb` (on PATH) is in the editor's `AndroidPlayer\SDK\platform-tools`. SpaceWarp is
**deliberately off** (it smears falling notes and text). Meta Platform SDK checklist
items are **deliberately ignored**: no entitlements, leaderboards or IAP.

## Build and test

Close the Unity editor first: `Get-Process Unity` prints nothing when it is
closed. Run `adb kill-server` before every batchmode run.

```powershell
.\build.ps1                # release
.\build.ps1 -Development   # profiler + script debugging, never for a tester
.\build.ps1 -Install       # build, then adb install to the headset
adb install -r Builds\PianoGlass.apk
adb logcat -s PianoGlassMidi Unity
```

`build.ps1` exits with Unity's code (0 ok, 1 failed) and prints every
`[PianoGlassBuild]` line. Never start `Unity.exe` with `&`: it returns at once
and looks like a failed build. EditMode tests (~3-4 min, no `-quit`):

```powershell
adb kill-server; $out = "$env:TEMP\pg-tests"; New-Item -ItemType Directory -Force $out | Out-Null
$p = Start-Process "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -PassThru -ArgumentList @(
  '-batchmode','-projectPath','C:\Dev\PianoGlass\PianoGlass','-buildTarget','Android',
  '-runTests','-testPlatform','EditMode','-testResults',"$out\editmode.xml",'-logFile',"$out\editmode.log")
$null = $p.Handle; $p.WaitForExit()
([xml](Get-Content "$out\editmode.xml"))."test-run" | Select-Object total, passed, failed
```

A SceneSetup method: same command, `-quit -executeMethod PianoGlass.EditorTools.SceneSetup.<Method>`.

**Unity MCP needs the editor open; `build.ps1` needs it closed.** Use the MCP to
look (Console, hierarchy, field values), never to change: scene changes go
through `Assets/Editor/SceneSetup.cs`. A task that needs both: inspect, close
Unity, build, reopen. Never build first and change the scene afterwards.

## Hardware realities

- **One USB-C port** for both the MIDI keyboard and the PC cable. What always works: build
  over USB, `adb install -r`, unplug, connect the keyboard, **App Library → Unknown Sources**.
- **Passthrough is monochrome and low-res**: draw a legible overlay on the real keys.
- **No depth sensor**, so no occlusion: keep the highway above and behind the keys.
- **About half a Quest 3's GPU**: URP, no realtime shadows, unlit, 72 Hz.

## Traps already hit — do not re-learn these

Full text in `docs/traps.md`.
- A `.java` plugin compiles only once Android is ticked in its import settings
  (toggle off, Apply, on, Apply), or it fails at runtime with `ClassNotFoundException`.
- Screen Space - Overlay canvases are invisible in VR: World Space, scale ~0.001,
  and registered on the keyboard (a world-space Canvas at y = 0 is on the floor).
- A running adb server makes batchmode Unity exit `0xC0000409`: `adb kill-server`.
- Unity hides file extensions: rename `.cs.txt` files in Explorer.
- Don't touch `Assets/Plugins/Android/AndroidManifest.xml`; Meta's Gradle step
  adds passthrough and anchors. Check the APK with `aapt dump permissions`.
- A MIDI note-on with velocity 0 is a note-off. To receive, open the keyboard's OUTPUT port.
- Assign materials in the Inspector or SceneSetup; `Shader.Find` gets stripped.
- Always name `OVRInput.Controller.LTouch` / `RTouch`.
- The first `DateTime.Now` on the headset takes ~380 ms. Profile stalls before guessing.
- `FindFirstObjectByType` is deprecated in 6.6: use `FindAnyObjectByType`.
- `git commit -am` skips new files: `git add -A`.
- Meta XR Audio SDK can block builds with `CS0234 UnityEditor.GUID`: restart, or
  swap `sdk.all` for `sdk.core` + `sdk.interaction` 205.0.0.
- Passthrough shows black if the camera clears to `Skybox`: needs `SolidColor`
  with alpha 0 **and** `RenderSettings.skybox = null`; check HDR and post too.
- After a failed build, revert the extra `preloadedAssets` entry and delete
  `Assets/StreamingAssets/` and `Assets/Resources/PerformanceTestRun*` (keep
  `Assets/StreamingAssets.meta`). An IL2CPP `clang++` 0xC0000005 crash: retry once.

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

Whenever you mention an issue, write its number and title together, like
"#1 MIDI file loading". When you start work on an issue, move its card to Doing
on the project board (github.com/users/ryan-abou/projects/2) with gh, and tell
me in one line, like "Moved #1 MIDI file loading to Doing." PRs that finish a
feature or a bug from the board must say "Closes #N". Small fixes (typos,
cleanup) don't need an issue. When a PR is merged, tell me which issues it
closed, with their titles, and that GitHub will move them to Done.

Never add Co-Authored-By lines, 'Generated with Claude Code' footers, or any
Claude/AI attribution to commits, PR descriptions, issues, or comments.

## How I write on GitHub

Commit messages, issues, PR descriptions, and comments should sound like me, a
high school student, not a pro engineer or an AI.

- Short and plain. Write what I'd actually type. Commit subject under 60
  characters.
- Simple words. Say "keys line up wrong after taking the headset off," not
  "anchor drift degrades spatial persistence."
- Only use a technical word if it's the actual name of the thing (MIDI,
  calibration, passthrough, a file or class name).
- No em dashes, no emojis, no bold, no headers in commits.
- Don't use words like: leverage, robust, seamless, comprehensive, enhance,
  streamline, utilize, facilitate, ensure, implement (just say "add" or "make").
- Issues: 1 to 3 sentences about what's wrong or what I want, then a short
  checklist if it helps. No "Summary / Background / Acceptance Criteria"
  headings.
- PR descriptions: a few sentences about what changed and how I tested it. No
  long bullet lists.

Good commit examples:

```
Load songs from MIDI files
Fix keys showing up a little to the left after recalibrating
Add Twinkle Twinkle and Ode to Joy
```

Bad commit example:

```
Implement robust MIDI file ingestion pipeline with comprehensive error handling
```

## Current state (2026-11-08)

Proven on device: MIDI input, the key overlay, calibration (anchored in dev
builds), the highway (grid, note names, outcome colours), scoring, latency
compensation, results card, tempo, metronome, loops, auto-tempo, wait mode,
memory fade. On branches: `return-settle` (holds `anchor-trio`), `press-probe`,
`calib-bias-total`.

Merged from `midi-loading` after its headset round (`SPEC.md`): MIDI songs, three built-in
songs, the song picker, one-hand practice with outlines, tempo and time signature changes.
72 fps still needs reading on a release build. Next up: #11 Play songs with both hands, on
a `both-hands` branch. Known problem: at launch the keys start about a piano width to the
right until a recenter (`docs/history.md`).

Next: the board's To do column, top to bottom. When Ryan says "do Batch X", follow
"How a batch runs" in `docs/history.md`.

Rules that stay: the song clock is a `double` of seconds with a tempo multiplier, and note
positions come from `(noteTime − songTime)`, never frame counts. Every attempt is written
to JSON. Latency calibration is a system measurement, never the player's mean error.
