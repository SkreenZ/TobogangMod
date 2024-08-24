using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using LethalLib.Modules;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(EnemyAI))]
    public class EnemyAIPatch
    {
        [HarmonyPatch(nameof(EnemyAI.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(EnemyAI __instance)
        {
            __instance.gameObject.AddComponent<AudioSourcePlayer>();

            if (NetworkManager.Singleton.IsServer)
            {
                RandomSound randomSound = __instance.gameObject.AddComponent<RandomSound>();
                randomSound.enemy = __instance;
            }
        }

        [HarmonyPatch(nameof(EnemyAI.Update))]
        [HarmonyPostfix]
        private static void UpdatePostfix(EnemyAI __instance)
        {
        }
    }
}
