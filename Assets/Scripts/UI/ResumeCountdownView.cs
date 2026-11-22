using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// Shows the resume countdown (#44): a big 3, 2, 1 in a glass circle
    /// (round 13: not the long banner), gliding after the head the same way
    /// as the launch recenter prompt, so it is in view wherever the player looks. SongTransport runs the
    /// countdown; this only draws it. Builds itself after the scene loads.
    /// </summary>
    public sealed class ResumeCountdownView : MonoBehaviour
    {
        private const float NumberSize = 110f;
        private static readonly Vector2 Circle = new Vector2(190f, 190f);

        private SongTransport _transport;
        private SongPicker _style;
        private RecenterPromptView _view;
        private int _shown;

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
            int n = _transport.CountdownNumber;
            if (n == _shown) return;
            _shown = n;
            if (n == 0)
            {
                if (_view != null) _view.Hide();
                return;
            }
            if (_view == null) _view = RecenterPromptView.Create(_style, "", Circle);
            if (_view == null) return;
            _view.SetText(n.ToString(), NumberSize);
            if (!_view.Shown) _view.Show();
        }

        private void OnDestroy()
        {
            if (_view != null) Destroy(_view.gameObject);
        }
    }
}
