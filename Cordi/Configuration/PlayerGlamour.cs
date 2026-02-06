using System;
using System.Collections.Generic;

namespace Cordi.Configuration;

[Serializable]
public class PlayerGlamour
{
    public DateTime CapturedAt { get; set; } = DateTime.Now;
    public GearItem MainHand { get; set; } = new();
    public GearItem OffHand { get; set; } = new();
    public GearItem Head { get; set; } = new();
    public GearItem Body { get; set; } = new();
    public GearItem Hands { get; set; } = new();
    public GearItem Legs { get; set; } = new();
    public GearItem Feet { get; set; } = new();
    public GearItem Ears { get; set; } = new();
    public GearItem Neck { get; set; } = new();
    public GearItem Wrists { get; set; } = new();
    public GearItem LeftRing { get; set; } = new();
    public GearItem RightRing { get; set; } = new();

    [Serializable]
    public class GearItem
    {
        public uint ItemId { get; set; }
        public byte StainId { get; set; }
        public bool IsHq { get; set; }

        public GearItem() { }

        public GearItem(uint itemId, byte stainId, bool isHq = false)
        {
            ItemId = itemId;
            StainId = stainId;
            IsHq = isHq;
        }
    }
}
