"""Reads the streaks: for each click, the moment the beat line reaches the key line.
Same pixel column as streaks.py. Per frame: the key band's top row (first row where
the next 12 rows are all bright), and the bright one-row stripe (the beat line)
above it. The stripe's path over the frames before it reaches the band is fitted
with a straight line; where that line meets the band top is the crossing time.
Usage: python streakfit.py video x0 x1 y0 y1 before after clicks...
Positive offset = the click came after the line (late); negative = early."""
import subprocess, sys, json, os
import numpy as np

FF = os.environ.get("FFMPEG_BIN", r"C:\Users\Ryan\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin")
video = sys.argv[1]
x0, x1, y0, y1 = map(int, sys.argv[2:6])
before, after = float(sys.argv[6]), float(sys.argv[7])
clicks = [float(c) for c in sys.argv[8:]]
W, H = x1 - x0, y1 - y0

probe = subprocess.run([os.path.join(FF, "ffprobe.exe"), "-v", "error", "-select_streams", "v:0",
                        "-show_entries", "frame=pts_time", "-of", "json", video],
                       capture_output=True, text=True, check=True)
times = np.array([float(f["pts_time"]) for f in json.loads(probe.stdout)["frames"]])
raw = subprocess.run([os.path.join(FF, "ffmpeg.exe"), "-v", "error", "-i", video,
                      "-vf", f"crop={W}:{H}:{x0}:{y0},format=gray", "-fps_mode", "passthrough",
                      "-f", "rawvideo", "-"], capture_output=True, check=True).stdout
frames = np.frombuffer(raw, dtype=np.uint8).reshape(-1, H, W).astype(np.float32)
assert len(frames) == len(times)

results = []
for c in clicks:
    idx = [i for i, t in enumerate(times) if c - before <= t <= c + after]
    edges, pts = [], []
    for i in idx:
        p = np.median(frames[i], axis=1)
        if np.median(p) < 20:          # frame blacked out by the refresh band
            continue
        bg = np.median(p)
        bright = p > max(bg + 30, 90)
        runs = []
        for y in range(H):
            if not bright[y]: continue
            if runs and y == runs[-1][1] + 1: runs[-1][1] = y
            else: runs.append([y, y])
        # the keys: the bright run that reaches the bottom of the crop
        if not runs or runs[-1][1] < H - 2:
            continue
        edge = runs[-1][0]
        edges.append(edge)
        # the beat line: the lowest short bright run above the keys (its centre)
        lines = [r for r in runs[:-1] if r[1] - r[0] < 45]
        if lines:
            pts.append((times[i], (lines[-1][0] + lines[-1][1]) / 2.0, edge))
    if len(edges) < 5 or len(pts) < 5:
        print(f"click {c:.4f}: not enough clear frames"); continue
    # A line merging with the keys pulls the band's top up for a frame or two:
    # the band's own top is the most common (high) value.
    edge = float(np.percentile(edges, 75))
    # the approach of the line that reaches the band near this click: the lowest
    # ridge positions that keep increasing; keep those within 120 rows of the band
    t = np.array([q[0] for q in pts]); y = np.array([q[1] for q in pts], float)
    keep = (y > edge - 220) & (y < edge - 8)
    t, y = t[keep], y[keep]
    # drop points after the line has gone (a jump back up means the next line)
    order = np.argsort(t); t, y = t[order], y[order]
    cut = next((k for k in range(1, len(y)) if y[k] < y[k - 1] - 20), len(y))
    t, y = t[:cut], y[:cut]
    if len(t) < 4:
        print(f"click {c:.4f}: line not tracked"); continue
    b, a = np.polyfit(t, y, 1)              # rows per second, intercept
    resid = np.std(y - (a + b * t))
    cross = (edge - a) / b
    off = 1000 * (c - cross)
    results.append(off)
    print(f"click {c:.4f}: band top row {edge:.0f}, line speed {b:.0f} rows/s from {len(t)} frames "
          f"(fit sd {resid:.1f} rows), reaches the keys at {cross:.4f} -> click {off:+.0f} ms")

if results:
    r = np.array(results)
    print(f"\n{len(r)} beats: click minus line reaching the keys = mean {r.mean():+.0f} ms, "
          f"median {np.median(r):+.0f} ms, sd {r.std():.0f} ms   (negative = click early)")
