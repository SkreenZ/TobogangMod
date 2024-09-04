using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Video;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(TVScript))]
    internal class TVScriptPatch
    {
        private static AudioClip skyyartAudioClip;
        private static VideoClip skyyartVideoClip;

        public static void Load()
        {
            skyyartAudioClip = TobogangMod.MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/skyyart_audio.mp3");
            skyyartVideoClip = TobogangMod.MainAssetBundle.LoadAsset<VideoClip>("Assets/CustomAssets/skyyart_video.mp4");
        }

        [HarmonyPatch(nameof(TVScript.Update)), HarmonyPrefix]
        private static bool UpdatePrefix(TVScript __instance)
        {
            if (!__instance.tvAudioClips.Contains(skyyartAudioClip) || !__instance.tvClips.Contains(skyyartVideoClip))
            {
                __instance.tvAudioClips = [skyyartAudioClip];
                __instance.tvClips = [skyyartVideoClip];
                __instance.currentClip = 0;
            }

            return true;
        }

        [HarmonyPatch(nameof(TVScript.TurnOnTVServerRpc)), HarmonyPostfix]
        private static void TurnOnTvServerRpcPostfix(TVScript __instance)
        {
            __instance.TurnOnTVAndSyncClientRpc(__instance.currentClip, __instance.currentClipTime);
        }
    }
}
