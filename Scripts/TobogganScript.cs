using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TobogangMod.Scripts
{
    public class TobogganScript : NetworkBehaviour
    {
        private static readonly string TIP_TEXT = "Faire une offrande ({} coingues) : [E]";
        private static readonly string DISABLED_TIP_TEXT = "[Besoin de {} coingues pour faire une offrande]";
        private static readonly float MULTIPLIER_MULTIPLIER = 0.9f;
        private static readonly float MIN_MULTIPLIER = 1.2f;

        private InteractTrigger _interact = null!;
        private int _currentCost = 100;
        private float _currentMultiplier = 2f;
        private bool _isRunning = false;
        private PlayerControllerB _localPlayer = null!;

        private ParticleSystem _circleParticles = null!;
        private ParticleSystem _failParticles = null!;
        private ParticleSystem _successParticles = null!;
        private ParticleSystem _outerParticles = null!;
        private ParticleSystem _innerParticles = null!;

        private AudioSource _audioSource = null!;

        void Awake()
        {
            _interact = gameObject.GetComponentInChildren<InteractTrigger>();
            _localPlayer = StartOfRound.Instance.localPlayerController;

            _interact.onInteract = new InteractEvent();
            _interact.onInteract.AddListener(this, GetType().GetMethod(nameof(OnInteract)));

            _circleParticles = transform.Find("ItemLocation/Circle").gameObject.GetComponent<ParticleSystem>();
            _failParticles = transform.Find("ItemLocation/HitFail").gameObject.GetComponent<ParticleSystem>();
            _successParticles = transform.Find("ItemLocation/HitSuccess").gameObject.GetComponent<ParticleSystem>();
            _outerParticles = transform.Find("ItemLocation/Outer").gameObject.GetComponent<ParticleSystem>();
            _innerParticles = transform.Find("ItemLocation/Inner").gameObject.GetComponent<ParticleSystem>();

            _audioSource = transform.Find("ItemLocation").gameObject.GetComponent<AudioSource>();

            var successParticlesMain = _successParticles.main;
            successParticlesMain.simulationSpeed = 0.5f;

            var failParticlesMain = _failParticles.main;
            failParticlesMain.simulationSpeed = 0.8f;
        }

        void Update()
        {
            _interact.interactable = !_isRunning && CoinguesManager.Instance.GetCoingues(_localPlayer) >= _currentCost;

            UpdateTips();
        }

        private void UpdateTips()
        {
            _interact.holdTip = TIP_TEXT.Replace("{}", $"{_currentCost}");
            _interact.hoverTip = TIP_TEXT.Replace("{}", $"{_currentCost}");
            _interact.disabledHoverTip = _isRunning ? "[Offrande en cours]" : DISABLED_TIP_TEXT.Replace("{}", $"{_currentCost}");
        }

        public void OnInteract(PlayerControllerB player)
        {
            CoinguesManager.Instance.RemoveCoinguesServerRpc(player.NetworkObject, _currentCost);

            StartOffrandeServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartOffrandeServerRpc()
        {
            TobogangMod.Logger.LogDebug("interact with toboggan");

            StartOffrandeClientRpc();
        }

        [ClientRpc]
        private void StartOffrandeClientRpc()
        {
            _isRunning = true;

            StartCoroutine(MakeOffrande());
        }

        [ClientRpc]
        private void FinishOffrandeClientRpc(ulong spawnedItemId)
        {
            if (spawnedItemId == TobogangMod.NULL_OBJECT)
            {
                _failParticles.Play();
            }
            else
            {
                _successParticles.Play();
                _audioSource.PlayOneShot(TobogangMod.SuccessClip);

                var spawnedItem = TobogangMod.TryGet(spawnedItemId);

                if (spawnedItem != null)
                {
                    spawnedItem.gameObject.GetComponent<GrabbableObject>().scrapPersistedThroughRounds = true;
                }
            }

            _isRunning = false;
            _currentCost = (int)Math.Round(_currentCost * _currentMultiplier);
            _currentMultiplier = Math.Max(_currentMultiplier * MULTIPLIER_MULTIPLIER, MIN_MULTIPLIER);
        }

        private IEnumerator MakeOffrande()
        {
            _audioSource.Play();

            _circleParticles.Play();
            _outerParticles.Play();

            yield return new WaitForSeconds(5f);

            _innerParticles.Play();

            yield return new WaitForSeconds(5f);

            _circleParticles.Stop();

            yield return new WaitForSeconds(1f);

            _innerParticles.Stop();

            yield return new WaitForSeconds(0.5f);

            _outerParticles.Stop();

            if (!IsServer)
            {
                yield break;
            }

            yield return new WaitForSeconds(0.5f);

            if (Random.Range(0, 3) == 0)
            {
                // 33% chance to fail
                FinishOffrandeClientRpc(TobogangMod.NULL_OBJECT);
                yield break;
            }

            var allScraps = StartOfRound.Instance.allItemsList.itemsList.FindAll(item => item.isScrap && item.minValue > 10 && item.itemId == 0 && item.spawnPrefab != null && item.spawnPrefab.GetComponentsInChildren<ScanNodeProperties>() != null);

            var spawnedItem = CoinguesManager.Instance.SpawnScrap(allScraps[Random.Range(0, allScraps.Count)], transform.Find("ItemLocation").position + Vector3.up * 3, true);

            if (spawnedItem != null)
            {
                FinishOffrandeClientRpc(spawnedItem.GetComponent<NetworkObject>().NetworkObjectId);
            }
        }
    }
}
