using System;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Model
{
    public class RandomSoundEvent : INetworkSerializable
    {
        public String Name { get; set; }

        public ulong EnemyID { get; set; }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            string name = Name;
            serializer.SerializeValue(ref name);
            Name = name;

            ulong enemyID = EnemyID;
            serializer.SerializeValue(ref enemyID);
            EnemyID = enemyID;
        }

        public override string ToString()
        {
            return Name + " at " + EnemyID;
        }
    }
}
