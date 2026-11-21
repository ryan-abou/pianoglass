# Measuring the metronome's latency from a phone video

**Prefer the eyes-closed test (2026-11-21).** The 2026-11-20 video said the click was 89 ms
late; it was really about 200 ms early. Play Twinkle at 50% several times with the metronome
on and your eyes closed, going only by the click, then take the median of every press minus
its beat (real ms) from the attempt files. The right latency is the current setting plus
that median. The videos below are kept for reference.

How `SceneSetup.MetronomeOutputLatencyMs` was set for the Quest 3 (#16). Python 3 with
numpy, and ffmpeg (set `FFMPEG_BIN` to its `bin` folder if it isn't the default path).

1. In the headset: Twinkle, metronome on. Film 20-30 s through the lens at 60 fps,
   with the phone resting on something so it doesn't move.
2. Find the clicks in the sound (the last number is the threshold, in multiples of the
   background noise; 12 works):

   ```
   ffmpeg -i video.mp4 -vn -ac 1 -ar 48000 -c:a pcm_s16le audio.wav
   python clicks.py audio.wav <audio start_time from ffprobe> 12
   ```

   Use the evenly spaced ones (500 ms apart at 120 bpm; bar clicks are louder).
3. Pick a pixel column that beat lines cross but notes and vertical grid lines don't,
   and a band of rows from well above the keys to just into them. `streaks.py` draws
   one column per frame side by side, so each falling line is a straight stripe:
   check the crop with it first.

   ```
   python streaks.py video.mp4 x0 x1 y0 y1 0.35 0.25 streaks.png <clicks...>
   ```

4. `streakfit.py` fits each stripe and gives, per beat, the click minus the moment the
   line reaches the keys (positive: the click is late), then the median.

   ```
   python streakfit.py video.mp4 x0 x1 y0 y1 0.35 0.25 <clicks...>
   ```

   Throw out beats far from the rest (the phone moved, or a note covered the column).
   The right latency is the current setting plus the median. A steady video gives a
   spread of under 10 ms; two videos from the same day agreed within 10 ms.

If the phone was held by hand or moved between angles, use `beatfit.py` instead (same
arguments; try 0.6 0.6 for before and after). It measures each line's distance from
the key line in the same frame, so the phone moving doesn't count, and it decodes only
the frames around each click. Pick a column for each angle and run it once per angle.

Always clap a few times at the end, in view. The phone's own sound can lag its picture:
on 2026-11-20 it was 75 ms behind (the hands stop moving 75 ms before the clap sound),
and on 2026-11-15 it was within a frame. Take that lag off the result.

Reading contact sheets by eye with only 3 beats was too coarse (it said 310, then
200); this way said 238 and 248 for the same headset (2026-11-14).
