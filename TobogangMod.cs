using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using LobbyCompatibility.Attributes;
using LobbyCompatibility.Enums;
using LethalLib;
using LethalLib.Modules;
using TobogangMod.Scripts;
using Unity.Netcode;
using System.Reflection;
using static LethalLib.Modules.ContentLoader;
using System.Collections.Generic;
using LethalLib.Extras;

namespace TobogangMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
[LobbyCompatibility(CompatibilityLevel.ClientOnly, VersionStrictness.None)]
public class TobogangMod : BaseUnityPlugin
{
    public static TobogangMod Instance { get; private set; } = null!;
    public new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    public static AssetBundle MainAssetBundle { get; private set; } = null!;

    public static ContentLoader ContentLoader;
    public static Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        var dllFolderPath = System.IO.Path.GetDirectoryName(Info.Location);
        var assetBundleFilePath = System.IO.Path.Combine(dllFolderPath, "tobogangasset");
        MainAssetBundle = AssetBundle.LoadFromFile(assetBundleFilePath);

        ContentLoader = new ContentLoader(Instance.Info, MainAssetBundle, (content, prefab) => {
            Prefabs.Add(content.ID, prefab);
        });

        CreateItems();
        Patch();

        NetcodePatcher();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded !");
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");

        Harmony.PatchAll();

        Logger.LogDebug("Finished patching!");
    }

    void CreateItems()
    {
        ContentLoader.Register(new ScrapItem("Cramptes", "Assets/CustomAssets/CramptesItem.asset", 1000, Levels.LevelTypes.All, null, (Item item) => {
            CramptesItem script = item.spawnPrefab.AddComponent<CramptesItem>();
            script.itemProperties = item;
        }));

        Logger.LogInfo("Registered items");
    }

    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }

    private static void NetcodePatcher()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }
}
