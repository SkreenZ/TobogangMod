using BepInEx;
using LCSoundTool;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class RandomSound : NetworkBehaviour
    {
        private EnemyAI _enemy;
        public EnemyAI Enemy
        {
            get
            {
                return _enemy;
            }
            set
            {
                AssignEnemyServerRpc(value.NetworkObjectId);
            }
        }

        public static List<string> Sounds = null!;

        public float MinTimeBetweenSounds = 30f;

        public float MaxTimeBetweenSounds = 300f;

        public static GameObject NetworkPrefab = null!;


        private float timeUntilNextSound = -1f;

        public static void Init()
        {
            NetworkPrefab = LethalLib.Modules.NetworkPrefabs.CloneNetworkPrefab(TobogangMod.NetworkPrefab, "RandomSound");
            NetworkPrefab.AddComponent<RandomSound>();
        }

        private void Start()
        {
            TobogangMod.Logger.LogDebug("New RandomSound spawned");

            if (Sounds == null)
            {
                Sounds = new();

                foreach (var file in Directory.GetFiles(Path.Combine(Paths.PluginPath, "TobogangMod/sounds")))
                {
                    Sounds.Add(Path.GetFileName(file));
                }
            }

            // 1% chance to be crazy
            if (UnityEngine.Random.Range(0f, 1f) <= 0.01f && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                MinTimeBetweenSounds = 1f;
                MaxTimeBetweenSounds = 5f;
            }

            timeUntilNextSound = UnityEngine.Random.Range(MinTimeBetweenSounds, MaxTimeBetweenSounds);
        }


        private void Update()
        {
            bool isServer = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (!isServer || Enemy == null)
            {
                return;
            }

            timeUntilNextSound -= Time.deltaTime;

            if (timeUntilNextSound <= 0f)
            {
                timeUntilNextSound = UnityEngine.Random.Range(MinTimeBetweenSounds, MaxTimeBetweenSounds);

                int soundIndex = UnityEngine.Random.RandomRangeInt(0, Sounds.Count - 1);

                PlaySoundClientRpc(soundIndex);

                TobogangMod.Logger.LogInfo(Enemy.name + " has spawned a sound");
            }
        }

        [ServerRpc]
        void AssignEnemyServerRpc(ulong enemyId)
        {
            AssignEnemyClientRpc(enemyId);
        }

        [ClientRpc]
        void AssignEnemyClientRpc(ulong enemyId)
        {
            var networkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[enemyId];
            _enemy = networkObject.GetComponent<EnemyAI>();
        }

        [ClientRpc]
        void PlaySoundClientRpc(int soundIndex)
        {
            PlaySoundForEnemy(soundIndex);
        }

        void PlaySoundForEnemy(int soundIndex)
        {
            if (Enemy == null)
            {
                TobogangMod.Logger.LogError("Failed to find ennemy to play random sound " + soundIndex);
                return;
            }

            AudioSourcePlayer audioSourcePlayer = Enemy.GetComponent<AudioSourcePlayer>();

            if (audioSourcePlayer == null)
            {
                TobogangMod.Logger.LogError("Enemy didn't have an AudioSourcePlayer for enemy " + Enemy);
                return;
            }

            if (Sounds == null || soundIndex >= Sounds.Count)
            {
                TobogangMod.Logger.LogError("Sounds not loaded or invalid sound provided");
                return;
            }

            audioSourcePlayer.Play(SoundTool.GetAudioClip("TobogangMod/sounds", Sounds[soundIndex]));
        }
    }
}
