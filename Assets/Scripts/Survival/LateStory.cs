using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    // TEMP_BALANCE: the late story is a fixed chain of scenes; choice weights (care, trust, isolation, lure) are provisional.
    [Serializable] public class LateStoryChoice { public string label, detail, result, fact, requires, give, ending, flag; public int hours, amount, care, trust, isolation, lure; }
    [Serializable] public class LateStoryBeat { public string id, chapter, title, location, hint, body, condition, bodyIf, bodyElse, aftermath, unlocks, erases, route; public int delayDays, act; public bool daytime; public LateStoryChoice[] choices; }
    [Serializable] public class LateEndingDefinition { public string id, title, body; }
    [Serializable] public class LateStoryDefinition
    {
        public int delegateTrust, lureEnding;
        public string lureTitle, lureGatherBody, lureChoice, gatherBroadcast, archive, neighborMoved;
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
            foreach(var ending in endings) if(ending==null || new[]{ending.id,ending.title,ending.body}.Any(string.IsNullOrWhiteSpace) || Array.FindAll(endings,x=>x.id==ending.id).Length!=1) throw new InvalidOperationException("Invalid ending.");
            foreach(var id in new[]{"tomorrow_here","tomorrow_delegated","closed_door","last_broadcast","collapse"}) if(Ending(id)==null) throw new InvalidOperationException("Missing ending: "+id);
            foreach(var beat in beats)
            {
                if(beat==null || new[]{beat.id,beat.chapter,beat.title,beat.location,beat.hint,beat.body}.Any(string.IsNullOrWhiteSpace) || data.Location(beat.location)==null || beat.delayDays<0 || beat.choices==null || beat.choices.Length<1 || beat.choices.Length>3 || Array.FindAll(beats,x=>x.id==beat.id).Length!=1) throw new InvalidOperationException("Invalid late story beat: "+beat?.id);
                if(beat.act<2 || beat.act>5 || !string.IsNullOrEmpty(beat.route) && beat.route!="A1" || !string.IsNullOrEmpty(beat.erases) && (data.Location(beat.erases)==null || beat.erases=="home" || beat.erases==beat.location)) throw new InvalidOperationException("Invalid act or erasure: "+beat.id);
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
        public bool HasTrueArchive => EndingId=="tomorrow_delegated" || EndingId=="tomorrow_here" && LateFlags.Contains("archive_earned");
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
            if(EndingId!="tomorrow_here") return;
            if(LateFlags.Contains("neighbor_delegated")) EndingId="tomorrow_delegated";
            else UnlockLateStory("archive_earned",data);
        }
        public bool IsStoryLocationOpen(LocationDefinition place) => string.IsNullOrEmpty(place.requiresStoryFlag) || LateFlags.Contains(place.requiresStoryFlag);
        public string LateHint(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data); if(beat==null) return "";
            return "["+beat.chapter+"] DAY "+Math.Max(day,LateReadyDay)+"부터"+(beat.daytime?" 낮에":"")+" "+data.Location(beat.location).name+" · "+beat.hint;
        }
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
            if(beat==null || !CanAct || day<LateReadyDay || CurrentLocation!=beat.location || beat.daytime && IsNight(data.TEMP_BALANCE)) return false;
            if(beat.id=="network_trial" && !RelayReviewReady) return false;
            if(beat.id=="core_choice")
            {
                StartBrokenPath();
                return true;
            }
            LateScenePending=true; Note("["+beat.chapter+"] "+beat.title);
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
            if(LureEndingDue(beat)) return new[]{new LateStoryChoice{label=data.lateStory.lureChoice,detail="방송은 멈추지 않는다",result="라디오 속 목소리가 다시 집결지를 가리켰다.",fact="라디오에서 내가 퍼뜨린 집결 안내가 반복됐다.",ending="last_broadcast"}};
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
            if(choice.hours>0) { AdvanceTime(choice.hours,data); if(IsGameOver) return Report("끝까지 해낼 힘이 남지 않았다."); }
            if(!string.IsNullOrEmpty(choice.give)) Backpack.Add(choice.give,choice.amount);
            Care+=choice.care; LateTrust+=choice.trust; Isolation+=choice.isolation; Lure+=choice.lure;
            if(!string.IsNullOrEmpty(beat.unlocks)) foreach(var id in beat.unlocks.Split(',')) UnlockLateStory(id.Trim(),data);
            if(!string.IsNullOrEmpty(choice.flag)) UnlockLateStory(choice.flag,data);
            if(!string.IsNullOrEmpty(beat.erases)) EraseLocation(beat.erases,data);
            if(beat.id=="three_posts" && IsLocationUnlocked("soup_house",data)) EraseLocation("soup_house",data);
            if(beat.id=="seokhyeon_meet") Story.HasMetSeokhyeon=true;
            if(beat.id=="endless") BeginRelayNetwork(data);
            RecordFact("story",Script(choice.fact));
            string text=Script(choice.result+(string.IsNullOrEmpty(beat.aftermath)?"":"\n\n"+beat.aftermath));
            if(!string.IsNullOrEmpty(choice.ending))
            {
                EndingId=choice.ending switch
                {
                    "last_broadcast"=>"last_broadcast",
                    _ when BroadcastPolicy=="silent"=>"collapse",
                    "tomorrow_delegated" when !CanReachTrueEnding=>"collapse",
                    _=>choice.ending
                };
                return Report(text);
            }
            AdvanceLateStory(data);
            return Report(text);
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
        public string LateEndingText(SurvivalDefinitions data)
        {
            return Script(data.lateStory.Ending(EndingId)?.body??"");
        }
    }
}
