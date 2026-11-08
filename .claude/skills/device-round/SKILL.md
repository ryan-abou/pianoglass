---
name: device-round
description: Plan a headset test round (changes since the last tag, checklist, dev build + install), then grade it afterwards from recordings, notes and logcat.
disable-model-invocation: true
---

Before I build: list what changed since the last tagged release and write a checklist for this headset round. For each item: what I do, what pass looks like, how to record it, and which logcat lines you'll read. For recording: the Quest's built-in recorder is fine for stutter and the note highway, but it blacks out passthrough, so anything about keys lining up needs scrcpy over wireless adb or a phone video through the lens. Then build with build.ps1 -Development -Install.

After I test: pull any headset recordings with adb (from /sdcard/Oculus/VideoShots), and I'll give you my notes plus any scrcpy or phone video. Use /watch on the videos, compare everything to the checklist, and read the VrApi FPS lines. Tell me pass/fail per item and what to fix first.

## Project facts

- "Last tagged release" is `git describe --tags --abbrev=0`. If that tag is older
  than the last merged round (see `docs/history.md`), say so and ask which commit
  to compare against instead of listing months of changes.
- Building needs the Unity editor closed (`Get-Process Unity` prints nothing).
  `build.ps1` runs `adb kill-server` itself.
- Right after installing, before the headset is unplugged, run `adb logcat -G 16M`:
  the default 256 KiB buffer loses a whole session within minutes.
- Frame rate: `adb logcat -d -s VrApi | grep FPS=`, summarised with
  `grep -o "FPS=[0-9]*/[0-9]*" | sort | uniq -c`. Check the first and last
  timestamps cover the session before concluding anything. A development build
  runs about 69 fps where a release build holds 72.
- Our log tags: `[AnchorFlow]`, `[Score]`, `[KeyOverlay]`, `PianoGlassMidi`.
  Attempts: `adb pull /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/attempts/. <dir>`.
- After a failed or interrupted build, clean the leftovers listed in CLAUDE.md's traps.
