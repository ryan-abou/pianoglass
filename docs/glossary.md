# Glossary

Plain meanings for the technical words that come up while working on
PianoGlass. One line each, in alphabetical order, with an example from this
project. Add a word the first time it comes up.

- **APK**: the single app file that gets installed on the headset, like an installer on a PC. PianoGlass example: `Builds\PianoGlass.apk` (about 61 MB), put on the Quest with `adb install -r`.
- **branch**: a separate copy of the work where changes can be tried without touching the safe version. PianoGlass example: `drop-template-packages` holds the package removal until it passes on the headset, while `main` stays the version that works.
- **build**: turning the Unity project into an app the headset can run. PianoGlass example: `.\build.ps1` makes the APK and takes about 6 to 12 minutes.
- **commit**: a saved snapshot of your changes with a short note saying what changed. PianoGlass example: "Remove unused template packages" is one commit.
- **JSON**: a text format for saving data as labels and values, like a filled-in form. PianoGlass example: every attempt you play is saved on the headset as a `.json` file with your accuracy and timing.
- **manifest.json**: the project's shopping list of packages and which version of each to get. PianoGlass example: `Packages/manifest.json` says to use Meta XR SDK version 205.0.0.
- **merge**: bringing the commits from one branch into another. PianoGlass example: after a headset round passes, a feature branch is merged into `main`.
- **.meta file**: a small file Unity keeps next to every asset, like a name tag that also holds its import settings. PianoGlass example: deleting `Assets/Readme.asset` also meant deleting `Assets/Readme.asset.meta`, or Unity would complain.
- **package**: a ready-made add-on that someone else wrote, so you don't have to. PianoGlass example: the Meta XR SDK is the package that lets the app talk to the Quest.
- **packages-lock.json**: the receipt for the shopping list, showing exactly what Unity actually got, including the packages that other packages need. PianoGlass example: after removing two packages from `manifest.json`, Unity removed their 17 lines from this file by itself.
- **tag**: a name stuck on one commit so you can find it again, like a bookmark. PianoGlass example: `milestone-slice-playable` marks the first version where a song could be played.
- **test**: a small piece of code that checks another piece of code gives the right answer. PianoGlass example: 578 tests check things like the scoring and calibration maths without opening the headset.
