using System.Globalization;
using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// The maths for moving a panel by hand (#5): holding it rigidly in a hand,
    /// and saving where it was put relative to the keyboard, so it comes back
    /// in the same place after a relaunch and moves with the keys when they
    /// are nudged or calibrated again. Pure, so it is tested without a headset.
    /// </summary>
    public static class PanelPose
    {
        /// <summary><paramref name="world"/> seen from <paramref name="frame"/>.</summary>
        public static Pose ToFrame(Pose frame, Pose world)
        {
            Quaternion inv = Quaternion.Inverse(frame.rotation);
            return new Pose(inv * (world.position - frame.position), Normalized(inv * world.rotation));
        }

        /// <summary>A pose given in <paramref name="frame"/>, back in the world.</summary>
        public static Pose FromFrame(Pose frame, Pose local) =>
            new Pose(frame.position + frame.rotation * local.position, Normalized(frame.rotation * local.rotation));

        /// <summary>Eases <paramref name="current"/> toward <paramref name="target"/>:
        /// a hand shakes a little, and a far grab makes that shake bigger.
        /// <paramref name="rate"/> is per second; the same feel at any frame rate.</summary>
        public static Pose Smooth(Pose current, Pose target, float dt, float rate)
        {
            float t = 1f - Mathf.Exp(-rate * Mathf.Max(0f, dt));
            return new Pose(Vector3.Lerp(current.position, target.position, t),
                            Quaternion.Slerp(current.rotation, target.rotation, t));
        }

        /// <summary>A pointing ray as a pose with no roll: twisting the wrist
        /// while holding a far panel doesn't spin it.</summary>
        public static Pose RayPose(Vector3 origin, Vector3 direction)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return new Pose(origin, Quaternion.LookRotation(direction, up));
        }

        /// <summary>"x,y,z,qx,qy,qz,qw", the same on every language setting.</summary>
        public static string Format(Pose p)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return string.Join(",",
                p.position.x.ToString("R", c), p.position.y.ToString("R", c), p.position.z.ToString("R", c),
                p.rotation.x.ToString("R", c), p.rotation.y.ToString("R", c), p.rotation.z.ToString("R", c),
                p.rotation.w.ToString("R", c));
        }

        /// <summary>False for anything that isn't seven finite numbers making a
        /// real rotation, so a bad save puts the panel back in its usual spot.</summary>
        public static bool TryParse(string s, out Pose pose)
        {
            pose = Pose.identity;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split(',');
            if (parts.Length != 7) return false;
            var v = new float[7];
            for (int i = 0; i < 7; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]) ||
                    float.IsNaN(v[i]) || float.IsInfinity(v[i]))
                    return false;
            var q = new Quaternion(v[3], v[4], v[5], v[6]);
            float len = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (len < 0.5f || len > 1.5f) return false;
            pose = new Pose(new Vector3(v[0], v[1], v[2]), Normalized(q));
            return true;
        }

        private static Quaternion Normalized(Quaternion q)
        {
            float len = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return len < 1e-6f ? Quaternion.identity : new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len);
        }
    }
}
