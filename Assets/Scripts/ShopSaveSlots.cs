using System;
using System.IO;
using UnityEngine;

namespace XiuXianShop
{
    // Slot files only. Session owns the snapshot and all gameplay validation.
    public sealed class ShopSaveSlots
    {
        public readonly string DirectoryPath;
        public readonly int ManualSlotCount;
        public ShopSaveSlots(string directory,int manualSlotCount)
        {
            if(manualSlotCount<1)throw new ArgumentOutOfRangeException(nameof(manualSlotCount));
            DirectoryPath=directory;ManualSlotCount=manualSlotCount;
        }
        // Slot zero is automatic; 1..ManualSlotCount are manual.
        public string PathFor(int slot)
        {
            if(slot<0 || slot>ManualSlotCount)throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(DirectoryPath,slot==0?"auto.json":"manual-"+slot+".json");
        }
        public bool Exists(int slot)=>File.Exists(PathFor(slot));
        public string Read(int slot)=>File.ReadAllText(PathFor(slot));
        public void Write(int slot,ShopSession session)
        {
            var snapshot=JsonUtility.FromJson<ShopSave>(session.CaptureSave());
            snapshot.savedAtUtc=DateTime.UtcNow.ToString("O");
            WriteAtomic(PathFor(slot),JsonUtility.ToJson(snapshot,true));
        }
        public static void WriteAtomic(string path,string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path+".tmp",json);
            if(File.Exists(path))File.Replace(path+".tmp",path,null);
            else File.Move(path+".tmp",path);
        }
    }
}
