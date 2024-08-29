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
using TobogangMod.Scripts.Items;

namespace TobogangMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
[LobbyCompatibility(CompatibilityLevel.ClientOnly, VersionStrictness.None)]
public class TobogangMod : BaseUnityPlugin
{
    public class TobogangItems
    {
        public static readonly string PURGE = "Purge";
        public static readonly string TA_GUEULE = "TaGueule";
    }

    /* Instances */

    public static TobogangMod Instance { get; private set; } = null!;
    public new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    public static AssetBundle MainAssetBundle { get; private set; } = null!;
    public static GameObject NetworkPrefab { get; private set; } = null!;
    public static ContentLoader ContentLoader = null!;
    public static Dictionary<string, GameObject> Prefabs = new();

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        var dllFolderPath = System.IO.Path.GetDirectoryName(Info.Location);
        var assetBundleFilePath = System.IO.Path.Combine(dllFolderPath, "TobogangMod/tobogangasset");
        MainAssetBundle = AssetBundle.LoadFromFile(assetBundleFilePath);

        NetworkPrefab = (GameObject)MainAssetBundle.LoadAsset("NetworkHandler");

        ContentLoader = new ContentLoader(Instance.Info, MainAssetBundle, (content, prefab) => {
            Prefabs.Add(content.ID, prefab);
        });

        InitAll();
        CreateItems();
        Patch();

        NetcodePatcher();

        GeTobogangItems();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded !");
    }

    internal static void InitAll()
    {
        RandomSound.Init();
        CramptesManager.Init();
        CoinguesManager.Init();
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
        /*
        ContentLoader.Register(new ScrapItem(TobogangItems.Purge, "Assets/CustomAssets/TobogangPurge.asset", 0, Levels.LevelTypes.None, null, (Item item) => {
            TobogangPurge script = item.spawnPrefab.AddComponent<TobogangPurge>();
            script.itemProperties = item;
            script.grabbable = true;
            script.itemProperties.canBeGrabbedBeforeGameStart = true;
        }));
        */

        ContentLoader.Register(new ScrapItem(TobogangItems.TA_GUEULE, "Assets/CustomAssets/TobogangTaGueule.asset", 0, Levels.LevelTypes.None, null, item => {
            var script = item.spawnPrefab.AddComponent<TobogangTaGueule>();
            script.itemProperties = item;
            script.grabbable = true;
            script.itemProperties.canBeGrabbedBeforeGameStart = true;
            script.itemProperties.rotationOffset = new Vector3(0f, 90f, 0f);
            script.itemProperties.positionOffset = new Vector3(0f, 0.08f, 0f);
        }));

        Logger.LogInfo("Registered items");
    }

    public static TobogangItem[] GeTobogangItems()
    {
        List<TobogangItem> outItems = [];

        foreach (var prefab in Prefabs.Values)
        {
            var item = prefab.GetComponent<TobogangItem>();

            if (item != null)
            {
                outItems.Add(item);
            }
        }

        return outItems.ToArray();
    }

    public static TobogangItem? GeTobogangItem(string id)
    {
        foreach (var prefab in Prefabs.Values)
        {
            var item = prefab.GetComponent<TobogangItem>();

            if (item != null && item.TobogangItemId == id)
            {
                return item;
            }
        }

        return null;
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
