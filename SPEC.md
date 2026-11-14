# SPEC: MIDI songs, hands and the song picker

Issues: **#1 Load songs from MIDI files** and **#2 Add 3 public domain songs**,
then a Both hands option (see "How the work is split").
Written 2026-11-07 from an interview with Ryan, updated 2026-11-08. No code
exists for this yet.

## What we're building, in one paragraph

PianoGlass stops using the 8-bar Twinkle typed into `Song.cs`. Instead it
loads songs from MIDI files. Three songs come with the app (Twinkle, Ode to
Joy, Minuet in G), and any `.mid` you copy into the app's Songs folder on the
headset is loaded too. Every note in a file is loaded and marked left hand or
right hand, and you practice **one hand at a time or both together**. With one
hand, your hand's notes fall as solid slabs. The other hand's notes fall as
outlines in their own colour, and they are never scored or waited for. With
Both, every note is solid in its hand's colour, and all of them are scored and
waited for. A Liquid Glass song picker, pressed with
the controller ray, shows at launch and from a new "Songs" button on the
results card. Songs that change tempo or beats per bar part-way through play
with correct bar lines.

## Decisions from the interview

### Which hand a note belongs to

All of this lives in one class, `HandAssigner`, so it is easy to make smarter
later (#9 Smarter left/right hand guessing).

1. **A track name says so.** A track is one strand of a MIDI file, usually one
   staff of the sheet music. The name decides when it contains `left`, `right`,
   `bass`, `treble`, `LH` or `RH` (any capitals; `LH`/`RH` only as whole
   words, so "Rhythm" doesn't count). The song is marked **hands named**.
2. **Two note tracks, no usable names.** The track with the higher average
   pitch is the right hand. The song is marked **hands guessed**, and the
   picker shows that label.
3. **One note track** (this includes every "format 0" file, which packs
   everything into one track). No split at middle C. The song is marked
   **hands unknown**, and it can only be practiced as **All notes**.

Defaults I picked for cases the rules don't cover:
- **Three or more note tracks**: the names decide if every note track gets a
  hand from rule 1. Otherwise the song is **hands unknown**. It never mixes a
  name-based hand with a guess.
- Tracks with no notes (title, tempo) are ignored when counting.
- Channel 10 (drums in General MIDI) is ignored.

**Swap hands.** Each song has a "swap hands" setting, saved per song in
PlayerPrefs under its file name (`PianoGlass.SwapHands.<file>`). It flips left
and right after the rules run. It has no menu button yet. For now it's set with
a context-menu item on the `CurrentSong` component in the Editor. On the
headset it can only be changed by a later menu.

### Cleaning up notes when a file loads

Real files can be messy, so a small class, `ChordTidy`, runs once on every
song, after hands are decided and after MIDI ticks have become seconds. It
does two things, in this order.

**1. Close notes become one chord.** A note that starts within **40 ms** of a
chord's first note joins that chord, and moves to start exactly with it.

- **Why 40 ms (my default):** in a file recorded by a real person, the notes of
  one chord land a few tens of milliseconds apart. Real fast notes are further
  apart than that: a 32nd note at 120 BPM is 62.5 ms.
- **Measured from the chord's first note**, not from the note before, so a fast
  run can't chain into one giant chord.
- **Uses the file's own seconds**, so the tempo setting doesn't change what
  counts as a chord.
- **Looks at both hands together**, so a chord split between the hands is one
  chord in Both.
- **The notes move, the scoring stays the same.** After this step, a chord's
  notes start at exactly the same time, which is what `ScoringSession`,
  `MemoryFade` and `WaitRecall` already check for. None of their chord code
  has to change.
- **The cost:** grace notes closer than 40 ms turn into a chord you can press
  in any order.

**2. The same key at the same moment becomes one note.** The note keeps the
longest length of its copies.

- Copies from one hand, or from a hands-unknown song, are simply one note.
- Copies from two hands become one **shared** note, so neither hand loses it.
  It counts as yours whichever hand you pick, and you press it once. With one
  hand it's drawn in your hand's colour, with no outline copy. With Both it's
  drawn in the right hand's colour.
- Swap hands leaves a shared note shared.

The load report counts how many notes moved and how many merged, and prints
them on the `[Songs]` Console line. The picker shows nothing about it.

Checked 2026-11-08: the Minuet has no key shared by both hands, and its closest
notes are 250 ms apart. Twinkle and Ode have no shared keys in their note lists
either. So this step changes nothing in the three built-in songs, and every
note count in this spec stands.

### What you see while practicing

- **Your hand**: solid slabs in your hand's colour, scored, lit keys, note
  names, memory fade, wait mode: everything works as it does today.
- **The other hand**: outline-only slabs in that hand's colour. No
  transparency, because memory fade already uses see-through notes. They are
  never scored, wait mode never waits for them, and pressing one counts as a
  wrong key like any other unexpected key.
- **All notes** (hands-unknown songs): every note is solid and scored, drawn in
  a neutral "all notes" colour, and wait mode waits for whole chords. The
  attempt's `hand` is `"both"`.
- **Both** (songs marked hands named or hands guessed): every note is solid in
  its own hand's colour, left in sand and right in blue. Wait mode and scoring
  treat all notes the same, like All notes does, so a chord split between the
  hands waits for every key. Lit keys, note names and memory fade cover every
  note. The attempt's `hand` is `"both"`.

Defaults I picked:
- Outline notes have **no note-name label** and **don't light keys**. That
  keeps the keyboard about your hand only, and saves text meshes.
- **Memory fade only fades your hand.** Outlines stay as they are. Memory fade
  tests your memory of your part.

### Colours, all in one place

A new `NotePalette` asset (`Assets/UI/NotePalette.asset`) holds left hand,
right hand, all notes, hit, missed and wrong. Nothing else hard-codes these
colours. `OutcomeTint` keeps its maths and reads its colours from the palette.
This is the ground for #10 Note color themes.

| Role | Default | Why |
|---|---|---|
| Right hand | blue `#66ADF2` | today's note colour, already the planned default |
| Left hand | sand `#E8B86D` | planned default in `docs/feature-evaluation.md` |
| All notes | lavender `#B49CF0` | doesn't hint at either hand |
| Hit / missed / wrong | mint `#3DDC97` / slate `#5A636E` / coral `#FF6B5B` | unchanged, reserved |

An EditMode test checks that every hand colour is clearly different from every
outcome colour, and from each other, by a minimum colour distance. A future
theme that breaks that rule fails the test.

### The song picker

- **When**: at launch, once the keyboard placement is valid. Also from a
  **"Songs" button on the results card**, which replaces the hidden "Next
  song" button. Hidden while a song plays. It steps aside for calibration
  instructions, the same way the results card does.
- **Where**: away from the keys. My default is centred on the keyboard, about
  25 cm above the top of the highway, tilted toward you, and registered on the
  keyboard like every other canvas. You said menus get redesigned after hand
  tracking, so this is a placeholder spot, tuned in the headset round.
- **What**: a **Left | Right | Both switch at the top** (Both arrives with the
  second pull request), then one row per song: the title, plus a small line
  like `3/4 · 32 bars · Hands guessed`. A song marked hands unknown says **All
  notes** on its row and ignores the switch. Rows that couldn't load are
  greyed out with the reason. Five rows per page, with Prev/Next pills when
  there are more. A **Close** pill keeps the current song.
- **Pressing a row** loads that song with the switch's hand, stopped at the
  top. The picker hides, and you press play (A, or the A0 + A♯0 chord) as
  usual.
- **Remembered**: the last song and hand are saved and loaded at launch. If
  that file is gone, it falls back to the first built-in song.
- The results card's existing switch-hand button now really switches hand and
  restarts. After a Both or All notes attempt it isn't offered, because there
  is no other hand. `AttemptHands.Other` already works that way.

### Tempo and beats-per-bar changes

Fully supported. Today the highway grid, bar numbers, the metronome, loops and
the results card's bar pills all work from one `Bpm` and one `BeatsPerBar`.
That gets replaced by a **tempo map**: a list of every beat in the song (its
time in seconds, its bar, and its beat in the bar), worked out once when the
song loads. MIDI files measure time in ticks, small steps of a beat, and the
tempo map turns ticks into seconds. Once every beat is in a list,
beats-per-bar changes come almost free, so they're supported too.

- Beats follow the time signature's bottom number: 3/4 has 3 quarter-note
  beats, and 6/8 has 6 eighth-note beats.
- The lead-in before bar 1 continues backwards using the first tempo and time
  signature.
- `SongData.Bpm` stays as the **first** tempo. It is shown in the picker and
  written to the attempt file.
- **Fallback warning**: if a time signature change lands in the middle of a
  bar, the song still loads, with the row label "Bar lines may be off".
- The song clock rule doesn't change: song time is a `double` of seconds with
  a tempo multiplier, and note positions come from `noteTime − songTime`.

### Your own files

- Folder: the app's own folder, so no permission prompts:
  `/sdcard/Android/data/com.ryanabourjeili.pianoglass/files/Songs/`
  (`Application.persistentDataPath` + `/Songs`). The app makes the folder at
  launch if it's missing, so it's there to copy into.
- **With adb** (always works):
  ```powershell
  adb push "My Song.mid" /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/Songs/
  ```
- **By dragging over USB**: plug the Quest into the PC, put on the headset and
  allow file access. Then in File Explorer open *Quest 3 → Internal shared
  storage → Android → data → com.ryanabourjeili.pianoglass → files → Songs*
  and drag the `.mid` in. Newer Quest software sometimes hides `Android/data`
  over USB. If the folder isn't there, use adb. The headset round checks which
  one works on your Quest.
- In the Editor the same code reads `%USERPROFILE%\AppData\LocalLow\<company>\PianoGlass\Songs\`,
  so own files are tested at the desk too.
- Own files show "Hands guessed" or "Hands unknown" when that applies.
  Built-in songs have named tracks, so they show no label.
- A file is re-read when the picker opens, so you don't need to restart the
  app after copying a file in.

### The three built-in songs

Stored in `Assets/Songs/` as real `.mid` files, so MuseScore opens them
directly. A small **importer** (editor code that tells Unity how to read a file
type it doesn't know) turns each one into an asset Unity can ship.
`SceneSetup` puts the three assets in the song list. Nothing goes in
`StreamingAssets` (see the build trap in `CLAUDE.md`).

**1. Twinkle, Twinkle, Little Star**: the short beginner version, kept as it
is. The right hand is exactly today's 8 bars from `Song.cs` (C major, 4/4,
120 BPM). The single-note left hand is my default:

| Bar | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|
| Left hand | C3 (4 beats) | F3, C3 (2 + 2) | F3, C3 | G3, C3 | C3, F3 | C3, G3 | C3, F3 | C3, G3 |

**2. Ode to Joy**: the full 16-bar theme in C major, 4/4. Right hand
(quarter notes unless marked):

| Bars | Right hand |
|---|---|
| 1-4 | E E F G · G F E D · C C D E · E(1½) D(½) D(2) |
| 5-8 | E E F G · G F E D · C C D E · D(1½) C(½) C(2) |
| 9-12 | D D E C · D E(½) F(½) E C · D E(½) F(½) E D · C D G3(2) |
| 13-16 | E E F G · G F E D · C C D E · D(1½) C(½) C(2) |

The notes are in octave 4 unless marked. The `G3` in bar 12 dips below middle
C in the right hand, which shows that hands come from tracks, not from middle
C. The left hand plays single notes only, no chords: C3 and G2, a whole note
per bar, or two halves where the harmony changes (bars 4, 8, 9, 10, 12, 16).
The exact list goes in the writer script, and the tests check it note by note.

**Tempo: 120 BPM, my default.** The study plays both passages at 60 BPM, and
the Minuet file is 120 BPM. At 120, both passages need the same **50%** tempo
setting, so there's one less thing to get wrong between sessions.

**3. Minuet in G major, BWV Anh. 114**: your file, `SongSources/Minuet_in_G.mid`,
moved to `Assets/Songs/MinuetInG.mid`. Checked today: format 1, 480 ticks per
beat, tracks "Right Hand" (129 notes, B3–B5) and "Left Hand" (75 notes,
G2–E4), one 3/4, one 120 BPM, key G, 32 bars, no repeats. It ends on a B3 D4 G4
chord in the right hand. `SongSources/` is deleted after the move.

**Licences**: `Assets/Songs/SOURCES.md` records, for each song, that the
composition is public domain (traditional French melody 1761; Beethoven 1824;
Petzold c. 1725), and who made the file. Twinkle and Ode are written for
PianoGlass and released as CC0. The Minuet's line, checked 2026-11-07:

> Minuet in G major, BWV Anh. 114 (Christian Petzold, c. 1725, public domain).
> Source sheet: the PianoXML engraving, marked "Public Domain (PianoXML
> typeset)" at the foot of the score, downloaded from
> https://en.truongca.com/nhac/7294-bach-minuet-in-g-major-bwv-anh-114 .
> Turned into MIDI by Ryan with help from Claude, and checked bar by bar
> against that sheet. Repeats are not written out.

The website itself names no licence, only a general "for reference,
non-commercial" note. The **sheet's own public-domain label** is what passes
#2's rule. A copy of that PDF is kept next to the song as
`Assets/Songs/sources/MinuetInG-PianoXML.pdf`, so the evidence survives if the
site changes. That covers the "write down where each file came from" item on
#2.

Twinkle and Ode are written by an editor-only script,
`Assets/Editor/StarterSongWriter.cs` (menu *PianoGlass → Songs → Write starter
songs*), from note lists. The `.mid` files it makes are committed. The script
is kept so the songs can be remade.

## Your study plan: does the passage exist as described?

I checked `docs/research/Abourjeili_Ryan_1A-Research-Plan_2026_v2.pdf`. It
describes two difficulty-matched 8-measure **right-hand** passages at a fixed
**60 BPM**. Passage A is the "Ode to Joy" theme (Beethoven, 1824). Passage B
is the opening of the Minuet in G major, BWV Anh. 114.

| | Passage A: Ode to Joy, bars 1-8, right hand | Passage B: Minuet, bars 1-8, right hand |
|---|---|---|
| Exists in the file | yes, once I write it (bars 1-8 above) | yes, checked in your file |
| Notes | 30 | 33 |
| Range | C4–G4 (7 semitones) | F♯4–G5 (13 semitones) |
| Time at 60 BPM | 32 s (4/4) | 24 s (3/4) |
| Tempo setting for 60 BPM | 50% | 50% |

Things for you to decide. I haven't changed anything about the study:
1. **Minuet notes, checked against the source sheet (2026-11-07).** Bar 8
   (B4 then A4, inside Passage B), bar 28 (left hand D4 then a rest) and the
   last chord (B3 D4 G4) all match the PianoXML sheet. Other editions may
   differ in small places. For the study, Passage B is this edition, so write
   "PianoXML edition" in the study notes. Still to check by eye: bars 25 and
   26, where the left hand has two voices and the picture was too small for me
   to be sure.
2. **"Matched for note count and range."** The counts are close (30 vs 33),
   but the range isn't: the Minuet passage spans almost twice as many keys and
   has eighth notes where the Ode has quarters. Changing a passage may need an
   update to the approved plan, so I've left both as they are.
3. **Playing only bars 1-8.** The app can't yet start a session limited to
   bars 1-8 except through a practice loop. In a loop, auto-tempo changes the
   tempo, and the plan fixes it at 60 BPM. A "study passage" mode (and the CSV
   log, G6) is its own issue, outside this spec.

## Testing plan

Almost everything is checked at the desk. Only the last step needs the
headset.

### 1. EditMode tests (headless, the command in `CLAUDE.md`)

| Test file | What it proves |
|---|---|
| `MidiFileReaderTests` | Reads tiny files **typed in byte by byte from the MIDI rules**, not made by the writer, so a shared mistake can't hide: format 0 and 1, running status, note-on with velocity 0 = note-off, variable-length numbers (1 to 4 bytes), the same key struck again before release (paired first-in-first-out), zero-length notes (kept, with a 50 ms minimum length), drum channel ignored, notes outside A0–C8 skipped and counted, junk/truncated/non-MIDI files give a clear error instead of a crash |
| `TempoMapTests` | Ticks to seconds across tempo changes. Bars and beats across a 4/4 → 3/4 change. 6/8 beats. Lead-in beats. A mid-bar change gives the warning. A one-tempo song gives exactly what today's `Bpm` maths gives (so nothing that passed on the headset moves) |
| `HandAssignerTests` | One test per rule (named, guessed, unknown), the "Rhythm" non-match, three tracks, format 0, and swap hands for each status |
| `ChordTidyTests` | Two notes 39 ms apart become one chord at the first note's time, and two notes 41 ms apart stay separate. A fast run (notes 30 ms apart) doesn't chain into one chord. A chord split between the hands is one chord. The same key twice at one moment becomes one note with the longer length. Copies from two hands become one shared note, and each hand's note count stays the same. A file with no close notes comes out exactly as it went in |
| `StarterSongTests` | **Twinkle**: every note checked (pitch, start, length, hand), and the right hand is identical to the old hard-coded melody. **Ode**: every note checked. **Minuet**: loads as 32 bars of 3/4 at 120 BPM, two tracks, hands named with 129 right and 75 left notes, and bars 1-8 right hand hold 33 notes. **Ode** bars 1-8 right hand hold 30 notes (the two study passages) |
| `SongLibraryTests` | Built-ins plus folder files in a stable order. A broken file becomes a greyed row with its reason. The remembered song is restored, or falls back to the first built-in. Hands-unknown rows offer only All notes. Second pull request: named and guessed rows offer Both too |
| `NotePaletteTests` | Hand colours are far enough from outcome colours and from each other |
| `ScoringTests`, `WaitModeTests` (extended) | The other hand's notes are never scorable, wait mode never waits for them, and a chord split across hands waits only for your hand's notes. A shared note needs one press, whichever hand you picked. All existing tests still pass. Second pull request: with Both, every note is scorable and a chord split across hands waits for every key |

### 2. Play Mode check in the Editor (Unity MCP reads the Console)

New editor-only helpers, so nobody needs a headset or a real keyboard:
- `MidiEditorSimulator` gets **Autoplay my hand (perfect)** and **Autoplay
  with 3 mistakes**. They press each of your hand's notes at its song time,
  through `InjectNoteOn`/`InjectNoteOff`, using the tempo setting.
- `EditorUiPointer`: in the Editor, a mouse click in the Game view presses
  `GlassButton`s. The controller ray (`OvrUiPointer`) needs a real controller,
  so this stands in for it.
- `SongCheckRunner`, menu *PianoGlass → Checks → Run song check*. In Play Mode
  it runs the whole sequence below by itself and prints one `[SongCheck]` line
  per step, ending with `[SongCheck] PASS` or `FAIL <step>`.

The sequence, and the Console lines it expects:
1. Launch: `[Songs] 3 built-in, N own` and the picker opens with the
   remembered song.
2. Pick **Ode to Joy, Right**: `[Songs] picked 'Ode to Joy' right, 16 bars,
   4/4, 120 BPM`, and `[Score] attempt started ... 62 notes` (right hand only).
3. Autoplay perfect: the results card shows 100%. The attempt JSON has
   `"hand": "right"`.
4. Turn wait mode on and autoplay only the first 4 right-hand notes, then
   stop: the clock holds at note 5 and never waits on a left-hand note.
5. Press **Songs** on the results card and pick **Minuet, Left**. The grid
   shows 3 beats per bar, the last bar is 32, and autoplay gives 100% over 75
   notes.
6. **Own files**: before the run, the check copies two test files into the
   Editor's Songs folder: one with two unnamed tracks and a tempo change, and
   one broken file. Expected: the first row says "Hands guessed" and its bar
   lines stay on its notes after the tempo change. The broken row is greyed
   with its reason.
7. Stop and play again: the last song and hand are restored.

The second pull request adds one more step:

8. Pick **Minuet, Both**: `[Score] attempt started ... 204 notes`. Autoplay
   presses every note and gives 100%, and the attempt JSON has `"hand": "both"`.
   Then turn wait mode on and autoplay only the right-hand notes: the clock
   holds at the first left-hand note.

The Unity MCP is used only to **look**: Console, hierarchy, field values.
Entering Play Mode and running the menu item doesn't change the scene. If the
MCP can't press Play, Ryan presses it once and I read the results.

### 3. Headset rounds (one for each pull request)

**Songs.** One development build of `midi-loading`, one install, one session.
Things only the headset can show:
- The controller ray presses picker rows, the Left/Right switch, Close and the
  results card's Songs button.
- Outline notes read as "the other hand" against passthrough. Hand colours
  look clearly different from mint, slate and coral. The picker spot isn't in
  the way.
- **72 fps** while playing the Minuet with both hands drawn (204 notes). Read
  `VrApi FPS=` in logcat on a release build (dev builds run slower).
- `adb push` a file into Songs, open the picker, and it's there. Try
  drag-and-drop over USB and write down whether `Android/data` was visible.
- Both study passages at 50% tempo: the highway runs at 60 BPM, right hand
  only.

**Both.** One development build of `both-hands`, one install, one session:
- Sand and blue are easy to tell apart when both hands fall at once.
- In wait mode, a chord split between the hands holds the clock until every
  key is down.
- **72 fps** on the Minuet with Both, now that all 204 notes are solid and have
  note names. Read `VrApi FPS=` on a release build.

## End-to-end check: proof it works

All of these must be true. Each step names how it's checked.

1. **Tests**: the EditMode run shows `failed = 0`, and its total is higher
   than before by the new tests. *(headless test command)*
2. **Desk**: `[SongCheck] PASS` appears in the Console after *Run song
   check*. *(Unity MCP)*
3. **Files**: you open `TwinkleTwinkle.mid`, `OdeToJoy.mid` and
   `MinuetInG.mid` in MuseScore, and each looks and sounds like a real
   recording. The Minuet matches the PianoXML sheet, including bars 25 and 26.
   *(Ryan)*
4. **Headset**: on the Quest, with the keyboard plugged in: pick Ode to Joy →
   Right, play it, see your notes solid blue and the left hand outlined in
   sand, and get a results card. Press Songs, pick Minuet → Left, play bars
   1-4 with wait mode on, and the clock waits only for left-hand notes. Push
   your own MuseScore file with adb, and it shows in the picker and plays with
   bar lines on the notes. Frame rate holds 72 fps. *(Ryan, checklist in the
   round)*
5. **Both (second pull request)**: on the Quest, pick Minuet → Both and see the
   left hand solid sand and the right hand solid blue. With wait mode on, the
   clock holds on a chord until both hands' keys are down. The results card
   says "Both hands" and never offers to switch hands. Frame rate holds 72 fps.
   *(Ryan, checklist in the round)*
6. Record **30 seconds of video** of step 4, and of step 5 for the second pull
   request (milestone rule), and copy each APK to `Milestones/`.

## Files involved

**New, pure logic** (Meta-free, in the `PianoGlass.Song` assembly, tested
headless):
- `Assets/Scripts/Song/Midi/MidiFileReader.cs`: bytes to tracks and events.
- `Assets/Scripts/Song/Midi/TempoMap.cs`: ticks to seconds, and the list of
  every beat and bar.
- `Assets/Scripts/Song/Midi/HandAssigner.cs`: the only place hands are
  decided.
- `Assets/Scripts/Song/Midi/ChordTidy.cs`: moves close notes into one chord and
  merges the same key at the same moment.
- `Assets/Scripts/Song/Midi/MidiSongLoader.cs`: puts those together into
  a `SongData` plus a load report (warnings, or the reason it failed).
- `Assets/Scripts/Song/SongLibrary.cs`: the list of songs, rows, remembered
  pick.
- `Assets/Scripts/Song/NotePalette.cs` + `Assets/UI/NotePalette.asset`.

**Changed, pure logic**:
- `Song.cs`: `SongNote` gets a `Hand` (left, right, shared, or none on a
  hands-unknown song). `SongData` gets the hands status and the
  tempo map, and bar maths goes through the tempo map. The old one-tempo
  constructor stays, so existing tests and the hard-coded Twinkle keep working.
  `TwinkleTwinkleLittleStar()` stays as a test fixture only.
- `BeatGrid.cs`, `LoopRange.cs`, `MetronomeSchedule.cs`: read beats and bars
  from the tempo map.
- `OutcomeTint.cs`: colours come from the palette.
- `Scoring/Core/ScoringSession.cs`: "is this note mine?" goes by note index
  (hand), not pitch.

**New and changed components**:
- New `Assets/Scripts/Transport/CurrentSong.cs`: holds the loaded song and
  hand, raises `SongChanged`, reads the Songs folder, saves the last pick and
  swap hands. It replaces the five separate `TwinkleTwinkleLittleStar()` calls
  in `SongTransport`, `NoteHighway`, `PianoKeyHighlighter`, `ScoreKeeper` and
  `Metronome`, which all rebuild on `SongChanged`. So do `HighwayGrid` and
  `PracticeLoop` (switching songs leaves any loop).
- New `Assets/Scripts/UI/SongPicker.cs`: the Liquid Glass panel, built from
  `GlassPanel` and `GlassButton`.
- `NoteHighway.cs`: hand colours, outline slabs, rebuild on song change, and
  **pooled slabs and labels**. Pooling means a small set of objects is reused
  as notes scroll by, instead of one per note in the song. That covers the
  pooling item on #1.
- New `Assets/Shaders/NoteOutline.shader` + material: outline-only slabs with
  a fixed edge width.
- `PianoKeyHighlighter.cs`: only your hand's due keys light.
- `ScoreKeeper.cs`: the attempt's `hand` is `left`, `right` or `both`. No
  schema change is needed: the field already exists.
- `ResultsCard.cs`: the Songs button, and the switch-hand event wired to
  `CurrentSong`.
- `Midi/MidiEditorSimulator.cs`: the autoplay items.

**Editor only**:
- `Assets/Editor/MidiImporter.cs`: the `.mid` importer.
- `Assets/Editor/StarterSongWriter.cs`: writes Twinkle and Ode.
- `Assets/Editor/SongCheckRunner.cs` and `Assets/Scripts/UI/EditorUiPointer.cs`
  (inside `#if UNITY_EDITOR`).
- `Assets/Editor/SceneSetup.cs`: a new `ConfigureSongs()` that adds
  `CurrentSong`, the picker canvas, the palette, the outline material and the
  three song assets.

**Songs and docs**:
- `Assets/Songs/TwinkleTwinkle.mid`, `OdeToJoy.mid`, `MinuetInG.mid`,
  `SOURCES.md`.
- `docs/controls.md` (picker, Songs button, switch hand), `docs/feature-evaluation.md`
  (F38, F7, F11 status), `docs/history.md` (round results), `docs/glossary.md`,
  `CLAUDE.md` (current state).

**Second pull request (Both)**:
- `SongPicker.cs`: "Both" on the switch. Hands-unknown rows still ignore it.
- `CurrentSong.cs`: the hand can be Both, saved and restored like the others.
- `Scoring/Core/ScoringSession.cs`: with Both, every note is yours.
- `NoteHighway.cs`: with Both, every note is solid in its own hand's colour.
- `PianoKeyHighlighter.cs`: with Both, every due key lights.
- `Midi/MidiEditorSimulator.cs`: autoplay presses every note with Both.
- `Assets/Editor/SongCheckRunner.cs`: the Both step.
- `docs/controls.md`, `docs/feature-evaluation.md`, `docs/history.md`,
  `CLAUDE.md`.

## How the work is split

Two pull requests, in this order. Each merges to `main` only after its own
headset round passes.

1. **Songs.** Branch `midi-loading` from `main` → PR "Closes #1, closes #2":
   reader, tempo map, hand assigner, chord tidy, loader, `CurrentSong`, pooling,
   outlines and palette, picker (Left | Right), Songs folder, simulator
   autoplay, SceneSetup, the importer, the writer, the three songs,
   `SOURCES.md`, `StarterSongTests`, the song check.
2. **Both hands.** Branch `both-hands` from `main`, after the first PR is
   merged → PR "Closes #11" (#11 Play songs with both hands): the Both option
   and everything under "Second pull request" in the files list.

Commits follow the logical steps, one each and pushed each time, every one
compiling: reader + tests, tempo map + tests, hand assigner + tests, chord tidy
+ tests, loader, palette, scoring by hand, `CurrentSong` swap-over, highway
pooling, outline shader, picker, results card button, Songs folder, SceneSetup
wiring, editor helpers, each song, docs. Then, on `both-hands`: scoring and
highway for Both, the switch option, the song check step, docs.

## Out of scope

- Showing **which hand missed** on the results card (#6 Results card shows
  which hand missed).
- Any **sound**: the other hand isn't played for you.
- **Smarter hand guessing** (#9), **colour themes** and any settings menu
  (#10, S1), and a menu button for swap hands.
- **Hand-tracking pokes** for the picker (#5), and choosing songs with piano
  keys (I1).
- **Per-song history** (F37) and a **per-song tempo**. The tempo stays one
  saved setting for all songs, as today.
- An in-app file browser, downloads, MusicXML, and editing songs.
- **Pickup bars** (a song that starts part-way into a bar): bar numbers will
  count the partial bar as bar 1. Repeat signs and endings, sustain pedal,
  velocity and key signatures are all ignored.
- Songs that use the control keys **A0 to D♯1** load and play, but those
  notes may trigger the tempo/metronome chords. That's a known limit.
- Study features: a bars 1-8 "study passage" mode with fixed tempo, the
  Display-Only flags, the CSV log (plan items 1.5 and 1.6, G6).

## Small defaults I picked (change any of them)

- Twinkle's left hand (table above). The Ode's key (C major) and tempo (120
  BPM, so both study passages use 50%).
- Swap hands stored in PlayerPrefs per file name, set from an Editor context
  menu for now.
- Outline notes: no labels, no lit keys, not faded by memory fade.
- The "all notes" colour is lavender `#B49CF0`.
- The picker sits about 25 cm above the top of the highway, with 5 rows per
  page, a Close pill, and opens at launch on the remembered song.
- Three or more unnamed tracks, or format 0, means hands unknown.
- Zero-length notes are kept with a 50 ms minimum length. Notes outside the 88
  keys are skipped and counted in the load report.
- The Songs folder is re-read every time the picker opens.
- Notes within 40 ms of a chord's first note count as that chord.
- A note shared by both hands is drawn in the right hand's colour with Both.
