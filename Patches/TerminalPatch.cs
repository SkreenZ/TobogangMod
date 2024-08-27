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
    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPatch
    {
        public enum SpecialNodes
        {
            UnknownWord = 10
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

            string s = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded);
            s = __instance.RemovePunctuation(s);
            string[] array = s.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            if (array.Length == 0)
            {
                return;
            }

            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

            TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
            node.clearPreviousText = true;

            if (__instance.terminalNodes.specialNodes.IndexOf(__result) == (int)SpecialNodes.UnknownWord)
            {
                switch (array[0])
                {
                    case "caca":
                    {
                        node.displayText = "prout\n\n";
                        __result = node;
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
                                node.displayText = "Tu as besoin d'un slot libre\n\n";
                                node.playClip = __instance.terminalNodes.specialNodes[(int)SpecialNodes.UnknownWord].playClip;
                                node.playSyncedClip = __result.playSyncedClip;
                            }
                            else
                            {
                                node.displayText = "kdo\n\n";
                                CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.Purge, player.GetComponent<NetworkObject>());
                            }

                            __result = node;

                            break;
                    }
                }
            }
        }

    }
}
