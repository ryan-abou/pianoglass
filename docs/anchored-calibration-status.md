# Anchored calibration — status

Keeping the virtual keyboard on the real piano across relaunches, recenters
and reboots, by storing the keyboard relative to a Meta spatial anchor instead
of in tracking space (which the runtime re-bases on every recenter).

Development builds only (`CalibrationFlowController.useAnchoredCalibration`,
honoured only when `Debug.isDebugBuild`). Release builds still use the legacy
`keyboard_calibration.json` path, unchanged.

Updated after every phase. Device results are what was observed on the Quest 2;
anything not yet tried on the headset is marked **PENDING DEVICE TEST**.

---

## Phase summary

| Phase | Commit | What it did | Device result |
|---|---|---|---|
| 0 | `c2d7455` | Inspection and plan. `OVRProjectConfig.sceneSupport = None` (nothing uses scene data; `Required` only added the `USE_SCENE` spatial-data prompt). Anchor support stays enabled. | Passed in Phase 4 run: `USE_ANCHOR_API` granted, `USE_SCENE` absent. |
| 1 | `2a67bf1` | Pure Core maths and schema, no behaviour change: `AnchoredKeyboard` (yaw-only frame, world↔anchor-local, one-/two-point correction, plausibility), `PivotCalibration`, profile schema v2 with atomic writes, `CalibrationFlow` state machine, Meta-free anchor service interface. | n/a (no runtime change) |
| 2 | `04d7af0` | `IsPlacementValid` gate on `KeyboardCalibration`: every overlay and the transport hide/ignore input while placement is not trusted. | Passed (Phase 4 regression run). |
| 2b | `0b8efe7` | Controller-tip pivot calibration (left menu, rock the tip ~10 s) and yaw fine-adjust (left stick X). | Passed: tip RMS **1.58 mm** over 301 samples. |
| — | `8e9b0d6` | Note highway 40 mm closer to the player; panel unchanged. | Passed: highway at new depth. |
| — | `39b8861` | Nudge cleared automatically after a successful tip calibration (it was double-counting the tip gap). | Passed: nudge auto-cleared. |
| 3 | `42acb6c` | `OvrPianoAnchorService` on the non-obsolete `OVRSpatialAnchor` async API, editor fake, dev-only spike harness (`OvrAnchorSpike`, now disabled in the scene). | Spike superseded by Phase 4 run. |
| — | `6df3722` | CLAUDE.md: adb-server trap; `MANIFEST-additions.txt` matches the real manifest. | n/a |
| 4 | `a8ca173` | Overlays follow the anchor. `AnchoredCalibrationSession` + `CalibrationFlowController` (placement owner of `KeyboardCalibration`), `PianoAnchorManager` republishes the layout on > 1 mm / 0.1° anchor movement. Launch localizes automatically; nothing created on launch; nothing erased on any failure path; the old anchor is erased only after the new profile is on disk. | **All passed** — see below. |
| 5 | branch `phase5-verify` | Verifying banner, A confirms → Ready + `lastVerifiedUtc`; Middle C one-point correction (right grip) as a rigid anchor-local shift; recalibration panel wording; in-headset test checklist. EditMode 190/190. | **All passed** (2026-10-26) — see below. |

---

## Phase 4 device results — Quest 2, 2026-10-21 — ALL PASSED

- **Permissions:** `USE_ANCHOR_API` granted; `USE_SCENE` absent.
- **Tip calibration:** RMS 1.58 mm (301 samples); nudge auto-cleared.
- **Fresh two-touch calibration:** key width 23.4 mm (expect ~23.5), no
  nudge needed. A0 exact, Middle C within a few mm, C8 a few mm off.
- **Relaunch:** load + localize ~100 ms.
- **Recenter ×3** (anchor world-pose jumps up to 1.1 m / 180°): keys stayed
  on the piano.
- **Headset off ~10 min:** relocalized ~2 s after putting it back on.
- **Reboot:** localized in 134 ms.
- **Launch facing away from the piano:** worked.
- **Regression:** MIDI, highway at new depth, all four display modes,
  transport, passthrough — all OK.
- **Logs** (outside the repo): `C:\Dev\PianoGlass\Milestones\phase4-test.txt`,
  `phase4-before-reboot.txt`, `phase4-after-reboot.txt`.

### Observations carried into Phase 5

1. **Misleading panel text during recalibration.** The anchor line said
   "no saved piano yet -- calibrate" during a *re*calibration even though a
   saved piano existed. → Fixed in Phase 5 (wording only).
2. **Seated parallax on the far keys.** Viewed from above, alignment is
   near-perfect; seated, the far keys look offset. Hypothesis: a small
   key-*height* error (before tip calibration the height had been nudged
   +11 mm), which barely shows from directly above but shows as parallax at
   the shallow angle of a seated view. **Not confirmed.** Phase 5's Middle C
   fix measures the vertical error directly and logs it, and the Phase 5
   checklist includes a seated-vs-standing check after the fix.

---

## Phase 5 — verification and Middle C correction

**Status: passed on device (Quest 2, 2026-10-26).** EditMode 190/190.
Device results at the end of this section.

Development builds, anchored mode only:

- After localizing or after a fresh calibration the flow is in
  **Verifying**: overlays drawn, and the panel's prompt line is the banner
  `A: looks right · right grip: Middle C fix · B: recalibrate`; the anchor
  line reads `verifying -- do the keys line up?`.
- **A** in Verifying confirms → **Ready** and writes `lastVerifiedUtc` to the
  active profile (`[AnchorFlow] verified by the player: lastVerifiedUtc=...`).
  That press does not also start the song — `SongTransport` ignores A while
  confirmation is awaited and in the frame it happens, whichever input
  script updates first. The next A plays/pauses as before. Y and the MIDI
  restart chord are unaffected.
- **Right grip** (Verifying or Ready) starts a **Middle C fix**: touch the
  front edge of Middle C (C4, MIDI 60) and pull the right trigger. The raw
  offset from the expected front-edge point (nudge included — it is
  measured against what is drawn) is logged as
  `[AnchorFlow] verify error x/y/z mm = +x / +y / +z (total n mm) -- ...` in
  the keyboard frame (x along the keys, y up, z toward the player) and the
  applied shift is shown on the panel. It is applied as a rigid shift of the
  stored geometry in anchor-local space (span and yaw unchanged), vertical
  capped at 10 mm, and persisted in the profile (`lastCorrection =
  "middle-c"`, `lastCorrectionMm` anchor-local, geometry updated). Then back
  to Verifying: A to accept the corrected keys.
- Over **150 mm** (50 mm until 2026-11-05, see "Anchor drift") is refused with
  `Middle C is N mm off -- piano may have moved -- press B to recalibrate`;
  nothing moves and nothing is written.
- Right grip again cancels; a fix started from Ready and cancelled stays in
  Ready (nothing changed, nothing to re-confirm). An applied fix always
  returns to Verifying.
- During a recalibration with an existing profile the anchor line says
  `recalibrating -- touch the lowest key`, then
  `recalibrating -- now the highest key` — never "no saved piano yet",
  which is now only first-time setup.
- A fresh calibration resets `lastCorrection` to `"none"` and
  `lastCorrectionMm` to zero, as it already reset `lastVerifiedUtc`.

Code: Core (`CalibrationFlow` — cancelled corrections return to their
starting state; `AnchoredCalibrationSession` — `Confirm`,
`ToggleCorrectionAsync`, `OnMiddleCTouchedAsync`, new panel statuses;
`AnchoredKeyboard` — limits as constants, rejection wording;
`PianoProfileEdits.ApplyCorrection`; `PianoProfile.lastCorrectionMm`). Shell:
`IKeyboardPlacementOwner` grows confirm / correction / placed-prompt members,
`KeyboardCalibration` forwards them, `OvrCalibrationInput` binds A and right
grip, `SongTransport` leaves the confirming A alone, `CalibrationPanel`
highlights the banner and shows the applied fix. No scene change.

**In-headset checklist.** Development builds show the ten device-test
steps in a column to the right of the calibration panel
(`DeviceChecklist` in Core, tested; `DeviceChecklistRunner` added at
runtime by `CalibrationPanel`, no scene change). Each step ticks itself
from what actually happened -- flow transitions, the transport starting,
the head dropping 25 cm (seated), a one-frame anchor pose jump (recenter),
a MIDI note -- and the measured fix, cap and seated errors are shown under
it. It does not judge alignment or wording; the tester still does that by
eye. Progress survives relaunches in `device_checklist.json` and resets
with each new build. Logs: `[AnchorFlow] checklist: step N (...) Passed`.

### Phase 5 device results

Quest 2, 2026-10-26, development build with the in-headset checklist.
**All ten steps behaved as intended**, as judged by the tester: launch →
Verifying with the banner; A confirmed without starting the song, and the
next A played; the Middle C fix applied and survived a relaunch; a cancel
from Ready stayed Ready; a touch over the limit (then 50 mm) was refused; the vertical
cap held; recalibration said "recalibrating -- touch the lowest key";
a recenter kept the keys on the piano, and MIDI lit them.

**Seated check (Phase 4 observation 2): improved, not resolved.** After
one seated Middle C fix, A0 and C8 looked "a very little bit off" from the
seat, with the overlay still slightly *lower* than the key edges. The
seated y value was not written down. The key width was again 23.4 mm, the
same as in Phase 4 on this piano, so it is most likely the piano's real
key width. It is also a horizontal reading, about 5 mm over the whole span,
with the end keys placed where they were touched, so it cannot explain a
*vertical* offset. That is still consistent with a small height error seen
at a shallow angle.

**Checklist bug (test tool only, not Phase 5 behaviour).** The panel stayed
on step 4 ("Fix survives a relaunch") after the fix had visibly survived,
so steps 7 and 9 were run by hand. Not investigated. The device log would
show whether `[AnchorFlow] checklist: step 4 ... Passed` was ever logged.

## Anchor drift (2026-11-05)

Ryan: after a day away the keys were about 4 key widths off, with the piano not
moved, and about one key off after taking the headset off for a minute.

**From the log:** the anchor loaded in 124 ms and reported tracked, so nothing
failed. Middle C measured +78 / -25 / -24 mm off (86 mm). After the headset came
back, Ryan nudged 14 mm along the keys and 4 mm in depth.

**Why this looks like the headset's map and not our geometry:** the stored
geometry and nudge were the same numbers as the session before, which Ryan played
on without complaint, and the anchor sits at the middle of the keyboard, so a turn
cannot produce a shift that large at Middle C. The earlier "all passed" results
never tested more than a reboot or ten minutes away. Not proven: nothing measured
the room independently, and a second anchor would shift with the same map, so more
anchors are not the obvious cure.

**The nudge was hurting a fresh calibration.** Straight after recalibrating, Middle
C read +7.5 / -15.6 / -5.5 mm with a nudge of (-11, +5, +6) mm on. Without the nudge
that is about -3.5 / -10.6 / +0.5 mm: the touches were right to a few millimetres
along the keys and in depth, and the nudge was the error. The steady 10 mm or so
low is the height question from Phase 4, still open.

**What changed (branch `calib-recover`).** None of it stops the drift; it makes
recovering cheap:
- A Middle C fix now reaches 150 mm, so that 86 mm is one touch, not a full
  recalibration. The 10 mm vertical cap stays.
- Once the controller tip is measured, a fresh calibration clears the nudge.
- A frame gap, or lost tracking, of 3 s or more sends Ready back to Verifying, so
  the keys get looked at after the headset comes back.
- `[AnchorFlow] anchor refined` logs slow movement, to show whether the anchor
  settles in the first seconds after a launch. If it does, waiting before Verifying
  may help. `verify error` lines across days measure the drift itself.

The lasting fix to try is the "learn from key presses" idea in `CLAUDE.md`, which
could correct drift during play without a touch.

## Coming back to the headset (second round, 2026-11-05)

One session, 21:21-22:19, the piano not moved. Ryan's requirement after it: he does
not want to recalibrate every time the headset comes off.

| Headset off | What the log shows |
|---|---|
| 2 min | Confirmed with no touch and no nudge |
| 2 min, with a recenter | Confirmed with no touch and no nudge |
| 19 min | Middle C measured 21.0 mm off (+19.1 / -7.1 / -5.1). One touch fixed it, 16 s after coming back |

So the 150 mm limit, the return to Verifying and the one-touch fix all work, and none
of it needed a recalibration. Ryan still has to look and sometimes touch, which is
what the rest of this section is trying to remove.

**The anchor keeps moving after it comes back.** `anchor refined` logged 15.5 mm
within 15 s of a launch and 17.6 mm within 5 s of the 19-minute return, then smaller
moves during play (5-9 mm). That log cannot separate the map shifting from tracking
space being re-based, which moves the anchor's world pose harmlessly. The witness
anchors below can.

**Clearing the nudge on recalibration was my mistake.** Four fresh calibrations in
a row (key width 23.0-23.6 mm) were each followed by the same correction, about
-5 to -10 mm along the keys and -3 to -6 mm in depth. That steady offset is what the
carried nudge was holding. What was wrong that morning was the drift mixed into it.
Branch `calib-bias` keeps them apart: the nudge settled on before confirming a fresh
calibration is what the next one starts from, and later nudges are not.

**What PianoVision does (web research, 2026-11-05).** Reviews and store reviews
report readjusting at every use and drift during it. Its "Magic Calibration" uses the
passthrough cameras to find the keyboard, and Meta lists that camera access for
Quest 3 and 3S only. Nothing found says users keep alignment between sessions.

**Options, in order:**
1. Wait for the anchor to settle before asking for a check, if the witnesses show it
   does settle.
2. Three anchors, if they drift apart. Branch `anchor-trio` measures it: every 5 mm
   of change in agreement logs `witnesses vs primary`, and every Middle C touch logs
   how far each anchor and their average put the key from the real one.
3. Correct from key presses during play. Branch `press-probe` logs `[PressProbe]`
   lines. It assumes the nearest finger pressed, so it cannot see a shift over half
   a key, and it needs hand tracking on with the controllers put down.
4. A marked spot on the piano for the controller, which the headset sees directly.
   Ryan's last choice.

## What's next

1. Measure observation 2: a seated Middle C fix with the y value written
   down (`verify error x/y/z mm` in the log). Then decide whether the
   fix's vertical part (capped at 10 mm) is enough, or whether a dedicated
   height check is needed.
2. Fix the checklist's step 4 not advancing, before it is used again.
3. Two-point correction (C3 + C6) for yaw/width errors a one-point fix
   cannot remove — the Core maths (`SolveTwoPoint`) already exists, and the
   flow already has the states.
4. Promote anchored calibration from development-only to release once it has
   survived a user-test session.
