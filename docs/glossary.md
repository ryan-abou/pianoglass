# Glossary

Plain meanings for the technical words that come up while working on
PianoGlass. One line each, in alphabetical order, with an example from this
project. Add a word the first time it comes up.

- **anchor**: a spot in the room the headset remembers, so the app can pin things to it, like a pushpin on a corkboard. PianoGlass example: the keyboard is saved relative to an anchor, so it comes back on the piano after a relaunch.
- **APK**: the single app file that gets installed on the headset, like an installer on a PC. PianoGlass example: `Builds\PianoGlass.apk` (about 61 MB), put on the Quest with `adb install -r`.
- **batchmode**: running Unity with no window, from a command. PianoGlass example: the tests and `[SongCheck] PASS` both run this way while the editor is closed.
- **branch**: a separate copy of the work where changes can be tried without touching the safe version. PianoGlass example: `drop-template-packages` holds the package removal until it passes on the headset, while `main` stays the version that works.
- **build**: turning the Unity project into an app the headset can run. PianoGlass example: `.\build.ps1` makes the APK and takes about 6 to 12 minutes.
- **commit**: a saved snapshot of your changes with a short note saying what changed. PianoGlass example: "Remove unused template packages" is one commit.
- **controller profile**: a setting that tells the app which kind of controller to expect and what its buttons are called. PianoGlass example: the Oculus Touch profile is on, and the Touch Plus profile (the Quest 3's controllers) is off.
- **depth sensor**: a part of the headset that measures how far away things are, like a tape measure for the whole room at once. PianoGlass example: the Quest 3 has one, but PianoGlass doesn't use it to hide notes behind your hands.
- **EditMode tests**: tests that run inside the Unity editor without pressing Play or using the headset. PianoGlass example: 727 of them run in about a minute while the editor window is closed.
- **gh**: GitHub's command-line tool, so GitHub jobs can be done from the terminal instead of the website. PianoGlass example: moving a card on the project board to Doing with `gh project item-edit`.
- **importer**: editor code that tells Unity how to read a file type it doesn't understand on its own. PianoGlass example: the `.mid` importer turns each song file into an asset that ships inside the APK.
- **issue**: a numbered to-do or bug report on GitHub, like a ticket. PianoGlass example: "#1 MIDI file loading" is an issue with a card on the project board.
- **JSON**: a text format for saving data as labels and values, like a filled-in form. PianoGlass example: every attempt you play is saved on the headset as a `.json` file with your accuracy and timing.
- **logcat**: the headset's running diary of messages from every app. PianoGlass example: `adb logcat -s Unity` shows lines like `[AnchorFlow] verify error x/y/z mm`.
- **manifest.json**: the project's shopping list of packages and which version of each to get. PianoGlass example: `Packages/manifest.json` says to use Meta XR SDK version 205.0.0.
- **merge**: bringing the commits from one branch into another. PianoGlass example: after a headset round passes, a feature branch is merged into `main`.
- **.meta file**: a small file Unity keeps next to every asset, like a name tag that also holds its import settings. PianoGlass example: deleting `Assets/Readme.asset` also meant deleting `Assets/Readme.asset.meta`, or Unity would complain.
- **occlusion**: a real thing hiding a virtual thing that is behind it. PianoGlass example: without it, a note slab draws on top of your hand even when your hand is in front.
- **package**: a ready-made add-on that someone else wrote, so you don't have to. PianoGlass example: the Meta XR SDK is the package that lets the app talk to the Quest.
- **packages-lock.json**: the receipt for the shopping list, showing exactly what Unity actually got, including the packages that other packages need. PianoGlass example: after removing two packages from `manifest.json`, Unity removed their 17 lines from this file by itself.
- **passthrough**: the headset's cameras showing you the real room, so virtual things can sit on top of it. PianoGlass example: you see your real piano through passthrough, in colour on the Quest 3 and black and white on the Quest 2.
- **persistentDataPath**: the app's own private folder on the headset, where it can read and write without asking permission. PianoGlass example: attempt files go there, and your own songs go in its `Songs` folder.
- **Play Mode**: running the app inside the Unity Editor on the PC, without the headset. PianoGlass example: the song check runs in Play Mode and prints `[SongCheck] PASS` in the Console.
- **pooling**: reusing a small set of objects over and over instead of making a new one each time, like a restaurant washing plates instead of buying new ones. PianoGlass example: the highway reuses a few dozen note slabs as notes scroll by, instead of one per note in the song.
- **PR (pull request)**: a request on GitHub to merge a branch into `main`, where you can see every change before it goes in. PianoGlass example: a PR that says "Closes #1" closes issue #1 by itself when it is merged.
- **recenter**: holding the Meta button so the headset resets which way is "forward". PianoGlass example: a recenter moves the room's coordinates, and the anchor brings the keys back onto the piano.
- **RMS**: a kind of average size of the errors, where big errors count extra. PianoGlass example: the controller tip measurement reported RMS 1.3 mm on the Quest 3, so its samples agreed closely.
- **scrcpy**: a free PC program that shows the headset's screen live in a window and can record it. PianoGlass example: it's one way to record the overlay lining up with the real keys, since the Quest's own recorder leaves passthrough out.
- **shader**: a small program that tells the graphics chip how to colour something. PianoGlass example: `NoteOutline.shader` draws only the edge of the other hand's notes.
- **tag**: a name stuck on one commit so you can find it again, like a bookmark. PianoGlass example: `milestone-slice-playable` marks the first version where a song could be played.
- **tempo map**: the list of where every beat of a song falls in seconds, worked out once from the MIDI file. PianoGlass example: it keeps bar lines on the notes in a song that slows down part-way.
- **test**: a small piece of code that checks another piece of code gives the right answer. PianoGlass example: 727 tests check things like the scoring and calibration maths without opening the headset.
- **tick**: the small step of time a MIDI file counts in, a slice of one beat. PianoGlass example: the Minuet file uses 480 ticks per beat.
- **track**: one strand of a MIDI file, usually one staff of the sheet music. PianoGlass example: the Minuet file has a "Right Hand" track and a "Left Hand" track.
