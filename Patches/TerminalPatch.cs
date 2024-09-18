using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using TobogangMod.Scripts;
using TobogangMod.Scripts.Items;
using Unity.Netcode;
using UnityEngine;
using static Unity.Audio.Handle;
using NetworkManager = Unity.Netcode.NetworkManager;
using Random = UnityEngine.Random;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPatch
    {
        public enum TerminalSounds
        {
            PurchaseSuccess = 0,
            Error = 1
        }

        struct ItemNode
        {
            public TerminalNode node;
            public TobogangItem item;
        }

        static ItemNode? GetItemNode(TerminalNode node)
        {
            foreach (var itemNode in ITEM_TERMINAL_NODES.Values)
            {
                if (itemNode.node == node)
                {
                    return itemNode;
                }
            }

            return null;
        }

        private static readonly TerminalNode COINGUES_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode TOBOGANG_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode CRAMPTES_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode CLAIM_NODE = ScriptableObject.CreateInstance<TerminalNode>();
#if DEBUG
        private static readonly TerminalNode MOTHERLODE_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode DAMAGE_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode FINISH_BET_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode REROLL_NODE = ScriptableObject.CreateInstance<TerminalNode>();
#endif

        private static readonly Dictionary<TerminalNode, List<string>> CUSTOM_TERMINAL_NODES = new()
        {
#if DEBUG
            { MOTHERLODE_NODE, ["motherlode"] },
            { DAMAGE_NODE, ["damage"] },
            { FINISH_BET_NODE, ["finishbet"] },
            { REROLL_NODE, ["reroll", "roll"] },
#endif
            { CRAMPTES_NODE, ["cramptes", "cramptés", "crampte", "crampté"] },
            { COINGUES_NODE, ["coingues", "coingue", "coingu", "coing", "coin"] },
            { TOBOGANG_NODE, ["tobogang", "tobogan", "toboga", "tobog", "tobo"] },
            { CLAIM_NODE,    ["claim"] }
    };

        private static readonly Dictionary<string, ItemNode> ITEM_TERMINAL_NODES = new();

        private static TobogangItem? currentlyBuyingItem;
        private static bool _initialized = false;

        [HarmonyPatch(nameof(Terminal.Start)), HarmonyPostfix]
        private static void StartPostfix(Terminal __instance)
        {
            currentlyBuyingItem = null;

            if (_initialized)
            {
                return;
            }

            _initialized = true;

            foreach (var customNode in CUSTOM_TERMINAL_NODES)
            {
                foreach (var word in customNode.Value)
                {
                    TerminalKeyword keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
                    keyword.word = word;
                    keyword.specialKeywordResult = customNode.Key;

                    __instance.terminalNodes.allKeywords = [.. __instance.terminalNodes.allKeywords, keyword];
                }
            }

            foreach (var item in TobogangMod.GetTobogangItems())
            {
                var node = ScriptableObject.CreateInstance<TerminalNode>();
                node.clearPreviousText = true;
                node.displayText = $"Acheter {item.itemProperties.itemName} pour {item.CoinguesPrice} coingues ?\n\n";
                node.displayText += "Please CONFIRM or DENY.\n\n";

                foreach (var word in item.Keywords)
                {
                    TerminalKeyword keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
                    keyword.word = word;
                    keyword.specialKeywordResult = node;

                    __instance.terminalNodes.allKeywords = [.. __instance.terminalNodes.allKeywords, keyword];
                }

                ITEM_TERMINAL_NODES[item.TobogangItemId] = new ItemNode{ item = item, node = node};
            }

            TobogangMod.Logger.LogDebug($"Registered {CUSTOM_TERMINAL_NODES.Count} custom terminal keywords");
        }

        [HarmonyPatch(nameof(Terminal.QuitTerminal)), HarmonyPostfix]
        private static void QuitTerminalPostfix()
        {
#if DEBUG
            TobogangMod.Logger.LogDebug("Quit terminal, reset currentlyBuyingItem");
#endif
            currentlyBuyingItem = null;
        }

        [HarmonyPatch(nameof(Terminal.ParsePlayerSentence))]
        [HarmonyPostfix]
        private static void ParsePlayerSentencePostfix(Terminal __instance, ref TerminalNode __result)
        {
            if (__result == null)
            {
                TobogangMod.Logger.LogDebug("Result was null");
                return;
            }

#if DEBUG
            TobogangMod.Logger.LogDebug($"Terminal specialNode index: {__instance.terminalNodes.specialNodes.IndexOf(__result)}");
#endif

            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

            TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
            node.clearPreviousText = true;

            var itemNode = GetItemNode(__result);

            string s = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded);
            s = __instance.RemovePunctuation(s);
            var args = s.Split(' ');

            if (currentlyBuyingItem != null)
            {
                if (s == "confirm")
                {
                    if (TryBuyCurrentItem(player, ref node))
                    {
                        node.playSyncedClip = (int)TerminalSounds.PurchaseSuccess;
                    }
                    else
                    {
                        node.playSyncedClip = (int)TerminalSounds.Error;
                    }
                }
                else
                {
                    node.displayText = "Achat annulé";
                }

                currentlyBuyingItem = null;
            }
            else if (__result == COINGUES_NODE)
            {
                int coingues = CoinguesManager.Instance.GetCoingues(player);
                node.displayText = $"Tu as {coingues} coingue{(coingues > 1 ? "s" : "")}";
            }
            else if (__result == TOBOGANG_NODE)
            {
                node.displayText =  "Items Tobogang\n";
                node.displayText += "____________________________\n\n";
                var items = TobogangMod.GetTobogangItems().ToList();
                items.Sort(((item1, item2) => item1.CoinguesPrice - item2.CoinguesPrice));

                foreach (var item in items)
                {
                    node.displayText += $"* {item.itemProperties.itemName}  //  {item.CoinguesPrice} coingues\n";
                }
            }
            else if (__result == CRAMPTES_NODE)
            {
                node.displayText = CramptesManager.Instance.CurrentCramptesPlayer != null
                    ? $"{CramptesManager.Instance.CurrentCramptesPlayer.playerUsername} a les cramptés."
                    : "Personne n'a les cramptés.";
            }
            else if (__result == CLAIM_NODE)
            {
                if (StartOfRound.Instance.inShipPhase)
                {
                    node.displayText = "Tu ne peux claim que quand le vaisseau a atterri.";
                    node.playSyncedClip = (int)TerminalSounds.Error;
                }
                else if (RoundManager.Instance.timeScript.currentDayTime < CoinguesManager.CLAIM_TIME)
                {
                    node.displayText = "Tu ne peux claim qu'à partir de 12:00.";
                    node.playSyncedClip = (int)TerminalSounds.Error;
                }
                else
                {
                    var alreadyClaimed = CoinguesManager.Instance.HasClaimedToday(player);
                    var streak = CoinguesManager.Instance.GetClaimStreak(player) + 1;

                    CoinguesManager.Instance.ClaimServerRpc(player.NetworkObject);

                    node.displayText = alreadyClaimed
                        ? "Tu as déjà claim aujourd'hui. Tu perds 1 coingue (frais de dossier)."
                        : $"[{streak + 1}] {CoinguesManager.CLAIM_VALUE + streak} coingues obtenus.";

                    if (alreadyClaimed)
                    {
                        node.playSyncedClip = (int)TerminalSounds.Error;
                    }
                }
            }
            else if (args[0] == "bet")
            {
                var activeBet = CoinguesManager.Instance.GetPlayerBet(player);
                TobogangMod.Logger.LogDebug(RoundManager.Instance.timeScript.daysUntilDeadline);

                if (activeBet != null)
                {
                    var targetBetPlayer = TobogangMod.TryGet(activeBet.Value.PlayerNetId);
                    string targetBetPlayerName = targetBetPlayer != null
                        ? targetBetPlayer.GetComponentInChildren<PlayerControllerB>().playerUsername
                        : "<inconnu>";

                    node.displayText = $"Tu as déjà un bet actif : {activeBet.Value.Amount} coingue{(activeBet.Value.Amount > 1 ? "s" : "")} sur {targetBetPlayerName}";
                    node.playSyncedClip = (int)TerminalSounds.Error;
                }
                else if (args.Length != 3 || !uint.TryParse(args[2], out var amount) || amount <= 0)
                {
                    node.displayText = "Permet de parier sur le joueur le plus rentable sur un shift.\nUtilisation: bet [joueur] [nb de coingues à parier]";
                    node.playSyncedClip = (int)TerminalSounds.Error;
                }
                else if (RoundManager.Instance.timeScript.daysUntilDeadline != 3)
                {
                    node.displayText = "Tu ne peux bet que pendant le premier jour du shift.";
                }
                else
                {
                    PlayerControllerB? betPlayer = null;

                    foreach (var p in StartOfRound.Instance.allPlayerScripts)
                    {
                        if (__instance.RemovePunctuation(p.playerUsername.Replace(" ", "")) == args[1])
                        {
                            betPlayer = p;
                            break;
                        }
                    }

                    if (betPlayer == null)
                    {
                        node.displayText = $"Joueur inconnu : {args[1]}";
                        node.playSyncedClip = (int)TerminalSounds.Error;
                    }
                    else if (CoinguesManager.Instance.GetCoingues(player) < amount)
                    {
                        node.displayText = "Tu n'as pas assez de coingues.";
                        node.playSyncedClip = (int)TerminalSounds.Error;
                    }
                    else
                    {
                        CoinguesManager.Instance.SetPlayerBetServerRpc(player.NetworkObject, betPlayer.NetworkObject, amount);

                        node.displayText = $"{amount} coingues pariés sur {betPlayer.playerUsername}";
                    }
                }
            }
            else if (itemNode.HasValue && currentlyBuyingItem == null)
            {
                currentlyBuyingItem = itemNode.Value.item;
                return;
            }
#if DEBUG
            else if (__result == MOTHERLODE_NODE)
            {
                CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 1000);
                node.displayText = $"1000 coingues obtenus";
            }
            else if (__result == DAMAGE_NODE)
            {
                player.DamagePlayer(1);
                node.displayText = $"1 de dégat infligé";
            }
            else if (__result == FINISH_BET_NODE)
            {
                node.displayText = "Finish bet";
                CoinguesManager.Instance.PlayerProfits[CoinguesManager.GetPlayerId(player)] = 1000;
                CoinguesManager.Instance.FinishBetcoingueServerRpc();
            }
            else if (__result == REROLL_NODE)
            {
                var oldSeed = StartOfRound.Instance.randomMapSeed;
                StartOfRound.Instance.randomMapSeed = Random.Range(1, Int32.MaxValue);
                __instance.RotateShipDecorSelection();
                StartOfRound.Instance.randomMapSeed = oldSeed;

                int i = 0;
                foreach (var unlockable in StartOfRound.Instance.unlockablesList.unlockables)
                {
                    TobogangMod.Logger.LogDebug($"Unlockable {unlockable.unlockableName} ({i++})");
                }

            }
            else if (args[0] == "tete")
            {
                CoinguesManager.Instance.SetPlayerDiscoServerRpc(player.NetworkObject, !CoinguesManager.Instance.DiscoPlayers.Contains(player.NetworkObjectId));
            }
#endif
            else
            {
                return;
            }

            node.displayText += "\n\n";
            __result = node;
        }

        private static bool TryBuyCurrentItem(PlayerControllerB player, ref TerminalNode node)
        {
            if (currentlyBuyingItem == null)
            {
                TobogangMod.Logger.LogDebug("Tried to buy a null item");
                return false;
            }

            if (currentlyBuyingItem.CoinguesPrice > CoinguesManager.Instance.GetCoingues(player))
            {
                node.displayText = "Tu n'as pas assez de coingues pour acheter cet item.";
                return false;
            }

            int slot = player.FirstEmptyItemSlot();

            if (slot == -1)
            {
                node.displayText = "Tu as besoin d'un slot libre.";
                return false;
            }

            CoinguesManager.Instance.RemoveCoinguesServerRpc(player.NetworkObject, currentlyBuyingItem.CoinguesPrice);

            CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(currentlyBuyingItem.TobogangItemId, player.GetComponent<NetworkObject>());
            node.displayText = $"Achat de {currentlyBuyingItem.itemProperties.itemName} réussi.";
            currentlyBuyingItem = null;

            return true;
        }

#if DEBUG
        [HarmonyPatch(nameof(Terminal.PlayTerminalAudioServerRpc)), HarmonyPostfix]
        private static void PlayTerminalAudioServerRpcPostfix(int clipIndex)
        {
            TobogangMod.Logger.LogDebug($"Played terminal audio index {clipIndex}");
        }

        [HarmonyPatch(nameof(Terminal.Update)), HarmonyPostfix]
        private static void UpdatePostfix(Terminal __instance)
        {
            __instance.groupCredits = 9999;
        }
#endif
    }
}
