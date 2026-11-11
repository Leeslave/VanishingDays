using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class SurvivalBalanceChecks
    {
        [MenuItem("Vanishing Days/Validate survival balance and automatic water")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load();int count=0;
            Action<bool,string> check=(ok,why)=>{count++;if(!ok)throw new Exception(why);};
            Func<SurvivalState> fresh=()=>SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            foreach(int duration in new[]{2,5,10})
            {
                var s=fresh();s.InstalledFacilities.Add("water_collector");int start=s.hour;uint rng=s.RandomState;
                s.StartWaterCollection(duration);check(s.hour==start && s.CollectorElapsedHours==0,"Starting collector spent time.");
                s.AdvanceTime(duration-1,data);check(!s.CollectorReady && !s.CollectorNoticePending,"Collector finished early.");
                int elapsed=s.CollectorElapsedHours;s.StartWaterCollection(2);check(s.CollectorCycleHours==duration && s.CollectorElapsedHours==elapsed,"Starting again reset active collection.");
                int raw=s.HomeStorage.Count("raw_water");s.CollectWater();check(s.HomeStorage.Count("raw_water")==raw,"Unfinished collector paid out.");
                s.CurrentLocation="store";s.AwayFromHome=true;s.Backpack.Add("water",10);s.AdvanceTime(1,data);
                check(s.CollectorReady && s.CollectorNoticePending && s.CollectorCapacity==SurvivalState.CollectorYield(duration) && s.RandomState==rng,"Away collection or completion notification failed.");
                s.CollectWater();check(s.HomeStorage.Count("raw_water")==raw,"Collected tank while away.");
                s.AdvanceTime(3,data);check(s.CollectorElapsedHours==duration && s.HomeStorage.Count("raw_water")==raw,"Full collector overflowed into storage.");
                s.CurrentLocation="home";s.AwayFromHome=false;start=s.hour;s.CollectWater();
                check(s.hour==start && s.HomeStorage.Count("raw_water")==raw+SurvivalState.CollectorYield(duration) && !s.CollectorReady && !s.CollectorNoticePending,"Collection spent time/lost water/did not reset.");
                s.CollectWater();check(s.HomeStorage.Count("raw_water")==raw+SurvivalState.CollectorYield(duration),"Double collection duplicated water.");
                check(s.CanStartCollector,"Empty collector could not restart.");
            }
            for(int level=1;level<=3;level++)
            {
                var s=fresh();s.InstalledFacilities.Add("water_filter");if(level>=2)s.InstalledFacilities.Add("water_filter2");if(level==3)s.InstalledFacilities.Add("water_filter3");
                s.HomeStorage.Add("raw_water",14);int hour=s.hour,water=s.HomeStorage.Count("water");
                s.Craft("purify_water",data);s.Craft("purify_water",data);
                check(s.HomeStorage.Count("raw_water")==2 && s.HomeStorage.Count("water")==water+2*(level+3) && s.hour==hour+2,"Purifier yield/loss/time incorrect.");
                s.Craft("purify_water",data);check(s.HomeStorage.Count("raw_water")==2,"Purifier spent incomplete batch.");
            }
            var state=fresh();state.StartWaterCollection(2);check(state.CollectorCycleHours==0,"Missing collector allowed collection.");
            state.InstalledFacilities.Add("water_collector");state.StartWaterCollection(3);check(state.CollectorCycleHours==0,"Unsupported collection duration accepted.");
            state.PendingEncounter=new Encounter{kind="hungry"};state.StartWaterCollection(2);check(state.CollectorCycleHours==0,"Collector bypassed encounter.");
            foreach(string item in new[]{"chocolate","soda","energy_bar"})
            {
                state=fresh();state.CurrentLocation="store";state.AwayFromHome=true;state.Vitals.Hunger=50;state.Thirst=60;state.HydrationHours=8;state.Backpack.Add(item,1);int hour=state.hour;uint rng=state.RandomState;
                state.Eat(item,data);check(state.hour==hour && state.RandomState==rng && state.Backpack.Count(item)==0 && state.Vitals.Hunger==50-data.Item(item).hungerRelief,"Portable food time/cost/effect failed.");
                if(item=="soda")check(state.Thirst==20 && state.HydrationHours==0,"Soda did not postpone next drinking need.");
                state=fresh();state.CurrentLocation="store";state.AwayFromHome=true;state.HomeStorage.Add(item,1);state.Eat(item,data);check(state.HomeStorage.Count(item)==1,"Away consumption accessed home food.");
            }
            state=fresh();state.CurrentLocation="school";state.AwayFromHome=true;state.UnlockedLocations.AddRange(new[]{"empty_house","school"});
            state.hour=11;check(state.SearchNightWarning(data)=="","Safe search warned.");
            state.hour=14;int before=state.hour;uint random=state.RandomState;check(state.HomeArrivalAfterSearch(data).Hour==18 && state.SearchNightWarning(data)!="","Exact 18:00 return did not warn.");
            state.hour=16;check(state.CalendarDate.AddHours(state.hour+data.Location("school").travelHours).Hour==18 && state.SearchNightWarning(data).Contains("20:00"),"16:00 search/return warning incorrect.");
            state.hour=18;check(state.SearchNightWarning(data).Contains("이미 야간"),"Already-night search did not warn.");
            state.hour=23;check(state.HomeArrivalAfterSearch(data).Day==state.CalendarDate.AddDays(1).Day && state.SearchNightWarning(data)!="","Midnight search warning lost date.");
            check(state.RandomState==random && state.hour==23,"Warning preview mutated state.");
            string path=Path.GetFullPath("Builds/Validation/balance-save.json");Directory.CreateDirectory(Path.GetDirectoryName(path));var store=new SessionSaveStore(path,data);
            Action clean=()=>{foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);};
            try
            {
                clean();state=fresh();state.InstalledFacilities.AddRange(new[]{"water_collector","water_filter"});state.StartWaterCollection(5);state.AdvanceTime(3,data);store.Save(state);
                check(store.TryLoad(out state,out _) && state.CollectorCycleHours==5 && state.CollectorElapsedHours==3,"Load reset collector progress.");
                state.AdvanceTime(2,data);store.Save(state);check(store.TryLoad(out state,out _) && state.CollectorReady && state.CollectorNoticePending,"Load lost full tank/notification.");
                state.CollectWater();store.Save(state);check(store.TryLoad(out state,out _) && state.HomeStorage.Count("raw_water")==SurvivalState.CollectorYield(5) && state.CollectorCycleHours==0,"Load duplicated/reset recovered water.");
                clean();state=fresh();state.schemaVersion=12;state.InstalledFacilities.AddRange(new[]{"water_collector","water_filter"});state.Thirst=50;state.HydrationHours=5;state.Vitals.Hunger=32;state.Sleepiness=48;state.Backpack.Add("tuna",2);
                foreach(var location in state.Locations)location.remaining.stacks.RemoveAll(x=>x.id=="chocolate" || x.id=="soda" || x.id=="energy_bar");
                state.Locations.Find(x=>x.id=="store").remaining.Remove("water",4);File.WriteAllText(path,JsonUtility.ToJson(state));
                check(store.TryLoad(out state,out _) && state.schemaVersion==13 && state.HasFacility("water_filter") && state.Thirst==50 && state.HydrationHours==5 && state.Vitals.Hunger==32 && state.Sleepiness==48 && state.Backpack.Count("tuna")==2,"v12 migration reset progression/vitals/facilities.");
                check(state.Locations.Find(x=>x.id=="store").remaining.Count("water")==0 && state.Locations.Find(x=>x.id=="store").remaining.Count("chocolate")==4,"Migration replenished water or omitted snacks.");
                state.Locations.Find(x=>x.id=="store").remaining.Remove("chocolate",4);store.Save(state);check(store.TryLoad(out state,out _) && state.Locations.Find(x=>x.id=="store").remaining.Count("chocolate")==0,"Reload replenished snacks.");
                clean();state=fresh();state.CollectorCycleHours=2;File.WriteAllText(path,JsonUtility.ToJson(state));check(!store.TryLoad(out _,out _),"Save accepted tank without facility.");
                state.InstalledFacilities.Add("water_collector");state.CollectorElapsedHours=3;File.WriteAllText(path,JsonUtility.ToJson(state));check(!store.TryLoad(out _,out _),"Save accepted overflow progress.");
            }
            finally{clean();}
            Debug.Log("VD_SURVIVAL_BALANCE_OK: "+count+" assertions; passive timed tanks, exact purifier yields, portable food, night boundaries and v12 migration.");
        }
    }
}
