using HarmonyLib;

namespace SoftSway
{
    internal static class Hooks
    {
        // The game recalculates breast DynamicBone values from the card's softness/weight sliders
        // (clothes changes, body edits, H-scene events). While we own the breast chain that would
        // silently overwrite our values, so skip it - the same guard BPC and SomaDynamics use.
        [HarmonyPrefix, HarmonyPatch(typeof(BustSoft), "ReCalc")]
        private static bool BustSoftReCalcPrefix(BustSoft __instance)
        {
            SoftSwayler c = SoftSwayler.FindByBustSoft(__instance);
            return c == null || !c.OwnsBreasts;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BustGravity), "ReCalc")]
        private static bool BustGravityReCalcPrefix(BustGravity __instance)
        {
            SoftSwayler c = SoftSwayler.FindByBustGravity(__instance);
            return c == null || !c.OwnsBreasts;
        }
    }
}
