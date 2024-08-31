using System;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
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
        private static readonly float CRAZY_DETECTION_RADIUS = 20f;

        [HarmonyPatch(nameof(EnemyAI.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(EnemyAI __instance)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                GameObject randomSoundObject = GameObject.Instantiate(RandomSound.NetworkPrefab, __instance.gameObject.transform);
                randomSoundObject.GetComponent<RandomSound>().NetworkObject.Spawn();
            }
        }

        [HarmonyPatch(nameof(EnemyAI.Update))]
        [HarmonyPostfix]
        private static void UpdatePostfix(EnemyAI __instance)
        {
            float distance = -1f;
            PlayerControllerB? playerInRange = null;

            foreach (var collider in Physics.OverlapSphere(__instance.transform.position, CRAZY_DETECTION_RADIUS))
            {
                var player = collider.GetComponent<PlayerControllerB>();

                if (player == null)
                {
                    continue;
                }

                var d = Vector3.Distance(collider.transform.position, player.transform.position);

                if (player != null && (distance < 0f || d < distance))
                {
                    playerInRange = player;
                    distance = d;
                }
            }

            if (playerInRange != null)
            {
                __instance.SetMovingTowardsTargetPlayer(StartOfRound.Instance.localPlayerController);
            }
        }
    }
}
