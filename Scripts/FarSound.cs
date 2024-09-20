using UnityEngine;

namespace TobogangMod.Scripts
{
    public class FarSound : MonoBehaviour
    {
        public float MinDistance = 50f;
        public float MaxDistance = 100f;
        public AudioClip Clip = null!;

        private AudioSource _audioSource = null!;

        void Start()
        {
            if (Clip == null)
            {
                TobogangMod.Logger.LogError("NULL CLIP");
            }

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = Clip;
            _audioSource.loop = false;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.spatialBlend = 1f;

            UpdateDistance();

            _audioSource.Play();
        }

        void Update()
        {
            UpdateDistance();
        }

        void UpdateDistance()
        {
            var listener = StartOfRound.Instance.localPlayerController.gameObject.transform;

            _audioSource.minDistance = MinDistance;
            _audioSource.maxDistance = MaxDistance;
            _audioSource.volume = Vector3.Distance(listener.position, gameObject.transform.position) <= MinDistance ? 0f : 1f;
        }
    }
}