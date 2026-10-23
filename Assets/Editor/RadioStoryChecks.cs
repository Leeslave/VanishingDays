using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
#endif

namespace VanishingDays.Editor
{
    public static class RadioStoryChecks
    {
        static SurvivalState WithRadio(SurvivalDefinitions data)
        {
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            s.Backpack.Add("radio",1); s.Story.HasRadio=true; return s;
        }
        static SurvivalState AtSite(SurvivalDefinitions data)
        {
            var s=WithRadio(data); s.ListenRadio(data); s.Travel("supply_site",data,()=>1); return s;
        }
        public static int ValidateData(SurvivalDefinitions data)
        {
            data.Validate(); int count=0;
            Action<bool,string> check=(ok,why)=>{count++; if(!ok) throw new Exception(why);};
            var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); int hour=s.hour; uint rng=s.RandomState;
            s.ListenRadio(data); s.Travel("supply_site",data,()=>0); s.ApproachCrowd(data);
            check(!s.Story.HasHeardSPBroadcast && !s.Story.SupplyRumorUnlocked && s.CurrentLocation=="home" && s.hour==hour && s.RandomState==rng && !s.AwayFromHome,"Pre-radio gates leaked.");
            s=WithRadio(data); s.Rest(data); check(!s.Story.SupplyRumorUnlocked && !s.Story.CrowdIncidentStarted && !s.Story.HasMetDongrim,"Radio acquisition auto-started story.");
            s.Travel("store",data,()=>1); s.ListenRadio(data); check(!s.Story.HasHeardSPBroadcast,"First broadcast played outside home.");
            s=WithRadio(data); hour=s.hour; int day=s.day; rng=s.RandomState; s.ListenRadio(data);
            check(s.Story.HasHeardSPBroadcast && s.Story.SupplyRumorUnlocked && s.IsLocationUnlocked("supply_site",data),"Broadcast did not unlock rumor/site.");
            check(s.hour==hour && s.day==day && s.RandomState==rng && !s.Story.HasMetDongrim && !s.Story.CrowdIncidentStarted,"Broadcast changed clock/RNG/incident.");
            int cards=s.journal.FindAll(x=>x.Contains("[정보 카드]")).Count;
            check(cards==1 && s.LastRadioBroadcastId==data.radioStory.firstBroadcast.id,"First broadcast/card missing.");
            s.ListenRadio(data); check(s.journal.FindAll(x=>x.Contains("[정보 카드]")).Count==1 && s.LastRadioBroadcastId==data.radioStory.generalBroadcast.id,"Repeat radio duplicated clue or missed general broadcast.");
            s.Rest(data); check(!s.Story.CrowdIncidentStarted,"Staying home started incident.");
            s=AtSite(data); check(s.Story.SupplySiteVisited && !s.Story.CrowdIncidentStarted && !s.Story.HasMetDongrim,"Visiting site auto-triggered rescue.");
            hour=s.hour; rng=s.RandomState; int stock=s.Backpack.Count("radio"); s.Search(data,()=>0);
            check(s.hour==hour && s.RandomState==rng && s.Backpack.Count("radio")==stock && s.Locations.Find(x=>x.id=="supply_site").searches==0,"Story site generated loot/time/encounter.");
            s.ObserveSupplySite(data); check(s.Crowd.Step==CrowdStoryStep.Observing && !s.HasActiveCrowdIncident && !s.Story.HasMetDongrim,"Observation entered mandatory scene.");
            s.Travel("home",data,()=>1); check(!s.Story.HasMetDongrim && !s.Story.CrowdIncidentStarted,"Leaving site triggered rescue.");
            s.Travel("supply_site",data,()=>1); check(s.CanApproachCrowd(data),"Revisit after leaving cannot approach.");
            s=WithRadio(data); s.ListenRadio(data); s.hour=data.TEMP_BALANCE.nightStarts-data.Location("supply_site").travelHours; s.Travel("supply_site",data,()=>1);
            check(s.IsNight(data.TEMP_BALANCE) && !s.CanApproachCrowd(data),"Arrival at dusk incorrectly allowed crowd.");
            hour=s.hour; s.ApproachCrowd(data); s.ObserveSupplySite(data); check(s.hour==hour && !s.Story.CrowdIncidentStarted,"Night approach changed incident/time.");
            s.Travel("home",data,()=>1); s.hour=4; s.Travel("supply_site",data,()=>1); check(!s.IsNight(data.TEMP_BALANCE) && s.CanApproachCrowd(data),"Arrival at dawn not allowed.");
            foreach(bool drop in new[]{true,false})
            {
                s=AtSite(data); s.Backpack.Add("tuna",3); hour=s.hour; rng=s.RandomState;
                s.ApproachCrowd(data); check(s.Crowd.Step==CrowdStoryStep.Panic && s.HasActiveCrowdIncident && !s.CanAct,"Approach did not lock incident.");
                s.Travel("home",data,()=>0); s.Search(data,()=>0); s.Rest(data); s.AdvanceTime(24,data); s.TreatInjury("bandage",data);
                check(s.hour==hour && s.CurrentLocation=="supply_site" && s.RandomState==rng,"Incident allowed survival actions/time/RNG.");
                s.ContinueCrowdStory(data); check(s.Crowd.Step==CrowdStoryStep.Panic,"Panic skipped response.");
                int hp=s.Vitals.CurrentHP; s.ChooseCrowdResponse(drop,data);
                check(s.Crowd.Step==CrowdStoryStep.Rescue && s.Backpack.Count("radio")==1,"Response did not guarantee rescue/protect radio.");
                check(s.Backpack.Count("tuna")==3-(drop?data.radioStory.TEMP_BALANCE.dropBagLoss:data.radioStory.TEMP_BALANCE.keepBagLoss),"Response lost wrong supplies.");
                check(drop?s.Vitals.CurrentHP==hp && s.Vitals.Injuries.Count==0:s.Vitals.CurrentHP<hp && s.Vitals.Injuries.Count==1,"Choice injury difference missing.");
                int supplies=s.Backpack.Count("tuna"), injuries=s.Vitals.Injuries.Count; s.ChooseCrowdResponse(drop,data);
                check(s.Backpack.Count("tuna")==supplies && s.Vitals.Injuries.Count==injuries,"Choice applied twice.");
                s.ContinueCrowdStory(data); check(s.Crowd.Step==CrowdStoryStep.Disappearance && !s.Story.HasMetDongrim,"Disappearance step/gate wrong.");
                s.ContinueCrowdStory(data); check(s.Crowd.Step==CrowdStoryStep.Introduction && !s.Story.HasMetDongrim,"Introduction completed early.");
                s.ContinueCrowdStory(data); check(s.Story.HasMetDongrim && s.Story.DongrimRescueCompleted && s.Story.CrowdIncidentResolved && s.Story.DongrimContactHintReceived && s.CanAct,"Meeting did not complete/release survival loop.");
                check(!s.journal.Exists(x=>x.Contains("{PlayerNickname}")) && s.journal.Exists(x=>x.Contains("저는 안재현이라고 합니다")),"Script name placeholder leaked or missed fallback.");
                check(s.PlayerNickname=="" && s.ProtagonistScriptName=="안재현" && !s.Story.JoinedLiberationFront && !s.Story.HasMetJiwoo && !s.Story.HasMetSeokhyeon,"Meeting modified profile or jumped to P1-B.");
                check(s.hour==hour && s.day==1 && s.RandomState==rng,"Mandatory scene forced time/RNG.");
                int journal=s.journal.Count; s.ContinueCrowdStory(data); s.ApproachCrowd(data); s.ChooseCrowdResponse(!drop,data);
                check(s.journal.Count==journal && s.Backpack.Count("tuna")==supplies,"Completed incident replayed/rewarded.");
                s.Travel("home",data,()=>1); s.Travel("supply_site",data,()=>1); check(!s.CanApproachCrowd(data),"Revisiting replayed meeting.");
            }
            s=AtSite(data); s.Vitals.CurrentHP=1; s.Vitals.Hunger=100; s.ApproachCrowd(data); s.ChooseCrowdResponse(false,data);
            s.ContinueCrowdStory(data); s.ContinueCrowdStory(data); s.ContinueCrowdStory(data);
            check(!s.IsGameOver && s.Vitals.CurrentHP==1 && s.Story.HasMetDongrim,"Mandatory scene killed a 1-HP starving player.");
            s.PlayerNickname="민수"; check(s.Script("{PlayerNickname}(이)라고")=="민수라고","Vowel-final nickname particle wrong.");
            s.PlayerNickname="재현"; check(s.Script("{PlayerNickname}: {PlayerNickname}(이)라고")=="재현: 재현이라고","Consonant-final nickname particle wrong.");
            return count;
        }
#if UNITY_EDITOR
        [MenuItem("Vanishing Days/Validate radio to Dongrim v0.4")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int count=ValidateData(data);
            string path=Path.GetFullPath("Builds/Validation/radio-story-save-test.json"); var saves=new SessionSaveStore(path,data);
            foreach(bool drop in new[]{true,false})
            {
                var s=AtSite(data); s.PlayerNickname="기록자닉네임"; s.Backpack.Add("tuna",3); s.ObserveSupplySite(data);
                saves.Save(s); if(!saves.TryLoad(out s,out _) || s.Crowd.Step!=CrowdStoryStep.Observing) throw new Exception("Observation restore failed.");
                s.ApproachCrowd(data);
                for(int step=0;step<5;step++)
                {
                    var expected=s.Crowd.Step; int tuna=s.Backpack.Count("tuna"), hp=s.Vitals.CurrentHP;
                    saves.Save(s); if(!saves.TryLoad(out s,out _) || s.Crowd.Step!=expected || s.Backpack.Count("tuna")!=tuna || s.Vitals.CurrentHP!=hp || s.PlayerNickname!="기록자닉네임") throw new Exception("Mid-story save/restore failed: "+expected);
                    if(expected==CrowdStoryStep.Panic) s.ChooseCrowdResponse(drop,data); else s.ContinueCrowdStory(data);
                }
                if(!s.Story.HasMetDongrim || s.Story.JoinedLiberationFront) throw new Exception("Restored meeting completion failed.");
                if(!s.journal.Exists(x=>x.Contains("저는 기록자닉네임이라고 합니다"))) throw new Exception("Meeting did not use the restored nickname.");
            }
            var legacy=WithRadio(data); legacy.schemaVersion=3; legacy.Locations.RemoveAll(x=>x.id=="supply_site"); legacy.Crowd=null;
            File.WriteAllText(path,JsonUtility.ToJson(legacy));
            if(!saves.TryLoad(out var migrated,out _) || migrated.schemaVersion!=4 || migrated.Locations.Count!=5 || migrated.Story.SupplyRumorUnlocked || migrated.HomeStorage.Count("bandage")!=3 || !migrated.Story.HasRadio) throw new Exception("v3 migration failed.");
            migrated.ListenRadio(data); saves.Save(migrated); if(!saves.TryLoad(out migrated,out _) || !migrated.Story.SupplyRumorUnlocked) throw new Exception("Migrated radio unlock failed.");
            var preRadio=SurvivalState.NewGame(GameDifficulty.HARD,data,99); preRadio.schemaVersion=3; preRadio.Locations.RemoveAll(x=>x.id=="supply_site"); preRadio.Crowd=null;
            File.WriteAllText(path,JsonUtility.ToJson(preRadio));
            if(!saves.TryLoad(out var beforeRadio,out _) || beforeRadio.Story.HasRadio || beforeRadio.Story.SupplyRumorUnlocked || beforeRadio.CurrentLocation!="home" || beforeRadio.HomeStorage.Count("bandage")!=1) throw new Exception("Pre-radio legacy migration gave story/items.");
            legacy.Story.HasHeardSPBroadcast=true; legacy.Story.CompleteDongrimRescue(true);
            File.WriteAllText(path,JsonUtility.ToJson(legacy));
            if(!saves.TryLoad(out var alreadyMet,out _) || !alreadyMet.Story.HasMetDongrim || alreadyMet.Crowd.Step!=CrowdStoryStep.Completed || alreadyMet.CanApproachCrowd(data)) throw new Exception("Legacy completed meeting was replayed/reset.");
            // Corrupted cross-stage saves must fall back, never consume costs a second time.
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            var invalid=AtSite(data); invalid.ApproachCrowd(data); invalid.Crowd.Step=CrowdStoryStep.Rescue;
            File.WriteAllText(path,JsonUtility.ToJson(invalid)); if(saves.TryLoad(out _,out _)) throw new Exception("Missing response was accepted.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            Debug.Log("VD_RADIO_STORY_CHECKS_OK: "+count+" assertions; both choices restored at every stage; v3 migration and invalid-stage rejection.");
        }
#endif
    }
}
