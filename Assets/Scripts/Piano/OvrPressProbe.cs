using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using PianoGlass.Midi;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Development builds: logs each key press against the tracked fingertips,
    /// to measure whether presses could correct drift (KeyPressProbe). It draws
    /// nothing and moves nothing. Hands are only tracked with the controllers put
    /// down, and the headset's hand tracking setting has to be on.
    ///
    /// Made at runtime rather than through SceneSetup: it is a measurement that
    /// should leave no trace in the scene once it has answered the question.
    /// </summary>
    public sealed class OvrPressProbe : MonoBehaviour
    {
        private const int SummaryEvery = 20;

        private static readonly OVRSkeleton.BoneId[] OvrTips =
        {
            OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.Hand_MiddleTip,
            OVRSkeleton.BoneId.Hand_RingTip, OVRSkeleton.BoneId.Hand_PinkyTip,
        };
        private static readonly OVRSkeleton.BoneId[] XrTips =
        {
            OVRSkeleton.BoneId.XRHand_ThumbTip, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.XRHand_MiddleTip,
            OVRSkeleton.BoneId.XRHand_RingTip, OVRSkeleton.BoneId.XRHand_LittleTip,
        };

        private sealed class TrackedHand
        {
            public OVRHand Hand;
            public OVRSkeleton Skeleton;
            public bool OpenXr;
        }

        private readonly List<TrackedHand> _hands = new List<TrackedHand>();
        private readonly List<Vector3> _tips = new List<Vector3>();
        private readonly PressStats _stats = new PressStats();
        private KeyboardCalibration _calibration;
        private int _noHands;
        private int _lowConfidence;
        private int _noFinger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isEditor || !Debug.isDebugBuild) return;
            new GameObject("PressProbe").AddComponent<OvrPressProbe>();
        }

        private void Start()
        {
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
            if (_calibration == null || rig == null || MidiInput.Instance == null)
            {
                Debug.LogWarning("[PressProbe] off: no calibration, camera rig or MIDI");
                enabled = false;
                return;
            }
            Build(rig, OVRHand.Hand.HandLeft);
            Build(rig, OVRHand.Hand.HandRight);
            MidiInput.Instance.NoteOn += OnNoteOn;
            Debug.Log($"[PressProbe] on, {_hands.Count} hands");
        }

        private void OnDestroy()
        {
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn -= OnNoteOn;
        }

        /// <summary>Set up while inactive: both components read these fields as
        /// they wake, and neither offers a public way to set them.</summary>
        private void Build(OVRCameraRig rig, OVRHand.Hand type)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo handType = typeof(OVRHand).GetField("HandType", Private);
            FieldInfo skeletonType = typeof(OVRSkeleton).GetField("_skeletonType", Private);
            FieldInfo updateRoot = typeof(OVRSkeleton).GetField("_updateRootPose", Private);
            if (handType == null || skeletonType == null || updateRoot == null)
            {
                Debug.LogError("[PressProbe] the SDK's hand fields were not found");
                return;
            }

            var go = new GameObject($"PressProbe {type}");
            go.SetActive(false);
            go.transform.SetParent(rig.trackingSpace, false);

            OVRHand hand = go.AddComponent<OVRHand>();
            handType.SetValue(hand, type);
            OVRSkeleton skeleton = go.AddComponent<OVRSkeleton>();
            OVRSkeleton.SkeletonType skeletonKind = ((OVRSkeleton.IOVRSkeletonDataProvider)hand).GetSkeletonType();
            skeletonType.SetValue(skeleton, skeletonKind);
            updateRoot.SetValue(skeleton, true);
            go.SetActive(true);

            bool openXr = skeletonKind == OVRSkeleton.SkeletonType.XRHandLeft ||
                          skeletonKind == OVRSkeleton.SkeletonType.XRHandRight;
            _hands.Add(new TrackedHand { Hand = hand, Skeleton = skeleton, OpenXr = openXr });
        }

        private void OnNoteOn(int note, int velocity, double time)
        {
            if (!_calibration.IsPlacementValid || _calibration.Layout == null) return;

            _tips.Clear();
            bool allHighConfidence = true;
            foreach (TrackedHand h in _hands)
            {
                if (!h.Hand.IsTracked || !h.Skeleton.IsInitialized || h.Skeleton.Bones == null) continue;
                if (!h.Hand.IsDataHighConfidence) allHighConfidence = false;
                OVRSkeleton.BoneId[] tips = h.OpenXr ? XrTips : OvrTips;
                foreach (OVRBone bone in h.Skeleton.Bones)
                    if (System.Array.IndexOf(tips, bone.Id) >= 0) _tips.Add(bone.Transform.position);
            }

            if (_tips.Count == 0)
            {
                _noHands++;
            }
            else
            {
                PressReading r = KeyPressProbe.Measure(_calibration.Layout, note, _tips);
                if (!r.Found)
                {
                    _noFinger++;
                }
                else
                {
                    Debug.Log($"[PressProbe] {MidiInput.NoteName(note)} dx {r.Dx:+0.0;-0.0} dy {r.Dy:+0.0;-0.0} dz {r.Dz:+0.0;-0.0} " +
                              $"next {r.NextDx:+0.0;-0.0} conf {(allHighConfidence ? "high" : "low")}");
                    if (allHighConfidence) _stats.Add(r);
                    else _lowConfidence++;
                }
            }

            if ((_stats.Count + _noHands + _noFinger + _lowConfidence) % SummaryEvery == 0)
                Debug.Log($"[PressProbe] {_stats.Count} presses: dx {_stats.MedianDx:+0.0;-0.0} spread {_stats.SpreadDx:0.0}, " +
                          $"dy {_stats.MedianDy:+0.0;-0.0}, dz {_stats.MedianDz:+0.0;-0.0} mm; " +
                          $"no hands {_noHands}, low confidence {_lowConfidence}, no finger near {_noFinger}");
        }
    }
}
