using System;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using HarmonyLib;
using TobogangMod.Scripts;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(CozyLights))]
    public class CozyLightsPatch
    {
        [HarmonyPatch(nameof(CozyLights.Update)), HarmonyPostfix]
        private static void UpdatePostfix(CozyLights __instance)
        {
            var player = __instance.GetComponentInParent<PlayerControllerB>();

            if (player != null)
            {
                var on = CoinguesManager.Instance.DiscoPlayers.Contains(player.NetworkObjectId);

                __instance.cozyLightsOn = on;
                __instance.cozyLightsAnimator.SetBool("on", on);
                __instance.SetAudio();
            }
        }
    }
}
