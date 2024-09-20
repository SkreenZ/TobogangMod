using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace TobogangMod.Scripts
{
    public class AutoDestroy : MonoBehaviour
    {
        public float TimeToLive = 5f;

        private float _timeAlive = 0f;

        void Update()
        {
            _timeAlive += Time.deltaTime;

            if (_timeAlive >= TimeToLive)
            {
                Destroy(gameObject);
            }
        }
    }
}
