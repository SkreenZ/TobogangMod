using BepInEx;
using LCSoundTool;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class RandomSound : NetworkBehaviour
    {
        public static List<string> Sounds = [];

        public float MinTimeBetweenSounds = 30f;

        public float MaxTimeBetweenSounds = 300f;

        public static GameObject NetworkPrefab = null!;


        private float _timeUntilNextSound = -1f;

        public static void Init()
        {
            NetworkPrefab = LethalLib.Modules.NetworkPrefabs.CloneNetworkPrefab(TobogangMod.NetworkPrefab, "RandomSound");
            NetworkPrefab.AddComponent<RandomSound>();
            NetworkPrefab.AddComponent<AudioSourcePlayer>();

            if (Sounds.Count == 0)
            {
                foreach (var file in Directory.GetFiles(Path.Combine(Paths.PluginPath, "TobogangMod/sounds")))
                {
                    Sounds.Add(Path.GetFileName(file));
                }
            }
        }

        private void Start()
        {
            TobogangMod.Logger.LogDebug("New RandomSound spawned");

            // 1% chance to be crazy
            if (UnityEngine.Random.Range(0f, 1f) <= 0.01f && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                MinTimeBetweenSounds = 1f;
                MaxTimeBetweenSounds = 5f;
            }

            _timeUntilNextSound = UnityEngine.Random.Range(MinTimeBetweenSounds, MaxTimeBetweenSounds);
        }


        private void Update()
        {
            bool isServer = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (!isServer)
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

        [ClientRpc]
        void PlaySoundClientRpc(int soundIndex)
        {
            PlaySound(soundIndex);
        }

        void PlaySound(int soundIndex)
        {
            AudioSourcePlayer audioSourcePlayer = gameObject.GetComponent<AudioSourcePlayer>();

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

            audioSourcePlayer.Play(SoundTool.GetAudioClip("TobogangMod/sounds", Sounds[soundIndex]));
        }
    }
}
