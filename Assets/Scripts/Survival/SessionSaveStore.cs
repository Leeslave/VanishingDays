using System;
using System.IO;
using UnityEngine;

namespace VanishingDays
{
    public sealed class SessionSaveStore
    {
        public string Path { get; }
        readonly SurvivalDefinitions definitions;
        public bool Exists => File.Exists(Path) || File.Exists(Path+".bak");
        public SessionSaveStore(string path,SurvivalDefinitions data) { Path=path; definitions=data; }
        public void Save(SurvivalState state)
        {
            Validate(state,definitions);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temp=Path+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(state,true));
            if(File.Exists(Path)) File.Replace(temp,Path,Path+".bak"); else File.Move(temp,Path);
        }
        public bool TryLoad(out SurvivalState state,out string error)
        {
            error=""; state=null;
            foreach(string path in new[]{Path,Path+".bak"})
            {
                if(!File.Exists(path)) continue;
                try { var restored=JsonUtility.FromJson<SurvivalState>(File.ReadAllText(path)); if(restored?.PendingEncounter!=null && string.IsNullOrEmpty(restored.PendingEncounter.kind)) restored.PendingEncounter=null; Validate(restored,definitions); restored.UpgradeMedicalLoot(definitions); state=restored; return true; }
                catch(Exception e) { error=e.Message; }
            }
            return false;
        }
        public static void Validate(SurvivalState s,SurvivalDefinitions data)
        {
            if(s==null || s.schemaVersion!=3 || s.day<1 || s.day>365000 || s.hour<0 || s.hour>=24 || data.Location(s.CurrentLocation)==null || !Enum.IsDefined(typeof(GameDifficulty),s.Difficulty)) throw new InvalidDataException("Invalid session header.");
            if(s.Vitals==null || s.Vitals.BaseMaxHP<=0 || s.Vitals.CurrentHP<0 || s.Vitals.CurrentHP>s.Vitals.BaseMaxHP || s.Vitals.Hunger<0 || s.Vitals.Hunger>100 || s.Vitals.Injuries==null || s.Story==null || s.Locations==null || s.Locations.Count!=4 || s.journal==null || s.Equipment==null || s.InstalledSecurity==null || s.RandomState==0) throw new InvalidDataException("Invalid session state.");
            if(s.InjuryRestProgress<0 || s.InjuryRestProgress>=data.TEMP_BALANCE.injuryRestHours || s.MedicalLootVersion<0 || s.MedicalLootVersion>1) throw new InvalidDataException("Invalid recovery progress.");
            ValidateInventory(s.Backpack,data); ValidateInventory(s.HomeStorage,data);
            foreach(var location in data.locations)
            {
                var matches=s.Locations.FindAll(x=>x!=null && x.id==location.id);
                if(matches.Count!=1 || matches[0].searches<0) throw new InvalidDataException("Invalid location progress.");
                ValidateInventory(matches[0].remaining,data);
            }
            foreach(var id in s.Equipment) if(data.Item(id)==null || (data.Item(id).strength==0 && data.Item(id).defense==0) || s.Equipment.FindAll(x=>x==id).Count!=1) throw new InvalidDataException("Invalid equipment.");
            foreach(var id in s.InstalledSecurity) if(data.Security(id)==null || s.InstalledSecurity.FindAll(x=>x==id).Count!=1) throw new InvalidDataException("Invalid security.");
            foreach(var injury in s.Vitals.Injuries) if(injury==null || float.IsNaN(injury.recoveryPenalty) || injury.recoveryPenalty<0 || injury.recoveryPenalty>1) throw new InvalidDataException("Invalid injury.");
            if(float.IsNaN(s.Vitals.HungerPenalty) || s.Vitals.HungerPenalty<0 || s.Vitals.HungerPenalty>1) throw new InvalidDataException("Invalid hunger penalty.");
            if(s.AwayFromHome!=(s.CurrentLocation!="home")) throw new InvalidDataException("Invalid away state.");
            int radios=s.Backpack.Count("radio")+s.HomeStorage.Count("radio");
            if(radios>1 || s.Story.HasRadio!=(radios==1) || (!s.Story.HasRadio && (s.Story.HasHeardSPBroadcast || s.Story.HasMetDongrim || s.Story.DongrimRescueCompleted || s.Story.HasMetJiwoo || s.Story.JoinedLiberationFront || s.Story.HasMetSeokhyeon)) || s.Story.DongrimRescueCompleted!=s.Story.HasMetDongrim) throw new InvalidDataException("Invalid story gates.");
            if(s.PendingEncounter!=null && s.PendingEncounter.kind!="hungry") throw new InvalidDataException("Invalid pending encounter.");
        }
        static void ValidateInventory(Inventory inventory,SurvivalDefinitions data)
        {
            if(inventory?.stacks==null) throw new InvalidDataException("Missing inventory.");
            foreach(var stack in inventory.stacks) if(stack==null || data.Item(stack.id)==null || stack.amount<0 || inventory.stacks.FindAll(x=>x.id==stack.id).Count!=1) throw new InvalidDataException("Invalid inventory.");
        }
    }
}
