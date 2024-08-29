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

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPatch
    {
        public enum SpecialNodes
        {
            UnknownWord = 10
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
#if DEBUG
        private static readonly TerminalNode MOTHERLODE_NODE = ScriptableObject.CreateInstance<TerminalNode>();
        private static readonly TerminalNode DAMAGE_NODE = ScriptableObject.CreateInstance<TerminalNode>();
#endif

        private static readonly Dictionary<TerminalNode, List<string>> CUSTOM_TERMINAL_NODES = new()
        {
#if DEBUG
            { MOTHERLODE_NODE, ["motherlode"] },
            { DAMAGE_NODE, ["damage"] },
#endif
            { COINGUES_NODE, ["coingues", "coingue", "coingu", "coing", "coin"] },
            { TOBOGANG_NODE, ["tobogang", "tobogan", "toboga", "tobog", "tobo"] }
        };

        private static readonly Dictionary<string, ItemNode> ITEM_TERMINAL_NODES = new();

        private static TobogangItem? currentlyBuyingItem;

        [HarmonyPatch(nameof(Terminal.Start)), HarmonyPostfix]
        private static void StartPostfix(Terminal __instance)
        {
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

            foreach (var item in TobogangMod.GeTobogangItems())
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

            bool isError = false;
            var itemNode = GetItemNode(__result);

            string s = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded);
            s = __instance.RemovePunctuation(s);

            if (currentlyBuyingItem != null)
            {
                if (s == "confirm")
                {
                    node.displayText = "Confirmed your order\n\n";
                }
                else
                {
                    node.displayText = "Order cancelled\n\n";
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

                foreach (var item in TobogangMod.GeTobogangItems())
                {
                    node.displayText += $"* {item.itemProperties.itemName}  //  {item.CoinguesPrice} coingues\n";
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
                CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 100);
                node.displayText = $"100 coingues obtenus";
            }
            else if (__result == DAMAGE_NODE)
            {
                player.DamagePlayer(1);
                node.displayText = $"1 de dégat infligé";
            }
#endif
            else
            {
                return;
            }

            /*
            if (__instance.terminalNodes.specialNodes.IndexOf(__result) == (int)SpecialNodes.UnknownWord)
            {
                switch (__result)
                {
                    case COINGUES_NODE:
                    {
                        int coingues = CoinguesManager.Instance.GetCoingues(player);
                        node.displayText = $"Tu as {coingues} coingue{(coingues > 1 ? "s" : "")}";
                        break;
                    }

                    case "tobo":
                    case "tobog":
                    case "toboga":
                    case "tobogan":
                    case "tobogang":
                    {
                            int slot = player.FirstEmptyItemSlot();

                            if (slot == -1)
                            {
                                node.displayText = "Tu as besoin d'un slot libre";
                                isError = true;
                            }
                            else
                            {
                                node.displayText = "kdo\n\n";
                                CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.Purge, player.GetComponent<NetworkObject>());
                            }

                            break;
                    }

#if DEBUG
                    case "motherlod":
                    {
                        CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 100);
                        node.displayText = "100 coingues gagnés\n\n";
                        break;
                    }
#endif

                    default:
                    {
                        return;
                    }
                }
            }
            */

            if (isError)
            {
                node.playClip = __instance.terminalNodes.specialNodes[(int)SpecialNodes.UnknownWord].playClip;
                node.playSyncedClip = __result.playSyncedClip;
            }

            node.displayText += "\n\n";
            __result = node;
        }

    }
}
