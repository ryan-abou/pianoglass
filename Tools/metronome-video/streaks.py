"""Streak images: for each click, one thin pixel column per frame (around the key
line), laid side by side in time order, so a falling beat line is a diagonal
stripe and the frame it reaches the key line can be read exactly.
Usage: python streaks.py video x0 x1 y0 y1 before after out_png clicks...
Prints each strip's frame times relative to its click."""
import subprocess, sys, json, os
import numpy as np

FF = os.environ.get("FFMPEG_BIN", r"C:\Users\Ryan\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin")
video = sys.argv[1]
x0, x1, y0, y1 = map(int, sys.argv[2:6])
before, after = float(sys.argv[6]), float(sys.argv[7])
out = sys.argv[8]
clicks = [float(c) for c in sys.argv[9:]]
W, H = x1 - x0, y1 - y0
COL = 6  # pixels per frame in the streak image

probe = subprocess.run([os.path.join(FF, "ffprobe.exe"), "-v", "error", "-select_streams", "v:0",
                        "-show_entries", "frame=pts_time", "-of", "json", video],
                       capture_output=True, text=True, check=True)
times = np.array([float(f["pts_time"]) for f in json.loads(probe.stdout)["frames"]])
raw = subprocess.run([os.path.join(FF, "ffmpeg.exe"), "-v", "error", "-i", video,
                      "-vf", f"crop={W}:{H}:{x0}:{y0},format=gray", "-fps_mode", "passthrough",
                      "-f", "rawvideo", "-"], capture_output=True, check=True).stdout
frames = np.frombuffer(raw, dtype=np.uint8).reshape(-1, H, W)
assert len(frames) == len(times)

strips = []
for c in clicks:
    idx = [i for i, t in enumerate(times) if c - before <= t <= c + after]
    cols = []
    for i in idx:
        col = np.median(frames[i], axis=1).astype(np.uint8)  # one value per row
        cols.append(np.repeat(col[:, None], COL, axis=1))
    strip = np.concatenate(cols, axis=1)
    # tick marks on top: white where the click is (between frames), grey every 50 ms
    ticks = np.zeros((10, strip.shape[1]), np.uint8)
    for k, i in enumerate(idx):
        dt = times[i] - c
        if k > 0 and (times[idx[k - 1]] - c) < 0 <= dt: ticks[:, k * COL - 1:k * COL + 1] = 255
        if int(np.floor((times[idx[k - 1]] - c) / 0.05)) != int(np.floor(dt / 0.05)) and k > 0:
            ticks[5:, k * COL] = 128
    strips.append(np.vstack([ticks, strip, np.zeros((6, strip.shape[1]), np.uint8)]))
    print(f"click {c:.4f}: " + " ".join(f"{k}:{1000 * (times[i] - c):+.0f}" for k, i in enumerate(idx)))

w = max(s.shape[1] for s in strips)
img = np.vstack([np.pad(s, ((0, 0), (0, w - s.shape[1]))) for s in strips])
# write a PGM, then let ffmpeg turn it into a PNG
pgm = out + ".pgm"
with open(pgm, "wb") as f:
    f.write(f"P5 {img.shape[1]} {img.shape[0]} 255\n".encode()); f.write(img.tobytes())
subprocess.run([os.path.join(FF, "ffmpeg.exe"), "-v", "error", "-y", "-i", pgm, out], check=True)
os.remove(pgm)
print(f"wrote {out} ({img.shape[1]}x{img.shape[0]})")
