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
                try { var restored=JsonUtility.FromJson<SurvivalState>(File.ReadAllText(path)); if(restored?.PendingEncounter!=null && string.IsNullOrEmpty(restored.PendingEncounter.kind)) restored.PendingEncounter=null; UpgradeLegacySession(restored,definitions); Validate(restored,definitions); restored.UpgradeMedicalLoot(definitions); state=restored; return true; }
                catch(Exception e) { error=e.Message; }
            }
            return false;
        }
        public static void Validate(SurvivalState s,SurvivalDefinitions data)
        {
            if(s==null || s.schemaVersion!=4 || s.day<1 || s.day>365000 || s.hour<0 || s.hour>=24 || data.Location(s.CurrentLocation)==null || !Enum.IsDefined(typeof(GameDifficulty),s.Difficulty)) throw new InvalidDataException("Invalid session header.");
            if(s.Vitals==null || s.Vitals.BaseMaxHP<=0 || s.Vitals.CurrentHP<0 || s.Vitals.CurrentHP>s.Vitals.BaseMaxHP || s.Vitals.Hunger<0 || s.Vitals.Hunger>100 || s.Vitals.Injuries==null || s.Story==null || s.Locations==null || s.Locations.Count!=data.locations.Length || s.journal==null || s.Equipment==null || s.InstalledSecurity==null || s.RandomState==0) throw new InvalidDataException("Invalid session state.");
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
            ValidateStory(s,data);
        }
        static void UpgradeLegacySession(SurvivalState s,SurvivalDefinitions data)
        {
            if(s==null || s.schemaVersion!=3) return;
            if(s.Locations==null || s.Locations.Count!=4 || s.Story==null) throw new InvalidDataException("Invalid legacy world.");
            foreach(string id in new[]{"home","store","empty_house","school"})
                if(s.Locations.FindAll(x=>x!=null && x.id==id).Count!=1) throw new InvalidDataException("Invalid legacy location.");
            s.Locations.Add(new LocationProgress { id="supply_site" });
            s.Crowd=new CrowdStoryProgress(); s.LastRadioBroadcastId="";
            if(s.Story.HasMetDongrim && s.Story.DongrimRescueCompleted)
            {
                s.Crowd.Step=CrowdStoryStep.Completed;
                s.Story.HasHeardSPBroadcast=true; s.Story.SupplyRumorUnlocked=true;
                s.Story.SupplySiteVisited=true; s.Story.CrowdIncidentStarted=true;
                s.Story.CrowdIncidentResolved=true; s.Story.DongrimContactHintReceived=true;
            }
            s.schemaVersion=4;
        }
        static void ValidateStory(SurvivalState s,SurvivalDefinitions data)
        {
            var f=s.Story; var c=s.Crowd;
            if(c==null || !Enum.IsDefined(typeof(CrowdStoryStep),c.Step) || !s.IsLocationUnlocked(s.CurrentLocation,data)) throw new InvalidDataException("Invalid story position.");
            if(f.SupplyRumorUnlocked && (!f.HasRadio || !f.HasHeardSPBroadcast)) throw new InvalidDataException("Invalid rumor gate.");
            if((f.SupplySiteVisited || c.Step!=CrowdStoryStep.None) && !f.SupplyRumorUnlocked) throw new InvalidDataException("Invalid site gate.");
            if(f.CrowdIncidentStarted!=(c.Step>=CrowdStoryStep.Panic) || f.CrowdIncidentResolved!=(c.Step==CrowdStoryStep.Completed) || f.DongrimContactHintReceived!=f.CrowdIncidentResolved || f.HasMetDongrim!=f.CrowdIncidentResolved) throw new InvalidDataException("Invalid crowd completion.");
            if(c.Step!=CrowdStoryStep.None && !f.SupplySiteVisited) throw new InvalidDataException("Unvisited crowd scene.");
            if(s.HasActiveCrowdIncident && (s.CurrentLocation!="supply_site" || s.PendingEncounter!=null || s.IsGameOver)) throw new InvalidDataException("Invalid active incident.");
            if(c.BagChoice!="" && c.BagChoice!="drop" && c.BagChoice!="hold" && c.BagChoice!=null) throw new InvalidDataException("Invalid crowd choice.");
            if(c.Step>=CrowdStoryStep.Rescue && c.Step<CrowdStoryStep.Completed && string.IsNullOrEmpty(c.BagChoice)) throw new InvalidDataException("Missing crowd choice.");
            if(!string.IsNullOrEmpty(s.LastRadioBroadcastId) && (s.LastRadioBroadcastId!=data.radioStory.firstBroadcast.id && s.LastRadioBroadcastId!=data.radioStory.generalBroadcast.id || !f.SupplyRumorUnlocked)) throw new InvalidDataException("Invalid broadcast id.");
        }
        static void ValidateInventory(Inventory inventory,SurvivalDefinitions data)
        {
            if(inventory?.stacks==null) throw new InvalidDataException("Missing inventory.");
            foreach(var stack in inventory.stacks) if(stack==null || data.Item(stack.id)==null || stack.amount<0 || inventory.stacks.FindAll(x=>x.id==stack.id).Count!=1) throw new InvalidDataException("Invalid inventory.");
        }
    }
}
