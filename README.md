# Soft Sway

**Breast and butt jiggle physics for Koikatsu, with a simple one-window UI.**

Works with **Koikatsu / Koikatsu Party** and **Koikatsu Sunshine**. Open the window with **Shift + J** in the character maker, Studio or H scenes, pick a preset or move a few sliders, and save the card.

---

## Download

**[⬇ Latest release](https://github.com/LazyNinja457/SoftSway/releases/latest)**

| Game | Direct download (v1.0.0) |
|---|---|
| Koikatsu / Koikatsu Party | [**KK_SoftSway_v1.0.0.zip**](https://github.com/LazyNinja457/SoftSway/releases/download/v1.0.0/KK_SoftSway_v1.0.0.zip) |
| Koikatsu Sunshine | [**KKS_SoftSway_v1.0.0.zip**](https://github.com/LazyNinja457/SoftSway/releases/download/v1.0.0/KKS_SoftSway_v1.0.0.zip) |

Only download the one for your game. Older and newer versions are on the [Releases page](https://github.com/LazyNinja457/SoftSway/releases).

---

## Install

1. You need **BepInEx 5**, **BepisPlugins** and **KKAPI / KKSAPI 1.38 or newer**. These all come with [HF Patch](https://github.com/ManlyMarco/KK-HF_Patch) (Koikatsu) or [KKS HF Patch](https://github.com/ManlyMarco/KKS-HF_Patch) (Sunshine).
2. Extract the zip **directly into your game folder** (the folder that has `Koikatsu.exe` or `KoikatsuSunshine.exe`). You should end up with:
   ```
   <game folder>\BepInEx\plugins\SoftSway\KK_SoftSway.dll      (or KKS_SoftSway.dll)
   ```
3. **Turn off other plugins that change breast/butt physics**, such as BreastPhysicsController or the breast/butt part of SomaDynamics. They change the same bones and will fight each other.
4. Start the game.

To uninstall, delete the `BepInEx\plugins\SoftSway` folder.

---

## How to use

### Quick start

1. Load a character (in the maker, Studio or an H scene).
2. Press **Shift + J**. The Soft Sway window opens.
3. Make sure **Enable Soft Sway for this character** is ticked.
4. Click the **Breasts** tab, then click a preset, for example **Bouncy**.
5. Click the **Butt** tab and pick a preset there too.
6. **Save the card** (or the Studio scene). The settings are stored inside it and come back every time it's loaded.

### The window, top to bottom

| Part | What it does |
|---|---|
| **◀ Name ▶** | Choose which character you're editing. In Studio and the maker it automatically follows the character you have selected. **X** closes the window. |
| **Enable Soft Sway for this character** | Main on/off switch for this character. Off = the game's normal physics. |
| **Breasts / Butt tabs** | Each body part has its own settings. |
| **Override … physics** | Turn one part on/off. For example, leave breasts on and butt off. |
| **Firm · Natural · Soft · Bouncy · Jelly** | One-click presets. The active one is highlighted. |
| **Sliders** | Fine-tune the feel (see below). **R** resets a slider. |
| **Clothing & body** *(breasts only)* | How much clothing calms the bounce, and size adaptation. |
| **[+] Advanced** | Shows the exact physics numbers. Optionally edit them by hand. |
| **Reset / Copy to everyone** | Reset this tab, or copy it to every other character in the scene. |
| **My presets** | Save your own settings under a name and load them later. |
| **Set as default** | New characters start with these settings. |
| **Apply to everyone** | Copy both tabs to every loaded character. |

### The sliders

All sliders go from **0 to 2**. **1.00 is "natural"**, and the presets are just different slider combinations.

| Slider | Lower | Higher |
|---|---|---|
| **Bounce** | Barely moves | Big, long wobble |
| **Softness** | Tight, snaps back quickly | Heavy, slow, fluid |
| **Motion response** | Ignores body movement | Swings and lags behind movement |
| **Droop** (−1 to 1) | Lifts up | Sags down (0 = game default) |

Breasts only:

| Option | What it does |
|---|---|
| **With bra** | Bounce multiplier while a bra is worn (default 0.80 = 20% less) |
| **With top** | Bounce multiplier while a top is worn without a bra (default 0.90) |
| **Adapt to breast size** | Bigger breasts move heavier and lag more, smaller ones stay tighter. Updates live as you change the size in the maker. |

The line under these shows what the plugin currently sees, e.g. `Now: bra + top · size 62%`.

### Advanced mode (optional)

Click **[+] Advanced** to see the exact values sent to the game for each bone: Damping, Elasticity, Stiffness and Inert. Tick **Edit raw values manually** to take full control of each bone. Manual mode starts from your current slider result, so nothing jumps. Click **Use sliders** to go back.

### Presets and defaults

- **Save a preset:** type a name under *My presets* and click **Save**. Click the name later to load it onto any character.
- Preset files are plain text in `BepInEx\config\SoftSway\presets\`, so you can share them with friends.
- **Set as default:** every character whose card has no Soft Sway data yet starts with these settings.

---

## How it works

Koikatsu already has jiggle physics. Each breast and butt cheek is a small chain of bones driven by the game's **DynamicBone** system. The game sets those bones' physics values from the card's softness/weight sliders, and those values are quite limited.

Soft Sway takes over those same bones instead of adding new physics on top. That keeps it fast and compatible with poses, animations and H scenes.

1. **Sliders → physics values.** Your Bounce / Softness / Motion response / Droop settings are turned into the four values each bone understands:
   - **Damping:** how quickly movement dies out.
   - **Elasticity:** how strongly it is pulled back to rest.
   - **Stiffness:** how far it can move away from rest.
   - **Inert:** how much it just follows the body instead of lagging.

   At 1.00 every slider gives a well-tested "soft" baseline. Moving away from 1.00 scales the values up or down smoothly.
2. **Context adjustments.** Before the values are applied, bounce is reduced when a bra or top is worn, and the result is adjusted for breast size.
3. **Safety limits.** Values are kept inside a range where the bones stay stable, so extreme settings can't make them vibrate forever.
4. **Writing to the game.** The values are written into the game's own breast and hip bone chains. The game is stopped from overwriting them while Soft Sway is in control.
5. **Watching for changes.** Every frame the plugin checks (very cheaply) whether clothing or breast size changed, and only re-applies when something did.
6. **Clean off switch.** Before changing anything, the plugin remembers the game's original values. Turning a part or character off puts those back exactly.
7. **Saving.** Settings are stored inside the character card (and Studio scenes). Cards are only touched if Soft Sway is actually used on that character.

Based on ideas from [BreastPhysicsController](https://github.com/IllusionMods/BreastPhysicsController) (native bone control, card saving, restoring originals) and [SomaDynamics](https://github.com/DevoLutioner/SomaDynamics) (simple feel sliders, presets, males off by default). The code is new.

---

## Settings file

`BepInEx\config\softsway.koikatsu.cfg`. You can also change these in-game from the plugin settings menu (**F1**).

| Setting | Default | |
|---|---|---|
| Open window | Shift + J | Keyboard shortcut |
| Master switch | On | Turns the plugin off for everyone without changing cards |
| Auto-enable on new characters | On | Characters without Soft Sway data start enabled with your default |
| Males off by default | On | Male characters start disabled (can still be enabled by hand) |
| Follow Studio/Maker selection | On | Window follows the selected character |
| Window scale (%) | 100 | Make the window bigger or smaller (75–200) |

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Shift + J does nothing | Check the DLL is in `BepInEx\plugins\SoftSway\` and that KKAPI is installed. Look for errors in `BepInEx\LogOutput.log`. |
| "No breast/butt physics bones" message | The character is low-poly or still loading. Wait a moment, or use the full model. |
| Physics feel wrong or jumpy | Make sure BreastPhysicsController / SomaDynamics breast & butt control are turned off. |
| Settings lost after reloading | Save the card or scene after changing settings. |
| Males don't jiggle | Intended by default. Tick *Enable* for that character, or turn off *Males off by default*. |

Found a bug? Open an issue and attach your `BepInEx\LogOutput.log`.

---

## Building from source

Requires the .NET SDK (6 or newer). The NuGet feeds are set up in `nuget.config`.

```bash
dotnet build SoftSway.sln -c Release
```

The DLLs and release zips appear in `bin/`.

## License

GPL-3.0
