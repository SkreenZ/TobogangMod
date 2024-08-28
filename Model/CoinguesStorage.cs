using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace TobogangMod.Model
{
    [Serializable]
    public class CoinguesStorage : INetworkSerializable
    {
        private Dictionary<string, int> _coingues = new Dictionary<string, int>();

        public int this[string key]
        {
            get => _coingues.ContainsKey(key) ? _coingues[key] : 0;
            set => _coingues[key] = value;
        }

        public void Add(string key, int value)
        {
            if (_coingues.ContainsKey(key))
            {
                _coingues[key] += value;
            }
            else
            {
                _coingues[key] = value;
            }
        }

        public bool Remove(string key)
        {
            return _coingues.Remove(key);
        }

        public bool TryGetValue(string key, out int value)
        {
            return _coingues.TryGetValue(key, out value);
        }

        public bool ContainsKey(string key)
        {
            return _coingues.ContainsKey(key);
        }

        public void Clear()
        {
            _coingues.Clear();
        }

        public Dictionary<string, int>.KeyCollection Keys => _coingues.Keys;
        public Dictionary<string, int>.ValueCollection Values => _coingues.Values;

        // INetworkSerializable implementation
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serialize or deserialize the dictionary length
            int count = _coingues.Count;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader)
            {
                // Deserialize the dictionary
                _coingues = new Dictionary<string, int>();
                for (int i = 0; i < count; i++)
                {
                    string key = string.Empty;
                    int value = 0;

                    serializer.SerializeValue(ref key);
                    serializer.SerializeValue(ref value);

                    _coingues[key] = value;
                }
            }
            else
            {
                // Serialize the dictionary
                foreach (var kvp in _coingues)
                {
                    string key = kvp.Key;
                    int value = kvp.Value;

                    serializer.SerializeValue(ref key);
                    serializer.SerializeValue(ref value);
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("CoinguesStorage contents:");

            foreach (var kvp in _coingues)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            return sb.ToString();
        }
    }
}
