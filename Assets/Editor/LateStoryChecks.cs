using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class LateStoryChecks
    {
        // The first companion expedition finished, back at home: the point where the late story begins.
        static SurvivalState AfterExpedition(SurvivalDefinitions data)
        {
            var s=ErasureNewsChecks.AfterGarak(data);
            s.UnlockedLocations.AddRange(new[]{"empty_house","school"});
            s.Travel("home",data,()=>1); s.day=2; s.hour=8; s.Travel("empty_house",data,()=>1); s.ResolveDongrimMeeting(false,data);
            s.Travel("home",data,()=>1); s.day=3; s.hour=8; s.Travel("school",data,()=>1); s.ResolveDongrimMeeting(false,data);
            s.day=4; s.hour=8; s.MeetCompanion(data); s.ContinueCompanion(data); s.Travel("home",data,()=>1);
            s.day=s.CompanionReadyDay; s.hour=8; s.Travel("school",data,()=>1); if(s.PendingDongrimMeeting) s.ResolveDongrimMeeting(false,data);
            s.MeetCompanion(data); s.ContinueCompanion(data); s.ContinueCompanion(data,true); s.ContinueCompanion(data);
            s.Travel("home",data,()=>1); return s;
        }
        // Moves to the current scene's place on its day, in daylight, and opens the scene.
        static void Reach(SurvivalState s,SurvivalDefinitions data)
        {
            var beat=s.CurrentBeat(data);
            s.Vitals.Hunger=0; s.Thirst=0; s.HydrationHours=0; s.Sleepiness=0;
            if(s.day<s.LateReadyDay) s.day=s.LateReadyDay;
            s.hour=9;
            if(s.CurrentLocation!=beat.location) s.Travel(beat.location,data,()=>1);
            if(s.PendingDongrimMeeting) s.ResolveDongrimMeeting(false,data);
            if(!s.LateScenePending) s.TryBeginLateStory(data);
        }
        static void Play(SurvivalState s,SurvivalDefinitions data,params int[] picks)
        {
            foreach(int pick in picks)
            {
                Reach(s,data);
                if(!s.LateScenePending) throw new Exception("Scene did not open: "+s.CurrentBeat(data)?.id);
                if(s.CurrentBeat(data).id=="rumor_table" && pick==0) s.Backpack.Add("rice_meal",1);
                string id=s.CurrentBeat(data).id; s.ChooseLateStory(pick,data);
                if(s.LateScenePending) throw new Exception("Choice did not resolve: "+id);
            }
        }
        [MenuItem("Vanishing Days/Validate late story and endings")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0; int hunger=data.TEMP_BALANCE.hungerPerHour; data.TEMP_BALANCE.hungerPerHour=0;
            Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
            string path=Path.GetFullPath("Builds/Validation/late-story-save.json"); var saves=new SessionSaveStore(path,data);
            Action clean=()=>{foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);}; clean();
            try
            {
                var fresh=SurvivalState.NewGame(GameDifficulty.NORMAL,data,7);
                check(fresh.CurrentBeat(data)==null && !fresh.TryBeginLateStory(data) && fresh.Locations.Count==data.locations.Length,"Late story started before the first expedition.");
                check(!fresh.IsLocationUnlocked("relay_room",data) && !fresh.IsLocationUnlocked("sp_station",data),"Story places visible from the start.");

                var s=AfterExpedition(data);
                check(s.Companion==CompanionStep.Completed && s.CurrentBeat(data)?.id=="absent_share" && s.LateReadyDay==s.day+1,"Late story did not queue after the expedition.");
                s.hour=9; s.Travel("shelter",data,()=>1); if(s.PendingDongrimMeeting) s.ResolveDongrimMeeting(false,data);
                check(!s.LateScenePending,"Scene opened before its day.");
                Reach(s,data);
                check(s.LateScenePending && !s.CanAct,"Daytime arrival did not open the first scene.");
                var copy=JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(s)); copy.hour=22; copy.LateScenePending=false;
                check(!copy.TryBeginLateStory(data),"Daytime scene opened at night.");
                saves.Save(s); check(saves.TryLoad(out var restored,out _) && restored.LateScenePending && restored.LateBeatText(data)==s.LateBeatText(data),"Pending scene not restored.");
                restored.CurrentLocation="home"; restored.AwayFromHome=false; check(!TrySave(saves,restored),"Scene pending away from its place was saved.");
                int tuna=s.Backpack.Count("tuna"); s.ChooseLateStory(2,data);
                check(s.Backpack.Count("tuna")==tuna+1 && s.Isolation==1 && s.LateStep==1 && s.LateReadyDay==s.day+2 && s.CanAct,"Choice reward/weight/next day wrong.");
                check(s.ChooseLateStory(0,data)=="지금은 그 선택을 할 수 없다." && s.LateStep==1,"Resolved scene could be chosen again.");
                s.day=s.LateReadyDay; s.hour=9; s.TryBeginLateStory(data);
                check(s.LateScenePending && !s.CanChooseLate(0,data) && s.CanChooseLate(1,data),"Meal requirement not enforced at the shared table.");

                // Path A: care throughout, then the direct visit.
                s=AfterExpedition(data); s.NeighborStage=1;
                Play(s,data,0,0,0,0,0,0,0,0,0,0,0,0);
                check(s.LateFlags.Contains("warned") && s.Care>=10 && s.LateTrust>=data.lateStory.delegateTrust && s.Lure==0,"Care path weights wrong.");
                check(s.NeighborMoved && s.IsLocationUnlocked("pharmacy",data) && s.IsLocationUnlocked("relay_room",data) && s.IsLocationUnlocked("sp_station",data),"Story unlocks missing.");
                int greetings=s.NeighborGreetings; s.Travel("home",data,()=>1); s.GreetNeighbor(data);
                check(s.NeighborGreetings==greetings,"Moved neighbor still answered through the wall.");
                Reach(s,data); check(s.CurrentBeat(data).id=="final_call" && s.CanChooseLate(1,data),"Trusted delegation unavailable.");
                s.ChooseLateStory(0,data);
                check(!s.HasEnded && s.CurrentBeat(data).id=="final_visit","Direct visit did not lead to the visit scene.");
                Reach(s,data); check(s.CurrentLocation=="pharmacy" && s.LateScenePending,"Visit scene did not open at the pharmacy.");
                s.ChooseLateStory(0,data);
                check(s.EndingId=="tomorrow_here" && !s.CanAct && s.Travel("home",data,()=>1)=="지금은 이동할 수 없다." && s.LateEndingText(data).Contains("관측을 종료한다"),"Direct visit ending wrong or did not stop play.");
                saves.Save(s); check(saves.TryLoad(out restored,out _) && restored.EndingId=="tomorrow_here","Ending not saved.");

                // Path B: same relationships, the check handed to Dongrim.
                s=AfterExpedition(data); Play(s,data,0,0,0,0,0,0,0,0,0,0,0,0);
                Reach(s,data); s.ChooseLateStory(1,data); check(s.EndingId=="continued_care","Delegated check did not reach its ending.");

                // Path C: closing off; delegation has no one to ask.
                s=AfterExpedition(data); Play(s,data,2,2,2,2,1,1,2,0,1,2,1,1);
                check(s.Isolation>=7 && s.LateTrust<data.lateStory.delegateTrust && s.Lure==0,"Isolation path weights wrong.");
                Reach(s,data); check(!s.CanChooseLate(1,data),"Delegation open without trust.");
                s.ChooseLateStory(2,data); check(s.EndingId=="closed_door","Closed door ending wrong.");

                // Path D: repeating gathering calls overrides the last choice.
                s=AfterExpedition(data); Play(s,data,1,0,1,0,0,0,0,0,2,1,2,0);
                Reach(s,data); var beat=s.CurrentBeat(data);
                check(s.Lure>=data.lateStory.lureEnding && s.LureEndingDue(beat,data) && s.LateChoices(data).Length==1,"Gathering calls did not take over the final scene.");
                s.ChooseLateStory(0,data); check(s.EndingId=="last_broadcast","Last broadcast ending wrong.");

                // Saves written before the late story keep their progress and gain the new places.
                var old=AfterExpedition(data); old.Locations.RemoveAll(x=>x.id=="relay_room" || x.id=="sp_station" || x.id=="construction_site" || x.id=="seongnae_bridge"); old.LateFlags=null; old.EndingId=null;
                clean(); File.WriteAllText(path,JsonUtility.ToJson(old));
                check(saves.TryLoad(out restored,out _) && restored.Locations.Count==data.locations.Length && restored.LateFlags!=null && restored.CurrentBeat(data)?.id=="absent_share","Older save did not gain the late story.");
                Debug.Log("VD_LATE_STORY_CHECKS_OK: "+checks+" assertions; gates, saves, unlocks, moved neighbor and all four endings.");
            }
            finally { data.TEMP_BALANCE.hungerPerHour=hunger; clean(); }
        }
        static bool TrySave(SessionSaveStore saves,SurvivalState s) { try { saves.Save(s); return true; } catch(InvalidDataException) { return false; } }
    }
}
