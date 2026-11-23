using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PianoGlass.Piano;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// Shows the resume count (#44) in a glass circle (round 13: not the long
    /// banner), gliding after the head the same way as the launch recenter
    /// prompt, so it is in view wherever the player looks. SongTransport runs
    /// the count; this only draws it.
    ///
    /// Round 15 (Ryan's design): a metronome count-in longer than 3 beats
    /// starts with a row of empty dots, and one fills in, with a small pulse,
    /// on each click; the last three beats are 3, 2, 1. Each thing shows for
    /// exactly its beat, and the 1 goes the frame the song plays. Without the
    /// metronome it is 3, 2, 1, a second each. Builds itself after the scene
    /// loads.
    /// </summary>
    public sealed class ResumeCountdownView : MonoBehaviour
    {
        private const float NumberSize = 110f;
        private static readonly Vector2 Circle = new Vector2(190f, 190f);

        /// <summary>The row of dots: no wider than this, each at most this big.</summary>
        private const float RowWidth = 150f, MaxDot = 24f;
        /// <summary>A dot that just filled grows this much, and settles back over this long.</summary>
        private const float PulseGrow = 0.45f, PulseSeconds = 0.18f;

        private SongTransport _transport;
        private SongPicker _style;
        private RecenterPromptView _view;
        private RectTransform _row;
        private readonly List<Image> _dots = new List<Image>();
        private Sprite _ring, _disc;
        private Color _ink = Color.white;
        private int _shownBeat = -1, _shownDots = -1, _filled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add() => new GameObject(nameof(ResumeCountdownView)).AddComponent<ResumeCountdownView>();

        private void Start()
        {
            _transport = FindAnyObjectByType<SongTransport>();
            _style = FindAnyObjectByType<SongPicker>(FindObjectsInactive.Include);
            if (_transport == null || _style == null) enabled = false;
        }

        private void LateUpdate()
        {
            ResumeCountdown c = _transport.Countdown;
            double now = Time.realtimeSinceStartupAsDouble;
            int beat = c.BeatIndex(now);
            if (beat < 0)
            {
                if (_view != null && _view.Shown) _view.Hide();
                _shownBeat = _shownDots = -1;
                return;
            }

            if (_view == null)
            {
                _view = RecenterPromptView.Create(_style, "", Circle);
                if (_view == null) return;
                GlassTokens tokens = _style.Style.Item4;
                _ink = tokens != null ? tokens.textPrimary : GlassTokens.DefaultTextPrimary;
                _ink.a = 0.92f;
            }

            if (beat != _shownBeat || c.Dots != _shownDots)
            {
                _shownBeat = beat;
                _shownDots = c.Dots;
                int n = c.Number(now);
                _view.SetText(n > 0 ? n.ToString() : "", NumberSize);
                _filled = n > 0 ? 0 : c.FilledDots(now);
                ShowDots(n > 0 ? 0 : c.Dots, _filled);
                if (!_view.Shown) _view.Show();
            }

            // The dot that just filled pulses with its click.
            if (_filled > 0 && _filled <= _dots.Count)
            {
                float s = (float)c.SinceBeat(now);
                float k = s < PulseSeconds ? 1f + PulseGrow * (1f - s / PulseSeconds) : 1f;
                _dots[_filled - 1].rectTransform.localScale = Vector3.one * k;
            }
        }

        /// <summary><paramref name="count"/> dots in a row, the first
        /// <paramref name="filled"/> filled in; none hides the row.</summary>
        private void ShowDots(int count, int filled)
        {
            if (count <= 0)
            {
                if (_row != null) _row.gameObject.SetActive(false);
                return;
            }
            if (_row == null)
            {
                var go = new GameObject("Dots", typeof(RectTransform));
                _row = (RectTransform)go.transform;
                _row.SetParent(_view.transform, false);
                _row.anchorMin = _row.anchorMax = new Vector2(0.5f, 0.5f);
                _row.sizeDelta = new Vector2(RowWidth, MaxDot);
                _ring = MakeDot(ring: true);
                _disc = MakeDot(ring: false);
            }
            _row.gameObject.SetActive(true);

            while (_dots.Count < count)
            {
                var go = new GameObject("Dot", typeof(RectTransform));
                go.transform.SetParent(_row, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                _dots.Add(image);
            }

            // Gaps half a dot wide; the row fits the circle however many there are.
            float size = Mathf.Min(MaxDot, RowWidth / (1.5f * count - 0.5f));
            float pitch = size * 1.5f;
            float left = -(count - 1) * pitch * 0.5f;
            for (int i = 0; i < _dots.Count; i++)
            {
                Image dot = _dots[i];
                bool used = i < count;
                dot.gameObject.SetActive(used);
                if (!used) continue;
                RectTransform rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(left + i * pitch, 0f);
                rt.localScale = Vector3.one;
                dot.sprite = i < filled ? _disc : _ring;
                dot.color = _ink;
            }
        }

        /// <summary>A white circle, filled or just its outline, smooth at the edge.</summary>
        private static Sprite MakeDot(bool ring)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = ring ? "CountDotRing" : "CountDot",
            };
            var pixels = new Color[size * size];
            float r = size * 0.5f - 1f, edge = 1.5f, thick = size * 0.12f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01((r - d) / edge);
                if (ring) a *= Mathf.Clamp01((d - (r - thick)) / edge);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            if (_view != null) Destroy(_view.gameObject);
        }
    }
}
