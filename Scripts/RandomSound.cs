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
        public EnemyAI enemy;

        public float probabilityIncreasePerSecond = 0.01f;

        public static List<string> Sounds = null!;


        private float timeSinceLastSound = 0f;

        private void Start()
        {
            TobogangMod.Logger.LogInfo("New RandomSound spawned");

            if (Sounds == null)
            {
                Sounds = new();

                foreach (var file in Directory.GetFiles(Path.Combine(Paths.PluginPath, "TobogangMod")))
                {
                    Sounds.Add(Path.GetFileName(file));
                }
            }
        }


        private void Update()
        {
            bool isServer = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (!isServer || enemy == null)
            {
                return;
            }

            float prevSecs = (float)Math.Floor(timeSinceLastSound);
            timeSinceLastSound += Time.deltaTime;
            float currSecs = (float)Math.Floor(timeSinceLastSound);

            float proba = currSecs * probabilityIncreasePerSecond;

            if (currSecs > prevSecs && UnityEngine.Random.Range(0f, 1f) <= proba)
            {
                timeSinceLastSound = 0f;

                var e = new Model.RandomSoundEvent
                {
                    SoundIndex = UnityEngine.Random.RandomRangeInt(0, Sounds.Count - 1),
                    EnemyID = enemy.gameObject.GetComponent<NetworkObject>().NetworkObjectId
                };
                NetworkHandler.Instance.EventClientRpc(e);

                TobogangMod.Logger.LogInfo(enemy.name + " has spawned a sound");
            }
        }

        public static void PlaySoundForEnemy(int soundIndex, ulong enemyID)
        {
            GameObject enemy = RetrieveGameObject(enemyID);

            if (enemy == null)
            {
                TobogangMod.Logger.LogError("Failed to find ennemy " + enemyID + " to play random sound " + soundIndex);
                return;
            }

            AudioSourcePlayer audioSourcePlayer = enemy.GetComponent<AudioSourcePlayer>();

            if (audioSourcePlayer == null)
            {
                TobogangMod.Logger.LogError("Enemy didn't have an AudioSourcePlayer");
                return;
            }

            if (Sounds == null || soundIndex >= Sounds.Count)
            {
                TobogangMod.Logger.LogError("Sounds not loaded or invalid sound provided");
                return;
            }

            audioSourcePlayer.Play(SoundTool.GetAudioClip("TobogangMod", Sounds[soundIndex]));
        }

        static GameObject RetrieveGameObject(ulong networkObjectId)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
            {
                return netObj.gameObject;
            }

            Debug.LogError("Could not find GameObject with NetworkObjectId: " + networkObjectId);
            return null;
        }
    }
}
