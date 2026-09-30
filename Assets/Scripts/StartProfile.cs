using System;

namespace XiuXianShop
{
    [Serializable] public sealed class StartProfile : AuthoredRecord
    {
        public string title;
        public int year,month,money,stamina;
    }
    [Serializable] public sealed class StartItem : AuthoredRecord
    {
        public string profileId,itemId;
        public int quantity;
        public ContainerId area;
        public ContentOptionalInt x,y,rotation;
    }
    [Serializable] public sealed class StartState : AuthoredRecord
    {
        public string profileId,type,stateId;
        public bool value;
    }
}
