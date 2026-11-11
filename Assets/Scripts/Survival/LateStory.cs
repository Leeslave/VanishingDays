using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    // TEMP_BALANCE: the late story is a fixed chain of scenes; choice weights (care, trust, isolation, lure) are provisional.
    [Serializable] public class LateStoryChoice { public string label, detail, result, fact, requires, give, ending, flag; public int hours, amount, care, trust, isolation, lure; }
    [Serializable] public class LateStoryBeat { public string id, chapter, title, location, hint, body, condition, bodyIf, bodyElse, aftermath, unlocks, erases, route, window; public int delayDays, act, endHour, mealRelief, leadHours; public bool daytime, endsAtHome; public LateStoryChoice[] choices; }
    [Serializable] public class LateEndingDefinition { public string id, title, body; }
    [Serializable] public class LateStoryDefinition
    {
        public int delegateTrust, lureEnding;
        public string lureTitle, lureGatherBody, lureChoice, gatherBroadcast, archive, neighborMoved;
        public string towelWaiting, towelDark, towelAway, towelDone, towelMissing, towelHabit, towelNever;
        public string[] towelSeen;
        public LateStoryBeat[] beats;
        public LateEndingDefinition[] endings;
        public LateEndingDefinition Ending(string id) => Array.Find(endings,x=>x.id==id);
        public static LateStoryDefinition Load()
        {
            var asset=Resources.Load<TextAsset>("Survival/LateStory");
            if(asset==null) throw new InvalidOperationException("Missing late story.");
            return JsonUtility.FromJson<LateStoryDefinition>(asset.text);
        }
        public void Validate(SurvivalDefinitions data)
        {
            if(beats==null || beats.Length<2 || endings==null || delegateTrust<1 || lureEnding<1 || new[]{lureTitle,lureGatherBody,lureChoice,gatherBroadcast,archive,neighborMoved}.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Incomplete late story.");
            if(towelSeen==null || towelSeen.Length<1 || towelSeen.Concat(new[]{towelWaiting,towelDark,towelAway,towelDone,towelMissing,towelHabit,towelNever}).Any(string.IsNullOrWhiteSpace) || !towelHabit.Contains("{TowelDays}")) throw new InvalidOperationException("Incomplete towel checks.");
            foreach(var ending in endings) if(ending==null || new[]{ending.id,ending.title,ending.body}.Any(string.IsNullOrWhiteSpace) || Array.FindAll(endings,x=>x.id==ending.id).Length!=1) throw new InvalidOperationException("Invalid ending.");
            foreach(var id in new[]{"tomorrow_here","tomorrow_delegated","closed_door","last_broadcast","collapse"}) if(Ending(id)==null) throw new InvalidOperationException("Missing ending: "+id);
            if(!archive.Contains("{LedgerNames}") || !archive.Contains("{ObservedDays}")) throw new InvalidOperationException("The archive must name its keepers and observed days.");
            foreach(var beat in beats)
            {
                if(beat==null || new[]{beat.id,beat.chapter,beat.title,beat.location,beat.hint,beat.body}.Any(string.IsNullOrWhiteSpace) || data.Location(beat.location)==null || beat.delayDays<0 || beat.choices==null || beat.choices.Length<1 || beat.choices.Length>3 || Array.FindAll(beats,x=>x.id==beat.id).Length!=1) throw new InvalidOperationException("Invalid late story beat: "+beat?.id);
                if(beat.act<2 || beat.act>5 || !string.IsNullOrEmpty(beat.route) && beat.route!="A1" || !string.IsNullOrEmpty(beat.erases) && (data.Location(beat.erases)==null || beat.erases=="home" || beat.erases==beat.location)) throw new InvalidOperationException("Invalid act or erasure: "+beat.id);
                if(!string.IsNullOrEmpty(beat.window) && beat.window!="morning" && beat.window!="evening" || beat.endHour<0 || beat.endHour>23 || beat.leadHours<0 || beat.leadHours>6) throw new InvalidOperationException("Invalid scene time: "+beat.id);
                foreach(var choice in beat.choices)
                {
                    if(choice==null || new[]{choice.label,choice.detail,choice.result,choice.fact}.Any(string.IsNullOrWhiteSpace) || choice.hours<0 || choice.care<0 || choice.trust<0 || choice.isolation<0 || choice.lure<0) throw new InvalidOperationException("Invalid late story choice in "+beat.id);
                    if(!string.IsNullOrEmpty(choice.give) && (data.Item(choice.give)==null || choice.amount<1) || !string.IsNullOrEmpty(choice.ending) && Ending(choice.ending)==null) throw new InvalidOperationException("Invalid late story reward/ending in "+beat.id);
                    if(!string.IsNullOrEmpty(choice.requires) && choice.requires!="meal" && choice.requires!="trust") throw new InvalidOperationException("Unknown late story requirement in "+beat.id);
                }
            }
            if(beats.Last().choices.Any(x=>string.IsNullOrEmpty(x.ending))) throw new InvalidOperationException("The last scene must end the story.");
        }
    }
    public partial class SurvivalState
    {
        internal const int CurrentStoryRevision=2;
        static readonly string[] PreviousBeats={"absent_share","rumor_table","false_report","crowd_hall","records_compare","patrol_past","neighbor_moves","seokhyeon_reports","seokhyeon_meet","erasing_each_other","observation","endless","final_call","final_visit"};
        static readonly string[] RevisionOneBeats={
            "yeongsuk_soup","absent_share","rumor_table","late_medicine","patrol_past","records_compare","neighbor_moves",
            "seokhyeon_reports","old_friends","seokhyeon_meet","joint_rules","three_posts","false_report","crowd_hall",
            "post_vanished","core_choice","erasing_each_other","observation","sp_crosscheck","jiwoo_grief","jiwoo_handover",
            "endless","network_trial","quiet_posts","final_call","final_visit"
        };
        public int LateStoryRevision;
        public int LateStep, LateReadyDay, Care, LateTrust, Isolation, Lure;
        // Clock time the last scene ended, so a scene chained in the same place can let the hours it narrates pass first.
        public int LateLastSceneAt;
        public bool LateScenePending;
        public string LateStoryResult="";
        public string EndingId="";
        public List<string> LateFlags=new List<string>();
        static readonly string[] Meals={"rice_meal","spam_meal","tuna_meal","ramen_meal"};
        public bool HasEnded => !string.IsNullOrEmpty(EndingId);
        public bool HasLateStoryResult => !string.IsNullOrEmpty(LateStoryResult);
        // The late story continues the first companion expedition; before that it stays silent.
        public bool LateStoryStarted => Companion==CompanionStep.Completed;
        public int CurrentAct(SurvivalDefinitions data) => HasEnded?5:CurrentBeat(data)?.act??(Story.CrowdIncidentResolved?2:1);
        public bool NeighborMoved => LateFlags.Contains("neighbor_moved");
        public LateStoryBeat CurrentBeat(SurvivalDefinitions data) => LateStoryStarted && !HasEnded && LateStep>=0 && LateStep<data.lateStory.beats.Length?data.lateStory.beats[LateStep]:null;
        public bool LureEndingDue(LateStoryBeat beat) => beat?.id=="final_call" && BroadcastPolicy=="gather";
        public bool HasTrueArchive => EndingId=="tomorrow_delegated" || EndingId=="tomorrow_here";
        void InitializeLateStory() => LateStoryRevision=CurrentStoryRevision;
        public void UpgradeLateStory(SurvivalDefinitions data)
        {
            if(LateFlags==null) LateFlags=new List<string>();
            if(EndingId==null) EndingId="";
            if(LateStoryRevision==1 && LateStep>=0 && LateStep<RevisionOneBeats.Length)
            {
                string resumeId=RevisionOneBeats[LateStep];
                if(!HasEnded)
                {
                    if(resumeId=="post_vanished")
                    {
                        UnlockLateStory("previous_story:false_report",data);
                        UnlockLateStory("previous_story:crowd_hall",data);
                    }
                    else if(resumeId=="false_report" || resumeId=="crowd_hall") EraseLocation("river_post",data);
                }
                LateStep=Array.FindIndex(data.lateStory.beats,x=>x.id==resumeId);
                UpgradeEndingId(data);
                InitializeLateStory();
                return;
            }
            if(LateStoryRevision!=0 || LateStep<0 || LateStep>=PreviousBeats.Length) return;
            string old=PreviousBeats[LateStep];
            string resume=old;
            if(!HasEnded)
            {
                foreach(string completed in PreviousBeats.Take(LateStep).Where(id=>id=="false_report" || id=="crowd_hall" || id=="records_compare" || id=="erasing_each_other" || id=="observation"))
                {
                    string flag="previous_story:"+completed;
                    if(!LateFlags.Contains(flag)) LateFlags.Add(flag);
                }
                if(BrokenPath?.Stage>0)
                {
                    resume=BrokenPath.Stage<4?"core_choice":"erasing_each_other";
                    UnlockLateStory("neighbor_moved",data);
                    UnlockLateStory("pharmacy",data);
                    Story.HasMetSeokhyeon=true;
                }
                else if(LateStep>=9) resume="joint_rules";
                else if(old=="false_report" || old=="crowd_hall") resume="patrol_past";
                else if(old=="absent_share" && !LateScenePending) resume="yeongsuk_soup";
                if(resume!=old) LateScenePending=false;
                if(resume=="joint_rules") UnlockLateStory("relay_room",data);
                if(LateStep>=1) UnlockLateStory("construction_site",data);
            }
            LateStep=Array.FindIndex(data.lateStory.beats,x=>x.id==resume);
            UpgradeEndingId(data);
            InitializeLateStory();
        }
        void UpgradeEndingId(SurvivalDefinitions data)
        {
            if(EndingId=="continued_care") EndingId="collapse";
            if(EndingId=="tomorrow_here" && LateFlags.Contains("neighbor_delegated")) EndingId="tomorrow_delegated";
        }
        public bool IsStoryLocationOpen(LocationDefinition place) => string.IsNullOrEmpty(place.requiresStoryFlag) || LateFlags.Contains(place.requiresStoryFlag);
        public string LateHint(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data); if(beat==null) return "";
            // While the bridge is being repaired the queued scene is not the current goal.
            if(BrokenPathFieldWork) return "["+beat.chapter+"] "+data.brokenPath.hint;
            // The date is only worth showing while it is still ahead; a hint that already names the place keeps it once.
            string when=beat.window=="morning"?"아침에 ":beat.window=="evening"?"저녁에 ":beat.daytime?"낮에 ":"";
            bool toldWhen=when!="" && beat.hint.StartsWith(when,StringComparison.Ordinal);
            string lead=(LateReadyDay>day?"DAY "+LateReadyDay+"부터 ":"")+(toldWhen?"":when);
            string place=data.Location(beat.location).name;
            string text=(toldWhen?beat.hint.Substring(when.Length):beat.hint).StartsWith(place,StringComparison.Ordinal)?lead+beat.hint:lead+place+" · "+beat.hint;
            if(beat.id=="network_trial" && !RelayReviewReady && RelayActive) text+=" (거점 방송 "+Relay.broadcasts+"/2회, 또는 DAY "+(Relay.startDay+3)+"부터)";
            return "["+beat.chapter+"] "+text;
        }
        // Scenes that open on a time of day wait for it, so the clock agrees with the narration.
        bool InBeatWindow(LateStoryBeat beat,SurvivalDefinitions data) => beat.window switch
        {
            "morning"=>hour>=data.TEMP_BALANCE.dayStarts && hour<12,
            "evening"=>hour>=17,
            _=>true
        };
        public bool TryBeginLateStory(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data);
            // Act 3 holds the later scenes until the people on the far side of the bridge are brought back.
            if(BrokenPathActive) return false;
            if(CanAct && Relay.collapseDue && BroadcastPolicy!="gather")
            {
                EndingId="collapse";
                Note("연락망과 배분 창고가 함께 멈췄다.");
                return true;
            }
            if(beat==null || !CanAct || day<LateReadyDay || CurrentLocation!=beat.location || beat.daytime && IsNight(data.TEMP_BALANCE) || !InBeatWindow(beat,data)) return false;
            if(beat.id=="network_trial" && !RelayReviewReady) return false;
            if(beat.id=="core_choice")
            {
                StartBrokenPath();
                return true;
            }
            // Scenes chained in one place narrate the hours between them; the clock catches up before the next one.
            int since=Clock-LateLastSceneAt, wait=LateLastSceneAt>0 && since>=0 && since<beat.leadHours?beat.leadHours-since:0;
            if(wait>0) { AdvanceTime(wait,data); if(IsGameOver) return true; }
            LateScenePending=true; Note("["+beat.chapter+"] "+beat.title+(wait>0?" · "+wait+"시간 뒤":""));
            return true;
        }
        bool LateCondition(string condition) => condition switch
        {
            "neighbor_met" => NeighborStage>0,
            "seokhyeon_coop" => BrokenPath.SeokhyeonCoop,
            "donglim_lost" => !BrokenPath.DonglimAlive,
            _ => LateFlags.Contains(condition)
        };
        public string LateBeatText(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data); if(beat==null) return "";
            if(LureEndingDue(beat)) return Script(data.lateStory.lureGatherBody);
            string text=beat.body;
            // The final morning measures the missing towel against how often the player actually looked.
            if(beat.id=="final_call")
            {
                int cut=text.IndexOf("\n\n",StringComparison.Ordinal);
                string habit=TowelDays>0?data.lateStory.towelHabit.Replace("{TowelDays}",TowelDays.ToString()):data.lateStory.towelNever;
                text=cut<0?text+" "+habit:text.Insert(cut," "+habit);
            }
            if(!string.IsNullOrEmpty(beat.condition))
            {
                string extra=LateCondition(beat.condition)?beat.bodyIf:beat.bodyElse;
                if(!string.IsNullOrEmpty(extra)) text=beat.id=="jiwoo_handover"?extra+"\n\n"+text:text+"\n\n"+extra;
            }
            return Script(text);
        }
        int MealsAvailable => Meals.Sum(x=>Backpack.Count(x)+(CurrentLocation=="home"?HomeStorage.Count(x):0));
        public LateStoryChoice[] LateChoices(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data); if(beat==null) return new LateStoryChoice[0];
            if(LureEndingDue(beat)) return new[]{new LateStoryChoice{label=data.lateStory.lureChoice,detail="방송은 멈추지 않는다",result="라디오 속 목소리가 다시 집결지를 가리켰다.",fact="라디오에서 직접 읽었던 집결 안내가 반복됐다.",ending="last_broadcast"}};
            return beat.choices;
        }
        public bool CanChooseLate(int index,SurvivalDefinitions data)
        {
            var choices=LateChoices(data);
            if(!LateScenePending || IsGameOver || index<0 || index>=choices.Length) return false;
            var choice=choices[index];
            return choice.requires switch
            {
                "meal"=>MealsAvailable>0,
                "trust"=>LateFlags.Contains("jiwoo_shared") || LateTrust>=data.lateStory.delegateTrust,
                _=>true
            };
        }
        public string ChooseLateStory(int index,SurvivalDefinitions data)
        {
            if(!CanChooseLate(index,data)) return "지금은 그 선택을 할 수 없다.";
            var beat=CurrentBeat(data); var choice=LateChoices(data)[index];
            LateScenePending=false;
            if(choice.requires=="meal") { string meal=Meals.First(x=>Backpack.Count(x)>0 || CurrentLocation=="home" && HomeStorage.Count(x)>0); (Backpack.Count(meal)>0?Backpack:HomeStorage).Remove(meal,1); }
            int sceneHours=Math.Max(choice.hours,beat.endHour-hour);
            if(sceneHours>0) { AdvanceTime(sceneHours,data); if(IsGameOver) return Report("끝까지 해낼 힘이 남지 않았다."); }
            if(!string.IsNullOrEmpty(choice.give)) Backpack.Add(choice.give,choice.amount);
            Care+=choice.care; LateTrust+=choice.trust; Isolation+=choice.isolation; Lure+=choice.lure;
            if(!string.IsNullOrEmpty(beat.unlocks)) foreach(var id in beat.unlocks.Split(',')) UnlockLateStory(id.Trim(),data);
            if(!string.IsNullOrEmpty(choice.flag)) UnlockLateStory(choice.flag,data);
            // A place erased by the scene is announced with it, not only in the next day's record.
            var erased=new List<string>();
            if(!string.IsNullOrEmpty(beat.erases) && !IsLocationErased(beat.erases)) { erased.Add(data.Location(beat.erases).name); EraseLocation(beat.erases,data); }
            if(beat.id=="three_posts" && IsLocationUnlocked("soup_house",data)) { erased.Add(data.Location("soup_house").name); EraseLocation("soup_house",data); }
            if(beat.id=="seokhyeon_meet") Story.HasMetSeokhyeon=true;
            if(beat.id=="endless") BeginRelayNetwork(data);
            LateLastSceneAt=Clock;
            // The final morning's two bowls come out of the rice at home.
            int rice=beat.id=="final_call" && string.IsNullOrEmpty(choice.ending) && CurrentLocation=="home"?Math.Min(200,HomeStorage.Count("rice")):0;
            if(rice>0) HomeStorage.Remove("rice",rice);
            // A scene that ends at home walks the way back, so the clock and the night agree with the text.
            int walk=beat.endsAtHome && CurrentLocation!="home"?data.Location(CurrentLocation).travelHours:0;
            string from=data.Location(CurrentLocation).name;
            if(beat.endsAtHome) { if(walk>0) AdvanceTime(walk,data); if(IsGameOver) return Report("돌아갈 힘이 남지 않았다."); FinishSceneAtHome(data); }
            RecordFact("story",Script(choice.fact));
            string text=Script(choice.result+(string.IsNullOrEmpty(beat.aftermath)?"":"\n\n"+beat.aftermath));
            if(rice>0) text+="\n\n[식량 · 쌀 -"+rice+"g]";
            foreach(string name in erased) text+="\n\n[지도 · "+name+" 사라짐]";
            if(walk>0) text+="\n\n[귀가 · "+from+"에서 "+walk+"시간 · "+hour.ToString("00")+":00 집 도착]";
            // A meal eaten inside the scene counts like any other meal.
            if(beat.mealRelief>0)
            {
                Vitals.Hunger=Math.Max(0,Vitals.Hunger-beat.mealRelief); UpdateHungerPenalty(data.TEMP_BALANCE);
                text+="\n\n[식사 · 허기 -"+beat.mealRelief+"]";
            }
            if(!string.IsNullOrEmpty(choice.ending))
            {
                EndingId=choice.ending switch
                {
                    "last_broadcast" or "closed_door"=>choice.ending,
                    _ when BroadcastPolicy=="silent"=>"collapse",
                    "tomorrow_delegated" or "tomorrow_here" when !CanReachTrueEnding=>"collapse",
                    _=>choice.ending
                };
                return Report(text);
            }
            AdvanceLateStory(data);
            return Report(text);
        }
        void FinishSceneAtHome(SurvivalDefinitions data)
        {
            if(CurrentLocation=="home") return;
            string previous=CurrentLocation;
            CurrentLocation="home";
            FinishDeferredErasure(previous,data);
            MarkVisited("home");
            AwayFromHome=false;
            StoreCarriedOnReturn(data);
        }
        void AdvanceLateStory(SurvivalDefinitions data)
        {
            LateStep++;
            var next=CurrentBeat(data);
            while(next!=null && (LateFlags.Contains("previous_story:"+next.id) || !string.IsNullOrEmpty(next.route) && next.route!=BrokenPath.Outcome))
            {
                LateStep++;
                next=CurrentBeat(data);
            }
            if(next!=null) LateReadyDay=day+next.delayDays;
        }
        void UnlockLateStory(string id,SurvivalDefinitions data)
        {
            if(!LateFlags.Contains(id)) LateFlags.Add(id);
            // An ordinary place the story points to opens even if the player never searched their way there.
            var place=data.Location(id);
            if(place!=null && string.IsNullOrEmpty(place.requiresStoryFlag) && !IsLocationErased(id) && !IsLocationUnlocked(id,data)) UnlockedLocations.Add(id);
        }
        // EXP-01 counts the days actually observed and only lists the people the ledger was actually kept by.
        public string ArchiveText(SurvivalDefinitions data)
        {
            string names=BrokenPath!=null && BrokenPath.Stage>0 && !BrokenPath.DonglimAlive
                ?"{PlayerNickname}, 한지우의 이름이 있었다. 그보다 앞장에는 최동림의 글씨가 남아 있었다."
                :"{PlayerNickname}, 최동림, 한지우의 이름이 있었다.";
            return Script(data.lateStory.archive.Replace("{LedgerNames}",names).Replace("{ObservedDays}",day.ToString()));
        }
        public string LateEndingText(SurvivalDefinitions data)
        {
            return Script(data.lateStory.Ending(EndingId)?.body??"");
        }
    }
}
