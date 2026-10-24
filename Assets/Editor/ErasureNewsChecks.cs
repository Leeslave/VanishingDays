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
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); s.hour=7; s.AdvanceTime(1,data);
            check(s.MorningIssues.Count==0,"News started before radio acquisition.");
            s.Backpack.Add("radio",1); s.Story.HasRadio=true; s.hour=23; s.AdvanceTime(1,data);
            check(s.ErasureHistory.Count==0 && s.Diaries.Count==1,"Midnight generated news instead of diary.");
            s.AdvanceTime(8,data); check(s.MorningIssues.Count==1 && s.MorningIssues[0].kind=="first" && !s.Story.SupplyRumorUnlocked,"First morning did not preserve unplayed first broadcast.");
            s.ReadMorning(s.day,data); check(s.Story.SupplyRumorUnlocked && !s.Story.HasMetDongrim,"Morning first broadcast skipped actual Garak encounter.");
            uint rng=s.RandomState; s.day++; s.hour=8; s.PrepareMorning(data);
            check(s.ErasureHistory.Count==1 && !s.Story.CrowdIncidentResolved && s.HasPendingMorning,"Regular news still required Garak completion.");
            check(s.RandomState==rng,"News changed encounter RNG.");
            string first=s.ErasureHistory[0].locationId; uint newsRng=s.ErasureRandomState;
            s.PrepareMorning(data); check(s.ErasureHistory.Count==1 && s.ErasureRandomState==newsRng,"Same morning duplicated news.");
            int hour=s.hour; s.ReadMorning(s.day,data); check(s.hour==hour && s.RandomState==rng,"Reading changed time/RNG.");
            string path=Path.GetFullPath("Builds/Validation/erasure-news-save.json"); var store=new SessionSaveStore(path,data);
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            store.Save(s); check(store.TryLoad(out var loaded,out _) && loaded.ErasureHistory[0].locationId==first && loaded.ErasureRandomState==newsRng,"Morning save changed erasure report/RNG.");
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
            check(store.TryLoad(out var migrated,out _) && migrated.schemaVersion==12 && migrated.ErasureHistory.Count==0 && migrated.LastErasureDay==10 && migrated.Vitals.CurrentMental==79 && migrated.HomeStorage.Count("spam")==3,"Existing session migration reset vitals/supplies or backfilled news.");
            migrated.hour=23; migrated.AdvanceTime(9,data);
            check(migrated.NextUnreadErasureNews.day==11,"Migrated session did not start at next midnight.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            migrated.ErasureHistory.Add(new ErasureReport { day=11,locationId=migrated.ErasureHistory[0].locationId });
            File.WriteAllText(path,JsonUtility.ToJson(migrated)); check(!store.TryLoad(out _,out _),"Duplicate report/date accepted.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            Debug.Log("VD_ERASURE_NEWS_CHECKS_OK: "+checks+" assertions; gates, midnight, deterministic independent RNG, 50 unique sites, unread queue, exhausted pool and legacy migration.");
        }
    }
}
