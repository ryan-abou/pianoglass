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
| 5 | branch `phase5-verify` | Verifying banner, A confirms → Ready + `lastVerifiedUtc`; Middle C one-point correction (right grip) as a rigid anchor-local shift; recalibration panel wording. EditMode 180/180. | **PENDING DEVICE TEST** |

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

**Status: built (`.\build.ps1 -Development`), EditMode 180/180 (22 new),
PENDING DEVICE TEST.** Nothing below has been tried on the headset.

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
- Over **50 mm** is refused with
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

### Phase 5 device results

PENDING DEVICE TEST — fill in from the Phase 5 checklist, including the
seated-vs-standing far-key check and the measured vertical error, which is
the first direct evidence for or against Phase 4 observation 2.

## What's next

1. Phase 5 device test.
2. If the seated parallax is confirmed as a height error, decide whether the
   Middle C fix's vertical part (capped at 10 mm) is enough or whether a
   dedicated height check is needed.
3. Two-point correction (C3 + C6) for yaw/width errors a one-point fix
   cannot remove — the Core maths (`SolveTwoPoint`) already exists, and the
   flow already has the states.
4. Promote anchored calibration from development-only to release once it has
   survived a user-test session.
