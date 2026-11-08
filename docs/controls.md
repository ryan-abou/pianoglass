# Controls

Moved out of CLAUDE.md on 2026-11-07. Keep this file current: add a row whenever a control is added, moved or removed.

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
| Nudge left/right, nearer/further | right stick | — | `OvrCalibrationInput` | Rigid shift. Made before confirming a fresh calibration, it is what the next one starts from |
| Nudge up/down | left stick up/down | — | `OvrCalibrationInput` | Axis-locked against yaw |
| Turn the keyboard (yaw) | left stick left/right | — | `OvrCalibrationInput` | ±10° |
| Clear the nudge | right stick click | — | `OvrCalibrationInput` | A fresh calibration resets it to the steady offset learned from the last one |
| Measure the controller tip | left menu button (again = cancel) | — | `ControllerTipCalibration` | Written to the profile root |
| "Looks right" in Verifying | A | — | `OvrCalibrationInput` | Anchored mode; that A does not reach play/pause. Asked after launch, calibration, and 3 s or more away |
| Middle C fix: start / cancel | right grip | — | `OvrCalibrationInput` | Anchored mode; then touch Middle C and pull the right trigger |
| Play / pause | A | — | `OvrSongTransport` | |
| Restart from the top and play | left Y | A0 + A♯0 | `OvrSongTransport`, `SongTransport` | The chord works whenever calibrated, playing or not. Also how a practice loop is left |
| Start a practice loop | right controller ray + right trigger on the results card's "Loop bars" button | — | `ResultsCard` → `PracticeLoop` | The button shows when a bar is under 75%: that bar, and a neighbour under 90% |
| Choose bars to loop | right controller ray + right trigger on a bar pill | — | `ResultsCard`, `BarSelection` | One pill, then another for a range; the suggestion's button becomes "Loop bars 3–5". The same pill again, alone, clears it |
| Press a results card button (Retry, Songs, More/Less info, the suggestion's button) | right controller ray + right trigger | — | `OvrUiPointer`, `GlassButton` | Stopgap until hand poke (Phase 4). The ray shows only while a button is pressable; the trigger goes to calibration unless the ray is on a button |
| Open the song picker | right controller ray + right trigger on the results card's Songs button | — | `ResultsCard` → `SongPicker` | The picker also opens by itself at launch, once the keys are placed |
| Choose which hand to practice | right controller ray + right trigger on the picker's Left or Right pill | — | `SongPicker` | Songs marked hands unknown ignore it and play All notes |
| Pick a song | right controller ray + right trigger on a picker row | — | `SongPicker` → `CurrentSong` | Loads it stopped at the top; press play as usual. Greyed rows can't be pressed |
| Next or previous page of songs | right controller ray + right trigger on Prev or Next | — | `SongPicker` | Five songs a page. Only there when there are more than five |
| Close the picker, keeping the current song | right controller ray + right trigger on Close | — | `SongPicker` | Playing a song also hides it |
| Switch to the other hand and restart | right controller ray + right trigger on the results card's "Switch to left hand" | — | `ResultsCard` → `CurrentSong` | Offered after a perfect run at full speed. Not offered after All notes |
| Tempo −5% / +5% | — | hold A0, press B0 / C1 | `SongTransport` | 25–200%, only while stopped or paused, saved |
| Metronome on/off | — | hold A0, press C♯1 | `SongTransport` → `Metronome` | Only while stopped or paused, saved |
| Wait mode on/off | — | hold A0, press D1 | `SongTransport` | Only while stopped or paused, saved. While paused part-way it starts a new attempt from there |
| Memory fade on/off | — | hold A0, press D♯1 | `SongTransport` | Only while stopped or paused, saved. Works on whole songs and in loops |
| Display mode (cycle) | left X | — | `OvrKeyOverlayInput` | FullOverlay / Translucent / ActiveKeysOnly / EdgeMarkers, saved |
| Passthrough on/off | left stick click | — | `OvrKeyOverlayInput` | A/B test tool |

Development tools that are not player controls:
- Swap hands for a song: the *Swap hands for this song* context menu on
  `CurrentSong`, in the Editor only. Saved per file name. No headset menu yet.
- `MidiEditorSimulator` has *Autoplay my hand (perfect)* and *Autoplay with 3
  mistakes* context menus, and `EditorUiPointer` lets a mouse click in the Game
  view press glass buttons. Editor only.
- *PianoGlass → Checks → Run song check* runs the whole song flow in Play Mode.
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
| **Results card** (F30/F32) | rank, accuracy, change vs last, timing, wrong notes, best streak, bar pills, one suggestion; buttons: retry, songs (opens the picker), more info (details panel) | When an attempt completes |
| **Settings** (S1, `PianoSettings`) | display mode, passthrough, hand colours (F13), due-key toggle (F18), metronome, memory fade, wait mode, auto-tempo on/off (Ryan, 2026-11-05: always on until these menus exist) | From the song panel |
| **Calibration panel** (today's status panel, restyled) | start calibration, "looks right", Middle C fix, nudge pad (6 arrows plus yaw ⟲/⟳, held to repeat), clear nudge, measure controller tip | During setup, and from Settings → Keyboard |
| **Song picker** (F38/F39) | Left/Right switch and song list today; per-song history later | At launch and from the results card; from the song panel later |

What stays off the menus, and why:
- **The status panel goes or shrinks (Ryan, 2026-11-05).** It carries a loop's
  pass and how the last one went only until these menus can show that.
- **The piano chords stay.** While playing, hands are on the keys, and a
  chord is faster than reaching for a panel. Each chord gets a matching
  button, so nothing is chord-only.
- **Calibration stays on the controller (Ryan, 2026-11-07).** The
  hand-tracking menus (#5) leave calibration out: the two-point capture,
  Middle C fix, nudges, "looks right" and tip measuring all keep their
  controller buttons above. The touches need the controller's tip, and that
  is the measurement itself. A hand-tracked fingertip is not assumed to be
  accurate enough on a Quest 2 (tip calibration measured 1.58 mm; a white
  key is 23.5 mm wide). The calibration panel row above is for later; the
  hand calibration ideas in `docs/calibration-rounds.md` stay unbuilt.
- **Development tools stay on controllers:** passthrough on/off and the
  device checklist.

