using System;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class RandomSound : NetworkBehaviour
    {
        public EnemyAI enemy;

        public float probabilityIncreasePerSecond = 0.01f;


        private float timeSinceLastSound = 0f;


        private void Start()
        {
            TobogangMod.Logger.LogInfo("New RandomSound spawned");

            if (enemy == null)
            {
                return;
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
                    Name = "bruh.mp3",
                    EnemyID = enemy.gameObject.GetComponent<NetworkObject>().NetworkObjectId
                };
                NetworkHandler.Instance.EventClientRpc(e);

                TobogangMod.Logger.LogInfo(enemy.name + " has spawned a sound");
            }
        }

        public static void PlaySoundForEnemy(string soundName, ulong enemyID)
        {
            GameObject enemy = RetrieveGameObject(enemyID);

            if (enemy == null)
            {
                TobogangMod.Logger.LogError("Failed to find ennemy " + enemyID + " to play random sound " + soundName);
                return;
            }

            AudioSourcePlayer audioSourcePlayer = enemy.GetComponent<AudioSourcePlayer>();

            if (audioSourcePlayer == null)
            {
                TobogangMod.Logger.LogError("Enemy didn't have an AudioSourcePlayer");
                return;
            }

            audioSourcePlayer.Play(soundName);
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
