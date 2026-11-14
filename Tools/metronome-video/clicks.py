"""Find metronome click onsets in a mono 16-bit wav. Prints container-time seconds.
Usage: python -I clicks.py audio.wav audio_start_offset_seconds"""
import sys, wave
import numpy as np

path, offset = sys.argv[1], float(sys.argv[2])
with wave.open(path) as w:
    sr = w.getframerate()
    x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64)
print(f"wav: {len(x)/sr:.3f} s at {sr} Hz")

# A click is a sharp broadband transient: high-pass by first difference,
# then a 2 ms energy envelope.
d = np.diff(x, prepend=x[0])
win = int(0.002 * sr)
env = np.sqrt(np.convolve(d * d, np.ones(win) / win, mode="same"))
noise = np.median(env)
thr = noise * float(sys.argv[3]) if len(sys.argv) > 3 else noise + 0.25 * (np.percentile(env, 99.9) - noise)

onsets, i, refractory = [], 0, int(0.25 * sr)
while i < len(env):
    if env[i] > thr:
        # walk back to where the envelope first rose above 20% of this peak
        peak_i = i + int(np.argmax(env[i:i + int(0.01 * sr)]))
        lo = max(0, i - int(0.01 * sr))
        seg = env[lo:peak_i + 1]
        rise = lo + int(np.argmax(seg > noise + 0.2 * (env[peak_i] - noise)))
        onsets.append((rise / sr + offset, env[peak_i]))
        i = peak_i + refractory
    else:
        i += 1

print(f"noise {noise:.0f}, threshold {thr:.0f}, clicks found: {len(onsets)}")
prev = None
for t, a in onsets:
    gap = "" if prev is None else f"  gap {1000*(t-prev):6.1f} ms"
    print(f"{t:8.4f} s  amp {a:7.0f}{gap}")
    prev = t
