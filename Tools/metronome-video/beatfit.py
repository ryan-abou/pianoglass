"""Like streakfit.py, but for a hand-held phone: in every frame it measures the
beat line's distance from the key line in that same frame, so the phone moving
or tilting doesn't count, only the line falling. It also decodes only the
frames around each click (much faster on a long 4K video).
Usage: python beatfit.py video x0 x1 y0 y1 before after clicks...
Positive offset = the click came after the line (late); negative = early."""
import subprocess, sys, os, re
import numpy as np

FF = os.environ.get("FFMPEG_BIN", r"C:\Users\Ryan\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin")
video = sys.argv[1]
x0, x1, y0, y1 = map(int, sys.argv[2:6])
before, after = float(sys.argv[6]), float(sys.argv[7])
clicks = [float(c) for c in sys.argv[8:]]
W, H = x1 - x0, y1 - y0


def frames_around(start, length):
    """Grey crops and their times (the video's own clock) from start for length seconds."""
    # -copyts keeps the video's own clock, so -to is a time on that clock
    r = subprocess.run([os.path.join(FF, "ffmpeg.exe"), "-v", "info", "-ss", f"{max(0.0, start):.3f}", "-i", video,
                        "-to", f"{max(0.0, start) + length:.3f}", "-copyts",
                        "-vf", f"crop={W}:{H}:{x0}:{y0},format=gray,showinfo",
                        "-fps_mode", "passthrough", "-an", "-f", "rawvideo", "-"], capture_output=True)
    times = [float(m) for m in re.findall(rb"pts_time:\s*([0-9.]+)", r.stderr)]
    f = np.frombuffer(r.stdout, dtype=np.uint8).reshape(-1, H, W).astype(np.float32)
    n = min(len(times), len(f))
    return np.array(times[:n]), f[:n]


def runs_of(bright):
    runs = []
    for y in range(len(bright)):
        if not bright[y]: continue
        if runs and y == runs[-1][1] + 1: runs[-1][1] = y
        else: runs.append([y, y])
    return runs


results = []
for c in clicks:
    times, frames = frames_around(c - before, before + after)
    pts = []  # (time, distance above the key line of each beat line, spacing)
    for t, fr in zip(times, frames):
        p = np.median(fr, axis=1)
        bg = np.median(p)
        if bg < 20: continue
        runs = runs_of(p > max(bg + 30, 90))
        if not runs or runs[-1][1] < H - 2: continue
        edge = runs[-1][0]
        lines = [(edge - (r[0] + r[1]) / 2.0) for r in runs[:-1] if r[1] - r[0] < 45]
        lines = sorted(d for d in lines if d >= 6)
        if lines:
            pts.append((t, lines[0], lines[1] - lines[0] if len(lines) > 1 else np.nan))
    if len(pts) < 5:
        print(f"click {c:.4f}: not enough clear frames"); continue
    t = np.array([q[0] for q in pts]); d = np.array([q[1] for q in pts]); sp = np.array([q[2] for q in pts])
    spacing = np.nanmedian(sp) if np.any(~np.isnan(sp)) else 200.0
    # Each stretch between jumps (the lowest line merged into the keys and the
    # next one became lowest) is one line falling: fit each, and take the
    # crossing nearest the click.
    jumps = [0] + [k for k in range(1, len(d)) if d[k] > d[k - 1] + 0.3 * spacing] + [len(d)]
    best = None
    for s0, s1 in zip(jumps[:-1], jumps[1:]):
        tt, dd = t[s0:s1], d[s0:s1]
        keep = dd < 0.7 * spacing
        tt, dd = tt[keep], dd[keep]
        if len(tt) < 4: continue
        b, a = np.polyfit(tt, dd, 1)
        if b >= 0: continue
        cr = -a / b
        if best is None or abs(cr - c) < abs(best[0] - c):
            best = (cr, b, len(tt), np.std(dd - (a + b * tt)))
    if best is None:
        print(f"click {c:.4f}: line not tracked"); continue
    cross, b, nfit, resid = best
    tt = range(nfit)
    off = 1000 * (c - cross)
    results.append(off)
    print(f"click {c:.4f}: line spacing {spacing:.0f} rows, falls {-b:.0f} rows/s over {len(tt)} frames "
          f"(fit sd {resid:.1f} rows), reaches the keys at {cross:.4f} -> click {off:+.0f} ms")

if results:
    r = np.array(results)
    print(f"\n{len(r)} beats: click minus line reaching the keys = mean {r.mean():+.0f} ms, "
          f"median {np.median(r):+.0f} ms, sd {r.std():.0f} ms   (negative = click early)")
