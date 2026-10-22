using System;
using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Finds where the controller's plastic tip is, in the controller's own
    /// frame, from a short "hold the tip still and rock the controller"
    /// recording.
    ///
    /// Why this matters: the tip is a fixed offset from the controller's
    /// tracked origin *in the controller's frame*. Treat it as zero (as
    /// PointerTip at local 0,0,0 does) and every captured point is wrong by
    /// R·t, which points a different way whenever the wrist is angled
    /// differently. Reaching for A0 and for C8 it almost always is, so the two
    /// end points are wrong by different amounts -- a yaw and width error no
    /// world-space nudge can remove.
    ///
    /// The maths: while the tip stays on one spot p, every sample satisfies
    /// R_i·t + c_i = p. Solve for t (controller-local tip) and p (world pivot)
    /// by least squares. Eliminating p (it is the mean of R_i·t + c_i) leaves
    /// a 3×3 system: S·t = −Σ (R_i − R̄)ᵀ (c_i − c̄), with
    /// S = Σ (R_i − R̄)ᵀ (R_i − R̄).
    ///
    /// S's smallest eigenvalue says how well t is pinned down. A pure twist
    /// about one axis leaves t's component along that axis undetermined, and
    /// S shows exactly that as a near-zero eigenvalue. The solver refuses
    /// rather than returning a confident wrong answer.
    /// </summary>
    public static class PivotCalibration
    {
        /// <summary>Largest angle between any two samples must reach this.</summary>
        public const float DefaultMinSpreadDegrees = 15f;

        /// <summary>RMS rotation about the least-exercised axis must reach
        /// this, or one component of the offset is guesswork.</summary>
        public const float DefaultMinWeakAxisDegrees = 4f;

        public const int MinSamples = 20;

        public static PivotResult Solve(IReadOnlyList<Pose> controllerPoses,
                                        float minSpreadDegrees = DefaultMinSpreadDegrees,
                                        float minWeakAxisDegrees = DefaultMinWeakAxisDegrees)
        {
            int n = controllerPoses?.Count ?? 0;
            if (n < MinSamples)
                return PivotResult.Fail($"only {n} samples (need {MinSamples})", 0f, 0f);

            float spread = MaxPairwiseAngle(controllerPoses);

            // Means of R and c.
            var rBar = new double[3, 3];
            var cBar = new double[3];
            var r = new double[n][,];
            for (int i = 0; i < n; i++)
            {
                r[i] = Matrix(controllerPoses[i].rotation);
                Vector3 c = controllerPoses[i].position;
                cBar[0] += c.x; cBar[1] += c.y; cBar[2] += c.z;
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                        rBar[a, b] += r[i][a, b];
            }
            for (int a = 0; a < 3; a++)
            {
                cBar[a] /= n;
                for (int b = 0; b < 3; b++) rBar[a, b] /= n;
            }

            // S = Σ Dᵀ D and rhs = −Σ Dᵀ e, with D = R_i − R̄, e = c_i − c̄.
            var s = new double[3, 3];
            var rhs = new double[3];
            for (int i = 0; i < n; i++)
            {
                Vector3 ci = controllerPoses[i].position;
                double[] e = { ci.x - cBar[0], ci.y - cBar[1], ci.z - cBar[2] };
                for (int a = 0; a < 3; a++)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        double sum = 0;
                        for (int k = 0; k < 3; k++)
                            sum += (r[i][k, a] - rBar[k, a]) * (r[i][k, b] - rBar[k, b]);
                        s[a, b] += sum;
                    }
                    double dot = 0;
                    for (int k = 0; k < 3; k++) dot += (r[i][k, a] - rBar[k, a]) * e[k];
                    rhs[a] -= dot;
                }
            }

            // For a small rotation θ about axis u, (R − R̄)ᵀ(R − R̄) ≈ θ²(I − uuᵀ),
            // so sqrt(λ_min / n) is the RMS rotation the least-exercised axis saw.
            double lambdaMin = SmallestEigenvalue(s);
            float weakAxis = (float)(Math.Sqrt(Math.Max(0.0, lambdaMin) / n) * (180.0 / Math.PI));

            if (spread < minSpreadDegrees)
                return PivotResult.Fail(
                    $"the controller only turned {spread:0} degrees -- rock it further, about 30 each way",
                    spread, weakAxis);
            if (weakAxis < minWeakAxisDegrees)
                return PivotResult.Fail(
                    "the controller only twisted about one axis -- tilt it in circles, not just a wrist twist",
                    spread, weakAxis);

            if (!Solve3(s, rhs, out double[] t))
                return PivotResult.Fail("the rotations were degenerate -- try again", spread, weakAxis);

            Vector3 tip = new Vector3((float)t[0], (float)t[1], (float)t[2]);

            // p = R̄·t + c̄
            var p = new double[3];
            for (int a = 0; a < 3; a++)
                p[a] = rBar[a, 0] * t[0] + rBar[a, 1] * t[1] + rBar[a, 2] * t[2] + cBar[a];
            Vector3 pivot = new Vector3((float)p[0], (float)p[1], (float)p[2]);

            double sq = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 predicted = controllerPoses[i].rotation * tip + controllerPoses[i].position;
                sq += (predicted - pivot).sqrMagnitude;
            }
            float rms = (float)Math.Sqrt(sq / n);

            return PivotResult.Ok(tip, pivot, rms, spread, weakAxis);
        }

        /// <summary>Where the tip is for a given controller pose and
        /// controller-local tip offset. What PointerTip.position becomes
        /// once localPosition is set to the solved offset.</summary>
        public static Vector3 TipPosition(Pose controller, Vector3 tipOffset) =>
            controller.position + controller.rotation * tipOffset;

        // ------------------------------------------------------------------

        private static float MaxPairwiseAngle(IReadOnlyList<Pose> poses)
        {
            float max = 0f;
            for (int i = 0; i < poses.Count; i++)
                for (int j = i + 1; j < poses.Count; j++)
                    max = Mathf.Max(max, Quaternion.Angle(poses[i].rotation, poses[j].rotation));
            return max;
        }

        private static double[,] Matrix(Quaternion q)
        {
            Vector3 x = q * Vector3.right, y = q * Vector3.up, z = q * Vector3.forward;
            return new double[,]
            {
                { x.x, y.x, z.x },
                { x.y, y.y, z.y },
                { x.z, y.z, z.z },
            };
        }

        /// <summary>Gaussian elimination with partial pivoting.</summary>
        private static bool Solve3(double[,] m, double[] v, out double[] x)
        {
            var a = new double[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++) a[i, j] = m[i, j];
                a[i, 3] = v[i];
            }

            for (int col = 0; col < 3; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < 3; row++)
                    if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col])) pivot = row;
                if (Math.Abs(a[pivot, col]) < 1e-12) { x = null; return false; }

                if (pivot != col)
                    for (int j = 0; j < 4; j++) (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);

                for (int row = 0; row < 3; row++)
                {
                    if (row == col) continue;
                    double f = a[row, col] / a[col, col];
                    for (int j = col; j < 4; j++) a[row, j] -= f * a[col, j];
                }
            }

            x = new[] { a[0, 3] / a[0, 0], a[1, 3] / a[1, 1], a[2, 3] / a[2, 2] };
            return true;
        }

        /// <summary>Smallest eigenvalue of a symmetric 3×3 matrix, by the
        /// closed-form trigonometric method.</summary>
        private static double SmallestEigenvalue(double[,] a)
        {
            double p1 = a[0, 1] * a[0, 1] + a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2];
            double trace = a[0, 0] + a[1, 1] + a[2, 2];
            if (p1 < 1e-18)
                return Math.Min(a[0, 0], Math.Min(a[1, 1], a[2, 2]));

            double q = trace / 3.0;
            double p2 = (a[0, 0] - q) * (a[0, 0] - q) + (a[1, 1] - q) * (a[1, 1] - q) +
                        (a[2, 2] - q) * (a[2, 2] - q) + 2.0 * p1;
            double p = Math.Sqrt(p2 / 6.0);

            var b = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    b[i, j] = (a[i, j] - (i == j ? q : 0.0)) / p;

            double detB = b[0, 0] * (b[1, 1] * b[2, 2] - b[1, 2] * b[2, 1])
                        - b[0, 1] * (b[1, 0] * b[2, 2] - b[1, 2] * b[2, 0])
                        + b[0, 2] * (b[1, 0] * b[2, 1] - b[1, 1] * b[2, 0]);
            double halfDet = Math.Max(-1.0, Math.Min(1.0, detB / 2.0));
            double phi = Math.Acos(halfDet) / 3.0;

            // Eigenvalues are q + 2p·cos(phi + 2πk/3); k = 1 gives the smallest.
            return q + 2.0 * p * Math.Cos(phi + 2.0 * Math.PI / 3.0);
        }
    }

    public readonly struct PivotResult
    {
        public readonly bool Success;
        /// <summary>Tip position in the controller's local frame, metres.
        /// This is what PointerTip.localPosition should be.</summary>
        public readonly Vector3 TipOffset;
        /// <summary>The fixed point the tip was held on, world space.</summary>
        public readonly Vector3 Pivot;
        /// <summary>RMS distance of the predicted tip from the pivot, metres.
        /// Under ~2 mm is good; large means the tip slipped.</summary>
        public readonly float RmsMetres;
        public readonly float SpreadDegrees;
        public readonly float WeakAxisDegrees;
        public readonly string Error;

        private PivotResult(bool success, Vector3 tip, Vector3 pivot, float rms, float spread, float weak, string error)
        {
            Success = success; TipOffset = tip; Pivot = pivot; RmsMetres = rms;
            SpreadDegrees = spread; WeakAxisDegrees = weak; Error = error;
        }

        public static PivotResult Ok(Vector3 tip, Vector3 pivot, float rms, float spread, float weak) =>
            new PivotResult(true, tip, pivot, rms, spread, weak, null);

        public static PivotResult Fail(string error, float spread, float weak) =>
            new PivotResult(false, default, default, 0f, spread, weak, error);
    }
}
