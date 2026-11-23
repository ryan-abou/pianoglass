using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// What the Meta button highlight (#42) needs from the editor, so it is
    /// in the build: Meta's controller prefab (OVRControllerPrefab, which
    /// picks the right model for the headset and lines it up with the real
    /// controller), the see-through material, and the Meta button's part of
    /// the Quest 3 right controller, cut out in the editor because a build
    /// can't read Meta's mesh. Wired by SceneSetup.ConfigureMetaButtonHint.
    /// </summary>
    public sealed class MetaButtonHintAssets : MonoBehaviour
    {
        public GameObject controllerPrefab;
        public Material translucent;
        public Mesh touchPlusRightCap;
        /// <summary>The right controller from above with its Meta button lit,
        /// for the recenter banner (round 15). Rendered by MetaButtonPreview.</summary>
        public Texture2D diagram;
    }
}
