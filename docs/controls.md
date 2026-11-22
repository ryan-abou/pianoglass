# Controls

Moved out of CLAUDE.md on 2026-11-07. Keep this file current: add a row whenever a control is added, moved or removed.

## Controls — every one, today and in the hand-tracked menus

Keep this table current: add a row whenever a control is added, moved or
removed. Phase 4 (hand tracking, `docs/feature-evaluation.md`) moves most of
these off the controllers and into **Liquid Glass menus**. The design rule
from there: the controllers are set down on the piano, and hands either
play or poke.

### Today (2026-11-05)

Controllers always name `OVRInput.Controller.LTouch` / `RTouch`. The A0
piano chords (restart, tempo, metronome, wait mode, memory fade) were removed
on 2026-11-21 (#41): each had a song panel button, and nobody new would find
them. Tempo, metronome, wait mode and memory fade are song panel buttons only.

| Control | Controller (today) | Where | Notes |
|---|---|---|---|
| Capture the next calibration point | right trigger | `OvrCalibrationInput` | Two touches, lowest then highest key |
| Start calibration over | B, or left trigger | `OvrCalibrationInput` | |
| Nudge left/right, nearer/further | right stick | `OvrCalibrationInput` | Rigid shift. Made before confirming a fresh calibration, it is what the next one starts from |
| Nudge up/down | left stick up/down | `OvrCalibrationInput` | Axis-locked against yaw |
| Turn the keyboard (yaw) | left stick left/right | `OvrCalibrationInput` | ±10° |
| Clear the nudge | right stick click | `OvrCalibrationInput` | A fresh calibration resets it to the steady offset learned from the last one |
| Measure the controller tip | left menu button (again = cancel) | `ControllerTipCalibration` | Written to the profile root |
| "Looks right" in Verifying | A | `OvrCalibrationInput` | Anchored mode; that A does not reach play/pause. Asked after launch, calibration, and 3 s or more away |
| Middle C fix: start / cancel | right grip | `OvrCalibrationInput` | Anchored mode; then touch Middle C and pull the right trigger |
| Play / pause | A | `OvrSongTransport` | |
| Restart from the top and play | left Y | `OvrSongTransport`, `SongTransport` | Also how a practice loop is left; the song panel's Restart does the same. Not while the left grip is held (that Y is recording mode's) |
| Start a practice loop | right controller ray + right trigger on the results card's "Loop bars" button | `ResultsCard` → `PracticeLoop` | The button shows when a bar is under 75%: that bar, and a neighbour under 90% |
| Choose bars to loop | right controller ray + right trigger on a bar pill | `ResultsCard`, `BarSelection` | One pill, then another for a range; the suggestion's button becomes "Loop bars 3–5". The same pill again, alone, clears it |
| Press a results card button (Retry, Songs, More/Less info, the suggestion's button) | right controller ray + right trigger | `OvrUiPointer`, `GlassButton` | Stopgap until hand poke (Phase 4). The ray shows only while a button is pressable, or on a panel's glass; the trigger goes to calibration unless the ray is on a button or a panel |
| Open the song picker | right controller ray + right trigger on the results card's Songs button | `ResultsCard` → `SongPicker` | The picker also opens by itself at launch, once the keys are placed |
| Pick a song | right controller ray + right trigger on a picker row | `SongPicker` → `CurrentSong` | Then the hand (next row), unless the song's hands are unknown: then it loads at once with All notes. Loads stopped at the top; press play as usual. Greyed rows can't be pressed. Songs from your own Songs folder have a "Your song" tag (#40) |
| Choose which hand to practice | right controller ray + right trigger on Left, Right or Both in the picker's "Which hand?" step, after a song (#39) | `SongPicker` | The last choice is lit. Both: every note scored, each in its own hand's colour (#11). Back returns to the list |
| Next or previous page of songs | right controller ray + right trigger on Prev or Next | `SongPicker` | Five songs a page. Only there when there are more than five |
| Close the picker, keeping the current song | right controller ray + right trigger on Close | `SongPicker` | Playing a song also hides it |
| Switch to the other hand and restart | right controller ray + right trigger on the results card's "Switch to left hand" | `ResultsCard` → `CurrentSong` | Offered after a perfect run at full speed. Not offered after All notes |
| Play in time after wait mode | right controller ray + right trigger on the results card's "Play in time" | `ResultsCard` → `SongTransport.PlayInTime` | Offered after a good wait mode run: wait mode off, and the song from the top |
| Display mode (cycle) | left X | `OvrKeyOverlayInput` | FullOverlay / Translucent / ActiveKeysOnly / EdgeMarkers, saved. Not while the left grip is held (that X is the checklist's) |
| Passthrough on/off | left stick click | `OvrKeyOverlayInput` | A/B test tool |

Development builds only (compiled out of release; `DevOverlayController`, 2026-11-14).
The left grip is used by nothing else, and has to be held 0.3 s before X or Y count (a press made sooner is remembered),
so it does not happen by accident with the controllers set down on the piano:

| Control | Controller | Notes |
|---|---|---|
| Test checklist panel on/off | hold left grip, then left X | Opens in front of you, or where you last put it. Open or closed after a relaunch the way you left it. Format and push command: `docs/building.md` |
| Pass or fail a checklist item | right controller ray + right trigger on the row's Pass or Fail (or poke it, or pinch it with the hand ray) | The same button again clears it. Saved, survives a relaunch, logged as `[Checklist] PASS` / `FAIL` |
| Recording mode on/off | hold left grip, then left Y | Switches off every debug overlay (status panel, checklist) for milestone videos. Turning it off brings back whatever was showing. Remembered across launches |

Development tools that are not player controls:
- Swap hands for a song: the *Swap hands for this song* context menu on
  `CurrentSong`, in the Editor only. Saved per file name. No headset menu yet.
- `MidiEditorSimulator` has *Autoplay my hand (perfect)* and *Autoplay with 3
  mistakes* context menus, and `EditorUiPointer` lets a mouse click in the Game
  view press glass buttons. Editor only.
- *PianoGlass → Checks → Run song check* runs the whole song flow in Play Mode.
- `OvrAnchorSpike` (Phase 3 harness) uses both grips. It is disabled in the
  scene and must stay disabled alongside the anchored-calibration flow.

### Without controllers (#5, built 2026-11-14)

Put the controllers down and the Quest tracks your hands. Every glass button
(song panel, settings, song picker, results card, checklist) is pressed by
**poking it with an index fingertip** (`HandPokePointer`, `PokeDetector`): hover
within 4 cm, press as the fingertip pushes through the face, then pull back
1.5 cm to press again. The controller ray keeps working alongside.

Every panel (song panel, settings, song picker, results card, status panel,
checklist) can be **pinched and moved** (`PanelGrabber`, `GrabbablePanel`,
2026-11-14). Pinch its glass or edge and it stays in your hand the way you hold
it, so turning the wrist tilts it. A panel out of reach shows a ray from your
hand when you point at it: pinch with the ray on a button to press it, or on
the glass to move the panel, pulling your hand back to bring it closer. Where
you put a panel is saved relative to the keys, so it comes back there after a
relaunch and moves with the keys when they are nudged. Off while a song plays,
so a thumb passing under can't grab anything. A held panel follows your wrist,
and stays held as long as thumb and index stay together, measured from how far
apart they are, not the Quest's own pinch signal, which blinks off mid-move. If the
hand stops being tracked, the panel stays still for up to a second. Letting go:
once the fingers are clearly apart, the panel goes back to where it was the moment
they began to open, so it never slides while you let go (2026-11-20).

With a controller in hand, point the right controller's ray at a panel's glass (not a
button) and hold the right trigger to move it the same way as a far pinch; let go of
the trigger to put it down. Only once the keys are calibrated and no Middle C fix is
waiting, so the trigger still places calibration points.

A held panel never leans sideways. It snaps straight up once within 2.5 degrees
of it, and square to the keys once within 2.5 degrees of that, and stays snapped
until 6 degrees away. Snapping straight up makes the glass's own two upright
edges glow; square to the keys, its top and bottom edges (`SnapGuide`, since
2026-11-21; bars beside the panel before that). The glow flashes on snapping,
settles, and fades out when the snap or the panel is let go. Settings and the song
panel share one spot: moving either moves both. While controllers are down, the
controller buttons are ignored (a pinch reads as a trigger pull).

Until the launch recenter is done (#17), no panel shows at all, only the
recenter banner: before it they would all be a piano width off. A checklist
asked for while waiting opens in front of you after the recenter.

| Control | Without controllers | Where |
|---|---|---|
| Play / pause, restart | Song panel buttons (left end of the keyboard) | `SongPanel` |
| Tempo −/+, metronome, wait mode, memory fade | Song panel buttons, only while stopped or paused | `SongPanel` |
| Songs, hand switch | Song panel "Songs", then poke the picker | `SongPanel`, `SongPicker` |
| Settings | Song panel "Settings": shown in the song panel's place | `SettingsPanel` |
| Calibrate | Settings "Calibrate", then pick up the right controller: touch the middle of the front edge of A0, then C8, with its tip and pull the trigger (about 1 mm; the sideways part of the learned offset is not kept, since it is only where along the edge the tip went). Backup with no controller: press and hold A0 and C8 at their front edges with one index finger (about 1 cm; the keys are levelled afterwards) | `OvrCalibrationInput`, `HandCalibration` |
| Looks right | Settings "Looks right" | `SettingsPanel` |
| Middle C fix | Settings "Middle C fix", then touch the middle of the front edge of Middle C with the right controller's tip and pull the trigger. Controller only since 2026-11-21: by hand it moved the keys 12-19 mm by itself | `OvrCalibrationInput` |
| Nudge, turn, clear nudge | Settings nudge pad: 2 mm or 0.2 deg per poke, saved each time | `SettingsPanel` |
| Display mode | Settings "Display" | `SettingsPanel` |
| Move or tilt a panel | Pinch it and move your hand, or point at it from afar and pinch its glass. With a controller: ray on its glass, hold the right trigger | `PanelGrabber` |
| Press a button out of reach | Point at it (a ray shows) and pinch | `PanelGrabber` |
| Put every panel back | Settings "Reset panels" | `GrabbablePanel` |
| Recenter (the keys a piano width off at launch, #17) | Look at your palm, pinch and hold | Quest system gesture |
| Passthrough, test checklist, recording mode (development builds) | Settings buttons | `SettingsPanel` |

A key press only counts as a calibration touch while hands are tracked, so a
controller tip that presses a key never counts twice. Hand touches learn their
own calibration bias, kept apart from the controller tip's; a Middle C fix made
before "Looks right" is learned along with the nudge. The hand bias is the average
of the last 4 hand calibrations, since each one's nudge is partly its own error. A hand calibration keeps the
last calibration's white key width (within 6%) and turn (within 2.5 degrees), since
hand tracking measures the span short and two presses at different depths turn it.
It also puts both ends at their average height, since the piano is level and a
fingertip is only good to about 1 cm up and down (round 8 came out 14 mm tilted).

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
- **The piano chords are gone (Ryan, 2026-11-21, #41).** They were kept
  because hands are on the keys while playing, but four of the five only
  worked while stopped or paused, when a button is as quick, and every one
  had a button. Hidden controls are one more thing a new player can't find.
- **Calibration by hand too (Ryan, 2026-11-14: everything without
  controllers).** Built as key presses, not fingertip touches: MIDI says which
  key and when, the fingertip says where (accurate along the keys, 328 presses).
  The controller-tip way below stays. What 2026-11-07 said:
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

