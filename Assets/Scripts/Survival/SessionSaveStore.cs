using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VanishingDays
{
    public sealed class SessionSaveStore
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void JS_FileSystem_Sync();
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
            // File writes only update the in-memory filesystem. Queue persistence
            // after the final rename, including builds using Unity's default template.
            JS_FileSystem_Sync();
#endif
        }
        public bool TryLoad(out SurvivalState state,out string error)
        {
            error=""; state=null;
            foreach(string path in new[]{Path,Path+".bak"})
            {
                if(!File.Exists(path)) continue;
                try
                {
                    var restored=JsonUtility.FromJson<SurvivalState>(File.ReadAllText(path));
                    bool legacyVitals=restored!=null && restored.schemaVersion>=3 && restored.schemaVersion<=5;
                    if(restored?.PendingEncounter!=null && string.IsNullOrEmpty(restored.PendingEncounter.kind)) restored.PendingEncounter=null;
                    UpgradeLegacySession(restored,definitions);
                    restored?.UpgradeStoryLocations(definitions);
                    if(restored?.schemaVersion==5)
                    {
                        if(restored.Vitals!=null) { restored.Vitals.CurrentMental=100; restored.Vitals.BaseMaxMental=100; }
                        restored.schemaVersion=6;
                    }
                    if(restored?.schemaVersion==6) { restored.InitializeErasureNews(); restored.schemaVersion=7; }
                    if(restored?.schemaVersion==7) { restored.Sleepiness=0; restored.SleepHours=6; restored.schemaVersion=8; }
                    if(restored?.schemaVersion==8) { restored.UpgradeWaterSupply(definitions); restored.schemaVersion=9; }
                    if(restored?.schemaVersion==9) { restored.Thirst=restored.UnlimitedWater?0:Math.Max(0,Math.Min(100,restored.HydrationHours*10)); restored.schemaVersion=10; }
                    if(restored?.schemaVersion==10) { restored.UpgradeDailyLife(definitions); restored.schemaVersion=11; }
                    if(restored?.schemaVersion==11) { restored.UpgradeCrafting(definitions); restored.schemaVersion=12; }
                    if(restored?.schemaVersion==12) { restored.UpgradeSurvivalBalance(definitions); restored.schemaVersion=13; }
                    restored?.UpgradeOnboarding();
                    restored?.UpgradeLateStory(definitions);
                    restored?.UpgradeBrokenPath(definitions);
                    restored?.UpgradeCommunityProgress();
                    // Validate the original HP against the base maximum first so a
                    // corrupt save cannot be silently repaired into a valid state.
                    Validate(restored,definitions,legacyVitals);
                    if(legacyVitals) restored.Vitals.ClampToRecoveryCap(definitions.TEMP_BALANCE);
                    restored.UpgradeMedicalLoot(definitions); state=restored; return true;
                }
                catch(Exception e) { error=e.Message; }
            }
            return false;
        }
        public static void Validate(SurvivalState s,SurvivalDefinitions data,bool allowLegacyOverCap=false)
        {
            if(s==null || s.schemaVersion!=13 || s.day<1 || s.day>365000 || s.hour<0 || s.hour>=24 || data.Location(s.CurrentLocation)==null || !Enum.IsDefined(typeof(GameDifficulty),s.Difficulty)) throw new InvalidDataException("Invalid session header.");
            if(s.Vitals==null || s.Vitals.BaseMaxHP<=0 || s.Vitals.CurrentHP<0 || s.Vitals.CurrentHP>s.Vitals.BaseMaxHP || s.Vitals.Hunger<0 || s.Vitals.Hunger>100 || s.Vitals.Injuries==null || s.Story==null || s.Locations==null || s.Locations.Count!=data.locations.Length || s.journal==null || s.Equipment==null || s.InstalledSecurity==null || s.RandomState==0) throw new InvalidDataException("Invalid session state.");
            if(s.InjuryRestProgress<0 || s.InjuryRestProgress>=data.TEMP_BALANCE.injuryRestHours || s.MedicalLootVersion<0 || s.MedicalLootVersion>1) throw new InvalidDataException("Invalid recovery progress.");
            if(s.HydrationHours<0 || s.HydrationHours>=s.WaterInterval(data) || s.UnlimitedWater && s.HydrationHours!=0) throw new InvalidDataException("Invalid water progress.");
            if(s.Thirst<0 || s.Thirst>100 || s.UnlimitedWater && s.Thirst!=0) throw new InvalidDataException("Invalid thirst.");
            if(s.OnboardingVersion!=1 || s.TutorialStep<0 || s.TutorialStep>6 || s.TutorialViewed<0 || (s.TutorialViewed&~60)!=0 || s.TutorialActive && s.TutorialStep==6) throw new InvalidDataException("Invalid onboarding progress.");
            ValidateInventory(s.Backpack,data); ValidateInventory(s.HomeStorage,data);
            foreach(var location in data.locations)
            {
                var matches=s.Locations.FindAll(x=>x!=null && x.id==location.id);
                if(matches.Count!=1 || matches[0].searches<0 || matches[0].erasedDay<0 || matches[0].erasedDay>s.day || matches[0].erasedDay>0 && (location.id=="home" || s.CurrentLocation==location.id || matches[0].remaining?.stacks?.Exists(x=>x.amount>0)==true)) throw new InvalidDataException("Invalid location progress.");
                ValidateInventory(matches[0].remaining,data);
            }
            foreach(var id in s.Equipment) if(data.Item(id)==null || (data.Item(id).strength==0 && data.Item(id).defense==0) || s.Equipment.FindAll(x=>x==id).Count!=1) throw new InvalidDataException("Invalid equipment.");
            if(s.Equipment.FindAll(x=>data.Item(x).strength>0).Count>1 || s.Equipment.FindAll(x=>data.Item(x).strength==0 && data.Item(x).defense>0).Count>1) throw new InvalidDataException("Multiple items in one equipment slot.");
            if(s.InstalledFacilities==null) throw new InvalidDataException("Missing crafting facilities.");
            if(s.CollectorGuideStep<0 || s.CollectorGuideStep>3 || s.CollectorGuideStep>0 && (!s.HasFacility("water_collector") || s.CollectorCycleHours!=0) || s.CollectorCycleHours!=0 && s.CollectorCycleHours!=2 && s.CollectorCycleHours!=5 && s.CollectorCycleHours!=10 || s.CollectorElapsedHours<0 || s.CollectorElapsedHours>s.CollectorCycleHours || s.CollectorCycleHours>0 && !s.HasFacility("water_collector") || s.CollectorNoticePending && !s.CollectorReady) throw new InvalidDataException("Invalid water collector.");
            foreach(var id in s.InstalledFacilities) if(data.crafting.Find(id)==null || !data.crafting.Find(id).facility || s.InstalledFacilities.FindAll(x=>x==id).Count!=1 || !string.IsNullOrEmpty(data.crafting.Find(id).requiresFacility) && !s.HasFacility(data.crafting.Find(id).requiresFacility)) throw new InvalidDataException("Invalid crafting facility.");
            foreach(var id in s.InstalledSecurity) if(data.Security(id)==null || s.InstalledSecurity.FindAll(x=>x==id).Count!=1) throw new InvalidDataException("Invalid security.");
            foreach(var injury in s.Vitals.Injuries) if(injury==null || float.IsNaN(injury.recoveryPenalty) || injury.recoveryPenalty<0 || injury.recoveryPenalty>1) throw new InvalidDataException("Invalid injury.");
            if(float.IsNaN(s.Vitals.HungerPenalty) || s.Vitals.HungerPenalty<0 || s.Vitals.HungerPenalty>1) throw new InvalidDataException("Invalid hunger penalty.");
            if(s.Vitals.BaseMaxMental<=0 || s.Vitals.MentalMaxPenalty<0 || s.Vitals.MentalMaxPenalty>s.Vitals.BaseMaxMental || s.Vitals.CurrentMental<0 || s.Vitals.CurrentMental>s.Vitals.MentalCap || s.Sleepiness<0 || s.Sleepiness>100 || s.SleepHours<0 || s.SleepHours>24) throw new InvalidDataException("Invalid mental health/sleep.");
            if(!allowLegacyOverCap && s.Vitals.CurrentHP>s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)) throw new InvalidDataException("HP exceeds recovery cap.");
            if(s.AwayFromHome!=(s.CurrentLocation!="home")) throw new InvalidDataException("Invalid away state.");
            int radios=s.Backpack.Count("radio")+s.HomeStorage.Count("radio");
            if(radios>1 || s.Story.HasRadio!=(radios==1) || (!s.Story.HasRadio && (s.Story.HasHeardSPBroadcast || s.Story.HasMetDongrim || s.Story.DongrimRescueCompleted || s.Story.HasMetJiwoo || s.Story.JoinedLiberationFront || s.Story.HasMetSeokhyeon)) || s.Story.DongrimRescueCompleted!=s.Story.HasMetDongrim) throw new InvalidDataException("Invalid story gates.");
            if(s.PendingEncounter!=null && s.PendingEncounter.kind!="hungry" && s.PendingEncounter.kind!="trader") throw new InvalidDataException("Invalid pending encounter.");
            ValidateWorld(s,data);
            ValidateStory(s,data);
            ValidateErasureNews(s,data);
            ValidateDailyLife(s);
            ValidateLateStory(s,data);
            ValidateBrokenPath(s,data);
            ValidateCommunity(s,data);
        }
        static void ValidateLateStory(SurvivalState s,SurvivalDefinitions data)
        {
            var story=data.lateStory;
            if(s.LateStoryRevision!=SurvivalState.CurrentStoryRevision || s.LateFlags==null || s.EndingId==null || s.LateStep<0 || s.LateStep>=story.beats.Length || s.LateReadyDay<0 || s.Care<0 || s.LateTrust<0 || s.Isolation<0 || s.Lure<0) throw new InvalidDataException("Invalid late story progress.");
            if((s.LateStep>0 || s.LateScenePending || s.HasLateStoryResult || s.HasEnded || s.LateFlags.Count>0) && !s.LateStoryStarted) throw new InvalidDataException("Late story started early.");
            if(s.HasEnded && (story.Ending(s.EndingId)==null || s.LateScenePending)) throw new InvalidDataException("Invalid ending.");
            if(s.HasLateStoryResult && (s.LateScenePending || s.BrokenPathScenePending || s.PendingEncounter!=null || s.PendingDongrimMeeting || s.HasActiveCrowdIncident || s.IsGameOver)) throw new InvalidDataException("Overlapping story result.");
            foreach(var flag in s.LateFlags) if(string.IsNullOrWhiteSpace(flag) || s.LateFlags.FindAll(x=>x==flag).Count!=1) throw new InvalidDataException("Invalid late story flag.");
            var beat=s.CurrentBeat(data);
            if(s.LateScenePending && (beat==null || s.CurrentLocation!=beat.location || s.PendingEncounter!=null || s.PendingDongrimMeeting || s.HasActiveCrowdIncident || s.IsGameOver)) throw new InvalidDataException("Invalid late story scene.");
        }
        static void ValidateCommunity(SurvivalState s,SurvivalDefinitions data)
        {
            if(s.CommunityRequests==null || s.CommunityRequestDay<0 || s.CommunityRequestDay>s.day) throw new InvalidDataException("Invalid community request date.");
            foreach(var request in s.CommunityRequests)
                if(request==null || string.IsNullOrWhiteSpace(request.id) || string.IsNullOrWhiteSpace(request.text) || request.day<1 || request.day>s.CommunityRequestDay ||
                    data.Location(request.location)==null || request.amount<0 || request.amount>0 && data.Item(request.item)==null ||
                    request.rewardAmount<0 || request.rewardAmount>0 && data.Item(request.reward)==null ||
                    s.CommunityRequests.FindAll(x=>x.id==request.id).Count!=1) throw new InvalidDataException("Invalid community request.");
            var relay=s.Relay;
            if(relay?.nodes==null || relay.plan==null || relay.startDay<0 || relay.startDay>relay.cycleDay || relay.cycleDay>s.day ||
                relay.submittedDay<0 || relay.submittedDay>relay.cycleDay || relay.broadcasts<0 || relay.relayDays<0 || relay.supplyDays<0 ||
                relay.relayDays>relay.broadcasts || relay.supplyDays>relay.broadcasts || relay.checks<0 || relay.badDawns<0 || relay.handovers<0 ||
                relay.plan.Count>s.RelayDecisionLimit || relay.startDay==0 && (relay.nodes.Count!=0 || s.BroadcastPolicy!="") ||
                relay.startDay>0 && (relay.nodes.Count!=SurvivalState.RelayLocations.Length || s.BrokenPath.Stage!=4 || s.BroadcastPolicy==""))
                throw new InvalidDataException("Invalid relay progress.");
            ValidateInventory(relay.shelterStorage,data);
            var available=new System.Collections.Generic.HashSet<string>(s.RelayStaff());
            foreach(var node in relay.nodes)
                if(node==null || Array.IndexOf(SurvivalState.RelayLocations,node.id)<0 || node.supplies<0 || node.supplies>(node.shelf?4:3) ||
                    node.signal<0 || node.signal>3 || node.occupancy<0 || node.occupancy>3 || node.staff==null ||
                    node.staff!="" && (!available.Contains(node.staff) || relay.nodes.FindAll(x=>x.staff==node.staff).Count!=1) ||
                    relay.nodes.FindAll(x=>x.id==node.id).Count!=1) throw new InvalidDataException("Invalid relay node.");
            foreach(var action in relay.plan)
                if(action==null || s.RelayNodeAt(action.node)==null || action.action!="announce" && action.action!="staff" && action.action!="check" && action.action!="send")
                    throw new InvalidDataException("Invalid relay decision.");
        }
        static void ValidateBrokenPath(SurvivalState s,SurvivalDefinitions data)
        {
            var p=s.BrokenPath; var d=data.brokenPath;
            if(p==null || p.Surveyed==null || p.Site==null || p.Yard==null || p.Scene==null || p.CoreChoice==null || p.Method==null || p.Outcome==null || p.YeongsukState==null) throw new InvalidDataException("Missing broken path progress.");
            if(p.Stage<0 || p.Stage>4 || p.Helpers<0 || p.Helpers>2 || p.NamelessLost<0 || p.NamelessLost>d.maxNamelessLost || p.RescueDelayDays<0 || p.Readiness<-1 || p.Readiness>5 || p.Remaining<0 || p.FirstGroup<0 || p.Survivors<0 || p.Extra<0 || p.RescuedCount<0 || p.LostCount<0 || p.StartTime<0 || p.StartTime>s.Clock) throw new InvalidDataException("Invalid broken path numbers.");
            if(p.Scene!="" && d.Scene(p.Scene)==null || p.CoreChoice!="" && p.CoreChoice!="A" && p.CoreChoice!="B" || p.Method!="" && d.Method(p.Method)==null) throw new InvalidDataException("Invalid broken path ids.");
            if(p.Outcome!="" && p.Outcome!="A1" && p.Outcome!="A2" && p.Outcome!="B_Light" && p.Outcome!="B_Heavy" || p.YeongsukState!="" && p.YeongsukState!="Rescued" && p.YeongsukState!="Missing") throw new InvalidDataException("Invalid broken path outcome.");
            foreach(var id in p.Surveyed) if(d.Survey(id)==null || p.Surveyed.FindAll(x=>x==id).Count!=1) throw new InvalidDataException("Invalid broken path survey.");
            ValidateInventory(p.Site,data); ValidateInventory(p.Yard,data);
            foreach(var stack in p.Site.stacks) if(stack.amount>0 && !SurvivalState.IsLargeMaterial(stack.id)) throw new InvalidDataException("Only large materials are stacked at the bridge.");
            if(p.Stage>0 && !s.LateStoryStarted || p.Stage==0 && (p.Scene!="" || p.CoreChoice!="") || p.Stage>=2 && p.CoreChoice=="" || p.Stage==2 && (p.Built || p.Scene!="") || p.Stage>=3 && (!p.Built || p.Method=="") || p.Stage==4 && (p.Scene!="" || p.Outcome=="")) throw new InvalidDataException("Invalid broken path stage.");
            if(s.BrokenPathScenePending && (s.PendingEncounter!=null || s.LateScenePending || s.PendingDongrimMeeting || s.HasActiveCrowdIncident)) throw new InvalidDataException("Overlapping story scenes.");
        }
        static void ValidateDailyLife(SurvivalState s)
        {
            if(s.DailyFacts==null || s.Diaries==null || s.MorningIssues==null || s.DiaryStartDay<1 || s.DiaryStartDay>s.day || s.LastDiaryDay<0 || s.LastDiaryDay>=s.day || s.MorningLastDay<0 || s.MorningLastDay>s.day || s.MorningDeferredDay<0 || s.MorningDeferredDay>s.day || s.PartialDiaryDay<0 || s.PartialDiaryDay>s.day) throw new InvalidDataException("Invalid life records.");
            int previous=0;
            foreach(var entry in s.Diaries) { if(entry==null || entry.day<=previous || entry.day>=s.day || string.IsNullOrWhiteSpace(entry.body)) throw new InvalidDataException("Invalid diary."); previous=entry.day; }
            if(s.Diaries.Count>0 && previous!=s.LastDiaryDay) throw new InvalidDataException("Invalid diary date.");
            previous=0;
            foreach(var issue in s.MorningIssues) { if(issue==null || issue.day<=previous || issue.day>s.day || string.IsNullOrWhiteSpace(issue.body) || issue.kind!="first" && issue.kind!="regular" && issue.kind!="legacy" || !s.Story.HasRadio) throw new InvalidDataException("Invalid morning issue."); previous=issue.day; }
            foreach(var fact in s.DailyFacts) if(fact==null || fact.day<s.DiaryStartDay || fact.day>s.day || fact.hour<0 || fact.hour>23 || fact.count<1 || string.IsNullOrWhiteSpace(fact.kind) || string.IsNullOrWhiteSpace(fact.text)) throw new InvalidDataException("Invalid daily fact.");
            if(s.NeighborStage<0 || s.NeighborStage>3 || s.NeighborGreetings<0 || s.NeighborLastDay<0 || s.NeighborLastDay>s.day || !Enum.IsDefined(typeof(CompanionStep),s.Companion) || s.CompanionTrust<0 || s.CompanionTrust>3 || s.CompanionReadyDay<0 || s.CompanionMeal==null) throw new InvalidDataException("Invalid companion/neighbor progress.");
            if(s.Companion!=CompanionStep.None && (!s.Story.HasMetJiwoo || s.TotalDongrimMeetings<3) || s.HasActiveCompanion && (s.CurrentLocation!="school" || s.PendingEncounter!=null || s.HasActiveCrowdIncident || s.PendingDongrimMeeting)) throw new InvalidDataException("Invalid active companion story.");
            if(s.TowelDays<0 || s.TowelDays>s.day || s.TowelLastDay<0 || s.TowelLastDay>s.day || (s.TowelDays==0)!=(s.TowelLastDay==0)) throw new InvalidDataException("Invalid towel checks.");
            if(s.CompanionMeal!="" && s.CompanionMeal!="rice_meal" && s.CompanionMeal!="spam_meal" && s.CompanionMeal!="tuna_meal" && s.CompanionMeal!="ramen_meal") throw new InvalidDataException("Invalid companion meal.");
        }
        static void ValidateErasureNews(SurvivalState s,SurvivalDefinitions data)
        {
            if(s.ErasureHistory==null || s.ErasureRandomState==0 || s.LastErasureDay<0 || s.LastErasureDay>s.day || s.ErasureReadDay<0 || s.ErasureReadDay>s.LastErasureDay || s.ErasureHistory.Count>0 && !s.ErasureNewsEnabled) throw new InvalidDataException("Invalid erasure news state.");
            int previous=0; var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var report in s.ErasureHistory)
            {
                if(report==null || report.day<=previous || report.day>s.day || report.locationId==null) throw new InvalidDataException("Invalid daily erasure report.");
                if(report.locationId!="")
                {
                    if(data.erasureNews.Find(report.locationId)==null || !seen.Add(report.locationId)) throw new InvalidDataException("Invalid or repeated erasure location.");
                }
                else if(seen.Count!=data.erasureNews.locations.Length) throw new InvalidDataException("Missing erasure location before the catalog is exhausted.");
                previous=report.day;
            }
            if(s.ErasureHistory.Count>0 && previous!=s.LastErasureDay) throw new InvalidDataException("Invalid last erasure date.");
        }
        static void UpgradeLegacySession(SurvivalState s,SurvivalDefinitions data)
        {
            if(s==null) return;
            if(s.schemaVersion==4) { UpgradeWorld(s,data); return; }
            if(s.schemaVersion!=3) return;
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
            UpgradeWorld(s,data);
        }
        static void UpgradeWorld(SurvivalState s,SurvivalDefinitions data)
        {
            if(s.Locations==null || s.Locations.Count!=5 || s.Story==null) throw new InvalidDataException("Invalid v4 world.");
            foreach(string id in new[]{"home","store","empty_house","school","supply_site"})
                if(s.Locations.FindAll(x=>x!=null && x.id==id).Count!=1) throw new InvalidDataException("Invalid v4 location.");
            // Existing routes and exhausted pools stay intact. Only new locations get new pools.
            s.UnlockedLocations=new System.Collections.Generic.List<string>{"empty_house","school"};
            s.VisitedLocations=new System.Collections.Generic.List<string>{"home",s.CurrentLocation};
            if(s.CurrentLocation=="home") s.VisitedLocations.RemoveAt(1);
            foreach(var place in data.locations)
                if(!s.Locations.Exists(x=>x.id==place.id))
                {
                    var progress=new LocationProgress { id=place.id };
                    foreach(var stack in place.loot) progress.remaining.Add(stack.id,stack.amount);
                    s.Locations.Add(progress);
                }
            s.DongrimMeetings=s.Story.HasMetDongrim?1:0;
            s.LastDongrimMeetingDay=s.Story.HasMetDongrim?Math.Max(0,s.day-1):0;
            s.PendingDongrimMeeting=false; s.schemaVersion=5;
            s.DiscoverLocations(data);
        }
        static void ValidateWorld(SurvivalState s,SurvivalDefinitions data)
        {
            if(s.UnlockedLocations==null || s.VisitedLocations==null || s.DongrimMeetings<0 || s.LastDongrimMeetingDay<0 || s.LastDongrimMeetingDay>s.day || (s.Story.HasMetDongrim?s.DongrimMeetings<1:s.DongrimMeetings!=0 || s.LastDongrimMeetingDay!=0)) throw new InvalidDataException("Invalid world progress.");
            foreach(var id in s.UnlockedLocations) if(data.Location(id)==null || s.UnlockedLocations.FindAll(x=>x==id).Count!=1 || data.Location(id).requiresSupplyRumor) throw new InvalidDataException("Invalid unlocked location.");
            foreach(var id in s.VisitedLocations) if(data.Location(id)==null || s.VisitedLocations.FindAll(x=>x==id).Count!=1 || !s.IsLocationUnlocked(id,data) && !s.IsLocationErased(id)) throw new InvalidDataException("Invalid visited location.");
            if(s.PendingDongrimMeeting && (!s.Story.HasMetDongrim || s.CurrentLocation!=s.DongrimMeetingLocation || s.day<s.NextDongrimDay || s.IsNight(data.TEMP_BALANCE) || s.PendingEncounter!=null || s.HasActiveCrowdIncident || s.IsGameOver)) throw new InvalidDataException("Invalid Dongrim encounter.");
            foreach(var id in s.InstalledSecurity)
            {
                string required=data.Security(id).requiresSecurity;
                if(!string.IsNullOrEmpty(required) && !s.InstalledSecurity.Contains(required)) throw new InvalidDataException("Invalid door upgrade order.");
            }
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
