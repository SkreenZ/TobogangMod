using LCSoundTool;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class AudioSourcePlayer : MonoBehaviour
    {
        AudioSource audioSource;

        void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = false;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 50f;
            audioSource.spatialBlend = 1.0f;
        }

        public void Play(AudioClip audioClip)
        {
            if (audioClip == null)
            {
                TobogangMod.Logger.LogError("Tried to play an AudioSourcePlayer with a null AudioClip");
                return;
            }

            audioSource.PlayOneShot(audioClip);
        }
    }
}
