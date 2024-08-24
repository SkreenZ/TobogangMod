using System;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Model
{
    public class RandomSoundEvent : INetworkSerializable
    {
        public int SoundIndex { get; set; }

        public ulong EnemyID { get; set; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int soundIndex = SoundIndex;
            serializer.SerializeValue(ref soundIndex);
            SoundIndex = soundIndex;

            ulong enemyID = EnemyID;
            serializer.SerializeValue(ref enemyID);
            EnemyID = enemyID;
        }

        public override string ToString()
        {
            return SoundIndex + " at " + EnemyID;
        }
    }
}
