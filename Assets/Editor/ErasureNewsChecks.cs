using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class ErasureNewsChecks
    {
        public static SurvivalState AfterGarak(SurvivalDefinitions data,uint seed=123)
        {
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,seed);
            s.Backpack.Add("radio",1); s.Story.HasRadio=true; s.ListenRadio(data);
            s.Travel("supply_site",data,()=>1); s.ApproachCrowd(data); s.ChooseCrowdResponse(true,data);
            s.ContinueCrowdStory(data); s.ContinueCrowdStory(data); s.ContinueCrowdStory(data);
            return s;
        }
        [MenuItem("Vanishing Days/Validate daily erasure news")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0;
            Action<bool,string> check=(ok,why)=>{checks++; if(!ok) throw new Exception(why);};
            check(data.erasureNews.locations.Length==50,"Wrong catalog size.");
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); s.hour=23; s.AdvanceTime(1,data);
            check(s.day==2 && s.ErasureHistory.Count==0,"News started before acquiring a radio.");
            s.Backpack.Add("radio",1); s.Story.HasRadio=true; s.hour=23; s.AdvanceTime(1,data);
            check(s.ErasureHistory.Count==0,"Radio alone unlocked daily news.");
            s.Travel("home",data,()=>1); s.ListenRadio(data); s.hour=23; s.AdvanceTime(1,data);
            check(s.ErasureHistory.Count==0,"Broadcast/rumor alone unlocked daily news.");
            s=AfterGarak(data); uint rng=s.RandomState; s.GenerateDailyErasureNews(data);
            check(s.ErasureNewsEnabled && s.ErasureHistory.Count==0,"Meeting immediately generated same-day news.");
            s.hour=23; s.AdvanceTime(1,data);
            check(s.day==2 && s.ErasureHistory.Count==1 && s.HasUnreadErasureNews && s.NextUnreadErasureNews.day==2,"Midnight did not queue a news bulletin.");
            check(s.RandomState==rng,"News changed encounter RNG.");
            string first=s.ErasureHistory[0].locationId; uint newsRng=s.ErasureRandomState;
            s.GenerateDailyErasureNews(data); s.AdvanceTime(1,data);
            check(s.ErasureHistory.Count==1 && s.ErasureRandomState==newsRng,"Same-day generation duplicated/rerolled news.");
            int hour=s.hour; s.AcknowledgeErasureNews(); check(!s.HasUnreadErasureNews && s.hour==hour && s.RandomState==rng,"Reading news changed time/RNG.");
            var twin=AfterGarak(data); twin.hour=23; twin.AdvanceTime(1,data);
            check(twin.ErasureHistory[0].locationId==first && twin.ErasureRandomState==newsRng,"News is not deterministic for a saved seed.");
            int hungerRate=data.TEMP_BALANCE.hungerPerHour;
            try { data.TEMP_BALANCE.hungerPerHour=0; s.AdvanceTime(48,data); }
            finally { data.TEMP_BALANCE.hungerPerHour=hungerRate; }
            check(s.ErasureHistory.Count==3 && s.NextUnreadErasureNews.day==3,"Long actions skipped a day or lost queue order.");
            s.AcknowledgeErasureNews(); check(s.NextUnreadErasureNews.day==4,"Reading skipped an unread day.");
            string path=Path.GetFullPath("Builds/Validation/erasure-news-save.json"); var store=new SessionSaveStore(path,data);
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            store.Save(s); check(store.TryLoad(out var loaded,out _) && loaded.ErasureHistory.Count==3 && loaded.ErasureReadDay==3 && loaded.ErasureRandomState==s.ErasureRandomState,"News history/read state/RNG did not restore.");
            s.hour=23; loaded.hour=23; s.AdvanceTime(1,data); loaded.AdvanceTime(1,data);
            check(s.ErasureHistory[3].locationId==loaded.ErasureHistory[3].locationId,"Reload changed tomorrow's report.");
            s=AfterGarak(data); var seen=new HashSet<string>(); int seoul=0, towns=0;
            for(int day=2;day<=51;day++)
            {
                s.day=day; s.GenerateDailyErasureNews(data); var report=s.ErasureHistory[s.ErasureHistory.Count-1];
                check(seen.Add(report.locationId),"A place disappeared twice.");
                var place=data.erasureNews.Find(report.locationId);
                if(place.region=="서울") seoul++; else towns++;
            }
            check(seen.Count==50 && seoul==20 && towns==30,"Random pool failed to cover all 50 locations.");
            s.day=52; s.GenerateDailyErasureNews(data);
            check(s.ErasureHistory.Count==51 && s.ErasureHistory[50].locationId=="" && s.ErasureHeadline(s.ErasureHistory[50],data).Contains("없음"),"Exhausted pool repeated a place/crashed.");
            store.Save(s); check(store.TryLoad(out _,out _),"Full 50-place history failed save validation.");
            var old=AfterGarak(data); old.schemaVersion=6; old.day=10; old.Vitals.AdjustMental(-21); old.HomeStorage.Remove("spam",2);
            File.WriteAllText(path,JsonUtility.ToJson(old));
            check(store.TryLoad(out var migrated,out _) && migrated.schemaVersion==10 && migrated.ErasureHistory.Count==0 && migrated.LastErasureDay==10 && migrated.Vitals.CurrentMental==79 && migrated.HomeStorage.Count("spam")==3,"Existing session migration reset vitals/supplies or backfilled news.");
            migrated.hour=23; migrated.AdvanceTime(1,data);
            check(migrated.NextUnreadErasureNews.day==11,"Migrated session did not start at next midnight.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            migrated.ErasureHistory.Add(new ErasureReport { day=11,locationId=migrated.ErasureHistory[0].locationId });
            File.WriteAllText(path,JsonUtility.ToJson(migrated)); check(!store.TryLoad(out _,out _),"Duplicate report/date accepted.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            Debug.Log("VD_ERASURE_NEWS_CHECKS_OK: "+checks+" assertions; gates, midnight, deterministic independent RNG, 50 unique sites, unread queue, exhausted pool and legacy migration.");
        }
    }
}
