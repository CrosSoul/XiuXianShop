using System;
using UnityEngine;

namespace XiuXianShop
{
    // Fixed integer units avoid floating-point drift. Precision and shell price must be configured.
    [Serializable]
    public sealed class SpiritResourceDefinition
    {
        [SerializeField] int capacityUnits;
        [SerializeField] int unitsPerEquivalent;
        [SerializeField] int containerPrice;
        [SerializeField] bool reusable;
        public int CapacityUnits => capacityUnits;
        public int UnitsPerEquivalent => unitsPerEquivalent;
        public int ContainerPrice => containerPrice;
        public bool Reusable => reusable;

        public SpiritResourceDefinition(int capacityUnits,int unitsPerEquivalent,int containerPrice,bool reusable)
        {
            if(capacityUnits<=0 || unitsPerEquivalent<=0 || containerPrice<0)throw new ArgumentOutOfRangeException(nameof(capacityUnits));
            this.capacityUnits=capacityUnits;this.unitsPerEquivalent=unitsPerEquivalent;
            this.containerPrice=containerPrice;this.reusable=reusable;
        }
    }
}
