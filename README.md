# PianoGlass

PianoGlass is a piano trainer for the Meta Quest 3. You sit at a real piano, and
through the headset you see your real keys with notes falling onto them, like the
piano videos on YouTube but on your own keyboard. A USB MIDI keyboard tells the app
exactly which key you pressed and when.

Video: _(a 30 second clip from the headset goes here)_

## Why I made it

A video can show you the notes, but it can't tell you what you did wrong. PianoGlass
can, because it knows every key you press:

- If you play D instead of D sharp, the key turns coral and the app says which note it
  wanted.
- Every note is judged on timing (Perfect, Great, Good or Miss), so you see if you are
  rushing or dragging, in milliseconds.
- After each try, a results card shows your accuracy, your timing and your wrong
  notes. It also has a strip with one pill per bar, coloured by how well that bar
  went, so you can see that bar 9 is where you fall apart.
- It suggests what to do next, like "Bar 9 needs work" with a button to loop that bar,
  or playing your weaker hand alone when one hand made most of the mistakes.
- Practice tools: slower tempo, a metronome, looping bars, wait mode (the song waits
  for you to press the right key), playing one hand or both, and a mode that fades the
  notes out so you have to remember them.

Every menu is made to look like Apple's Liquid Glass: see-through, rounded and lit at
the edges.

## What you need

- A Meta Quest 3.
- An 88-key keyboard or digital piano with USB MIDI, plugged into its own power.
- A cable from the keyboard to the headset's USB-C port. If the keyboard's cable ends
  in a normal USB-A plug, you need a USB-C to USB-A adapter (OTG).
- A table or piano bench in a room with decent light. The headset tracks your hands
  and the room with its cameras.

## Trying it

### The easy way: my headset

If I left you my Quest 3, the app is already installed and set up for my piano.

1. Put the headset on and open **Library**, then the **Unknown Sources** tab, then
   **PianoGlass**.
2. Plug the keyboard into the headset's USB-C port.
3. If the app asks you to recenter, hold the **Meta button** on the right controller
   (or, with no controller, look at your palm and pinch and hold) until the keys line
   up with the piano.
4. Skip to "Playing a song" below.

### On your own Quest 3

Installing an app that is not from the store needs developer mode, and Meta only lets
a verified Meta developer account (18 or older) turn it on.

1. In the Meta Horizon phone app: tap the headset icon, tap your headset, then
   **Headset Settings**, then **Developer Mode**, and turn it on.
2. Download `PianoGlass.apk` from this repo's Releases page.
3. Plug the headset into your computer with a USB-C data cable and put it on. When it
   asks to allow USB debugging, choose **Always allow from this computer**.
4. On the computer, run `adb install -r PianoGlass.apk`. (`adb` comes with the Android
   SDK. Meta Quest Developer Hub can install an APK by drag and drop too.)
5. Unplug the computer. The headset has only one USB-C port, so the keyboard goes in
   the same port.
6. Open **Library**, then **Unknown Sources**, then **PianoGlass**.

Then calibrate (below), once for your piano.

## Calibrating (once per piano)

Calibration tells the app where your real keys are. It takes about 20 seconds and
you need the right controller for it, because its tip measures to about a millimetre
and a finger only to about a centimetre.

1. Put the controllers down for a moment and poke **Settings** on the glass panel at
   the left end of the keyboard. Poke **Calibrate**.
2. Pick up the right controller. Touch the tip to the **middle of the front edge of the
   lowest key** (A0, far left) and pull the trigger.
3. Do the same on the **highest key** (C8, far right).
4. The keys appear on your piano. If they are a little off, use the arrows under
   **Move the keys** in Settings (2 mm per poke). If a **Looks right** button lights
   up, poke it when they line up.

No controller? You can press and hold A0, then C8, with one finger instead. It works,
but expect to move the keys a little afterwards.

## Playing a song

Put the controllers down. Everything works with your hands: poke a glass button with
your index finger, or pinch a panel to move it.

1. On the song panel at the left end of the keyboard, poke **Songs** and pick one.
   Twinkle Twinkle Little Star is the easiest; Ode to Joy and Bach's Minuet in G come
   next. You can pick the left hand, the right hand, or both.
2. Poke **Play**. Notes fall toward the keys. Press each key as its note reaches the
   line on the key.
3. When the song ends, the results card comes up. Poke **More info** for every note's
   timing, **Retry** to go again, or the suggestion's button to do what it says.

Before you press Play, the song panel also has **Tempo -** and **Tempo +**, the
**Metronome**, **Wait mode** (good for a first try) and **Memory fade**.

## If something goes wrong

- **The keys are in the wrong place:** recenter (hold the Meta button). If they are
  still off, calibrate again.
- **Nothing happens when I press keys:** unplug the keyboard and plug it back in, and
  check it is turned on and has power. The app only listens to USB MIDI, not
  Bluetooth.
- **I can't find the app:** look in Library, under Unknown Sources, not in the main
  list.

## For developers

Unity 6000.6.0f1 with URP, Meta XR SDK 205.0.0, IL2CPP for ARM64. The versions are
pinned on purpose: don't upgrade them.

- Build with `.\build.ps1` (Unity must be closed). `.\build.ps1 -Install` builds and
  installs on a plugged-in headset.
- `docs/architecture.md` explains how the code is laid out.
- `docs/controls.md` lists every control.
- `docs/building.md` has the full build notes, and `docs/traps.md` the mistakes I
  already made so you don't have to.
- Songs are normal `.mid` files in `Assets/Songs`; `Assets/Songs/SOURCES.md` says where
  each one came from and why it is free to use.

Made by Ryan, a high school student, as a solo project.
