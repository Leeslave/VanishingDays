using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class LateStoryChecks
    {
        static void Check(bool ok,string message)
        {
            if(!ok) throw new Exception("Late story: "+message);
        }
        // JsonUtility turns a null encounter into an empty object, which would block every action on the copy.
        static SurvivalState Copy(SurvivalState s)
        {
            var copy=JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(s));
            if(copy.PendingEncounter!=null && string.IsNullOrEmpty(copy.PendingEncounter.kind)) copy.PendingEncounter=null;
            return copy;
        }
        static SurvivalState AfterExpedition(SurvivalDefinitions data)
        {
            var s=ErasureNewsChecks.AfterGarak(data);
            s.UnlockedLocations.AddRange(new[]{"empty_house","school"});
            Go(s,"home",data); s.day=2; Go(s,"empty_house",data);
            Go(s,"home",data); s.day=3; Go(s,"school",data);
            s.day=4; s.hour=8; s.MeetCompanion(data);
            Check(!s.ContinueCompanion(data).Contains(s.CompanionHint),"the companion schedule would be printed twice");
            Go(s,"home",data);
            s.day=s.CompanionReadyDay; Go(s,"school",data);
            s.MeetCompanion(data); s.ContinueCompanion(data); s.ContinueCompanion(data,true); s.ContinueCompanion(data);
            Go(s,"home",data);
            return s;
        }
        static void ResetNeeds(SurvivalState s)
        {
            s.Vitals.Hunger=0; s.Thirst=0; s.HydrationHours=0; s.Sleepiness=0;
            s.Backpack.Add("water",2);
        }
        static void Go(SurvivalState s,string place,SurvivalDefinitions data)
        {
            ResetNeeds(s);
            s.hour=8;
            if(s.CurrentLocation!=place) s.Travel(place,data,()=>1);
            if(s.PendingDongrimMeeting) s.ResolveDongrimMeeting(false,data);
            Check(s.CurrentLocation==place && !s.IsGameOver,"could not reach "+place);
        }
        static void Open(SurvivalState s,SurvivalDefinitions data)
        {
            var beat=s.CurrentBeat(data);
            s.day=Math.Max(s.day,s.LateReadyDay);
            Go(s,beat.location,data);
            if(!string.IsNullOrEmpty(beat.window))
            {
                s.LateScenePending=false;
                s.hour=beat.window=="morning"?14:10;
                Check(!s.TryBeginLateStory(data),beat.id+" opened outside its "+beat.window+" window");
                s.hour=beat.window=="morning"?5:4;
                Check(!s.TryBeginLateStory(data),beat.id+" opened before dawn");
                s.hour=beat.window=="morning"?9:19;
                Check(s.LateHint(data).Contains(beat.window=="morning"?"아침에":"저녁에"),beat.id+" hint does not name its time of day");
            }
            if(!s.LateScenePending && !s.BrokenPathScenePending) s.TryBeginLateStory(data);
        }
        static void ChooseBeat(SurvivalState s,SurvivalDefinitions data,int index=0)
        {
            Open(s,data);
            string id=s.CurrentBeat(data).id;
            if(id=="rumor_table" && index==0) s.Backpack.Add("rice_meal",1);
            Check(s.CanChooseLate(index,data),"choice unavailable at "+id);
            s.ChooseLateStory(index,data);
            Check(!s.LateScenePending,"choice not completed at "+id);
        }
        static void ChooseBroken(SurvivalState s,string action,SurvivalDefinitions data)
        {
            int index=Array.FindIndex(s.CurrentBrokenPathScene(data).choices,x=>x.action==action);
            Check(index>=0,"missing action "+action);
            s.ChooseBrokenPath(index,data);
        }
        static void FinishBroken(SurvivalState s,string route,SurvivalDefinitions data)
        {
            ChooseBroken(s,"core_"+route[0],data);
            ChooseBroken(s,"field",data);
            Check(s.LateHint(data).Contains(data.brokenPath.hint) && s.WorldHint(data).Contains(data.brokenPath.hint),"the map still points at the scene before the bridge");
            Go(s,"seongnae_bridge",data);
            s.SurveyBrokenPath("pier",data);
            string gapReport=s.SurveyBrokenPath("gap",data);
            Check(gapReport.Contains("[무전] 김영숙")==(route[0]=='A'),"Yeongsuk's radio line does not match route "+route);
            s.ChooseBrokenPathMethod("ladder",data);
            int trips=0;
            while(s.BrokenPath.Site.Count("ladder")<2 && trips++<2)
            {
                Go(s,"construction_site",data);
                for(int i=0;i<Math.Min(2,s.CarryCapacity);i++) s.TakeYardMaterial("ladder",data);
                Go(s,"seongnae_bridge",data);
                s.DropAtBridge(data);
            }
            Check(s.CanBuildBrokenPath(data),"normal path cannot build the ladder bridge");
            s.BuildBrokenPath(data);
            if(route[0]=='A') ChooseBroken(s,route,data);
            while(s.BrokenPathScenePending) s.ChooseBrokenPath(0,data);
            Check(s.BrokenPath.Stage==4 && s.CurrentAct(data)==4,"act 4 not unlocked after rescue");
        }
        static void Dawn(SurvivalState s,SurvivalDefinitions data)
        {
            int hours=(8-s.hour+24)%24;
            if(hours==0) hours=24;
            for(int left=hours;left>0;left-=4)
            {
                ResetNeeds(s);
                s.AdvanceTime(Math.Min(4,left),data);
            }
        }
        static void Broadcast(SurvivalState s,SurvivalDefinitions data,int iteration)
        {
            var target=iteration==0?"pharmacy":"shelter";
            s.QueueRelay(new RelayDecision{node=target,action="announce"});
            s.QueueRelay(new RelayDecision{node="relay_room",action="staff",option="jiwoo"});
            Check(s.CanSubmitRelay,"daily decisions unavailable");
            s.SubmitRelay(data);
            Check(!s.CanOperateRelay && s.Relay.plan.Count==0,"dawn budget could be reused");
        }
        [MenuItem("Vanishing Days/Validate late story and endings")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load();
            data.TEMP_BALANCE.hungerPerHour=0;
            var path=Path.GetFullPath("Builds/Validation/late-story-save.json");
            var saves=new SessionSaveStore(path,data);
            var resultSaves=new SessionSaveStore(Path.Combine(Path.GetDirectoryName(path),"late-story-result-save.json"),data);
            var fresh=SurvivalState.NewGame(GameDifficulty.NORMAL,data,7);
            Check(fresh.CurrentAct(data)==1 && !fresh.TryBeginLateStory(data),"first act begins in survival");

            var s=AfterExpedition(data);
            Check(s.CurrentAct(data)==2 && s.CurrentBeat(data)?.id=="yeongsuk_soup","second act did not queue after the expedition");
            var mugged=Copy(s);
            Go(mugged,"store",data);
            mugged.hour=8;
            int muggedRolls=0;
            mugged.Travel("home",data,()=>muggedRolls++==0?0:1);
            Check(mugged.PendingEncounter?.kind=="hungry" && mugged.lastReport==SurvivalState.HomeArrivalReport && mugged.EncounterBackdrop=="","an encounter on the way home shows the player already safe indoors");
            Open(s,data);
            saves.Save(s);
            Check(saves.TryLoad(out var restored,out _) && restored.LateScenePending && restored.CurrentBeat(data).id=="yeongsuk_soup","pending scene not restored");
            s=restored;
            s.ChooseLateStory(0,data);
            ChooseBeat(s,data,2);
            Check(s.IsLocationUnlocked("construction_site",data) && !s.IsLocationUnlocked("seongnae_bridge",data),"construction site should preview in act 2");
            s.PrepareCommunityRequests(data);
            var request=s.CommunityRequests.Find(x=>x.id=="soup_check");
            Check(request!=null && s.CommunityRequests.Count<=2,"community requests not generated");
            Go(s,"soup_house",data);
            int trust=s.LateTrust, water=s.Backpack.Count("water");
            s.CompleteCommunityRequest(request,data);
            Check(s.Backpack.Count("water")==water+2,"community reward was not delivered to the backpack");
            s.CompleteCommunityRequest(request,data);
            Check(s.Backpack.Count("water")==water+2 && s.LateFlags.Contains("survey_issued:soup_check"),"one-time survey paid twice or can reappear");
            Check(s.LateTrust==trust+1 && !s.IsLocationErased("soup_house"),"request reward or erasure before departure");
            Go(s,"home",data);
            Check(s.IsLocationErased("soup_house") && !s.IsLocationUnlocked("soup_house",data),"erasure not committed after departure");
            while(s.CurrentBeat(data).id!="post_vanished") ChooseBeat(s,data);
            Open(s,data);
            var oldPost=JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(s));
            oldPost.LateStoryRevision=1;
            oldPost.LateStep=14;
            File.WriteAllText(path,JsonUtility.ToJson(oldPost));
            Check(saves.TryLoad(out restored,out _) && restored.CurrentBeat(data).id=="post_vanished" && restored.LateScenePending,"old pending post scene did not resume");
            restored.ChooseLateStory(0,data);
            Check(restored.CurrentBeat(data).id=="core_choice" && restored.IsLocationErased("river_post"),"old save repeated the false broadcast or lost the erasure");
            s.ChooseLateStory(0,data);
            Check(s.CurrentBeat(data).id=="false_report" && s.IsLocationErased("river_post"),"the post did not disappear before the false broadcast");
            while(s.CurrentBeat(data).id!="core_choice") ChooseBeat(s,data);
            Check(s.day<18 && s.CurrentAct(data)==3 && s.IsLocationErased("river_post"),"act 3 calendar gate or missing erasure");
            Open(s,data);
            Check(s.BrokenPathScenePending && !s.LateScenePending && s.CurrentLocation=="relay_room","normal progression did not open the actual core choice");
            saves.Save(s);
            Check(saves.TryLoad(out restored,out _) && restored.BrokenPathScenePending && restored.IsLocationErased("river_post"),"act boundary or erasure lost on load");

            foreach(string route in new[]{"A1","A2","B"})
            {
                Check(saves.TryLoad(out s,out _),"could not restore core choice");
                FinishBroken(s,route,data);
                while(s.CurrentBeat(data).id!="endless") ChooseBeat(s,data);
                Check(s.RelayStaff().Contains("donglim")==(route!="A1"),"wrong survivor eligible for relay");
                ChooseBeat(s,data);
                Check(s.RelayActive && s.CurrentAct(data)==4 && s.IsLocationUnlocked("rescue_post",data),"relay did not start from the policy choice");
                Check(s.CurrentLocation=="home" && s.hour>=data.TEMP_BALANCE.nightStarts,"the broadcast scene says the player walked home after dark");
                if(route=="B")
                {
                    var towel=Copy(s);
                    Check(towel.NeighborMoved && !towel.CanCheckTowel(data) && towel.TowelStatus(data)==data.lateStory.towelDark,"the towel could be seen after dark");
                    Dawn(towel,data);
                    towel.PendingEncounter=null;
                    int seen=towel.TowelDays, facts=towel.DailyFacts.Count;
                    Check(towel.CanCheckTowel(data),"the morning towel check was unavailable at home");
                    towel.CheckTowel(data);
                    Check(towel.TowelDays==seen+1 && towel.TowelSeenToday && towel.DailyFacts.Count==facts+1 && !towel.CanCheckTowel(data),"the towel check did not count exactly once a day");
                    towel.CheckTowel(data);
                    Check(towel.TowelDays==seen+1,"the towel was counted twice in one day");
                    var towelSaves=new SessionSaveStore(Path.Combine(Path.GetDirectoryName(path),"towel-save.json"),data);
                    towelSaves.Save(towel);
                    Check(towelSaves.TryLoad(out var towelSave,out _) && towelSave.TowelDays==towel.TowelDays && towelSave.TowelLastDay==towel.day,"towel checks were lost on load");
                    var away=Copy(towel); away.day++; Go(away,"pharmacy",data);
                    Check(!away.CanCheckTowel(data) && away.TowelStatus(data)==data.lateStory.towelAway,"the towel could be seen away from home");
                }
                if(route=="A1")
                {
                    var grief=JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(s));
                    grief.PendingEncounter=null;
                    Dawn(grief,data);
                    Dawn(grief,data);
                    Check(grief.RelayHiddenAt("relay_room"),"grieving Jiwoo did not hide a request");
                    Go(grief,"relay_room",data);
                    grief.VisitRelay("relay_room",data);
                    Check(grief.CanAddressJiuRequest("relay_room",data),"visiting did not uncover the hidden request");
                    grief.AddressJiuRequest("relay_room",true,data);
                    Check(grief.JiuControlState=="Recovering","sharing the request did not advance Jiwoo's recovery");
                }
                s.HomeStorage.Add("tuna",6);
                var send=new RelayDecision{node="pharmacy",action="send",option="food",source="home"};
                int before=s.HomeStorage.Count("tuna")+s.HomeStorage.Count("spam");
                Check(s.CanQueueRelay(send),"could not queue a dispatch");
                s.QueueRelay(send);
                Check(s.HomeStorage.Count("tuna")+s.HomeStorage.Count("spam")==before,"queued dispatch spent supplies before submission");
                s.CancelRelay(0);
                Check(s.Relay.plan.Count==0 && s.HomeStorage.Count("tuna")+s.HomeStorage.Count("spam")==before,"cancelled dispatch spent supplies");
                s.QueueRelay(send);
                s.QueueRelay(new RelayDecision{node="relay_room",action="staff",option="jiwoo"});
                s.SubmitRelay(data);
                Check(s.HomeStorage.Count("tuna")+s.HomeStorage.Count("spam")==before-3,"confirmed dispatch did not use real supplies");
                Dawn(s,data);
                Broadcast(s,data,1);
                Go(s,"pharmacy",data);
                s.VisitRelay("pharmacy",data);
                ChooseBeat(s,data);
                Check(s.CurrentAct(data)==5,"completed operation did not open act 5");
                ChooseBeat(s,data);
                Open(s,data);
                Check(s.CanChooseLate(1,data),"trusted delegation unavailable");
                var direct=Copy(s);
                direct.ChooseLateStory(0,data);
                ChooseBeat(direct,data);
                Check(direct.EndingId=="tomorrow_here" && direct.HasTrueArchive,"direct visit with the handover did not reach the true ending");
                var directWithoutHandover=Copy(s);
                directWithoutHandover.LateFlags.Remove("jiwoo_shared");
                directWithoutHandover.ChooseLateStory(0,data);
                ChooseBeat(directWithoutHandover,data);
                Check(directWithoutHandover.EndingId=="collapse" && !directWithoutHandover.HasTrueArchive,"direct visit without the handover unlocked the true ending");
                var withoutHandover=Copy(s);
                withoutHandover.LateFlags.Remove("jiwoo_shared");
                withoutHandover.ChooseLateStory(1,data);
                Check(withoutHandover.EndingId=="collapse" && !withoutHandover.HasTrueArchive,"delegation without handover unlocked the true ending");
                s.LateStoryResult=s.ChooseLateStory(1,data);
                Check(s.EndingId=="tomorrow_delegated" && s.HasTrueArchive && !s.CanAct,"distributed handover and delegation did not unlock the true ending");
                resultSaves.Save(s);
                Check(resultSaves.TryLoad(out var endingSave,out _) && endingSave.HasTrueArchive && endingSave.LateStoryResult==s.LateStoryResult,"the saved story result or the true ending was lost");
                Check(!s.LateEndingText(data).Contains("관측 종료"),"archive leaked into main ending");
            }

            foreach(string policy in new[]{"gather","silent","closed"})
            {
                Check(saves.TryLoad(out s,out _),"could not restore before ending route");
                FinishBroken(s,"B",data);
                while(s.CurrentBeat(data).id!="endless") ChooseBeat(s,data);
                ChooseBeat(s,data,policy=="gather"?1:policy=="silent"?2:0);
                if(policy=="silent")
                {
                    var quiet=Copy(s);
                    for(int i=0;i<3;i++) { Dawn(quiet,data); quiet.Relay.badDawns=0; quiet.Relay.collapseDue=false; }
                    ChooseBeat(quiet,data);
                    ChooseBeat(quiet,data);
                    ChooseBeat(quiet,data,2);
                    Check(quiet.EndingId=="closed_door","refusing to answer after the transmitter was shut off still ended at the last post");
                    int morningIssues=s.MorningIssues.Count;
                    for(int i=0;i<10 && !s.Relay.collapseDue;i++) Dawn(s,data);
                    Check(s.MorningIssues.Count==morningIssues,"public broadcasts continued after the transmitter was shut off");
                    s.TryBeginLateStory(data);
                    Check(s.EndingId=="collapse" && !s.IsGameOver,"silent networks did not collapse independently of HP");
                }
                else
                {
                    for(int i=0;i<3;i++) Dawn(s,data);
                    if(policy=="gather") Check(s.MorningIssues.Last().body==data.lateStory.gatherBroadcast,"the copied voice did not replace the regular morning broadcast");
                    ChooseBeat(s,data);
                    ChooseBeat(s,data);
                    if(policy=="closed")
                    {
                        Open(s,data);
                        Check(s.TowelDays==0 && s.LateBeatText(data).Contains(data.lateStory.towelNever) && s.TowelMissing(data),"the final morning ignored that the window was never checked");
                        var watched=Copy(s); watched.TowelDays=4; watched.TowelLastDay=watched.day-1;
                        Check(watched.LateBeatText(data).Contains("4번의 아침"),"the final morning ignored the towel habit");
                    }
                    ChooseBeat(s,data,policy=="gather"?0:2);
                    Check(s.EndingId==(policy=="gather"?"last_broadcast":"closed_door"),"incorrect ending for "+policy);
                }
            }

            var legacy=AfterExpedition(data);
            legacy.LateStoryRevision=0;
            legacy.LateStep=8;
            legacy.LateFlags.Add("relay_room");
            legacy.Locations.RemoveAll(x=>x.id=="soup_house" || x.id=="river_post" || x.id=="rescue_post");
            File.WriteAllText(path,JsonUtility.ToJson(legacy));
            Check(saves.TryLoad(out restored,out _) && restored.CurrentBeat(data).id=="seokhyeon_meet" && restored.Locations.Count==data.locations.Length,"legacy beat identity lost");
            var oldTestEntry=SurvivalState.Act3TestStart(data,0,"기록자");
            oldTestEntry.LateStoryRevision=0;
            oldTestEntry.LateStep=9;
            oldTestEntry.LateFlags.Remove("neighbor_moved");
            oldTestEntry.LateFlags.Remove("pharmacy");
            oldTestEntry.UnlockedLocations.Remove("pharmacy");
            File.WriteAllText(path,JsonUtility.ToJson(oldTestEntry));
            Check(saves.TryLoad(out restored,out _) && restored.CurrentBeat(data).id=="core_choice" && restored.BrokenPathScenePending &&
                restored.NeighborMoved && restored.IsLocationUnlocked("pharmacy",data),"older Act 3 test save could not reach the final visit");
            string lateRaw=Resources.Load<TextAsset>("Survival/LateStory").text;
            int firstExperiment=lateRaw.IndexOf("EXP-01",StringComparison.Ordinal);
            Check(firstExperiment>lateRaw.IndexOf("\"archive\"",StringComparison.Ordinal) && !Resources.Load<TextAsset>("Survival/BrokenPath").text.Contains("EXP-01"),
                "EXP-01 was named before the true-ending archive");
            Debug.Log("VD_LATE_STORY_CHECKS_OK: normal act gates, permanent erasures, requests, saves, policy and endings.");
        }
    }
}
