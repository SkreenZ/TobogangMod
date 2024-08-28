using System.Collections.Generic;
using System.Linq;
using Discord;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPatch
    {
        public enum SpecialNodes
        {
            UnknownWord = 10
        }

        private static readonly TerminalNode COINGUES_NODE = ScriptableObject.CreateInstance<TerminalNode>();
#if DEBUG
        private static readonly TerminalNode MOTHERLODE_NODE = ScriptableObject.CreateInstance<TerminalNode>();
#endif

        private static readonly Dictionary<TerminalNode, List<string>> customTerminalNodes = new Dictionary<TerminalNode, List<string>>()
        {
#if DEBUG
            { MOTHERLODE_NODE, ["motherlode"] },
#endif
            { COINGUES_NODE, ["coingues", "coingue", "coingu", "coing", "coin"] }
        };

        [HarmonyPatch(nameof(Terminal.Start)), HarmonyPostfix]
        private static void StartPostfix(Terminal __instance)
        {
            foreach (var customNode in customTerminalNodes)
            {
                foreach (var word in customNode.Value)
                {
                    TerminalKeyword keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
                    keyword.word = word;
                    keyword.specialKeywordResult = customNode.Key;

                    __instance.terminalNodes.allKeywords = [.. __instance.terminalNodes.allKeywords, keyword];
                }
            }

            TobogangMod.Logger.LogDebug($"Registered {customTerminalNodes.Count} custom terminal keywords");
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

            if (__result == COINGUES_NODE)
            {
                int coingues = CoinguesManager.Instance.GetCoingues(player);
                node.displayText = $"Tu as {coingues} coingue{(coingues > 1 ? "s" : "")}";
            }
#if DEBUG
            else if (__result == MOTHERLODE_NODE)
            {
                CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 100);
                node.displayText = $"100 coingues obtenus";
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
