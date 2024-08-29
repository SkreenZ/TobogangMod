using System;
using System.Collections.Generic;
using System.Text;
using TobogangMod.Scripts.Items;

namespace TobogangMod.Scripts
{
    public class TobogangTaGueule : TobogangItem
    {
        TobogangTaGueule()
        {
            TobogangItemId = TobogangMod.TobogangItems.TA_GUEULE;
            CoinguesPrice = 150;
            Keywords = ["ta gueule", "ta gueul", "ta gueu", "ta gue", "ta gu", "ta g", "tg", "tagueule", "tagueul", "tagueu", "tague", "tagu", "tag"];
        }
    }
}
