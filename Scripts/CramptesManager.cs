using GameNetcodeStuff;
using LCSoundTool;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    internal class CramptesManager : NetworkBehaviour
    {
        public static CramptesManager Instance { get; private set; }

        public static GameObject NetworkPrefab { get; private set; }

        public PlayerControllerB CurrentCramptesPlayer { get; private set; } = null!;

        private static readonly float CRAMPTES_CHANCE_ON_DAMAGE = 1f; //0.05f;
        private static readonly float PROBA_INCREASE = 0.01f; // 0.00001f; // Average 395s

        private float timeUntilIncrease = 1f;
        private float currentProba = PROBA_INCREASE;
        private bool isProccing = false;
        private float clipTimer = 0f;

        public static void Init()
        {
            NetworkPrefab = LethalLib.Modules.NetworkPrefabs.CloneNetworkPrefab(TobogangMod.NetworkPrefab, "CramptesManager");
            NetworkPrefab.AddComponent<CramptesManager>();
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                TobogangMod.Logger.LogError("Multiple CramptesManager spawned!");
            }
        }

        void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (isProccing)
            {
                if (CurrentCramptesPlayer == null)
                {
                    isProccing = false;
                    return;
                }

                clipTimer -= Time.deltaTime;

                if (clipTimer <= 0f)
                {
                    FinishProcClientRpc();
                }

                return;
            }

            if (CurrentCramptesPlayer == null || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving || CurrentCramptesPlayer.isPlayerDead)
            {
                return;
            }

            timeUntilIncrease -= Time.deltaTime;

            if (timeUntilIncrease > 0f)
            {
                return;
            }

            timeUntilIncrease = 1f;

            if (Random.Range(0f, 1f) <= currentProba)
            {
                ProcClientRpc();
                return;
            }

            currentProba += PROBA_INCREASE;
        }

        [ServerRpc(RequireOwnership = false)]
        public void TryGiveCramptesOnDamageServerRpc(ulong playerId)
        {
            if (Random.Range(0f, 1f) <= CRAMPTES_CHANCE_ON_DAMAGE)
            {
                SetCramptesPlayerClientRpc(playerId);
            }
        }

        [ClientRpc]
        void SetCramptesPlayerClientRpc(ulong playerNetId, bool announce = true)
        {
            if (CurrentCramptesPlayer != null && CurrentCramptesPlayer.NetworkObjectId == playerNetId)
            {
                return;
            }

            string lastCramptesPlayer = CurrentCramptesPlayer != null ? CurrentCramptesPlayer.playerUsername : "";

            CurrentCramptesPlayer = playerNetId == 0 ? null : GetPlayer(playerNetId);
            currentProba = PROBA_INCREASE;

            if (CurrentCramptesPlayer != null)
            {
                TobogangMod.Logger.LogDebug("Given cramptés to " + CurrentCramptesPlayer.playerUsername);

                if (announce)
                {
                    HUDManager.Instance.DisplayGlobalNotification($"{CurrentCramptesPlayer.playerUsername} a choppé les cramptés");
                }
            }
            else if (lastCramptesPlayer != "")
            {
                TobogangMod.Logger.LogDebug("Removed cramptés from " + lastCramptesPlayer);

                if (announce)
                {
                    HUDManager.Instance.DisplayGlobalNotification($"{lastCramptesPlayer} s'est débarassé des cramptés");
                }
            }
        }

        [ClientRpc]
        void ProcClientRpc()
        {
            TobogangMod.Logger.LogDebug("Proc cramptés for " + CurrentCramptesPlayer.playerUsername);

            isProccing = true;

            AudioClip clip = SoundTool.GetAudioClip("TobogangMod/sounds", RandomSound.Sounds[Random.RandomRangeInt(0, RandomSound.Sounds.Count - 1)]);
            CurrentCramptesPlayer.GetComponent<AudioSourcePlayer>().Play(clip);
            clipTimer = clip.length;
        }

        [ClientRpc]
        void FinishProcClientRpc()
        {
            isProccing = false;

            Landmine.SpawnExplosion(CurrentCramptesPlayer.transform.position + Vector3.up, true, 5.7f, 6f);

            SetCramptesPlayerClientRpc(0, false);
        }

        static PlayerControllerB GetPlayer(ulong playerNetId)
        {
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerNetId].GetComponent<PlayerControllerB>();
        }
    }
}
