# Soft Sway

Breast and butt jiggle physics for **Koikatsu / Koikatsu Party** (`KK_SoftSway.dll`) and **Koikatsu Sunshine** (`KKS_SoftSway.dll`), with a single small window.

It takes the best parts of two existing plugins and puts them behind a simpler UI:

| From | What was kept |
|---|---|
| [BreastPhysicsController](https://github.com/IllusionMods/BreastPhysicsController) | Uses the game's own breast/hip DynamicBone chains, saves settings into the card, restores the original values when switched off, has user presets and a default for new characters, keeps the game from overwriting the values |
| [SomaDynamics](https://github.com/DevoLutioner/SomaDynamics) | A few "feel" sliders instead of raw numbers, one-click presets, an advanced per-bone mode, males off by default, one setting applied to every character |

### What's new

- **4 sliders per part:** Bounce, Softness, Motion response and Droop. 1.0 is the natural baseline and the range is 0 to 2.
- **5 one-click presets:** Firm, Natural, Soft, Bouncy, Jelly.
- **Clothing-aware breasts.** Bounce is scaled automatically for bra / top / bra + top / naked. You set two multipliers instead of BPC's 15 separate settings for each outfit and clothing state. A top that isn't equipped doesn't count as worn.
- **Adapts to breast size.** Larger breasts get heavier movement that lags more, smaller ones stay tighter. It updates live while you drag the size slider in Maker.
- **Live readout.** The advanced section shows the exact Damping / Elasticity / Stiffness / Inert values sent to the game. Switch to manual mode to edit them per bone. Manual mode starts from the current slider result, so nothing jumps.
- **Stability guard.** Values are clamped so extreme settings can't make bones vibrate forever.
- **Follows your selection** in Studio and Maker. There is also a ◀ ▶ character picker.
- **Copy to everyone:** copy one part, or apply the whole setup to every loaded character.
- **Master switch** to turn it off globally without touching cards. Cards are only written to when the plugin is in use for that character.
- **Scalable window** (75–200 %).

## Install

1. Install **BepInEx 5**, **BepisPlugins** and **KKAPI / KKSAPI 1.38+** (included in HF Patch / KKS HF Patch).
2. Copy the DLL for your game into `Koikatsu\BepInEx\plugins\SoftSway\`.
   - Koikatsu / Party: `KK_SoftSway.dll`
   - Sunshine: `KKS_SoftSway.dll`
3. **Don't run BreastPhysicsController or SomaDynamics' breast/butt control at the same time.** They write to the same bones and will fight over them.

## Use

Press **Shift + J** (configurable) in Maker, Studio or H scenes.

1. Pick the character. In Studio and Maker the window follows the selected character.
2. Make sure **Enable Soft Sway for this character** is ticked.
3. Choose the **Breasts** or **Butt** tab and click a preset, or move the sliders:
   - **Bounce:** how big the wobble is.
   - **Softness:** how loosely it springs back (heavier, more fluid).
   - **Motion response:** how strongly body movement drives the jiggle (more lag and swing).
   - **Droop:** extra gravity. Positive sags, negative lifts.
4. Breasts only: set **With bra / With top** multipliers and **Adapt to breast size**.
5. Save the card or scene. The settings are stored in it.

**Presets:** type a name and press *Save*, then click a name to load it. Files are in `BepInEx\config\SoftSway\presets\*.txt` (plain text, easy to share).
**Set as default:** characters without saved data start with these settings (`BepInEx\config\SoftSway\default.txt`).

### Config (`BepInEx\config\softsway.koikatsu.cfg`, or the F1 plugin settings menu)

| Setting | Default | |
|---|---|---|
| Open window | Shift+J | |
| Master switch | on | Global on/off |
| Auto-enable on new characters | on | Cards without data start enabled with your default |
| Males off by default | on | |
| Follow Studio/Maker selection | on | |
| Window scale (%) | 100 | |

## Build

Requires the .NET SDK (6+) and access to the BepInEx / IllusionMods NuGet feeds (configured in `nuget.config`).

```bash
dotnet build SoftSway.sln -c Release
```

The output is `bin/KK_SoftSway/KK_SoftSway.dll`, `bin/KKS_SoftSway/KKS_SoftSway.dll`, plus ready-to-extract zips in `bin/`.

## How it works (for modders)

- `PhysicsMapper` turns the sliders into DynamicBone values. Every slider is a curve that equals ×1 at 1.0, so the centre reproduces the "Soft" baseline that both source plugins use.
- `NativeChainApplier` takes a snapshot of each chain before the first write and restores it exactly when a part or character is turned off.
  - Breasts are written through `Patterns[0]` → `ParticlePtns` → `setPtn(0)` → `ReSetupDynamicBoneBust()`.
  - Hips are written straight to the live particles.
- `Hooks` skips `BustSoft.ReCalc` / `BustGravity.ReCalc` only while this plugin owns that character's breasts.
- `SoftSwayler` checks clothing state and breast size every frame (cheap, no allocations) and only re-applies when something changed.

## License

GPL-3.0, like BreastPhysicsController. The code was written from scratch. The baseline bone values and the hook approach come from the plugins credited above.
