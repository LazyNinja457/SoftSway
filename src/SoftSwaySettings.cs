using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SoftSway
{
    public enum Region { Breast = 0, Butt = 1 }

    /// <summary>Raw DynamicBone values for one bone of a chain.</summary>
    public sealed class BoneValues
    {
        public bool Rotation;
        public float Damping;
        public float Elasticity;
        public float Stiffness;
        public float Inert;

        public BoneValues() { }

        public BoneValues(bool rotation, float damping, float elasticity, float stiffness, float inert)
        {
            Rotation = rotation;
            Damping = damping;
            Elasticity = elasticity;
            Stiffness = stiffness;
            Inert = inert;
        }

        public BoneValues Clone()
        {
            return new BoneValues(Rotation, Damping, Elasticity, Stiffness, Inert);
        }

        public void CopyFrom(BoneValues other)
        {
            Rotation = other.Rotation;
            Damping = other.Damping;
            Elasticity = other.Elasticity;
            Stiffness = other.Stiffness;
            Inert = other.Inert;
        }
    }

    /// <summary>
    /// Settings for one body region. The three "feel" sliders are centred on 1.0,
    /// which reproduces a well-tested soft baseline; 0 is firm/still, 2 is extreme.
    /// </summary>
    public sealed class RegionSettings
    {
        public const float SliderMax = 2f;
        public const float GravityScale = 0.003f; // UI droop of 1.0 == this much native gravity

        public bool Enabled = true;
        public float Bounce = 1f;      // amplitude of the wobble
        public float Softness = 1f;    // how loosely it springs back
        public float Response = 1f;    // how strongly body motion drives it
        public float Droop;            // -1 (lift) .. +1 (sag)

        // Breast only
        public float BraBounce = 0.8f; // bounce multiplier while a bra is worn
        public float TopBounce = 0.9f; // bounce multiplier while a top is worn (and no bra)
        public bool SizeAware = true;  // heavier breasts lag and swing more

        public bool Manual;            // use the per-bone values below verbatim
        public BoneValues[] Bones;

        public RegionSettings(Region region)
        {
            Bones = PhysicsMapper.CreateBaseline(region);
        }

        public RegionSettings Clone(Region region)
        {
            var r = new RegionSettings(region);
            r.CopyFrom(this);
            return r;
        }

        public void CopyFrom(RegionSettings o)
        {
            Enabled = o.Enabled;
            Bounce = o.Bounce;
            Softness = o.Softness;
            Response = o.Response;
            Droop = o.Droop;
            BraBounce = o.BraBounce;
            TopBounce = o.TopBounce;
            SizeAware = o.SizeAware;
            Manual = o.Manual;
            for (int i = 0; i < Bones.Length && i < o.Bones.Length; i++)
                Bones[i].CopyFrom(o.Bones[i]);
        }

        public void SetFeel(float bounce, float softness, float response)
        {
            Bounce = bounce;
            Softness = softness;
            Response = response;
            Manual = false;
        }

        internal void Write(Dictionary<string, object> d, string p)
        {
            d[p + "enabled"] = Enabled;
            d[p + "bounce"] = Bounce;
            d[p + "softness"] = Softness;
            d[p + "response"] = Response;
            d[p + "droop"] = Droop;
            d[p + "braBounce"] = BraBounce;
            d[p + "topBounce"] = TopBounce;
            d[p + "sizeAware"] = SizeAware;
            d[p + "manual"] = Manual;
            for (int i = 0; i < Bones.Length; i++)
            {
                string b = p + "bone" + i + ".";
                d[b + "rotation"] = Bones[i].Rotation;
                d[b + "damping"] = Bones[i].Damping;
                d[b + "elasticity"] = Bones[i].Elasticity;
                d[b + "stiffness"] = Bones[i].Stiffness;
                d[b + "inert"] = Bones[i].Inert;
            }
        }

        internal void Read(Dictionary<string, object> d, string p)
        {
            Enabled = Val.Bool(d, p + "enabled", Enabled);
            Bounce = Val.Float(d, p + "bounce", 0f, SliderMax, Bounce);
            Softness = Val.Float(d, p + "softness", 0f, SliderMax, Softness);
            Response = Val.Float(d, p + "response", 0f, SliderMax, Response);
            Droop = Val.Float(d, p + "droop", -1f, 1f, Droop);
            BraBounce = Val.Float(d, p + "braBounce", 0f, 1.5f, BraBounce);
            TopBounce = Val.Float(d, p + "topBounce", 0f, 1.5f, TopBounce);
            SizeAware = Val.Bool(d, p + "sizeAware", SizeAware);
            Manual = Val.Bool(d, p + "manual", Manual);
            for (int i = 0; i < Bones.Length; i++)
            {
                string b = p + "bone" + i + ".";
                BoneValues v = Bones[i];
                v.Rotation = Val.Bool(d, b + "rotation", v.Rotation);
                v.Damping = Val.Float(d, b + "damping", 0f, 1f, v.Damping);
                v.Elasticity = Val.Float(d, b + "elasticity", 0f, 1f, v.Elasticity);
                v.Stiffness = Val.Float(d, b + "stiffness", 0f, 1f, v.Stiffness);
                v.Inert = Val.Float(d, b + "inert", 0f, 1f, v.Inert);
            }
        }
    }

    /// <summary>Everything stored per character (on the card) and in preset files.</summary>
    public sealed class CharacterSettings
    {
        public const int DataVersion = 1;

        public bool Enabled;
        public RegionSettings Breast = new RegionSettings(Region.Breast);
        public RegionSettings Butt = new RegionSettings(Region.Butt);

        public RegionSettings Get(Region region)
        {
            return region == Region.Breast ? Breast : Butt;
        }

        public CharacterSettings Clone()
        {
            var c = new CharacterSettings();
            c.CopyFrom(this);
            return c;
        }

        public void CopyFrom(CharacterSettings o)
        {
            Enabled = o.Enabled;
            Breast.CopyFrom(o.Breast);
            Butt.CopyFrom(o.Butt);
        }

        public Dictionary<string, object> ToDictionary()
        {
            var d = new Dictionary<string, object>();
            d["enabled"] = Enabled;
            Breast.Write(d, "breast.");
            Butt.Write(d, "butt.");
            return d;
        }

        public static CharacterSettings FromDictionary(Dictionary<string, object> d)
        {
            var c = new CharacterSettings();
            if (d == null) return c;
            c.Enabled = Val.Bool(d, "enabled", c.Enabled);
            c.Breast.Read(d, "breast.");
            c.Butt.Read(d, "butt.");
            return c;
        }

        /// <summary>Plain "key=value" text, used for presets and the global default.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("# SoftSway preset v").Append(DataVersion).Append('\n');
            foreach (KeyValuePair<string, object> kv in ToDictionary())
            {
                string value = kv.Value is float
                    ? ((float)kv.Value).ToString("R", CultureInfo.InvariantCulture)
                    : Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
                sb.Append(kv.Key).Append('=').Append(value).Append('\n');
            }
            return sb.ToString();
        }

        public static CharacterSettings FromText(string text)
        {
            var d = new Dictionary<string, object>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return FromDictionary(d);
        }
    }

    /// <summary>Tolerant value readers (card data may hold float, double, int or string).</summary>
    internal static class Val
    {
        public static bool Bool(Dictionary<string, object> d, string key, bool fallback)
        {
            object o;
            if (!d.TryGetValue(key, out o) || o == null) return fallback;
            try { return Convert.ToBoolean(o, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        public static float Float(Dictionary<string, object> d, string key, float min, float max, float fallback)
        {
            object o;
            if (!d.TryGetValue(key, out o) || o == null) return fallback;
            float v;
            try { v = Convert.ToSingle(o, CultureInfo.InvariantCulture); }
            catch { return fallback; }
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return v < min ? min : v > max ? max : v;
        }
    }
}
