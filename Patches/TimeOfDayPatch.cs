using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TobogangMod.Scripts;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(TimeOfDay))]
    public class TimeOfDayPatch
    {
        public static readonly float MAX_INTENSITY = 10000f;

        [HarmonyPatch(nameof(TimeOfDay.SetInsideLightingDimness)), HarmonyPostfix]
        private static void SetInsideLightingDimnessPostfix(TimeOfDay __instance)
        {
            if (CoinguesManager.Instance.IsSunExploding)
            {
                __instance.sunIndirect.enabled = true;
                __instance.sunAnimator.enabled = false;
                __instance.sunIndirect.intensity = CoinguesManager.Instance.CurrentSunIntensity;
            }
        }
    }
}
