# Calibration rounds

Moved out of CLAUDE.md on 2026-11-07, word for word. Per-phase device results, drift measurements and ideas 1-4 are in `docs/anchored-calibration-status.md`; this file holds what CLAUDE.md said about calibration.

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
  a rigid anchor-local shift, vertical capped at 10 mm, refused over 150 mm.
  Under 6 mm sideways with under 12 mm of height it changes nothing: that is the
  touch's own error (2026-11-05). Ready
  also returns to Verifying after 3 s or more away (headset off).
- **All logs start with `[AnchorFlow]`**: `adb logcat -s Unity | findstr AnchorFlow`.

---

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

## Calibration items from Batch D

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

