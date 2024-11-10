using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using TMPro;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    public class PlayerControllerPatch
    {
        public static readonly string MUTE_ICON = "TobogangMutedIcon";
        public static readonly string DEAF_ICON = "TobogangDeafenedIcon";
        public static GameObject? LocalPlayerCanvas { get; private set; } = null;
        public static bool LocalPlayerIsForcedToSprint = false;

        private static float _baseMovementSpeed = 0.5f;

        [HarmonyPatch(nameof(PlayerControllerB.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(PlayerControllerB __instance)
        {
            TobogangMod.Logger.LogDebug($"Player steam id: {__instance.playerSteamId}");

            _baseMovementSpeed = __instance.movementSpeed;

            if (LocalPlayerCanvas == null)
            {
                TobogangMod.Logger.LogDebug("Spawning local player canvas");

                var toboCanvas = TobogangMod.MainAssetBundle.LoadAsset<GameObject>("Assets/CustomAssets/TobogangCanvas.prefab");
                var canvas = GameObject.Find("IngamePlayerHUD");
                LocalPlayerCanvas = GameObject.Instantiate(toboCanvas.transform.Find("Root").gameObject, canvas.transform);

                var betcoingue = LocalPlayerCanvas.transform.Find("BetcoingueResult");
                var betcoingueTitle = betcoingue.transform.Find("Header/Title");
                var betcoinguePlayer = betcoingue.transform.Find("Header/PlayerName");
                betcoingueTitle.gameObject.GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                betcoinguePlayer.gameObject.GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                betcoingue.transform.Find("NoBet").gameObject.GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                betcoinguePlayer.gameObject.SetActive(false);

                for (int i = 0; i < 10; i++)
                {
                    betcoingue.transform.Find($"Content/Player{i}/Name").GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                    betcoingue.transform.Find($"Content/Player{i}/Amount").GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                    betcoingue.transform.Find($"Content/Player{i}/Coingues").GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                    betcoingue.transform.Find($"Content/Player{i}").gameObject.SetActive(false);
                }

                LocalPlayerCanvas.transform.Find("CoinguesAmount").gameObject.GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
                LocalPlayerCanvas.transform.Find("CoinguesAmountS").gameObject.GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;

                betcoingue.gameObject.SetActive(false);

                LocalPlayerCanvas.gameObject.SetActive(true);
            }

            if (NetworkManager.Singleton.IsServer)
            {
                GameObject randomSoundObject = GameObject.Instantiate(RandomSound.NetworkPrefab, __instance.transform.position, Quaternion.identity);
                var randomSound = randomSoundObject.GetComponent<RandomSound>();
                randomSound.NetworkObject.Spawn();

                randomSound.SetParentClientRpc(__instance.NetworkObject);
                randomSound.SetActiveServerRpc(false);
            }

            foreach (var icon in new List<string>{ MUTE_ICON, DEAF_ICON })
            {
                GameObject imgObject = new GameObject(icon);
                var canvas = __instance.usernameCanvas;

                RectTransform trans = imgObject.AddComponent<RectTransform>();
                trans.transform.SetParent(canvas.transform);
                trans.localScale = Vector3.one;
                trans.localRotation = Quaternion.identity;
                trans.anchoredPosition = new Vector2(0f, 0f);
                trans.sizeDelta = new Vector2(40, 40);
                trans.localPosition = new Vector3(0f, 50f, 0f);

                Image image = imgObject.AddComponent<Image>();
                image.sprite = TobogangMod.MainAssetBundle.LoadAsset<Sprite>($"Assets/CustomAssets/Images/{icon}.png");
                imgObject.transform.SetParent(canvas.transform);

                imgObject.SetActive(false);
            }
        }

        [HarmonyPatch(nameof(PlayerControllerB.Update)), HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
            if (!__instance.isPlayerControlled)
            {
                return;
            }

#if DEBUG
            __instance.sprintMeter = 1f;
#endif

            if (LocalPlayerIsForcedToSprint)
            {
                __instance.isSprinting = true;
                __instance.playerBodyAnimator.SetBool("Sprinting", true);
                __instance.movementSpeed = _baseMovementSpeed * 1.5f;
            }
            else
            {
                __instance.movementSpeed = _baseMovementSpeed;
            }
        }

        public static void SetCustomMoveInputVectorMethod(PlayerControllerB player)
        {
            if (!LocalPlayerIsForcedToSprint)
            {
                return;
            }

            player.sprintMeter = 1f;
            player.isSprinting = true;
            player.moveInputVector.Set(player.moveInputVector.x, 1f);
        }

        [HarmonyPatch(nameof(PlayerControllerB.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var moveInputVectorField = AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.moveInputVector));
            var isSprintingField = AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.isSprinting));

            var matcher = new CodeMatcher(instructions);
            bool foundAny = false;

            while (matcher.SearchForward(instruction =>
                       instruction.opcode == OpCodes.Stfld &&
                       ((FieldInfo)instruction.operand == moveInputVectorField || (FieldInfo)instruction.operand == isSprintingField)).IsValid)
            {
                foundAny = true;

                matcher
                    .Advance(1) // Move past the original Stfld instruction
                    .Insert(new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Ldarg_0), // Load 'this' (PlayerControllerB instance)
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerControllerPatch), nameof(SetCustomMoveInputVectorMethod)))
                    });
            }

            if (!foundAny)
            {
                throw new Exception("Could not find any moveInputVector assignment in the Update method.");
            }

            return matcher.InstructionEnumeration();
        }

        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer)), HarmonyPostfix]
        private static void DamagePlayerPostfix(int damageNumber, bool hasDamageSFX, bool callRPC,
            CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force, PlayerControllerB __instance)
        {
#if DEBUG
            TobogangMod.Logger.LogDebug($"{__instance.playerUsername} took {damageNumber} damage from {causeOfDeath}");
#endif

            if (causeOfDeath != CauseOfDeath.Bludgeoning)
            {
                CramptesManager.Instance.TryGiveCramptesOnDamageServerRpc(__instance.NetworkObjectId);
            }
        }

        [HarmonyPatch(nameof(PlayerControllerB.SetItemInElevator)), HarmonyPrefix]
        private static bool SetItemInElevatorPrefix(bool droppedInShipRoom, bool droppedInElevator,
            GrabbableObject gObject, PlayerControllerB __instance)
        {
            bool isServer = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (!isServer)
            {
                return true;
            }

            if (droppedInShipRoom && gObject.itemProperties.isScrap && gObject.scrapValue > 0 && !gObject.scrapPersistedThroughRounds && !RoundManager.Instance.scrapCollectedThisRound.Contains(gObject))
            {
#if DEBUG
                TobogangMod.Logger.LogDebug($"{__instance.playerUsername} dropped in ship {gObject.itemProperties.itemName}");
#endif
                CoinguesManager.Instance.AddCoinguesServerRpc(__instance.NetworkObject, (int)Math.Round(gObject.scrapValue * CoinguesManager.SCRAP_COINGUES_MULTIPLIER));
                CramptesManager.Instance.TryLoseCramptesServerRpc();
            }

            return true;
        }

        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer)), HarmonyPostfix]
        private static void KillPlayerPostfix(CauseOfDeath causeOfDeath, PlayerControllerB __instance)
        {
            CoinguesManager.Instance.RemoveCoinguesServerRpc(__instance.NetworkObject, CoinguesManager.DEATH_MALUS);
            CoinguesManager.Instance.SetClaimStreakServerRpc(__instance.NetworkObject, -1);
        }

#if DEBUG
        [HarmonyPatch(nameof(PlayerControllerB.SetHoverTipAndCurrentInteractTrigger)), HarmonyPostfix]
        private static void SetHoverTipAndCurrentInteractTriggerPostfix(PlayerControllerB __instance)
        {
            return;
            var ray = new Ray(__instance.gameplayCamera.transform.position, __instance.gameplayCamera.transform.forward);

            if (Physics.Raycast(ray, out var hit, __instance.grabDistance, __instance.interactableObjectsMask))
            {
                TobogangMod.Logger.LogDebug($"Hit: {hit.transform.gameObject}, layer: {hit.transform.gameObject.layer}");
            }
        }
#endif
    }
}
