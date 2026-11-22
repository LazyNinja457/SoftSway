using System.Collections.Generic;
using KKAPI.Maker;
using KKAPI.Studio;
using KKAPI.Utilities;
using UnityEngine;

namespace SoftSway
{
    internal sealed class SoftSwayWindow
    {
        private const int WindowId = 0x4A1660;
        private const float Width = 410f;

        public bool Visible;

        private Rect _rect = new Rect(40f, 120f, Width, 10f);
        private Region _tab = Region.Breast;
        private SoftSwayler _selected;
        private SoftSwayler _lastFollowed;
        private bool _showAdvanced;
        private int _advancedBone;
        private string _presetName = "";
        private Vector2 _presetScroll;
        private List<string> _userPresets;
        private string _status = "";
        private float _statusUntil;

        private GUIStyle _bold, _header, _small, _selectedButton;

        private static readonly string[] Tabs = { "Breasts", "Butt" };

        public void Draw()
        {
            InitStyles();
            float scale = Mathf.Clamp(SoftSwayPlugin.UiScalePercent.Value, 75, 200) / 100f;

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            _rect.width = Width;
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width / scale - 60f));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height / scale - 40f));

            IMGUIUtils.DrawSolidBox(_rect);
            _rect = GUILayout.Window(WindowId, _rect, DrawContents, SoftSwayPlugin.PluginName + "  v" + SoftSwayPlugin.Version);

            GUI.matrix = oldMatrix;
            // Stop clicks/scrolls on the window from moving the game camera.
            IMGUIUtils.EatInputInRect(new Rect(_rect.x * scale, _rect.y * scale, _rect.width * scale, _rect.height * scale));
        }

        private void InitStyles()
        {
            if (_bold != null) return;
            _bold = new GUIStyle(GUI.skin.toggle) { richText = true };
            _header = new GUIStyle(GUI.skin.label) { richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _selectedButton = new GUIStyle(GUI.skin.button);
            _selectedButton.normal.textColor = new Color(1f, 0.85f, 0.4f);
        }

        // ------------------------------------------------------------------ layout

        private void DrawContents(int id)
        {
            SyncSelection();

            if (!SoftSwayPlugin.MasterSwitch.Value)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(B("Plugin is switched off for everyone."), _header);
                if (GUILayout.Button("Turn on", GUILayout.Width(80f))) SoftSwayPlugin.MasterSwitch.Value = true;
                GUILayout.EndHorizontal();
            }

            DrawCharacterRow();

            if (_selected == null)
            {
                GUILayout.Label("No characters loaded yet.", _small);
                DrawTooltipAndDrag();
                return;
            }

            CharacterSettings s = _selected.Settings;
            bool enabled = GUILayout.Toggle(s.Enabled, B(" Enable Soft Sway for this character"), _bold);
            if (enabled != s.Enabled)
            {
                s.Enabled = enabled;
                _selected.MarkChanged();
            }

            if (!_selected.HasChains)
                GUILayout.Label("This character has no breast/butt physics bones right now (low-poly, or still loading).", _small);

            GUI.enabled = s.Enabled;
            _tab = (Region)GUILayout.Toolbar((int)_tab, Tabs);
            DrawRegion(s, _tab);
            GUI.enabled = true;

            GUILayout.Space(6f);
            DrawPresetsAndDefaults();
            DrawTooltipAndDrag();
        }

        private void DrawTooltipAndDrag()
        {
            string line = Time.realtimeSinceStartup < _statusUntil ? _status : GUI.tooltip;
            GUILayout.Label(string.IsNullOrEmpty(line) ? " " : line, _small);
            GUI.DragWindow();
        }

        private void DrawCharacterRow()
        {
            List<SoftSwayler> all = SoftSwayler.All;
            GUILayout.BeginHorizontal();
            GUI.enabled = all.Count > 1;
            if (GUILayout.Button("<", GUILayout.Width(26f))) Step(-1);
            GUI.enabled = true;
            string name = _selected == null ? "-" : _selected.DisplayName;
            int index = _selected == null ? 0 : all.IndexOf(_selected) + 1;
            GUILayout.Label(B(name.Replace("<", "(").Replace(">", ")")) + "   (" + index + "/" + all.Count + ")", _header, GUILayout.ExpandWidth(true));
            GUI.enabled = all.Count > 1;
            if (GUILayout.Button(">", GUILayout.Width(26f))) Step(1);
            GUI.enabled = true;
            if (GUILayout.Button(new GUIContent("X", "Close (" + SoftSwayPlugin.ToggleKey.Value + ")"), GUILayout.Width(26f)))
                Visible = false;
            GUILayout.EndHorizontal();
        }

        private void DrawRegion(CharacterSettings cs, Region region)
        {
            RegionSettings r = cs.Get(region);
            string part = region == Region.Breast ? "breast" : "butt";

            bool on = GUILayout.Toggle(r.Enabled, " Override " + part + " physics  (off = game default)");
            if (on != r.Enabled) { r.Enabled = on; _selected.MarkChanged(); }

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && r.Enabled;

            // One-click feel presets
            GUILayout.BeginHorizontal();
            foreach (Presets.Feel f in Presets.BuiltIn)
            {
                bool active = !r.Manual && Near(r.Bounce, f.Bounce) && Near(r.Softness, f.Softness) && Near(r.Response, f.Response);
                if (GUILayout.Button(new GUIContent(f.Name, f.Tip), active ? _selectedButton : GUI.skin.button))
                {
                    r.SetFeel(f.Bounce, f.Softness, f.Response);
                    _selected.MarkChanged();
                }
            }
            GUILayout.EndHorizontal();

            if (r.Manual)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Manual per-bone values are active; the sliders below are ignored.", _small);
                if (GUILayout.Button("Use sliders", GUILayout.Width(90f))) { r.Manual = false; _selected.MarkChanged(); }
                GUILayout.EndHorizontal();
            }

            bool changed = false;
            changed |= Slider("Bounce", "How big the wobble is. 0 = almost still, 1 = natural, 2 = extreme.", ref r.Bounce, 0f, 2f, 1f);
            changed |= Slider("Softness", "How loosely it springs back. Higher = heavier, slower, more fluid.", ref r.Softness, 0f, 2f, 1f);
            changed |= Slider("Motion response", "How strongly body movement drives the jiggle. Higher = more lag and swing.", ref r.Response, 0f, 2f, 1f);
            changed |= Slider("Droop", "Extra gravity. Positive sags, negative lifts. 0 = game default.", ref r.Droop, -1f, 1f, 0f);

            if (region == Region.Breast)
            {
                GUILayout.Label(B("Clothing & body"), _header);
                changed |= Slider("With bra", "Bounce multiplier while wearing a bra.", ref r.BraBounce, 0f, 1.5f, 0.8f);
                changed |= Slider("With top", "Bounce multiplier while wearing a top without a bra.", ref r.TopBounce, 0f, 1.5f, 0.9f);
                bool size = GUILayout.Toggle(r.SizeAware, new GUIContent(" Adapt to breast size",
                    "Larger breasts get heavier, laggier movement; smaller ones stay tighter."));
                if (size != r.SizeAware) { r.SizeAware = size; changed = true; }
                GUILayout.Label("Now: " + WearLabel(_selected.CurrentWear) + "   ·   size " +
                                Mathf.RoundToInt(_selected.CurrentSize * 100f) + "%", _small);
            }
            if (changed) _selected.MarkChanged();

            DrawAdvanced(r, region);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Reset " + part, "Back to Natural with default options.")))
            {
                bool keepEnabled = r.Enabled;
                r.CopyFrom(new RegionSettings(region));
                r.Enabled = keepEnabled;
                _selected.MarkChanged();
                Flash("Reset " + part + " settings.");
            }
            if (GUILayout.Button(new GUIContent("Copy " + part + " to everyone", "Copy this tab's settings to every other loaded character.")))
            {
                int n = 0;
                foreach (SoftSwayler c in SoftSwayler.All)
                {
                    if (c == null || c == _selected) continue;
                    c.Settings.Get(region).CopyFrom(r);
                    c.MarkChanged();
                    n++;
                }
                Flash("Copied " + part + " settings to " + n + " character(s).");
            }
            GUILayout.EndHorizontal();

            GUI.enabled = wasEnabled;
        }

        private void DrawAdvanced(RegionSettings r, Region region)
        {
            if (GUILayout.Button((_showAdvanced ? "[-]" : "[+]") + " Advanced (per-bone values)", GUI.skin.label))
                _showAdvanced = !_showAdvanced;
            if (!_showAdvanced) return;

            string[] labels = PhysicsMapper.BoneLabels(region);
            BoneValues[] live = _selected.ComputeBones(region);
            BoneValues[] fromSliders = _selected.ComputeBones(region, true);

            bool manual = GUILayout.Toggle(r.Manual, new GUIContent(" Edit raw values manually",
                "Take full control of Damping/Elasticity/Stiffness/Inert. Starts from the current slider result."));
            if (manual != r.Manual)
            {
                if (manual)
                    for (int i = 0; i < r.Bones.Length && i < fromSliders.Length; i++) r.Bones[i].CopyFrom(fromSliders[i]);
                r.Manual = manual;
                _selected.MarkChanged();
            }

            if (!r.Manual)
            {
                // Read-only view of what is actually being sent to the game.
                GUILayout.BeginHorizontal();
                GUILayout.Label("Bone", _small, GUILayout.Width(120f));
                foreach (string h in new[] { "Damp", "Elast", "Stiff", "Inert" })
                    GUILayout.Label(h, _small, GUILayout.Width(52f));
                GUILayout.EndHorizontal();
                for (int i = 0; i < live.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(labels[i], _small, GUILayout.Width(120f));
                    GUILayout.Label(live[i].Damping.ToString("0.000"), _small, GUILayout.Width(52f));
                    GUILayout.Label(live[i].Elasticity.ToString("0.000"), _small, GUILayout.Width(52f));
                    GUILayout.Label(live[i].Stiffness.ToString("0.000"), _small, GUILayout.Width(52f));
                    GUILayout.Label(live[i].Inert.ToString("0.000"), _small, GUILayout.Width(52f));
                    GUILayout.EndHorizontal();
                }
                return;
            }

            if (_advancedBone >= labels.Length) _advancedBone = 0;
            _advancedBone = GUILayout.Toolbar(_advancedBone, labels);
            BoneValues b = r.Bones[_advancedBone];
            bool changed = false;
            bool rot = GUILayout.Toggle(b.Rotation, new GUIContent(" Rotation calc", "Let this bone rotate as well as move."));
            if (rot != b.Rotation) { b.Rotation = rot; changed = true; }
            changed |= Slider("Damping", "Slows movement down. Low = wobbles longer.", ref b.Damping, 0f, 1f, fromSliders[_advancedBone].Damping);
            changed |= Slider("Elasticity", "Pull back towards the rest pose.", ref b.Elasticity, 0f, 1f, fromSliders[_advancedBone].Elasticity);
            changed |= Slider("Stiffness", "Limits how far it can move from the rest pose.", ref b.Stiffness, 0f, 1f, fromSliders[_advancedBone].Stiffness);
            changed |= Slider("Inert", "How much it simply follows the body. Low = more lag.", ref b.Inert, 0f, 1f, fromSliders[_advancedBone].Inert);
            if (changed) _selected.MarkChanged();
        }

        private void DrawPresetsAndDefaults()
        {
            GUILayout.Label(B("My presets"), _header);
            if (_userPresets == null) _userPresets = Presets.ListUserPresets();

            GUILayout.BeginHorizontal();
            _presetName = GUILayout.TextField(_presetName, 40, GUILayout.ExpandWidth(true));
            GUI.enabled = Presets.SanitizeName(_presetName).Length > 0;
            if (GUILayout.Button(new GUIContent("Save", "Save both tabs as a preset file."), GUILayout.Width(60f)))
            {
                if (Presets.SaveUser(_presetName, _selected.Settings))
                {
                    Flash("Saved preset '" + Presets.SanitizeName(_presetName) + "'.");
                    _userPresets = null;
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_userPresets != null && _userPresets.Count > 0)
            {
                _presetScroll = GUILayout.BeginScrollView(_presetScroll, GUILayout.Height(Mathf.Min(24f * _userPresets.Count + 6f, 96f)));
                string toDelete = null;
                foreach (string p in _userPresets)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent(p, "Load this preset onto the selected character.")))
                    {
                        CharacterSettings loaded = Presets.LoadUser(p);
                        if (loaded != null)
                        {
                            _selected.Settings.CopyFrom(loaded);
                            _selected.Settings.Enabled = true;
                            _selected.MarkChanged();
                            _presetName = p;
                            Flash("Loaded preset '" + p + "'.");
                        }
                    }
                    if (GUILayout.Button(new GUIContent("Del", "Delete this preset file."), GUILayout.Width(40f))) toDelete = p;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                if (toDelete != null)
                {
                    Presets.DeleteUser(toDelete);
                    _userPresets = null;
                    Flash("Deleted preset '" + toDelete + "'.");
                }
            }
            else
            {
                GUILayout.Label("No saved presets yet. Type a name and press Save.", _small);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Set as default", "New characters (cards without Soft Sway data) will start with these settings.")))
            {
                SoftSwayPlugin.SaveDefault(_selected.Settings);
                Flash("Saved as the default for new characters.");
            }
            if (GUILayout.Button(new GUIContent("Apply to everyone", "Copy both tabs to every loaded character and enable them.")))
            {
                int n = 0;
                foreach (SoftSwayler c in SoftSwayler.All)
                {
                    if (c == null || c == _selected) continue;
                    if (c.IsMale && SoftSwayPlugin.MalesOffByDefault.Value) continue;
                    c.Settings.CopyFrom(_selected.Settings);
                    c.Settings.Enabled = true;
                    c.MarkChanged();
                    n++;
                }
                Flash("Applied to " + n + " character(s)" + (SoftSwayPlugin.MalesOffByDefault.Value ? " (males skipped)." : "."));
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Settings are saved into the character card / scene when you save it.", _small);
        }

        // ------------------------------------------------------------------ helpers

        private bool Slider(string label, string tip, ref float value, float min, float max, float def)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tip), GUILayout.Width(118f));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            v = Mathf.Round(v * 100f) / 100f;
            GUILayout.Label(v.ToString("0.00"), GUILayout.Width(38f));
            if (GUILayout.Button(new GUIContent("R", "Reset to " + def.ToString("0.00")), GUILayout.Width(24f))) v = def;
            GUILayout.EndHorizontal();
            if (Mathf.Abs(v - value) < 0.0001f) return false;
            value = v;
            return true;
        }

        private void SyncSelection()
        {
            List<SoftSwayler> all = SoftSwayler.All;
            all.RemoveAll(c => c == null);

            if (SoftSwayPlugin.FollowSelection.Value)
            {
                SoftSwayler followed = GetFollowTarget();
                if (followed != null && followed != _lastFollowed) _selected = followed;
                _lastFollowed = followed;
            }
            if (_selected == null || !all.Contains(_selected))
                _selected = all.Count > 0 ? all[0] : null;
        }

        private static SoftSwayler GetFollowTarget()
        {
            try
            {
                if (StudioAPI.InsideStudio)
                {
                    foreach (SoftSwayler c in StudioAPI.GetSelectedControllers<SoftSwayler>())
                        return c;
                }
                else if (MakerAPI.InsideMaker)
                {
                    ChaControl cha = MakerAPI.GetCharacterControl();
                    if (cha != null) return cha.GetComponent<SoftSwayler>();
                }
            }
            catch { }
            return null;
        }

        private void Step(int dir)
        {
            List<SoftSwayler> all = SoftSwayler.All;
            if (all.Count == 0) return;
            int i = _selected == null ? 0 : all.IndexOf(_selected);
            _selected = all[((i + dir) % all.Count + all.Count) % all.Count];
        }

        private void Flash(string message)
        {
            _status = message;
            _statusUntil = Time.realtimeSinceStartup + 3f;
            SoftSwayPlugin.Log.LogInfo(message);
        }

        private static string B(string text)
        {
            return "<b>" + text + "</b>";
        }

        private static bool Near(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.011f;
        }

        private static string WearLabel(WearState w)
        {
            switch (w)
            {
                case WearState.Bra: return "bra";
                case WearState.Top: return "top, no bra";
                case WearState.BraAndTop: return "bra + top";
                default: return "naked";
            }
        }
    }
}
