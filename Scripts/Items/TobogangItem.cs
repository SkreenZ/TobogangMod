using System;
using System.Collections.Generic;
using System.Text;

namespace TobogangMod.Scripts.Items
{
    public class TobogangItem : PhysicsProp
    {
        public string TobogangItemId { get; protected set; } = "";
        public int CoinguesPrice { get; set; } = 0;
        public string[] Keywords { get; protected set; } = [];
    }
}
