using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using LobbyCompatibility.Attributes;
using LobbyCompatibility.Enums;
using LethalLib.Modules;
using TobogangMod.Scripts;
using System.Reflection;
using static LethalLib.Modules.ContentLoader;
using System.Collections.Generic;
using TobogangMod.Scripts.Items;
using System.IO;
using GameNetcodeStuff;
using Unity.Netcode;

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
        public static readonly string RP_JOEL = "RPJoel";
        public static readonly string CRAZY_TOBOBOT = "CrazyTobobot";
    }

    public static readonly ulong NULL_OBJECT = ulong.MaxValue;
    public static readonly string ASSETS_PATH = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TobogangMod");

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

        MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(ASSETS_PATH, "tobogangasset"));

        NetworkPrefab = (GameObject)MainAssetBundle.LoadAsset("NetworkHandler");

        ContentLoader = new ContentLoader(Instance.Info, MainAssetBundle, (content, prefab) => {
            Prefabs.Add(content.ID, prefab);
        });

        InitAll();
        CreateItems();
        Patch();

        NetcodePatcher();

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

        ContentLoader.Register(new ScrapItem(TobogangItems.TA_GUEULE, "Assets/CustomAssets/Items/TobogangTaGueule.asset", 0, Levels.LevelTypes.None, null, item => {
            var script = item.spawnPrefab.AddComponent<TobogangTaGueule>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new ScrapItem(TobogangItems.RP_JOEL, "Assets/CustomAssets/Items/TobogangRPJoel.asset", 0, Levels.LevelTypes.None, null, item => {
            var script = item.spawnPrefab.AddComponent<TobogangRPJoel>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new ScrapItem(TobogangItems.CRAZY_TOBOBOT, "Assets/CustomAssets/Items/TobogangCrazyTobobot.asset", 0, Levels.LevelTypes.None, null, item => {
            var script = item.spawnPrefab.AddComponent<TobogangCrazyTobobot>();
            script.itemProperties = item;
        }));

        Logger.LogInfo("Registered items");
    }

    public static TobogangItem[] GetTobogangItems()
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

    public static NetworkObject? TryGet(ulong objectNetId)
    {
        if (objectNetId == NULL_OBJECT)
        {
            return null;
        }

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects[objectNetId];
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
