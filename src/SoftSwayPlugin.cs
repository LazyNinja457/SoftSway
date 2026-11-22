using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using UnityEngine;

namespace SoftSway
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public sealed class SoftSwayPlugin : BaseUnityPlugin
    {
        public const string GUID = "softsway.koikatsu";
        public const string PluginName = "Soft Sway";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> MasterSwitch;
        internal static ConfigEntry<bool> AutoEnable;
        internal static ConfigEntry<bool> MalesOffByDefault;
        internal static ConfigEntry<bool> FollowSelection;
        internal static ConfigEntry<int> UiScalePercent;

        /// <summary>Settings given to characters that have no saved data on their card.</summary>
        internal static CharacterSettings DefaultSettings = new CharacterSettings();

        private SoftSwayWindow _window;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("General", "Open window", new KeyboardShortcut(KeyCode.J, KeyCode.LeftShift),
                "Shortcut that shows/hides the Soft Sway window.");
            MasterSwitch = Config.Bind("General", "Master switch", true,
                "Turn the whole plugin on/off for every character (cards keep their settings).");
            AutoEnable = Config.Bind("General", "Auto-enable on new characters", true,
                "Characters whose card has no Soft Sway data start enabled with your saved default settings.");
            MalesOffByDefault = Config.Bind("General", "Males off by default", true,
                "Male characters without card data start disabled even when auto-enable is on.");
            FollowSelection = Config.Bind("Interface", "Follow Studio/Maker selection", true,
                "The window automatically switches to the character selected in Studio or being edited in Maker.");
            UiScalePercent = Config.Bind("Interface", "Window scale (%)", 100,
                new ConfigDescription("Size of the window.", new AcceptableValueRange<int>(75, 200)));

            MasterSwitch.SettingChanged += (s, e) => SoftSwayler.MarkAllDirty();

            CharacterSettings saved = Presets.ReadFile(Presets.DefaultFile);
            if (saved != null) DefaultSettings = saved;

            Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
            CharacterApi.RegisterExtraBehaviour<SoftSwayler>(GUID);

            _window = new SoftSwayWindow();
        }

        private void Update()
        {
            if (ToggleKey.Value.IsDown())
                _window.Visible = !_window.Visible;
        }

        private void OnGUI()
        {
            if (_window.Visible)
                _window.Draw();
        }

        internal static void SaveDefault(CharacterSettings settings)
        {
            DefaultSettings = settings.Clone();
            Presets.WriteFile(Presets.DefaultFile, DefaultSettings);
        }
    }
}
