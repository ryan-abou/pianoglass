using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// The player's two tracked hands, for using the app without controllers
    /// (#5): poking Liquid Glass buttons and calibrating by pressing keys.
    /// Gives each hand's index fingertip in world space while that hand is
    /// tracked; with controllers in hand, nothing is tracked and nothing here
    /// does anything.
    ///
    /// Built the way the press probe built them (2026-11-05, 328 presses on
    /// device): an OVRHand and an OVRSkeleton per hand under the rig's tracking
    /// space, their private type fields set while inactive because neither
    /// offers a public way. Adds itself after the scene loads; no scene change.
    /// </summary>
    public sealed class TrackedHands : MonoBehaviour
    {
        public static TrackedHands Instance { get; private set; }

        private sealed class Hand
        {
            public OVRHand Ovr;
            public OVRSkeleton Skeleton;
            public OVRSkeleton.BoneId IndexTip;
            public Transform Tip;
        }

        private readonly List<Hand> _hands = new List<Hand>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            if (Application.isEditor) return; // no hand tracking in the editor
            var go = new GameObject(nameof(TrackedHands));
            DontDestroyOnLoad(go);
            go.AddComponent<TrackedHands>();
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                Debug.LogWarning("[Hands] no camera rig: hand tracking off");
                enabled = false;
                return;
            }
            Build(rig, OVRHand.Hand.HandLeft);
            Build(rig, OVRHand.Hand.HandRight);
            Debug.Log($"[Hands] on: {_hands.Count} hands");
        }

        public int Count => _hands.Count;

        /// <summary>Hand <paramref name="i"/>'s index fingertip, if that hand is tracked.</summary>
        public bool TryIndexTip(int i, out Vector3 world)
        {
            world = default;
            if (i < 0 || i >= _hands.Count) return false;
            Hand h = _hands[i];
            if (h.Ovr == null || !h.Ovr.IsTracked || h.Skeleton == null || !h.Skeleton.IsInitialized) return false;
            if (h.Tip == null)
            {
                foreach (OVRBone bone in h.Skeleton.Bones)
                    if (bone.Id == h.IndexTip) h.Tip = bone.Transform;
                if (h.Tip == null) return false;
            }
            world = h.Tip.position;
            return true;
        }

        /// <summary>Any hand tracked right now: the player has put the controllers down.</summary>
        public bool AnyTracked
        {
            get
            {
                for (int i = 0; i < _hands.Count; i++) if (TryIndexTip(i, out _)) return true;
                return false;
            }
        }

        /// <summary>The lower of the two index fingertips: the one pressing a
        /// key. False if no hand is tracked.</summary>
        public bool TryPressingIndexTip(out Vector3 world)
        {
            world = default;
            bool found = false;
            for (int i = 0; i < _hands.Count; i++)
            {
                if (!TryIndexTip(i, out Vector3 tip)) continue;
                if (!found || tip.y < world.y) world = tip;
                found = true;
            }
            return found;
        }

        private void Build(OVRCameraRig rig, OVRHand.Hand type)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo handType = typeof(OVRHand).GetField("HandType", Private);
            FieldInfo skeletonType = typeof(OVRSkeleton).GetField("_skeletonType", Private);
            FieldInfo updateRoot = typeof(OVRSkeleton).GetField("_updateRootPose", Private);
            if (handType == null || skeletonType == null || updateRoot == null)
            {
                Debug.LogError("[Hands] the SDK's hand fields were not found");
                return;
            }

            var go = new GameObject($"Hand {type}");
            go.SetActive(false);
            go.transform.SetParent(rig.trackingSpace, false);
            OVRHand hand = go.AddComponent<OVRHand>();
            handType.SetValue(hand, type);
            OVRSkeleton skeleton = go.AddComponent<OVRSkeleton>();
            OVRSkeleton.SkeletonType kind = ((OVRSkeleton.IOVRSkeletonDataProvider)hand).GetSkeletonType();
            skeletonType.SetValue(skeleton, kind);
            updateRoot.SetValue(skeleton, true);
            go.SetActive(true);

            bool openXr = kind == OVRSkeleton.SkeletonType.XRHandLeft || kind == OVRSkeleton.SkeletonType.XRHandRight;
            _hands.Add(new Hand
            {
                Ovr = hand,
                Skeleton = skeleton,
                IndexTip = openXr ? OVRSkeleton.BoneId.XRHand_IndexTip : OVRSkeleton.BoneId.Hand_IndexTip,
            });
        }
    }
}
