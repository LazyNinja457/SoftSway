using System.Collections.Generic;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;

namespace SoftSway
{
    public enum WearState { Naked = 0, Bra = 1, Top = 2, BraAndTop = 3 }

    /// <summary>One per character. Owns that character's settings and keeps the native chains in sync.</summary>
    public sealed class SoftSwayler : CharaCustomFunctionController
    {
        private const string DataKey = "settings";

        internal static readonly List<SoftSwayler> All = new List<SoftSwayler>();

        public CharacterSettings Settings = new CharacterSettings();

        private NativeChainApplier _applier;
        private bool _dirty = true;
        private bool _hadCardData;
        private bool _touched;

        // Last state the chains were built for; any change triggers a re-apply.
        private WearState _lastWear = (WearState)(-1);
        private int _lastSizeBucket = -1;

        public WearState CurrentWear { get; private set; }
        public float CurrentSize { get; private set; }
        public bool IsMale { get { return ChaControl != null && ChaControl.sex == 0; } }
        public bool HasChains { get { return NativeChainApplier.HasChains(ChaControl); } }

        /// <summary>True while this plugin owns the breast chain (the game's ReCalc is blocked).</summary>
        public bool OwnsBreasts
        {
            get { return Settings.Enabled && Settings.Breast.Enabled && _applier != null && _applier.BreastApplied; }
        }

        public string DisplayName
        {
            get
            {
                string n = null;
                try { n = ChaControl.fileParam.fullname; } catch { }
                if (string.IsNullOrEmpty(n)) n = ChaControl != null ? ChaControl.name : "?";
                return IsMale ? n + " (M)" : n;
            }
        }

        protected override void OnDestroy()
        {
            All.Remove(this);
            base.OnDestroy();
        }

        /// <summary>Call after editing Settings from code or UI.</summary>
        public void MarkChanged()
        {
            _touched = true;
            _dirty = true;
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            if (!All.Contains(this)) All.Add(this);

            // Put the previous card's chains back before taking over the new ones.
            if (_applier != null) _applier.RestoreAll();
            _applier = new NativeChainApplier(ChaControl);

            PluginData data = GetExtendedData();
            _hadCardData = data != null && data.data != null && data.data.ContainsKey(DataKey);
            _touched = false;

            if (_hadCardData)
            {
                Settings = CharacterSettings.FromDictionary(data.data[DataKey] as Dictionary<string, object>
                                                            ?? ToStringDict(data.data[DataKey]));
            }
            else
            {
                Settings = SoftSwayPlugin.DefaultSettings.Clone();
                bool auto = SoftSwayPlugin.AutoEnable.Value;
                if (IsMale && SoftSwayPlugin.MalesOffByDefault.Value) auto = false;
                Settings.Enabled = auto;
            }

            _lastWear = (WearState)(-1);
            _lastSizeBucket = -1;
            _dirty = true;
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            // Keep cards clean: only write data when the plugin is actually in use for this character.
            if (!Settings.Enabled && !_hadCardData && !_touched)
            {
                SetExtendedData(null);
                return;
            }
            var data = new PluginData { version = CharacterSettings.DataVersion };
            data.data[DataKey] = Settings.ToDictionary();
            SetExtendedData(data);
        }

        private void LateUpdate()
        {
            if (ChaControl == null || !ChaControl.loadEnd || _applier == null) return;

            CurrentWear = DetectWear();
            CurrentSize = DetectBreastSize();
            int sizeBucket = Mathf.RoundToInt(CurrentSize * 50f); // ignore sub-2% jitter while dragging sliders

            if (!_dirty && CurrentWear == _lastWear && sizeBucket == _lastSizeBucket) return;

            _lastWear = CurrentWear;
            _lastSizeBucket = sizeBucket;
            _dirty = false;
            Apply();
        }

        private void Apply()
        {
            if (!HasChains) return; // low-poly or not loaded yet; LateUpdate retries when state changes

            bool on = Settings.Enabled && SoftSwayPlugin.MasterSwitch.Value;

            if (on && Settings.Breast.Enabled)
                _applier.ApplyBreast(ComputeBones(Region.Breast), PhysicsMapper.Gravity(Settings.Breast));
            else
                _applier.RestoreBreast();

            if (on && Settings.Butt.Enabled)
                _applier.ApplyButt(ComputeBones(Region.Butt), PhysicsMapper.Gravity(Settings.Butt));
            else
                _applier.RestoreButt();
        }

        /// <summary>The exact values that are (or would be) sent to the game for a region right now.</summary>
        public BoneValues[] ComputeBones(Region region, bool ignoreManual = false)
        {
            RegionSettings s = Settings.Get(region);
            if (ignoreManual && s.Manual)
            {
                s = s.Clone(region);
                s.Manual = false;
            }
            float mul = 1f;
            if (region == Region.Breast)
            {
                if (CurrentWear == WearState.Bra) mul = s.BraBounce;
                else if (CurrentWear == WearState.Top) mul = s.TopBounce;
                else if (CurrentWear == WearState.BraAndTop) mul = Mathf.Min(s.BraBounce, s.TopBounce);
            }
            int count = PhysicsMapper.BoneNames(region).Length;
            var result = new BoneValues[count];
            for (int i = 0; i < count; i++)
                result[i] = PhysicsMapper.Compute(region, i, s, mul, CurrentSize);
            return result;
        }

        private WearState DetectWear()
        {
            // Unlike checking clothesState alone, also require the item to exist,
            // so "no top equipped" counts as not wearing one.
            bool bra = IsWorn((int)ChaFileDefine.ClothesKind.bra);
            bool top = IsWorn((int)ChaFileDefine.ClothesKind.top);
            if (bra && top) return WearState.BraAndTop;
            if (bra) return WearState.Bra;
            if (top) return WearState.Top;
            return WearState.Naked;
        }

        private bool IsWorn(int kind)
        {
            try
            {
                byte[] state = ChaControl.fileStatus.clothesState;
                GameObject[] objs = ChaControl.objClothes;
                if (state == null || kind >= state.Length || state[kind] != 0) return false;
                return objs == null || kind >= objs.Length || objs[kind] != null;
            }
            catch { return false; }
        }

        private float DetectBreastSize()
        {
            try
            {
                float[] shape = ChaControl.fileBody.shapeValueBody;
                const int bustSizeIndex = 1; // ChaFileDefine body shape index for "Breast size"
                if (shape != null && shape.Length > bustSizeIndex)
                    return Mathf.Clamp01(shape[bustSizeIndex]);
            }
            catch { }
            return 0.5f;
        }

        private static Dictionary<string, object> ToStringDict(object o)
        {
            // MessagePack can hand back Dictionary<object, object> for nested maps.
            var result = new Dictionary<string, object>();
            var generic = o as System.Collections.IDictionary;
            if (generic == null) return result;
            foreach (System.Collections.DictionaryEntry e in generic)
                if (e.Key != null) result[e.Key.ToString()] = e.Value;
            return result;
        }

        internal static SoftSwayler FindByBustSoft(BustSoft soft)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].ChaControl != null && ReferenceEquals(All[i].ChaControl.bustSoft, soft))
                    return All[i];
            return null;
        }

        internal static SoftSwayler FindByBustGravity(BustGravity gravity)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].ChaControl != null && ReferenceEquals(All[i].ChaControl.bustGravity, gravity))
                    return All[i];
            return null;
        }

        /// <summary>Re-apply on every character, e.g. after the master switch changes.</summary>
        internal static void MarkAllDirty()
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null) All[i]._dirty = true;
        }
    }
}
