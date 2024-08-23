using LCSoundTool;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class RandomSound : MonoBehaviour
    {
        public EnemyAI enemy;

        public float probabilityIncreasePerSecond = 0.01f;


        private float timeSinceLastSound = 0f;

        private AudioSource audioSource = null;

        private void Start()
        {
            if (enemy == null)
            {
                return;
            }

            audioSource = enemy.gameObject.AddComponent<AudioSource>();
            audioSource.clip = SoundTool.GetAudioClip("TobogangMod", "bruh.mp3");
            audioSource.loop = false;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 50f;
            audioSource.spatialBlend = 1.0f;
        }


        private void Update()
        {
            if (enemy == null)
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

                audioSource.Play();

                TobogangMod.Logger.LogInfo(enemy.name + " has spawned a sound");
            }
        }
    }
}
