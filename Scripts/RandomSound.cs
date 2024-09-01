using BepInEx;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;

namespace TobogangMod.Scripts
{
    public class RandomSound : NetworkBehaviour
    {
        private static readonly float BASE_MIN_TIME = 30f;
        private static readonly float BASE_MAX_TIME = 300f;
        private static readonly float CRAZY_MIN_TIME = 1f;
        private static readonly float CRAZY_MAX_TIME = 5f;

        public static List<AudioClip> Sounds = [];
        public static GameObject NetworkPrefab = null!;

        public AudioSource AudioSource = null!;
        public float MinTimeBetweenSounds = BASE_MIN_TIME;
        public float MaxTimeBetweenSounds = BASE_MAX_TIME;

        private bool _active = true;
        private float _timeUntilNextSound = -1f;

        public static void Init()
        {
            NetworkPrefab = TobogangMod.MainAssetBundle.LoadAsset<GameObject>("Assets/CustomPrefabs/SoundPlayer.prefab");
            NetworkPrefab.AddComponent<RandomSound>();
            NetworkPrefabs.RegisterNetworkPrefab(NetworkPrefab);

            if (Sounds.Count == 0)
            {
                foreach (var assetName in TobogangMod.MainAssetBundle.GetAllAssetNames())
                {
                    if (!assetName.ToLower().Contains("randomsounds"))
                    {
                        continue;
                    }

                    AudioClip clip = TobogangMod.MainAssetBundle.LoadAsset<AudioClip>(assetName);

                    if (clip != null)
                    {
                        Sounds.Add(clip);
                    }
                    else
                    {
                        TobogangMod.Logger.LogDebug($"Failed to load sound {assetName}");
                    }
                }

                TobogangMod.Logger.LogDebug($"Loaded {Sounds.Count} sounds");
            }
        }

        private void Start()
        {
            TobogangMod.Logger.LogDebug("New RandomSound spawned");

            AudioSource = GetComponent<AudioSource>();

            // 1% chance to be crazy
            if (UnityEngine.Random.Range(0f, 1f) <= 0.01f && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                SetCrazy(true);
            }

            _timeUntilNextSound = UnityEngine.Random.Range(MinTimeBetweenSounds, MaxTimeBetweenSounds);
        }


        private void Update()
        {
            bool isServer = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (!isServer || !_active)
            {
                return;
            }

            _timeUntilNextSound -= Time.deltaTime;

            if (_timeUntilNextSound <= 0f)
            {
                _timeUntilNextSound = UnityEngine.Random.Range(MinTimeBetweenSounds, MaxTimeBetweenSounds);

                var soundIndex = UnityEngine.Random.RandomRangeInt(0, Sounds.Count);

                PlaySoundClientRpc(soundIndex);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPositionServerRpc(Vector3 position)
        {
            SetPositionClientRpc(position);
        }

        [ClientRpc]
        public void SetPositionClientRpc(Vector3 position)
        {
            gameObject.transform.position = position;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetActiveServerRpc(bool active)
        {
            TobogangMod.Logger.LogDebug($"Random sound set active: {active}");

            if (active)
            {
                _timeUntilNextSound = UnityEngine.Random.Range(MinTimeBetweenSounds, MaxTimeBetweenSounds);
            }

            _active = active;
        }

        [ClientRpc]
        void PlaySoundClientRpc(int soundIndex)
        {
            PlaySound(soundIndex);
        }

        public void SetCrazy(bool crazy)
        {
            MinTimeBetweenSounds = CRAZY_MIN_TIME;
            MaxTimeBetweenSounds = CRAZY_MAX_TIME;
        }

        void PlaySound(int soundIndex)
        {
            AudioSource audioSourcePlayer = gameObject.GetComponent<AudioSource>();

            if (audioSourcePlayer == null)
            {
                TobogangMod.Logger.LogError("Couldn't find an AudioSourcePlayer to play RandomSound");
                return;
            }

            if (soundIndex >= Sounds.Count)
            {
                TobogangMod.Logger.LogError("Sounds not loaded or invalid sound provided");
                return;
            }

            audioSourcePlayer.PlayOneShot(Sounds[soundIndex]);
        }
    }
}
