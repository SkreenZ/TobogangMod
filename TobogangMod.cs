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
using TobogangMod.Patches;
using Unity.Netcode;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;
using static UnityEngine.ParticleSystem.PlaybackState;

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
        public static readonly string FUNKY_BOULE = "FunkyBoule";
        public static readonly string BUTINGUE = "Butingue";
        public static readonly string NUKE = "Nuke";
        public static readonly string SWAP = "Swap";
        public static readonly string ANNIHILATION = "Annihilation";
        public static readonly string ARMAGEDDON = "Armageddon";
        public static readonly string FRENESIE = "Frenesie";
    }

    public static readonly ulong NULL_OBJECT = ulong.MaxValue;
    public static readonly string ASSETS_PATH = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TobogangMod");

    public static AudioClip DrumRollClip { get; private set; } = null!;
    public static AudioClip PartyHornClip { get; private set; } = null!;
    public static AudioClip ConfettiClip { get; private set; } = null!;
    public static AudioClip SuccessClip { get; private set; } = null!;
    public static AudioClip NukeAlarmClip { get; private set; } = null!;
    public static AudioClip ShipTeleporterBeamClip { get; private set; } = null!;
    public static AudioClip SunExplosionClip { get; private set; } = null!;
    public static AudioClip DistantExplosionClip { get; private set; } = null!;
    public static AudioClip ArmageddonSirenClip { get; private set; } = null!;
    public static AudioClip InitialDIntro { get; private set; } = null!;
    public static AudioClip InitialDLoop { get; private set; } = null!;
    public static AudioClip RecycleClip { get; private set; } = null!;

    public static GameObject ConfettiPrefab { get; private set; } = null!;
    public static GameObject TobogganPrefab { get; private set; } = null!;
    public static GameObject TrashPrefab { get; private set; } = null!;

    /* Instances */

    public static TobogangMod Instance { get; private set; } = null!;
    public new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    public static AssetBundle MainAssetBundle { get; private set; } = null!;
    public static GameObject NetworkPrefab { get; private set; } = null!;
    public static ContentLoader ContentLoader = null!;
    public static Dictionary<string, GameObject> Prefabs = new();
    public static UnlockablesList Unlockables = null!;

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(ASSETS_PATH, "tobogangasset"));

        NetworkPrefab = MainAssetBundle.LoadAsset<GameObject>("NetworkHandler");

        DrumRollClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/drum_roll.mp3");
        DrumRollClip.LoadAudioData();
        PartyHornClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/party_horn.mp3");
        PartyHornClip.LoadAudioData();
        ConfettiClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/confetti.mp3");
        ConfettiClip.LoadAudioData();
        SuccessClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/success.mp3");
        SuccessClip.LoadAudioData();
        NukeAlarmClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/nuke_alarm.mp3");
        NukeAlarmClip.LoadAudioData();
        ShipTeleporterBeamClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/ship_teleporter_beam.mp3");
        ShipTeleporterBeamClip.LoadAudioData();
        SunExplosionClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/sun_explosion.mp3");
        SunExplosionClip.LoadAudioData();
        DistantExplosionClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/distant_explosion.mp3");
        DistantExplosionClip.LoadAudioData();
        ArmageddonSirenClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/armageddon_siren.wav");
        ArmageddonSirenClip.LoadAudioData();
        InitialDIntro = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/intiald_intro.mp3");
        InitialDIntro.LoadAudioData();
        InitialDLoop = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/intiald_loop.wav");
        InitialDLoop.LoadAudioData();
        RecycleClip = MainAssetBundle.LoadAsset<AudioClip>("Assets/CustomAssets/Audio/recycle.wav");
        RecycleClip.LoadAudioData();

        Unlockables = MainAssetBundle.LoadAsset<UnlockablesList>("Assets/CustomAssets/TobogangUnlockables.asset");

        ConfettiPrefab = MainAssetBundle.LoadAsset<GameObject>("Assets/CustomAssets/ConfettiPrefab.prefab");
        ConfettiPrefab.AddComponent<AutoDespawnScript>();
        NetworkPrefabs.RegisterNetworkPrefab(ConfettiPrefab);

        TobogganPrefab = MainAssetBundle.LoadAsset<GameObject>("Assets/CustomPrefabs/TobogganPrefab.prefab");
        TobogganPrefab.AddComponent<TobogganScript>();
        NetworkPrefabs.RegisterNetworkPrefab(TobogganPrefab);

        TrashPrefab = MainAssetBundle.LoadAsset<GameObject>("Assets/CustomPrefabs/TrashPrefab.prefab");
        TrashPrefab.AddComponent<TrashScript>();
        NetworkPrefabs.RegisterNetworkPrefab(TrashPrefab);

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
        TVScriptPatch.Load();
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

        ContentLoader.Register(new CustomItem(TobogangItems.TA_GUEULE, "Assets/CustomAssets/Items/TobogangTaGueule.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangTaGueule>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.RP_JOEL, "Assets/CustomAssets/Items/TobogangRPJoel.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangRPJoel>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.FUNKY_BOULE, "Assets/CustomAssets/Items/TobogangFunkyBoule.asset",  item => {
            var script = item.spawnPrefab.AddComponent<TobogangFunkyBoule>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.NUKE, "Assets/CustomAssets/Items/TobogangNuke.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangNuke>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.SWAP, "Assets/CustomAssets/Items/TobogangSwap.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangSwap>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.ARMAGEDDON, "Assets/CustomAssets/Items/TobogangArmageddon.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangArmageddon>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.ANNIHILATION, "Assets/CustomAssets/Items/TobogangAnnihilation.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangAnnihilation>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new CustomItem(TobogangItems.FRENESIE, "Assets/CustomAssets/Items/TobogangFrenesie.asset", item => {
            var script = item.spawnPrefab.AddComponent<TobogangFrenesie>();
            script.itemProperties = item;
        }));

        ContentLoader.Register(new ScrapItem(TobogangItems.BUTINGUE, "Assets/CustomAssets/Items/TobogangButingue.asset", 10, Levels.LevelTypes.All, null, (Item item) => {
            var script = item.spawnPrefab.AddComponent<TobogangButingue>();
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
