using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class DailyLifeChecks
    {
        static SurvivalState Ready(SurvivalDefinitions data)
        {
            var s=ErasureNewsChecks.AfterGarak(data);
            s.UnlockedLocations.AddRange(new[]{"empty_house","school"});
            s.Vitals.Hunger=0; s.Travel("home",data,()=>1); s.day=2; s.hour=8; s.Travel("empty_house",data,()=>1); s.ResolveDongrimMeeting(false,data);
            s.Travel("home",data,()=>1); s.day=3; s.hour=8; s.Vitals.Hunger=0; s.Travel("school",data,()=>1); s.ResolveDongrimMeeting(false,data);
            s.day=4; s.hour=8; return s;
        }
        [MenuItem("Vanishing Days/Validate daily life and first expedition")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0; int hunger=data.TEMP_BALANCE.hungerPerHour; data.TEMP_BALANCE.hungerPerHour=0;
            Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
            Func<SurvivalState> fresh=()=>SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            string path=Path.GetFullPath("Builds/Validation/daily-life-save.json"); var saves=new SessionSaveStore(path,data);
            Action clean=()=>{foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);}; clean();
            try
            {
                var s=fresh(); s.PlayerNickname="검증자"; s.hour=23; s.AdvanceTime(1,data);
                check(s.Diaries.Count==1 && s.NextDiary.day==1 && s.Diaries[0].body.Contains("검증자"),"Midnight diary/author missing.");
                check(!s.Diaries[0].body.Contains("안부") && !s.Diaries[0].body.Contains("잠을 잤다") && !s.Diaries[0].body.Contains("도착"),"Diary invented unperformed actions.");
                string frozen=s.Diaries[0].body; uint rng=s.RandomState; s.FinalizeDiary(1,data); s.ReadDiary(1);
                check(s.Diaries.Count==1 && s.Diaries[0].body==frozen && s.NextDiary==null && s.RandomState==rng,"Diary reading rerolled/duplicated time or content.");
                s=fresh(); s.Travel("store",data,()=>1); s.hour=23; s.Search(data,()=>1);
                check(s.Diaries.Count==1 && s.Diaries[0].body.Contains("수색을 시작") && !s.Diaries[0].body.Contains("생수"),"Cross-midnight search credited loot before finding it.");
                check(s.DailyFacts.Any(x=>x.day==2 && x.kind=="loot" && x.text.Contains("생수")),"Actual post-midnight loot was not tracked.");
                int facts=s.DailyFacts.Count; s.CurrentLocation="home"; s.AwayFromHome=false; s.Transfer("tuna",false,1,data);
                check(s.DailyFacts.Count==facts,"Storage clicking became a story event.");
                s=fresh(); s.HomeStorage.Add("water",100); s.AdvanceTime(50,data,true);
                check(s.Diaries.Count==2 && s.Diaries[0].body.Contains("16시간") && s.Diaries[1].body.Contains("24시간"),"Long sleep lost dates or assigned hours to the wrong diary.");
                s.ReadDiary(1); check(s.NextDiary.day==2,"Diary read queue skipped a date."); saves.Save(s);
                check(saves.TryLoad(out var restored,out _) && restored.NextDiary.day==2 && restored.Diaries[0].body==s.Diaries[0].body,"Unread diary state did not restore.");
                s=fresh(); s.hour=7; s.AdvanceTime(1,data); check(s.MorningIssues.Count==0,"Morning radio before acquisition.");
                s.Story.HasRadio=true; s.HomeStorage.Add("radio",1); s.CurrentLocation="store"; s.AwayFromHome=true; s.hour=7; s.AdvanceTime(1,data);
                check(s.MorningIssues.Count==1 && !s.HasPendingMorning && !s.Story.SupplyRumorUnlocked,"Remote home radio was heard or first broadcast auto-completed.");
                check(!s.ReadMorning(s.day,data),"Inaccessible radio could be read."); s.Travel("home",data,()=>1);
                check(s.HasPendingMorning && s.CanUseRadio,"Returning home lost queued broadcast.");
                int hour=s.hour; rng=s.RandomState; int water=s.HomeStorage.Count("water"); s.ReadMorning(s.day,data);
                check(s.Story.SupplyRumorUnlocked && !s.Story.HasMetDongrim && !s.Story.CrowdIncidentResolved,"First broadcast skipped actual market choices/rescue.");
                check(s.hour==hour && s.RandomState==rng && s.HomeStorage.Count("water")==water,"Listening spent time/water/RNG.");
                s.day++; s.hour=8; s.PrepareMorning(data); s.CurrentLocation="store"; s.AwayFromHome=true;
                check(!s.CanViewErasureReport(s.ErasureHistory.Last()),"Map leaked unheard news from a radio left at home.");
                s.ErasureReadDay=s.day; check(s.CanViewErasureReport(s.ErasureHistory.Last()),"Old heard news could not be reread outside.");
                s.CurrentLocation="home"; s.AwayFromHome=false;
                s.Transfer("radio",true,data); s.Travel("store",data,()=>1); s.Backpack.Add("water",100); s.AdvanceTime(48,data);
                check(s.MorningIssues.Count==4 && s.ErasureHistory.Count==3 && s.HasPendingMorning && !s.Story.CrowdIncidentResolved,"Morning gate/multi-day queue failed before Garak.");
                int issues=s.MorningIssues.Count; s.PrepareMorning(data); check(s.MorningIssues.Count==issues,"Same-date morning duplicated.");
                s.DismissMorning(); check(!s.HasPendingMorning && s.MorningIssues.Count(x=>!x.read)==3,"Inbox defer lost unread broadcasts.");
                saves.Save(s); check(saves.TryLoad(out restored,out _) && !restored.HasPendingMorning && restored.MorningIssues.Count(x=>!x.read)==3,"Inbox deferral not saved.");
                s.day++; s.hour=0; check(!s.HasPendingMorning,"Deferred morning news reopened at midnight.");
                s.hour=7; s.DismissMorning(); s.AdvanceTime(1,data); check(s.HasPendingMorning,"Closing old news before 08:00 suppressed the new morning issue.");
                string newsIds=string.Join(",",s.ErasureHistory.Select(x=>x.locationId)); s.hour=10; s.Travel("supply_site",data,()=>1); s.ApproachCrowd(data); s.ChooseCrowdResponse(true,data);
                s.ContinueCrowdStory(data); s.ContinueCrowdStory(data); s.ContinueCrowdStory(data);
                check(s.Story.CrowdIncidentResolved && string.Join(",",s.ErasureHistory.Select(x=>x.locationId))==newsIds,"Garak completion erased already-received morning news.");
                s=fresh(); s.Story.HasRadio=true; s.Backpack.Add("radio",1); s.Backpack.Add("water",100); s.AdvanceTime(50,data);
                check(s.MorningIssues.Count(x=>x.kind=="first")==1 && !s.Story.SupplyRumorUnlocked,"Unheard first broadcast duplicated or auto-unlocked.");
                s=fresh(); s.GreetNeighbor(data); check(s.NeighborStage==1 && s.NeighborGreetings==1 && s.DailyFacts.Any(x=>x.kind=="neighbor"),"Neighbor first encounter missing.");
                s.GreetNeighbor(data); check(s.NeighborGreetings==1,"Repeated same-day neighbor reward/event.");
                s.day=2; s.GreetNeighbor(data); s.day=4; s.GreetNeighbor(data);
                check(s.NeighborStage==2 && s.Vitals.CurrentHP==500 && s.DailyFacts.Last().text.Contains("아직"),"Unanswered greeting prematurely killed neighbor or invented an answer.");
                s.CheckNeighbor(data); check(s.NeighborStage==3 && s.CurrentLocation=="home" && s.DailyFacts.Last().text.Contains("직접 확인"),"Neighbor physical check did not resolve normally.");
                hour=s.hour; s.CheckNeighbor(data); check(s.hour==hour,"Neighbor check repeated time/result.");
                s=Ready(data); check(s.CompanionAvailable && s.CanMeetCompanion(data),"Third reunion did not unlock first companion appointment.");
                s.MeetCompanion(data); check(s.Companion==CompanionStep.Jiwoo && s.Story.HasMetJiwoo && !s.CanAct && !s.Story.JoinedLiberationFront,"Jiwoo opposition joined/moved the player or failed to lock event.");
                check(s.CompanionText(data).Contains("두고 올 수"),"Jiwoo's concern was not companion responsibility.");
                saves.Save(s); check(saves.TryLoad(out restored,out _) && restored.Companion==CompanionStep.Jiwoo,"Opposition dialogue did not restore.");
                s.ContinueCompanion(data); check(s.CanAct && s.Companion==CompanionStep.Preparation && s.CurrentLocation=="school","Preparation teleported home or blocked survival.");
                check(!s.CanMeetCompanion(data),"Appointment allowed before the next day.");
                s.Travel("home",data,()=>1); s.Cook("rice",data); s.Transfer("rice_meal",true,1,data);
                s.day=s.CompanionReadyDay+2; s.hour=8; s.Travel("school",data,()=>1);
                check(s.CanMeetCompanion(data),"Missed appointment permanently blocked story.");
                s.MeetCompanion(data); s.ContinueCompanion(data);
                check(s.Companion==CompanionStep.Decision && s.CompanionTrust==1 && s.Backpack.Count("rice_meal")==0,"Prepared meal not spent exactly once.");
                saves.Save(s); check(saves.TryLoad(out restored,out _) && restored.Companion==CompanionStep.Decision && restored.CompanionTrust==1,"In-expedition choice did not restore.");
                s.ContinueCompanion(data,true); check(s.Companion==CompanionStep.Debrief && s.CompanionTrust==3 && s.CompanionWithdrew,"Withdrawal result/trust wrong.");
                s.ContinueCompanion(data); hour=s.hour; s.ContinueCompanion(data);
                check(s.Companion==CompanionStep.Completed && s.hour==hour && s.CompanionTrust==3 && !s.Story.JoinedLiberationFront && s.CurrentLocation=="school","Expedition result duplicated or automatically joined/moved home.");
                s.Travel("home",data,()=>1); check(s.CurrentLocation=="home" && s.HomeStorage.Count("burner")==1 && s.CanAct,"Expedition did not return to original life loop.");
                s=Ready(data); s.MeetCompanion(data); s.ContinueCompanion(data); s.Travel("home",data,()=>1); s.day=s.CompanionReadyDay; s.hour=8; s.Travel("school",data,()=>1); s.MeetCompanion(data); s.ContinueCompanion(data);
                check(s.Companion==CompanionStep.Decision && s.CompanionMeal=="" && s.CompanionTrust==0,"Missing prepared meal blocked/pretended preparation.");
                s.ContinueCompanion(data,false); s.ContinueCompanion(data); check(s.Companion==CompanionStep.Completed && !s.CompanionWithdrew,"Alternative safe inspection did not finish.");
                s=ErasureNewsChecks.AfterGarak(data); s.day=2; s.GenerateDailyErasureNews(data); s.day=3; s.GenerateDailyErasureNews(data); s.ErasureReadDay=2;
                s.schemaVersion=10; s.Story.JoinedLiberationFront=true; s.Thirst=50; s.HydrationHours=5; s.Vitals.AdjustMental(-23);
                string oldId=s.ErasureHistory[1].locationId; uint oldNews=s.ErasureRandomState; clean(); File.WriteAllText(path,JsonUtility.ToJson(s));
                check(saves.TryLoad(out restored,out _) && restored.schemaVersion==12 && restored.Diaries.Count==0 && restored.DailyFacts.Count==0 && restored.DiaryStartDay==3,"Legacy upgrade invented old diaries or reset tracking date.");
                check(restored.ErasureHistory.Count==2 && restored.ErasureHistory[1].locationId==oldId && restored.ErasureReadDay==2 && restored.ErasureRandomState==oldNews && restored.MorningIssues.Count==1,"Legacy news/read state/RNG was reset or duplicated.");
                check(restored.Vitals.CurrentMental==77 && restored.Thirst==50 && restored.HydrationHours==5 && restored.Story.JoinedLiberationFront && restored.Companion==CompanionStep.None,"Legacy life upgrade reset vitals or reused membership for companions.");
                s=fresh(); s.hour=23; s.Vitals.Hunger=100; s.Vitals.CurrentHP=1; s.AdvanceTime(12,data);
                check(s.IsGameOver && s.hour==0 && s.MorningIssues.Count==0,"Death advanced into a false next morning.");
                clean(); s=fresh(); s.Diaries.Add(new DiaryEntry{day=1,body="future"}); File.WriteAllText(path,JsonUtility.ToJson(s)); check(!saves.TryLoad(out _,out _),"Future diary accepted.");
            }
            finally { data.TEMP_BALANCE.hungerPerHour=hunger; clean(); }
            Debug.Log("VD_DAILY_LIFE_CHECKS_OK: "+checks+" assertions; factual diaries, morning inbox, portable radio, neighbor, companion branches and legacy preservation.");
        }
    }
}
