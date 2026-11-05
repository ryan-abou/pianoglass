# Feature evaluation: PianoVision tutorial vs PianoGlass

Source: "PianoVision Tutorial" (PianoVision, YouTube `apwZTV-Rg0s`, 5:24),
watched 2026-10-30 with scene-change frames plus 30 extra frames pinned to the
moments the narration describes. Timestamps are video time.

Measured against `CLAUDE.md` and the code on `main` at `f626be0`.

**Decisions: Ryan, 2026-10-30.** Every feature was reviewed one by one. Ryan's
choice overrides my original verdict wherever they differ, and both are
recorded in Part 2. The test is no longer feedback only: a feature earns its
place if it **sharpens feedback or makes the app look and feel finished**.

---

## Part 1: Feature inventory

"Shown" means the feature is visibly used on screen. "Mentioned" means the
narration names it but it is never demonstrated.

### Setup and calibration

| # | Feature | Time | Shown? |
|---|---|---|---|
| F1 | Setup wizard "What are you playing on?": three cards (Acoustic / MIDI / Virtual) with pill badges ("RECOMMENDED" green, "SCORING DISABLED" pink) and a full-width Next button | 0:21–0:38 | Shown |
| F2 | Virtual hand-tracked piano on a flat surface or in mid-air | 0:19 | Mentioned. Only its card is shown, never played |
| F3 | Acoustic-piano mode: no MIDI, so no post-performance scores | 0:39 | Mentioned. Card shown with "SCORING DISABLED" |
| F4 | MIDI keyboard input, the recommended path | 0:31 | Shown |
| F5 | Keyboard "Adjuster" panel: rotate CW/CCW, forward/back/left/right/up/down, width +/−, step size ("1X Speed"), keyboard type, collapse chevron. "Available at any time." | 0:47–0:56 | Shown |
| F6 | Letter labels (C D E F G A B) on every key of the virtual overlay, with a coloured strip at each key's back edge | 0:50 onward | Shown |

### Song library and menu

| # | Feature | Time | Shown? |
|---|---|---|---|
| F7 | Song browser: left sidebar (Favorites, History, My Files, Exercises, then Level 0…), list rows of title plus composer, selected row filled violet, full-width white "Play Song" pill | 0:58 | Shown |
| F8 | Search field and shuffle button | 0:58 | Shown, never used |
| F9 | Preview toggle (speaker icon plus switch): songs auto-play when selected | 1:03–1:08 | Toggle shown. Audio cannot be verified from the video |
| F10 | Favourites: heart button on every row | 1:10 | Shown |
| F11 | 1000+ songs in difficulty levels 0–12; "start at level 0" | 1:13–1:20 | Shown (levels 0–4 visible) |
| F12 | Floating bottom dock: current level, wall clock ("3:56 PM"), round icon buttons (songs, multiplayer, settings, lightning bolt), collapse chevron | 0:58 onward | Shown. The lightning button is never explained |

### Note highway

| # | Feature | Time | Shown? |
|---|---|---|---|
| F13 | 3D note slabs coloured by hand: pink = left, blue = right | 1:27–1:40 | Shown |
| F14 | Note letter and finger number printed on each slab ("G 4", "E 3") | 1:33 onward | Shown |
| F15 | Finger number above each key at the hit line | 1:33 | Shown |
| F16 | Bar numbers in small black tags at the left edge of the highway | 1:27 onward | Shown, never mentioned |
| F17 | Grid on the highway: lane lines and horizontal beat lines | 1:27 onward | Shown, never mentioned |
| F18 | The key that is due fills with its hand's colour on the keyboard | 1:38 | Shown |
| F19 | Hit feedback: slab turns green and a green glow blooms at the hit line | 1:33–1:44 | Shown |
| F20 | Missed note: slab goes grey | 4:00 | Shown (grey G slab). This reading is inferred, never narrated |
| F21 | Chord name above a chord ("C major") | 1:44 | Shown |
| F22 | Waiting notes (wait mode), on by default: the song stops until you press the right keys | 1:42–1:53 | Shown |
| F23 | Waiting off: continuous playback | 3:55–4:04 | Shown |

### In-song control panel (under the keyboard)

| # | Feature | Time | Shown? |
|---|---|---|---|
| F24 | Control panel: song title, coral "Stop Song" button, progress bar with elapsed time ("0:10"), row of square icon toggles, hide chevron | 1:53–1:58, 3:54 | Shown |
| F25 | One-handed practice: pick a hand, the other hand's notes play automatically | 2:01–2:08 | Button shown ("Right"). Auto-play only mentioned |
| F26 | Tempo: −5% / "Tempo 1.00" / +5% | 1:56, 4:04–4:10 | Shown |
| F27 | Metronome toggle (metronome icon) | 3:54 | Icon shown, never mentioned |
| F28 | Record button (circle icon) | 3:54 | Icon shown, never mentioned |
| F29 | Sheet-music overlay (book icon): notation panel above the highway | 4:11–4:25 | Mentioned. A white panel is visible at 4:19 but never read |

### Results and practice

| # | Feature | Time | Shown? |
|---|---|---|---|
| F30 | "Song Complete" card: arc gauge (76%, "Tempo 100%"), Overview bars for Total / Left / Right with n/m and %, verdict tile "Needs work", score change vs last time (red ▼ −14%), wrong-notes count; buttons Back To Song Library / Play Song Again / Memory Practice (BETA) / Learning Engine (BETA) | 2:10–2:17 | Shown |
| F31 | "See More": per-section performance (A, B, A, C) for Left / Right / Both | 2:18–2:30 | Shown |
| F32 | Performance tab: section list, a grid of per-measure bars coloured red→amber→green, hint text "Tap a measure to loop. Tap a second to loop a range.", History table (hand, score, tempo), player badge "P1" | 2:38–2:52 | Shown |
| F33 | Looping a measure, range or section | 2:45–2:58 | Shown (measures 11 and 13 selected at 2:52) |
| F34 | Engine settings: Loop Type (Sections / Full Song), Intensity (Casual / Aggressive), Infinite Looping toggle | 2:52–3:02 | Shown |
| F35 | "Learning engine": finds the section you need to practise and slows the tempo to your level | 3:00–3:14 | Mentioned. A highway clip plays, but the adaptation is not visible |
| F36 | "Memory engine": note hints fade out over time so you play from memory | 3:15–3:36 | Mentioned. 3:25 shows one faded note only |
| F37 | History: songs you played recently | 3:38–3:45 | Shown |
| F38 | My Files: load your own MIDI files (MuseScore, the Discord MIDI channel) | 3:46–3:55 | Mentioned. Loading is never shown |
| F39 | Exercises: scales and chords, filterable by key (C, C#, D … Bb) | 4:36–4:45 | Shown |
| F40 | Multiplayer: jam with friends, hang out | 4:47–4:58 | Mentioned. A short highway clip is shown |
| F41 | Settings: Keyboard Setup (type MIDI / Acoustic / Virtual; size 25/32/37/49/61/76/88), Personalization, Environment, Instrument Audio, Tutorial | 4:58–5:03 | Shown (Keyboard Setup page only) |
| F42 | "Play-along audio", "finger helpers" | 5:03–5:06 | Mentioned only |
| F43 | Discord community | 3:52, 5:11 | Mentioned only |
| F44 | All UI driven by hand-tracking pokes; controllers set down on the piano | throughout | Shown |

---

## Part 2: Decisions

**Effort:** small = one session or less; medium = 2–4 sessions; large = more.
A session is roughly one evening.

**Mine** is my original verdict. It is shown only where Ryan chose differently.

### Add (21 items)

| # | Feature, as decided | Mine | Effort and files | Deps / cost |
|---|---|---|---|---|
| F19/F20 | **Outcome on the slab**: hit tints the slab, missed note greys out, wrong key flashes | | Small. `NoteHighway` per-slab colour, `ScoringSession.Judged` / `WrongNotePlayed` via `ScoreKeeper`, `PianoKeyHighlighter.SetColor` | None |
| F14 | **Note name on each slab** (no finger numbers) | | Small. Pooled TMP labels in `NoteHighway`, `MidiInput.NoteName` | None |
| F16 | **Bar lines and bar numbers on the highway**, cleanly drawn, matching the video's look | Maybe later | Small code, real visual risk (see "Harder than it looks"). Reuse `BeatGrid` maths from commit `cf0f8bc`; drawing in `NoteHighway` | None |
| F17 | **Beat lines and lane lines**, as clean as the video's grid and as close to it as possible | Skip | Same as F16: one grid, built together | None |
| F26 | **Tempo ±5% in the headset**, 25–200% (widened from 50–100% on 2026-11-03), always shown on the panel | | Small. `SongTransport`, `OvrSongTransport` (or MIDI chord), readout on the panel | None |
| F27 | **Metronome** (already Next #1 in `CLAUDE.md`) | | Medium. Measure audio output latency, `AudioSource.PlayScheduled`, on/off in the attempt | None |
| F30 | **Results card**: headline accuracy, verdict, change vs last attempt at the same tempo, wrong notes | | Small–medium. `ScoreKeeper` reads the previous attempt JSON; new results layout | None |
| F32 | **Bar heat strip** on the results, worst bar outlined | | Small. UI cells from `ScoringSession.Bars()` | None |
| F33 | **Loop a bar range**, starting with "loop worst bar ± 1" | | Medium. `SongClock` seek/loop, `SongTransport`, one attempt per pass, `ScoringSession` limited via `isPlayable` | None |
| F35 | **Auto-tempo inside the loop**, with visible rules | | Small after F26 + F33. Pure rule in Core, tested | None |
| F22 | **Wait mode**, judged on pitch only | | Medium. `SongClock` hold, `MidiInput.IsHeld`, scoring mode in the attempt | None |
| F36 | **Memory fade** over loop passes | | Small–medium. Per-slab alpha, transparent slab material assigned in `SceneSetup` | None |
| F38 | **Load your own MIDI files** (adb push, no file picker) | | Medium–large. SMF parser in Core, tempo map in `SongData`, fixture tests | None (own parser) |
| F7 | **Song picker** | | Small after F38 | None |
| F11 | **Free, public-domain starter library**, only if it costs nothing | Skip | Curation time, not code: sequencing or vetting files, grading difficulty | $0 by rule. See "Harder than it looks" for licensing |
| F13 | **Notes coloured by hand**, customisable in-app **from preset colour pairs** (decided 2026-10-30; a free colour picker is in Maybe later) | | Small after F38 for the colouring. The presets need the settings menu (S1) | None |
| F25 | **One-hand practice**: other hand dimmed and unscored, no auto-play | | Small after F13. Existing `isPlayable` filter | None |
| F18 | **Due-key highlight in the hand's colour**, with **an on/off toggle** in the settings menu | Skip (already have) | Small for colour after F13. The toggle comes with S1 | None |
| F44 | **Hand-tracking UI** for menus | Skip | Large. Meta Interaction SDK poke on world-space canvases, controller ↔ hands switching | Interaction SDK is in `com.meta.xr.sdk.all` (already installed); hand tracking already enabled in `OculusProjectConfig.asset` |
| F12 + F24 | **One hand-tracked panel**: dock and in-song controls merged (songs, settings, stop, tempo, progress, toggles). Ryan: "make both the last panel and this panel into one thing", read as the F12 dock + F24 | Skip, Skip | Medium after F44. New Canvas registered on the keyboard via `CanvasCalibrationFollower`, wired in `SceneSetup` | None |
| S1 | **Settings menu** (new; follows from F13 and F18): hand colours, due-key toggle, display mode, passthrough, wait mode, metronome, memory fade | — | Medium. A Meta-free `PianoSettings` component that saves to PlayerPrefs (as display mode does now), and its page on the F12/F24 panel | None |

### Maybe later (16 items)

| # | Feature | Mine | When to revisit |
|---|---|---|---|
| C1 | **Free colour picker for hand colours** (new; follows from F13) | — | **Planned**: once the menus (S1 + the hand-tracked panel) exist and presets have shipped. Same guard rail as the presets: the outcome colours stay reserved |
| A1 | **Better UI animations in general** (new; Ryan, 2026-11-04). First case: the results details panel's sections are meant to fade in one after another, but on the headset everything appears at once | — | After the MVP and the other important features |
| F37 | Per-song history | Maybe later | **Queued**: build once F30, F38 and F7 are done (Ryan wants it) |
| F39 | Exercises (scales, chords) | Maybe later | After asking experienced pianists which drills matter most |
| F6 | Letter labels on every key | Maybe later | Paint on the key plane covers hands (no depth sensor) |
| F41 | Keyboard sizes other than 88 | Maybe later | First tester with a smaller keyboard |
| F8 → I1 | **Piano keys as input**: letters A–Z on white keys for type-to-search, and keys for navigating menus in general | Skip | Once the library is big enough to search. Fits well: hands are already on the keys, MIDI is reliable, `SongTransport` already uses a MIDI chord |
| F10 | Favourites | Skip | When the library grows |
| F3 | Acoustic-piano mode (no scoring) | Skip | Only if non-MIDI players become a target |
| F9 | Song preview audio | Skip | If in-app audio arrives (needs a synth/soundfont) |
| F29 | Sheet-music overlay | Skip | Large; notation readability on Quest 2 is the open question |
| F34 | Loop/practice settings | Skip | After testers use the loop with defaults |
| F40 | Multiplayer | Skip | "Much later, if ever", as before |
| F43 | Community (Discord) | Skip | Once there are users outside the study |
| F4 | MIDI connection polish | Skip (have) | With the panel redesign |
| F5 | Hand-tracked keyboard adjuster | Skip (have) | After F44 and the panel exist |

### Skip (8 items)

| # | Feature | Reason |
|---|---|---|
| F1 | Setup wizard | One input type; `CalibrationFlow` handles first launch |
| F2 | Air / table piano | No real keys or MIDI, so no trustworthy feedback |
| F15 | Finger numbers | MIDI has no fingering data |
| F21 | Chord names | Low value for the target songs |
| F23 | Continuous playback changes | Already built; stays the timed, scored mode |
| F28 | Record button | Quest system recording exists |
| F31 | Section view | Bars are finer; MIDI rarely marks sections |
| F42 | Play-along audio, finger helpers | Synth dependency; no fingering data |

---

## Part 3: Build order

Four phases. Within a phase, the order matters only where a dependency is
noted.

### Phase 1: The highway looks finished (all in `NoteHighway`)

These all edit the same file. Doing them back to back means one round of
churn and one headset test.

1. **Grid look spike (F16 + F17).** Before writing the feature, put a
   headset screenshot next to the reference frames listed in "Highway grid:
   visual target" below, and iterate until they match. This is the step the
   dropped branch skipped.
2. **Bar lines, bar numbers, beat lines, lane lines (F16, F17).** The video
   is the only visual target. From `cf0f8bc` (tag `beat-lines-archive`),
   reuse **only** the timing maths: `BeatGrid.cs` (which song times get a
   line, which are bar lines) and `BeatGridTests.cs`. Do not reuse any of its
   drawing code or look. That means:
   - no cubes;
   - no amber lead-in lines;
   - no thick/bright bar lines;
   - no lines limited to the song's key range;
   - no count-in. It was dropped with the branch on 2026-10-30 and not
     revisited in the brainstorm; bring it back only if Ryan asks.
     **Ryan asked (2026-11-03):** the metronome (step 8) clicks during the
     3 s lead-in, which is the count-in returning as audio, not as lines.

   If the maths gets in the way of matching the video, change the maths.

   **Done, merged 2026-11-03** (branch `phase1-grid`, four headset rounds of
   phone photos through the lens). What shipped: the highway leans back 60°
   like the video's runway and starts 5 mm behind the keys (20 mm until the
   key depth was measured on 2026-11-03, then 15, then 5); one lane line
   per octave on each C's left edge (0.8 mm wide); beat lines very faint
   (alpha 0.06), bar lines brighter but not thicker (0.15); dark rounded tag
   with a white bar number, left of the lowest key, upright and facing the
   player; a dark hit line on the runway side of the key line only, so it
   never covers the keys. Drawn by `HighwayGrid` as one SDF-shaded mesh
   (`Assets/Shaders/HighwayGrid.shader`) with a minimum on-screen line width
   so thin lines don't shimmer.

### Highway grid: visual target

Ryan's requirement: the grid must look **exactly like the video**, and
nothing like the dropped `beat-lines` branch. These notes come from the
frames at 1:27, 1:33, 1:38, 1:44, 1:50, 3:08 and 4:00. They are a starting
point for the spike, not a substitute for looking at those frames.

- **What it is.** A wall of thin lines on the passthrough, with no fill
  behind it, rising from the back edge of the keys and receding in
  perspective. Notes fall down it onto the keys.
- **Colour.** Plain white/light grey only. No colour, no glow, no gradient.
  It reads as quiet next to the saturated note slabs.
- **Lane lines (vertical).** Thin, evenly spaced, running the full height of
  the wall and the **full width of the keyboard**, not just the song's
  range. They are the most visible part of the grid.
  - **Measure the spacing from the frames:** how many keys between lines,
    and which key each line sits on. I could not tell reliably from 512 px
    frames.
- **Beat lines (horizontal).** Thin, evenly spaced, fainter than the lane
  lines, spanning the full width. They move down with the notes, since they
  are drawn from `DisplaySongTime` like the slabs.
- **Bar lines.**
  - Check in the frames whether they are visibly heavier than beat lines.
    At 512 px they look nearly the same, with the bar marked mainly by its
    number tag.
  - Do not assume "thicker and brighter", which is what the dropped branch
    did.
- **Bar numbers.** White bold digit in a small, dark, rounded-rectangle tag
  at the **left end** of the bar's line. Tags scale with perspective (bigger
  near the keys).
- **Hit line.** A solid dark bar across the full width at the back edge of
  the keys, where the wall meets the keyboard. Finger numbers sit above it in
  the video (skipped: F15).
- **Lines are crisp, not shimmering.** On Quest 2 this needs MSAA and lines
  with a real minimum on-screen width. See "Harder than it looks" #2.

**Getting the frames back** (the downloaded copies were deleted):
- Run `/watch` on the video with `--timestamps
  1:27,1:33,1:38,1:44,1:50,3:08,4:00 --resolution 1024`.
- Higher-resolution frames will settle lane spacing and line weights.
- Keep them in a scratch folder, not in the repo.
3. **Outcome colouring (F19/F20).** After the grid, so the outcome colours
   are chosen against the final background.

   **Done, merged 2026-11-03** (branch `phase1-outcome-colours`). Hit slab →
   mint, missed slab → slate, each over 120 ms; wrong key → coral at once,
   steady dimmer coral while held, fades out as coral on release (Ryan: going
   back to green read as "correct"). Lit keys in ActiveKeysOnly at 55%
   opacity so hands show through. Values are provisional, to be tuned after
   more testers.
4. **Note names on slabs (F14).**

   **Done, merged 2026-11-03** (Batch A). One TMP label per slab near its
   bottom, on the wall plane, using the real ♯ glyph through the dynamic
   LiberationSans fallback. Hidden when the slab is too short to hold it.

### Phase 2: Results, tempo and practice

Steps 5–12 (and F14, plus two calibration fixes) are grouped into
Batches A, B and C, each tested in one headset round; see "Batches" in
`CLAUDE.md`. Tempo (step 5) is changed with a piano chord: hold A0 and press
B0 (−5%) or C1 (+5%).

5. **Tempo ±5% (F26).** **Done, merged 2026-11-03** (Batch A). 25–200%,
   saved in PlayerPrefs, always shown on the panel. A change while paused
   part-way keeps the song where it is; the attempt records its slowest tempo.
6. **Attempt schema v2.** It is not a feature but a prerequisite. Add `mode`
   (continuous/wait), `loopFromBar`/`loopToBar`, `hand`, `metronome` in one
   change, so steps 7–12 don't each bump the schema. **Done, merged
   2026-11-03** (Batch A): `schema` = 2; v1 files load with v1 defaults.
7. **Liquid Glass material and colour-token asset**, then the **results
   card (F30) + bar heat strip (F32)** built in that glass. The card is the
   first real menu, so the shared glass shader is made here and every later
   panel reuses it. The card needs step 5 to compare at the same tempo.
   **Done, merged 2026-11-05** (Batch B). Liquid Glass and the Outfit typeface
   approved by Ryan; card redesigned with a details panel and ray buttons.
8. **Metronome (F27).** Independent; it is the first in-app audio, so the
   latency measurement here also serves F9 if it ever happens. Clicks during
   the lead-in too (Ryan, 2026-11-03).
   **Done, merged 2026-11-05** (Batch B). Output latency 140 ms; a retest video
   had clicks 17 ms ahead of the lines (sd 9), within the phone's own audio/video
   uncertainty.
9. **Loop worst bar ± 1 (F33).** Most useful with step 7 showing which bar.
   Built (Batch C), awaiting a headset pass, as are steps 10-12.
10. **Auto-tempo in the loop (F35).** Needs steps 5 and 9.
11. **Wait mode (F22).** After the loop, so scoring and transport aren't
    changing at the same time.
12. **Memory fade (F36).** Needs step 9 (pass count drives the fade).

### Phase 3: Real songs

13. **MIDI file loading (F38).** Foundation for everything in this phase.
14. **Song picker (F7)**, driven by controller or piano keys for now (no
    hand tracking yet), with the logic in a Meta-free component so the
    Phase 4 panel can drive it later.
15. **Hand colours with a fixed default pair (F13)**, then **due key in hand
    colour (F18)**. Both need step 13.
16. **One-hand practice (F25).** Needs step 15.
17. **Public-domain starter library (F11).** Curation can start any time, on
    paper; loading it needs steps 13–14.
18. **Per-song history (F37).** Queued here: needs steps 7, 13 and 14.

### Phase 4: Hand-tracked UI and settings

19. **Hand-tracking spike (F44).** Check three things on a Quest 2 next to the
    piano:
    - poke a world-space button;
    - switch from controllers to hands and back without breaking
      calibration;
    - frame time stays at 72 Hz;
    - how accurate hand-based calibration is against the controller, for the
      two ideas in `CLAUDE.md` ("Calibration ideas to measure").
20. **Settings model (S1)**: a Meta-free `PianoSettings` saved to
    PlayerPrefs. It moves display mode, passthrough, wait mode, metronome and
    memory fade toggles into one place. **Hand-colour presets (F13)** and
    **the due-key toggle (F18)** live here. The free colour picker (C1) comes
    later, on top of these menus.
21. **Unified hand-tracked panel (F12 + F24)**: dock, in-song controls and
    the settings page from step 20.

Why hand tracking is last: everything before it works with the controllers
you already have. Building the logic Meta-free first (steps 14 and 20) means
the panel in step 21 is a new *view* on existing behaviour, not a rewrite.

### Size check

This is roughly 35–45 evenings of work:
- Phase 1: ~5
- Phase 2: ~12
- Phase 3: ~12
- Phase 4: ~10

At ~7 h/week that is more than the twelve-week timeline in `CLAUDE.md` is
likely to hold, if that timeline is a hard deadline. **Phases 1 and 2 are the
demonstrable core**. Phases 3 and 4 are where cuts should come from if time
runs short.

---

## Dependencies and things harder than they look

**Dependency chains among your picks**

- F38 MIDI loading → F7 picker → F11 library, F37 history
- F38 → F13 hand colours → F18 hand-coloured due key, F25 one hand
- F44 hand tracking → F12+F24 panel; S1 settings page → F13 colour
  customisation, F18 toggle
- F26 tempo → F30 "vs last attempt at the same tempo" → F35 auto-tempo
- F33 loop → F35 auto-tempo, F36 memory fade
- F16/F17 grid → F19/F20 colours (choose outcome colours against the final
  grid)

**Harder than they look**

1. **Hand tracking (F44) is the largest item on the list.**
   - Every current control is a controller button, and calibration needs
     the controller tip (`ControllerTipCalibration`). So the app must handle
     switching between controllers and hands, not replace one with the
     other.
   - Quest 2 hand tracking is noticeably less precise than Quest 3, so
     buttons need to be big (~3 cm).
   - Hands resting on the keys must not poke the panel by accident: keep it
     off the key plane, or disable pokes while a song plays.
   - Hand tracking also costs frame time on Quest 2; check 72 Hz in the
     spike.
   - Using the Interaction SDK closes the escape hatch in `CLAUDE.md`
     ("replace `com.meta.xr.sdk.all` with `com.meta.xr.sdk.core`"), because
     core does not include it.
   - Scene wiring must go through `SceneSetup.cs` like everything else.
2. **"Exactly like the video" grid lines (F16/F17): the hard part is
   rendering, not code.**
   - Thin, high-contrast lines shimmer and break up on Quest 2 when they are
     thinner than about a pixel at viewing distance. That is likely why the
     cube-based lines in `cf0f8bc` looked wrong.
   - Getting them clean usually takes three things:
     - MSAA on the URP asset (check `Mobile_RPAsset`);
     - lines with a real minimum width, or quads with soft alpha edges;
     - lines kept off the slabs' depth so they don't flicker.
   - That's why step 1 is a look-only spike.
   - The old branch is a baseline for timing maths only; its look is the
     opposite of the target.
   - Matching the look of a highway grid is fine. It's a common
     visual convention, not PianoVision's branding. Everything else stays
     PianoGlass's own (Part 4).
3. **Custom hand colours (F13) need guard rails.**
   - **Decided:** 4–6 preset pairs now; a free colour picker (C1) later,
     once the menus exist.
   - Presets and the picker must both keep away from the outcome colours
     (hit / miss / wrong). A player who picks mint for the right hand would
     otherwise make every note look "hit".
   - The picker is fiddly in VR: it needs a hue/brightness control big
     enough to poke with Quest 2 hand tracking.
4. **The free library (F11) has a licensing trap.**
   - A public-domain *composition* does not make a given *MIDI file* free to
     use: whoever transcribed it may hold rights in their file.
   - Safe sources:
     - files you sequence yourself;
     - files with an explicit public-domain or CC0 licence (some of the
       Mutopia Project).
   - CC-BY-SA files need attribution and share-alike terms, so check each
     one.
   - Grading difficulty is manual work per song.
5. **Wait mode, loop and one-hand all change what an attempt means.**
   - Results comparisons ("vs last attempt") must only compare attempts with
     the same mode, tempo, loop range and hand, or the numbers lie.
   - That is why step 6 settles the schema once.
6. **The due-key toggle (F18) overlaps the existing display modes.**
   `ActiveKeysOnly` / `EdgeMarkers` already decide whether keys light up, so
   the toggle and the left-X mode cycle should become one setting in S1, not
   two controls that disagree.
7. **Frame time.**
   - Text labels on every slab (F14) and bar numbers (F16) add TMP meshes;
     pool them.
   - Hand tracking on top: profile once with a development build after
     Phase 1 and again after Phase 4.

---

## Part 4: Visual inspiration (not copying)

### PianoVision's design language

- **Colour.** Near-black panels with a deep violet tint, set against a
  violet-lit room. One saturated violet is the accent for every selected
  state: selected song row, active level, toggles, BETA badges. Primary
  action is a white pill with dark text ("Play Song"); destructive is coral
  ("Stop Song"). Notes are hot pink (left) and cyan (right), with neon green
  for hits. Scores run from salmon through amber to pale green, on the gauge
  and on every bar.
- **Typography.** One geometric sans throughout, white on dark. Titles about
  2× body size. Small grey or white secondary lines under titles (the
  composer). Numbers are not tabular.
- **Shape and spacing.** Large corner radius on panels (~24 px at screen
  scale). Rows are generous (~60 px) with wide margins; the results card
  splits into rounded tiles with thin grey outlines. Buttons are pills or
  rounded squares, sized for a fingertip.
- **Layout.** Tablet metaphor: a floating rectangular panel at eye height,
  sidebar on the left, list on the right, full-width action button at the
  bottom. A second panel sits under the keyboard during play, and a floating
  dock is anchored below the menu.
- **Highway.** Thin, light, evenly spaced grid lines in perspective; bar
  numbers in small dark rounded tags at the left edge; slabs are solid,
  slightly rounded, labelled near the bottom.
- **Icons.** Simple line glyphs (music note, people, cog, bolt, heart,
  metronome, book) inside circles or rounded squares, filled violet when on.
- **Motion.** Slabs glow on contact and green "landing" blooms appear at the
  hit line. List rows highlight on hover. The flashy whip transitions in the
  video are video editing, not the app.
- **Overall feel.** Playful, neon, game-like. Clear for a beginner, and it
  reads well at a distance because everything is big and high-contrast.

What works:
- One accent colour means "selected / on".
- One score colour ramp is used everywhere.
- Results are split into tiles that each answer one question.
- Feedback is drawn on the notes themselves.
- The highway grid is quiet enough not to compete with the notes.

What PianoGlass should do differently:
- Its panels float away from the keys.
- Pink, cyan and green are the main signals, which is hard for colour-blind
  players.
- Results never say *how early or late* you were.

### Proposed direction for PianoGlass: "Instrument panel"

Calm, precise, numbers first, registered to the piano. A finished-looking
instrument, not a neon game.

- **Palette** (PianoGlass's own; none of it is PianoVision's):
  - **Surface**: Liquid Glass (Ryan's decision; see "Menu style: Liquid
    Glass" below). The exact tint and alpha are tuned on the headset, not
    fixed here.
  - **Text**: `#E9EEF2` primary, `#8A96A3` secondary.
  - **Default hand pair**: your current note blue `#66ADF2` (right) and warm
    sand `#E8B86D` (left). These are the defaults before the player
    customises them (F13).
  - **Outcomes**: hit mint `#3DDC97`, missed slate `#5A636E`, wrong coral
    `#FF6B5B`. These are reserved; hand presets may not use them.
  - **Grid** (F16/F17): **exception to "own identity"**. By Ryan's
    decision, the grid matches the video exactly (see "Highway grid: visual
    target" in Part 3). Do not restyle it to this palette.
  - **Timing**: ▲/▼ plus milliseconds ("▼ 37 ms late"), never colour alone.
- **Typography**: one clean sans for labels plus a monospaced numeral face
  (an OFL font such as JetBrains Mono or Roboto Mono, as a TMP font asset)
  for every number. Three sizes only.
- **Layout**: the unified hand-tracked panel (F12 + F24) sits **at the left
  end of the keyboard, angled toward the player**. That keeps it off the
  key plane and away from where the hands rest, and it reads like part of
  the instrument rather than a tablet floating in the room. Results unfold
  upward from a slim strip along the keyboard's back edge, with the bar heat
  strip running left to right like a timeline.
- **Motion**:
  - a hit tints the slab over ~120 ms;
  - a wrong key pulses coral once;
  - results count up in under half a second;
  - panel buttons depress visibly on poke (Phase 4);
  - nothing glows at rest.

### Menu style: Liquid Glass (decided 2026-10-30)

Ryan's requirement: every menu and panel is **translucent, like Apple's
Liquid Glass on the iPhone**, with **rounded corners**, and generally
nice-looking. This applies to:
- the results card;
- the status/calibration panel;
- the settings menu;
- the hand-tracked dock and control panel;
- the song picker;
- any future menu.

It does **not** apply to the highway grid, which matches the video.

What the look is made of:
- **Translucent body.** The room shows through the panel, lightly tinted,
  not a solid slab.
- **Bright edge.**
  - A thin, light rim around the panel, brightest along the top edge, like
    light catching the glass.
  - This, more than the translucency, is what reads as "glass".
- **A highlight that moves with your head.** A soft specular sheen that
  shifts as the view angle changes (a Fresnel/view-angle term in the shader).
  It is cheap, and in VR it is what makes glass look like a physical object.
- **Rounded corners everywhere.**
  - Panels get generous corner radii.
  - Buttons are pills or circles.
  - Nested elements (tiles inside a card) get a slightly smaller radius than
    their parent, so the curves stay concentric.
- **Soft depth.** Buttons and tiles sit slightly in front of the panel, with
  a faint shadow or darker inner edge, and depress visibly when poked.
- **Legible text.** White text with a subtle shadow. The tint must be
  strong enough to read over a bright window as well as a dark wall.

How to build it on Quest 2:
- One URP unlit transparent shader (Shader Graph). It draws:
  - rounded corners as a signed-distance function, so edges stay crisp at
    any size with no sprite slicing;
  - the rim;
  - the Fresnel sheen;
  - the tint.
- The material is created and assigned in `SceneSetup`, never via
  `Shader.Find`.
- Shared parameters (tint, alpha, rim strength, corner radius) live in the
  colour-token asset, so every panel matches.

The limitation:
- iPhone Liquid Glass **blurs and bends what is behind it**. On Quest 2 that
  is not possible over the real room.
- Passthrough is composited by the system underneath the app, and Quest 2
  apps cannot read the passthrough image (camera access is Quest 3/3S
  only).
- So the glass can be translucent, rimmed, highlighted and rounded, but it
  cannot frost or refract the room.
- Blurring the app's *own* content behind a panel (e.g. the highway) is
  possible but costs GPU time on Quest 2. Treat it as an experiment, not a
  requirement.
- **Check in the headset that translucent panels blend with passthrough
  correctly** (the panel's alpha decides how much room shows through), and
  check frame time at 72 Hz with several panels open.

### Concrete changes, ordered by impact

1. **Clean highway grid with bar numbers** (Phase 1). It is the biggest
   change to how the app looks while playing, and it is what you want
   matched to the video.
2. **Outcome colour on slabs and keys**: mint hit, slate miss, coral
   wrong-key pulse with the note name.
3. **Results as a structured card**, made of:
   - a headline accuracy number;
   - tiles for *vs last attempt*, *timing* (mean early/late in ms from
     `ErrorStats()`) and *wrong notes*;
   - the bar heat strip.
4. **Liquid Glass panels from one shared shader and colour-token asset**:
   - the rounded, translucent, rimmed glass material above;
   - a small `ScriptableObject` of tokens read by `CalibrationPanel`,
     `NoteHighway`, `PianoKeyHighlighter` and later the hand-tracked panel;
   - the hand-colour presets and glass parameters live in it too.

   Do this before the results card's final layout, so the card is built in
   glass from the start.
5. **The unified hand-tracked panel** at the keyboard's left end (Phase 4).
