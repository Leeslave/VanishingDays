using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    // TEMP_BALANCE: the late story is a fixed chain of scenes; choice weights (care, trust, isolation, lure) are provisional.
    [Serializable] public class LateStoryChoice { public string label, detail, result, fact, requires, give, ending, flag; public int hours, amount, care, trust, isolation, lure; }
    [Serializable] public class LateStoryBeat { public string id, chapter, title, location, hint, body, condition, bodyIf, bodyElse, aftermath, unlocks; public int delayDays; public bool daytime; public LateStoryChoice[] choices; }
    [Serializable] public class LateEndingDefinition { public string id, title, body, log; }
    [Serializable] public class LateStoryDefinition
    {
        public int delegateTrust, lureEnding;
        public string lureTitle, lureBody, lureChoice, epilogue, neighborMoved;
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
            if(beats==null || beats.Length<2 || endings==null || delegateTrust<1 || lureEnding<1 || new[]{lureTitle,lureBody,lureChoice,epilogue,neighborMoved}.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Incomplete late story.");
            foreach(var ending in endings) if(ending==null || new[]{ending.id,ending.title,ending.body,ending.log}.Any(string.IsNullOrWhiteSpace) || Array.FindAll(endings,x=>x.id==ending.id).Length!=1) throw new InvalidOperationException("Invalid ending.");
            foreach(var id in new[]{"tomorrow_here","continued_care","closed_door","last_broadcast"}) if(Ending(id)==null) throw new InvalidOperationException("Missing ending: "+id);
            foreach(var beat in beats)
            {
                if(beat==null || new[]{beat.id,beat.chapter,beat.title,beat.location,beat.hint,beat.body}.Any(string.IsNullOrWhiteSpace) || data.Location(beat.location)==null || beat.delayDays<0 || beat.choices==null || beat.choices.Length<1 || beat.choices.Length>3 || Array.FindAll(beats,x=>x.id==beat.id).Length!=1) throw new InvalidOperationException("Invalid late story beat: "+beat?.id);
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
        public int LateStep, LateReadyDay, Care, LateTrust, Isolation, Lure;
        public bool LateScenePending;
        public string EndingId="";
        public List<string> LateFlags=new List<string>();
        static readonly string[] Meals={"rice_meal","spam_meal","tuna_meal","ramen_meal"};
        public bool HasEnded => !string.IsNullOrEmpty(EndingId);
        // The late story continues the first companion expedition; before that it stays silent.
        public bool LateStoryStarted => Companion==CompanionStep.Completed;
        public bool NeighborMoved => LateFlags.Contains("neighbor_moved");
        public LateStoryBeat CurrentBeat(SurvivalDefinitions data) => LateStoryStarted && !HasEnded && LateStep>=0 && LateStep<data.lateStory.beats.Length?data.lateStory.beats[LateStep]:null;
        public bool LureEndingDue(LateStoryBeat beat,SurvivalDefinitions data) => beat!=null && beat.id=="final_call" && Lure>=data.lateStory.lureEnding;
        public void UpgradeLateStory(SurvivalDefinitions data)
        {
            if(LateFlags==null) LateFlags=new List<string>();
            if(EndingId==null) EndingId="";
            // Story places added after a save was written join it untouched and still locked.
            foreach(var place in data.locations) if(Locations!=null && !Locations.Exists(x=>x!=null && x.id==place.id)) Locations.Add(new LocationProgress{id=place.id});
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
            if(beat==null || !CanAct || day<LateReadyDay || CurrentLocation!=beat.location || beat.daytime && IsNight(data.TEMP_BALANCE)) return false;
            LateScenePending=true; Note("["+beat.chapter+"] "+beat.title);
            return true;
        }
        bool LateCondition(string condition) => condition=="neighbor_met"?NeighborStage>0:LateFlags.Contains(condition);
        public string LateBeatText(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data); if(beat==null) return "";
            if(LureEndingDue(beat,data)) return Script(data.lateStory.lureBody);
            string text=beat.body;
            if(!string.IsNullOrEmpty(beat.condition)) { string extra=LateCondition(beat.condition)?beat.bodyIf:beat.bodyElse; if(!string.IsNullOrEmpty(extra)) text+="\n\n"+extra; }
            return Script(text);
        }
        int MealsAvailable => Meals.Sum(x=>Backpack.Count(x)+(CurrentLocation=="home"?HomeStorage.Count(x):0));
        public LateStoryChoice[] LateChoices(SurvivalDefinitions data)
        {
            var beat=CurrentBeat(data); if(beat==null) return new LateStoryChoice[0];
            if(LureEndingDue(beat,data)) return new[]{new LateStoryChoice{label=data.lateStory.lureChoice,detail="방송은 멈추지 않는다",result=data.lateStory.lureChoice,fact="라디오에서 내가 퍼뜨린 집결 안내가 반복됐다.",ending="last_broadcast"}};
            return beat.choices;
        }
        public bool CanChooseLate(int index,SurvivalDefinitions data)
        {
            var choices=LateChoices(data);
            if(!LateScenePending || IsGameOver || index<0 || index>=choices.Length) return false;
            var choice=choices[index];
            return choice.requires!="meal" && choice.requires!="trust" || choice.requires=="meal" && MealsAvailable>0 || choice.requires=="trust" && LateTrust>=data.lateStory.delegateTrust;
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
            if(!string.IsNullOrEmpty(choice.flag) && !LateFlags.Contains(choice.flag)) LateFlags.Add(choice.flag);
            RecordFact("story",Script(choice.fact));
            string text=Script(choice.result+(string.IsNullOrEmpty(beat.aftermath)?"":"\n\n"+beat.aftermath));
            if(!string.IsNullOrEmpty(choice.ending)) { EndingId=choice.ending; return Report(text); }
            LateStep++;
            var next=CurrentBeat(data);
            if(next!=null) { LateReadyDay=day+next.delayDays; text+="\n\n"+LateHint(data); }
            return Report(text);
        }
        void UnlockLateStory(string id,SurvivalDefinitions data)
        {
            if(!LateFlags.Contains(id)) LateFlags.Add(id);
            // An ordinary place the story points to opens even if the player never searched their way there.
            var place=data.Location(id);
            if(place!=null && string.IsNullOrEmpty(place.requiresStoryFlag) && !IsLocationUnlocked(id,data)) UnlockedLocations.Add(id);
        }
        public string LateEndingText(SurvivalDefinitions data)
        {
            var ending=data.lateStory.Ending(EndingId); if(ending==null) return "";
            return Script(ending.body)+"\n\n"+ending.log+"\n\n"+Script(data.lateStory.epilogue);
        }
    }
}
