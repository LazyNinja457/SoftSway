using UnityEngine;

namespace SoftSway
{
    /// <summary>
    /// Turns the three simple "feel" sliders into raw DynamicBone_Ver02 values.
    ///
    /// Baselines are the community "Soft" breast/hip values used by BreastPhysicsController
    /// presets and SomaDynamics' native chain defaults. Every slider is a multiplier curve that
    /// equals 1.0 at the slider's centre (1.0), so the centre always reproduces the baseline.
    /// </summary>
    public static class PhysicsMapper
    {
        // Left-side names; right-side bones are matched by replacing "_R" with "_L".
        public static readonly string[] BreastBones = { "cf_j_bust01_L", "cf_j_bust02_L", "cf_j_bust03_L" };
        public static readonly string[] ButtBones = { "cf_d_siri01_L", "cf_j_siri_L_01" };

        public static readonly string[] BreastBoneLabels = { "Base (bust01)", "Middle (bust02)", "Tip (bust03)" };
        public static readonly string[] ButtBoneLabels = { "Base (siri01)", "Tip (siri_01)" };

        public static string[] BoneNames(Region region)
        {
            return region == Region.Breast ? BreastBones : ButtBones;
        }

        public static string[] BoneLabels(Region region)
        {
            return region == Region.Breast ? BreastBoneLabels : ButtBoneLabels;
        }

        public static BoneValues[] CreateBaseline(Region region)
        {
            if (region == Region.Breast)
            {
                return new[]
                {
                    new BoneValues(false, 0.08f, 0.15f, 0.25f, 0.60f),
                    new BoneValues(true, 0.05f, 0.08f, 0.07f, 0.50f),
                    new BoneValues(true, 0.05f, 0.08f, 0.07f, 0.50f),
                };
            }
            return new[]
            {
                new BoneValues(false, 0.03f, 0.12f, 0.09f, 0.25f),
                new BoneValues(true, 0.03f, 0.08f, 0.05f, 0.25f),
            };
        }

        private static readonly BoneValues[] BreastBaseline = CreateBaseline(Region.Breast);
        private static readonly BoneValues[] ButtBaseline = CreateBaseline(Region.Butt);

        /// <summary>Index of the bone in the region's list, or -1. Accepts left or right names.</summary>
        public static int BoneIndex(Region region, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string key = name.Replace("_R", "_L");
            string[] names = BoneNames(region);
            for (int i = 0; i < names.Length; i++)
                if (names[i] == key) return i;
            return -1;
        }

        /// <summary>
        /// Piecewise-linear curve on [0, 2]: value at 0 = <paramref name="atMin"/>,
        /// 1 at the centre, <paramref name="atMax"/> at 2.
        /// </summary>
        private static float Curve(float v, float atMin, float atMax)
        {
            v = Mathf.Clamp(v, 0f, RegionSettings.SliderMax);
            return v <= 1f ? Mathf.Lerp(atMin, 1f, v) : Mathf.Lerp(1f, atMax, v - 1f);
        }

        /// <summary>
        /// Compute final values for one bone.
        /// </summary>
        /// <param name="bounceMultiplier">Clothing multiplier (1 when naked).</param>
        /// <param name="size01">Breast size 0..1 (0.5 = average). Ignored unless SizeAware.</param>
        public static BoneValues Compute(Region region, int boneIndex, RegionSettings s,
            float bounceMultiplier, float size01)
        {
            if (s.Manual && boneIndex < s.Bones.Length)
                return Sanitize(s.Bones[boneIndex].Clone());

            BoneValues b = (region == Region.Breast ? BreastBaseline : ButtBaseline)[boneIndex];
            float bounce = Mathf.Clamp(s.Bounce * bounceMultiplier, 0f, RegionSettings.SliderMax);

            // Bounce: less damping and stiffness -> bigger, longer wobble.
            float damping = b.Damping * Curve(bounce, 2.6f, 0.35f);
            float elasticity = b.Elasticity * Curve(bounce, 1.8f, 0.60f);
            float stiffness = b.Stiffness * Curve(bounce, 3.0f, 0.35f);

            // Softness: weaker return forces -> slower, heavier, more fluid movement.
            damping *= Curve(s.Softness, 1.3f, 0.80f);
            elasticity *= Curve(s.Softness, 1.5f, 0.55f);
            stiffness *= Curve(s.Softness, 1.8f, 0.45f);

            // Motion response: Inert is "how much it simply follows the body". Lower = more lag/swing.
            float inert = b.Inert * Curve(s.Response, 1.6f, 0.25f);

            if (region == Region.Breast && s.SizeAware)
            {
                float size = Mathf.Clamp01(size01);
                // Small breasts stay a bit tighter, large ones a bit heavier.
                stiffness *= Mathf.Lerp(1.30f, 0.80f, size);
                damping *= Mathf.Lerp(1.15f, 0.90f, size);
                inert *= Mathf.Lerp(1.15f, 0.85f, size);
            }

            return Sanitize(new BoneValues(b.Rotation, damping, elasticity, stiffness, inert));
        }

        /// <summary>Keep values in a range where DynamicBone stays stable (no endless vibration).</summary>
        private static BoneValues Sanitize(BoneValues v)
        {
            v.Damping = Mathf.Clamp(v.Damping, 0.01f, 1f);
            v.Elasticity = Mathf.Clamp(v.Elasticity, 0.005f, 1f);
            v.Stiffness = Mathf.Clamp01(v.Stiffness);
            v.Inert = Mathf.Clamp01(v.Inert);
            return v;
        }

        public static Vector3 Gravity(RegionSettings s)
        {
            // Positive droop pulls down (negative Y).
            return new Vector3(0f, -Mathf.Clamp(s.Droop, -1f, 1f) * RegionSettings.GravityScale, 0f);
        }
    }
}
