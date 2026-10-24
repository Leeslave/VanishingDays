using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class WorldProgressionChecks
    {
        static SurvivalState FirstMeeting(SurvivalDefinitions data)
        {
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            s.UnlockedLocations.AddRange(new[]{"empty_house","school"});
            s.Backpack.Add("radio",1); s.Story.HasRadio=true; s.ListenRadio(data);
            s.Travel("supply_site",data,()=>1); s.ApproachCrowd(data); s.ChooseCrowdResponse(true,data);
            s.ContinueCrowdStory(data); s.ContinueCrowdStory(data); s.ContinueCrowdStory(data);
            return s;
        }
        [MenuItem("Vanishing Days/Validate world progression v0.5")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int count=0;
            Action<bool,string> check=(ok,why)=>{count++; if(!ok) throw new Exception(why);};
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); int hour=s.hour; uint rng=s.RandomState;
            s.Travel("school",data,()=>0);
            check(s.CurrentLocation=="home" && s.hour==hour && s.RandomState==rng,"Locked route spent time/RNG.");
            check(!s.LootSummary("store",data,true).Contains("1 / 1") && !s.IsLocationUnlocked("empty_house",data),"Unvisited loot/unlocks leaked.");
            s.Travel("store",data,()=>1); s.Search(data,()=>1);
            check(s.IsLocationUnlocked("empty_house",data) && !s.IsLocationUnlocked("school",data),"Store unlock skipped a stage.");
            check(s.LootPercent("store",data)>0 && s.LootPercent("store",data)<100 && s.HasSearchWork("store",data),"Remaining water did not keep the store searchable.");
            var storePool=s.Locations.Find(x=>x.id=="store").remaining; foreach(var stack in storePool.stacks) stack.amount=0;
            check(s.LootPercent("store",data)==100 && s.LootSummary("store",data).Contains("소진"),"Exhausted store status wrong.");
            hour=s.hour; rng=s.RandomState; s.Search(data,()=>0);
            check(s.hour==hour && s.RandomState==rng && s.PendingEncounter==null,"Empty search spent time/triggered robbery.");
            s.Travel("empty_house",data,()=>1); s.Search(data,()=>1);
            check(s.IsLocationUnlocked("school",data) && !s.IsLocationUnlocked("pharmacy",data),"House unlock skipped pharmacy gate.");
            check(s.LootPercent("empty_house",data)>0 && s.LootPercent("empty_house",data)<100 && s.Locations.Find(x=>x.id=="empty_house").remaining.Count("wood")==7 && !s.LootSummary("empty_house",data,true).Contains("목재"),"Remaining loot leaked or progress was not exact.");
            s.Vitals.Hunger=0; s.Travel("school",data,()=>1); s.Search(data,()=>1); s.Search(data,()=>1);
            check(s.IsLocationUnlocked("pharmacy",data) && !s.Story.HasRadio,"Pharmacy/radio unlock order wrong.");
            s.Search(data,()=>1); check(s.Story.HasRadio && s.LootPercent("school",data)<100,"Radio or school progress failed.");
            s=FirstMeeting(data); check(s.TotalDongrimMeetings==1 && s.LastDongrimMeetingDay==s.day && !s.PendingDongrimMeeting,"First-meeting handoff failed.");
            s.Travel("empty_house",data,()=>1); check(!s.PendingDongrimMeeting,"Same-day repeat encounter fired.");
            s.Travel("home",data,()=>1); s.day=2; s.hour=20; s.Travel("empty_house",data,()=>1);
            check(!s.PendingDongrimMeeting,"Night repeat encounter fired.");
            s.Travel("home",data,()=>1); s.day=3; s.hour=8; s.Vitals.Hunger=0;
            s.Travel("empty_house",data,()=>0);
            check(s.PendingDongrimMeeting && s.PendingEncounter==null && !s.CanAct,"Authored repeat lost priority over random encounter.");
            check(s.DongrimDialogue(data).Contains("철물점") && !s.IsLocationUnlocked("hardware",data),"Second scene/unlock timing wrong.");
            hour=s.hour; rng=s.RandomState; s.Search(data,()=>0); s.Travel("home",data,()=>0); s.AdvanceTime(5,data); s.ListenRadio(data);
            check(s.hour==hour && s.RandomState==rng && s.CurrentLocation=="empty_house","Pending dialogue allowed survival actions.");
            string path=Path.GetFullPath("Builds/Validation/world-progression-save-test.json"); var saves=new SessionSaveStore(path,data);
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            saves.Save(s); check(saves.TryLoad(out s,out _) && s.PendingDongrimMeeting,"Pending repeat did not survive reload.");
            check(!s.CanTradeWithDongrim,"Trade enabled without carried food.");
            s.ResolveDongrimMeeting(true,data); check(s.PendingDongrimMeeting && s.TotalDongrimMeetings==1,"Invalid trade skipped scene.");
            s.Backpack.Add("tuna",1); int bandages=s.Backpack.Count("bandage"); s.ResolveDongrimMeeting(true,data);
            check(s.TotalDongrimMeetings==2 && s.IsLocationUnlocked("hardware",data) && !s.IsLocationUnlocked("shelter",data) && s.Backpack.Count("tuna")==0 && s.Backpack.Count("bandage")==bandages+1,"Second meeting trade/unlock wrong.");
            s.ResolveDongrimMeeting(true,data); check(s.TotalDongrimMeetings==2 && s.Backpack.Count("bandage")==bandages+1,"Dialogue reward repeated.");
            s.Travel("home",data,()=>1); s.day=4; s.hour=8; s.Vitals.Hunger=0; s.Travel("school",data,()=>1);
            check(s.PendingDongrimMeeting && s.DongrimDialogue(data).Contains("주민"),"Third meeting did not trigger at school.");
            s.ResolveDongrimMeeting(false,data);
            check(s.TotalDongrimMeetings==3 && s.IsLocationUnlocked("shelter",data) && !s.Story.JoinedLiberationFront && !s.Story.HasMetJiwoo,"Third meeting missed route or forced unrelated story.");
            s.Travel("home",data,()=>1); s.day=5; s.hour=8; s.Vitals.Hunger=0; s.Travel("empty_house",data,()=>1);
            check(!s.PendingDongrimMeeting,"Patrol cooldown shorter than two days.");
            s.Travel("home",data,()=>1); s.day=6; s.hour=8; s.Vitals.Hunger=0; s.Travel("empty_house",data,()=>1);
            check(s.PendingDongrimMeeting && s.DongrimDialogue(data).Contains("붕대"),"Recurring patrol missing.");
            s.ResolveDongrimMeeting(false,data); check(s.TotalDongrimMeetings==4 && s.DongrimMeetingLocation=="school" && s.NextDongrimDay==8,"Patrol rotation/cooldown wrong.");
            s.Travel("shelter",data,()=>1); s.Search(data,()=>1); check(s.Backpack.Count("rice")==100,"Rice pool did not use 100g search units.");
            s.Travel("home",data,()=>1); s.HomeStorage.Add("wood",20); s.HomeStorage.Add("tools",10); hour=s.hour;
            s.InstallSecurity("door3",data); check(s.DoorLevel==0 && s.hour==hour,"Door upgrade bypassed prerequisites.");
            double risk=s.HomeRaidRisk(data); s.InstallSecurity("door1",data); s.InstallSecurity("door2",data); s.InstallSecurity("door3",data);
            check(s.DoorLevel==3 && s.HomeRaidRisk(data)<risk && s.HomeStorage.Count("wood")==11 && s.HomeStorage.Count("tools")==4,"Door costs/protection wrong.");
            hour=s.hour; s.InstallSecurity("door3",data); check(s.hour==hour && s.HomeStorage.Count("wood")==11,"Door upgrade charged twice.");
            saves.Save(s); check(saves.TryLoad(out var restored,out _) && restored.DoorLevel==3 && restored.DongrimMeetings==4 && restored.LootPercent("store",data)==s.LootPercent("store",data),"World/door save round trip failed.");
            var legacy=FirstMeeting(data); legacy.schemaVersion=4; legacy.Locations.RemoveAll(x=>x.id=="pharmacy" || x.id=="hardware" || x.id=="shelter");
            legacy.Locations.Find(x=>x.id=="store").remaining.Remove("tuna",1); legacy.Locations.Find(x=>x.id=="store").searches=1; legacy.InstalledSecurity.Add("analog");
            legacy.day=7; File.WriteAllText(path,JsonUtility.ToJson(legacy));
            check(saves.TryLoad(out var migrated,out _) && migrated.schemaVersion==12 && migrated.Locations.Count==8 && migrated.TotalDongrimMeetings==1 && migrated.NextDongrimDay==7,"v4 completed-story migration failed.");
            check(migrated.Locations.Find(x=>x.id=="store").remaining.Count("tuna")==0 && migrated.InstalledSecurity.Contains("analog") && migrated.IsLocationUnlocked("school",data) && !migrated.IsLocationUnlocked("hardware",data),"Migration replenished loot/reset security/unlocked story too early.");
            saves.Save(migrated); check(saves.TryLoad(out migrated,out _) && migrated.Locations.Find(x=>x.id=="store").remaining.Count("tuna")==0,"Migration was not idempotent.");
            // A malformed current save must not bypass the authored encounter guards.
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            migrated.PendingDongrimMeeting=true; File.WriteAllText(path,JsonUtility.ToJson(migrated));
            check(!saves.TryLoad(out _,out _),"Invalid repeat encounter accepted.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            Debug.Log("VD_WORLD_PROGRESSION_CHECKS_OK: "+count+" assertions; staged routes, exact loot, repeat meetings, patrol cooldown, trade conservation, door costs and v4 migration.");
        }
    }
}
