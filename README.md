# PianoGlass

PianoGlass is a piano trainer for the Meta Quest 3. You sit at a real piano and
the headset shows notes falling onto your real keys, like the falling note piano
videos on YouTube. The keyboard is plugged into the headset with USB, so the app
knows which key you pressed and when.

Video: _(a 30 second clip from the headset goes here)_

## Why I made it

I wanted something that tells you what you got wrong while you learn a song, which
a video can't do. Since the app gets every key press from the keyboard, it can:

- If you play D instead of D sharp, the key turns red and the app says which note it
  wanted.
- Every note is judged on timing (Perfect, Great, Good or Miss), and it shows how
  early or late you were in milliseconds.
- After each try, a results card shows your accuracy, your timing and your wrong
  notes. It also has a row with one box per bar, coloured by how well you played
  that bar.
- It suggests what to do next, like "Bar 9 needs work" with a button to loop that bar,
  or playing your weaker hand alone when one hand made most of the mistakes.
- There are practice tools too. You can slow the tempo down, turn on a metronome,
  loop bars, use wait mode (the song waits for you to press the right key), play one
  hand or both, and use memory fade, which fades the notes out so you have to remember
  them.

The menus are styled after Apple's Liquid Glass.

## What you need

- A Meta Quest 3.
- An 88-key keyboard or digital piano with USB MIDI.
- A cable from the keyboard to the headset's USB-C port. If the keyboard's cable ends
  in a normal USB-A plug, you need a USB-C to USB-A adapter.
- A room with decent light.

## Trying it

### Using my headset

If I left you my Quest 3, the app is already installed and set up for my piano.

1. Put the headset on and open **Library**, then the **Unknown Sources** tab, then
   **PianoGlass**.
2. Plug the keyboard into the headset's USB-C port.
3. If the app asks you to recenter, hold the **Meta button** on the right controller
   until the keys line up with the piano.

### On your own Quest 3

Installing an app that isn't from the store needs developer mode.

1. In the Meta Horizon phone app, tap the headset icon, tap your headset, then
   **Headset Settings**, then **Developer Mode**, and turn it on.
2. Download `PianoGlass.apk` from this repo's Releases page.
3. Plug the headset into your computer with a USB-C data cable and put it on. When it
   asks to allow USB debugging, choose **Always allow from this computer**.
4. On the computer, run `adb install -r PianoGlass.apk` (`adb` comes with the Android
   SDK).
5. Open **Library**, then **Unknown Sources**, then **PianoGlass**.

Then calibrate (below), once for your piano.

## Calibrating (once per piano)

Calibration tells the app where your real keys are. You need the right controller.

1. Poke **Settings** on the glass panel at the left end of the keyboard, then
   **Calibrate**.
2. Pick up the right controller. Touch the tip to the **middle of the front edge of the
   lowest key** (A0, far left) and pull the trigger.
3. Do the same on the **highest key** (C8, far right).
4. The keys appear on your piano. If they are a little off, use the arrows under
   **Move the keys** in Settings. Poke **Looks right** when they line up.

No controller? Press and hold A0, then C8, with one finger instead. You will probably
need to move the keys a little afterwards.

## Playing a song

Put the controllers down. Poke buttons with your index finger, or pinch a panel to
move it.

1. On the song panel at the left end of the keyboard, poke **Songs** and pick one.
   Twinkle Twinkle Little Star is the easiest. You can pick the left hand, the right
   hand, or both.
2. Poke **Play**. Press each key as its note reaches the line on the key.
3. When the song ends, the results card comes up. Poke **Retry** to go again, or
   **More info** to see each note's timing.

Before you press Play, the song panel also has **Tempo -** and **Tempo +**, the
**Metronome**, **Wait mode** and **Memory fade**.

## If something goes wrong

- **The keys are in the wrong place.** Recenter by holding the Meta button. If they
  are still off, calibrate again.
- **Nothing happens when I press keys.** Unplug the keyboard, plug it back in, and
  check it's on. The app only works with USB MIDI, not Bluetooth.
- **I can't find the app.** Look in Library under Unknown Sources.

Still stuck? Open an issue on this repo's Issues tab.

Made by Ryan, a high school student.
