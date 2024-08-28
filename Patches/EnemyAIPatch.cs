﻿using System;
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
            if (NetworkManager.Singleton.IsServer)
            {
                GameObject randomSoundObject = GameObject.Instantiate(RandomSound.NetworkPrefab, __instance.gameObject.transform);
                randomSoundObject.GetComponent<NetworkObject>().Spawn();
            }
        }

        [HarmonyPatch(nameof(EnemyAI.Update))]
        [HarmonyPostfix]
        private static void UpdatePostfix(EnemyAI __instance)
        {
        }
    }
}
