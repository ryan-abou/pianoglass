# History

Batch plans, round results and the state of the project as CLAUDE.md said it, moved out word for word on 2026-11-07. Calibration rounds are in `docs/calibration-rounds.md`.

## MIDI songs (`midi-loading`, built 2026-11-08)

Built from `SPEC.md`. At the desk: 727 EditMode tests pass (578 before), and
`[SongCheck] PASS` covers all 7 steps in batchmode Play Mode. The song check
caught one real bug: the results card comes up while the last note is still
sounding, so the clock still counted as playing and the picker closed itself.
The Songs button now stops the clock.

To run the song check with the editor closed, use the batchmode test command in
`CLAUDE.md` with `-executeMethod PianoGlass.EditorTools.SongCheckRunner.RunHeadless`
in place of the test arguments. Leave off `-quit`: it exits by itself with 0 or 1.

Still to do, on the headset: the Songs round in `SPEC.md`. The picker's spot is
a placeholder until then.

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
   done, Batch B), ~~loop worst bar~~, ~~auto-tempo~~, ~~wait mode~~, ~~memory fade~~ (all done,
   Batch C).
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

### Batch C status: merged into main 2026-11-05

Third headset round: fade passed in loops and on whole songs and was merged
(`batchC-fade`, `fade-song`). 68.0-68.7 fps during loops with hands tracked,
against 68.7-70.4 before, and Ryan felt no difference.

Second headset round: `loop-weak-bar` and `calib-recover` passed and were merged.

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
  timing: 90%+ one step fainter, 70-89% stays, 40-69% one step back, under 40%
  two steps back. Three mistakes in a row, or 5 s held at a note in
  wait mode, shows notes one step clearer for the rest of the pass, and a pass
  that needed help cannot go fainter. Tempo and fade never change together: tempo
  rises only at a glimpse and falls only at 100%. Values are in `MemoryFade`.
- **Fade in wait mode (Ryan's problem and flash, Claude's measure, 2026-11-05):**
  right keys say nothing without a clock, since every key can be tried. So a stop
  counts only if it was right first time and never had to be shown (`WaitRecall`),
  and the same ladder reads that share ("3 of 4 from memory"). At a glimpse, a
  wrong key flashes just that note on the highway, and 2.5 s held brings it back at
  15% until it is played (Ryan, 2026-11-06); both fade in over 0.2 s, and the rest
  of the pass stays hidden. Other steps keep three mistakes or 5 s. Accuracy and
  rank on the card are unchanged.
- **A pass with no key pressed moves nothing** (Ryan, 2026-11-06): the player has
  most likely walked away.

### Batch D (2026-11-05): four merged, second round next

Merged after one headset round: `attempt-empty` (confirming the keys after a song
ended saved a 0% attempt), `fade-song` (whole-song fade, saved per song), `pick-bars`
(tap the results card's pills to choose a loop), `calib-bias` (see the status doc).
`batchD-test` and `batchE-test` were builds; neither is ever merged.

`fade-wait` passed and was merged. `batchF-test` was a build, not merged.

Merged 2026-11-07 after the `batchF-test` round: `fade-reveal` (at a glimpse, a note
brought back fades in and stays until played, and a pass with no key pressed moves
nothing; both Ryan's) and `middle-c-small` (a Middle C touch on keys already right
made them worse).

Next round, each on its own branch:
- `return-settle` (holds `anchor-trio`): no check after time away unless the anchor
  moves, and nudges before the headset comes off count as the calibration bias. The
  bias did not land the keys where they were left (status doc, fifth round), and a
  20+ minute return has not been run.
- `press-probe`: still measuring. Nothing visible changes.

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
