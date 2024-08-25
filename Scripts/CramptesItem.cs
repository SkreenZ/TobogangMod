using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class CramptesItem : PhysicsProp
    {
        private void OnTriggerEnter(Collider other)
        {
            var player = other.gameObject.GetComponent<PlayerControllerB>();
            TobogangMod.Logger.LogDebug("Cramptes trigger enter");

            if (player == null)
            {
                return;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            TobogangMod.Logger.LogDebug("Cramptes collision enter");
        }
    }
}
