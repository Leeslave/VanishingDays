using System;
using System.Collections.Generic;
using UnityEngine;

namespace VanishingDays
{
    // Act 3 「끊어진 길」: the core choice, then surveying, carrying and building a way across the
    // severed Seongnae footbridge, then the evacuation. Design: Documentation/BrokenPath-Design.md.
    [Serializable] public class BrokenPathChoice { public string label, detail, action; }
    [Serializable] public class BrokenPathScene { public string id, chapter, title, body; public BrokenPathChoice[] choices; }
    [Serializable] public class BrokenPathMethod
    {
        public string id, name, summary, crossing, injured, retreat, builtLine, requires, crossingName, carryLine, doorLine;
        public int hours, minHours, helperHours, score;
        public ItemStack[] large, small;
        public int BuildHours(int helpers) => Math.Max(minHours,hours-helperHours*helpers);
    }
    [Serializable] public class BrokenPathSurvey { public string id, label, detail, body, bodyB; }
    [Serializable] public class BrokenPathDefinition
    {
        public int helperTrustOne, helperTrustTwo, steadyHours, shakingHours, peopleA, peopleB, maxNamelessLost;
        public int[] remainingByReadiness;
        public string hint, decayNote, lightLine, heavyLine;
        public ItemStack[] yard;
        public BrokenPathMethod[] methods;
        public BrokenPathSurvey[] surveys;
        public string[] farReportsA, farReportsB;
        public BrokenPathScene[] scenes;
        public BrokenPathScene Scene(string id) => Array.Find(scenes,x=>x.id==id);
        public BrokenPathMethod Method(string id) => Array.Find(methods,x=>x.id==id);
        public BrokenPathSurvey Survey(string id) => Array.Find(surveys,x=>x.id==id);
        public static BrokenPathDefinition Load()
        {
            var asset=Resources.Load<TextAsset>("Survival/BrokenPath");
            if(asset==null) throw new InvalidOperationException("Missing broken path script.");
            return JsonUtility.FromJson<BrokenPathDefinition>(asset.text);
        }
        public void Validate(SurvivalDefinitions data)
        {
            if(methods==null || methods.Length!=3 || surveys==null || surveys.Length!=4 || scenes==null || yard==null || remainingByReadiness==null || remainingByReadiness.Length!=5) throw new InvalidOperationException("Incomplete broken path script.");
            if(farReportsA==null || farReportsA.Length!=3 || farReportsB==null || farReportsB.Length!=3 || steadyHours<1 || shakingHours<=steadyHours || peopleA<5 || peopleB<5 || maxNamelessLost<0) throw new InvalidOperationException("Invalid broken path balance.");
            foreach(var method in methods)
            {
                if(string.IsNullOrWhiteSpace(method.id) || string.IsNullOrWhiteSpace(method.name) || string.IsNullOrWhiteSpace(method.crossingName) || string.IsNullOrWhiteSpace(method.carryLine) || string.IsNullOrWhiteSpace(method.doorLine) || Survey(method.requires)==null || method.minHours<1 || method.hours<method.minHours || method.large==null || method.small==null) throw new InvalidOperationException("Invalid construction method: "+method.id);
                foreach(var cost in method.large) if(!SurvivalState.IsLargeMaterial(cost.id) || cost.amount<1) throw new InvalidOperationException("Large materials must be ladders or planks.");
                foreach(var cost in method.small) if(data.Item(cost.id)==null || SurvivalState.IsLargeMaterial(cost.id) || cost.amount<1) throw new InvalidOperationException("Invalid small material.");
            }
            foreach(var stack in yard) if(data.Item(stack.id)==null || stack.amount<1) throw new InvalidOperationException("Invalid construction yard stock.");
            foreach(var scene in scenes)
            {
                if(string.IsNullOrWhiteSpace(scene.id) || string.IsNullOrWhiteSpace(scene.body) || scene.choices==null || scene.choices.Length<1 || scene.choices.Length>3) throw new InvalidOperationException("Invalid broken path scene: "+scene.id);
                foreach(var choice in scene.choices)
                {
                    string action=choice.action??"";
                    bool known=action=="core_A" || action=="core_B" || action=="field" || action=="A1" || action=="A2" || action=="finish" || action.StartsWith("next:") && Scene(action.Substring(5))!=null;
                    if(!known || string.IsNullOrWhiteSpace(choice.label)) throw new InvalidOperationException("Invalid broken path choice in "+scene.id);
                }
            }
            foreach(var id in new[]{"core_choice","path_broken_A","path_broken_B","evac_A","evac_B","result_A1","result_A2","parting","empty_ladle"}) if(Scene(id)==null) throw new InvalidOperationException("Missing broken path scene: "+id);
        }
    }

    [Serializable] public class BrokenPathProgress
    {
        // 0 not started · 1 core choice and the call for help · 2 field work · 3 evacuation scenes · 4 finished.
        public int Stage;
        public string Scene="", CoreChoice="", Method="", Outcome="", YeongsukState="";
        public int StartTime, Helpers;
        public List<string> Surveyed=new List<string>();
        // Large materials stacked at the near end of the bridge, and what is left at the construction site.
        public Inventory Site=new Inventory(), Yard=new Inventory();
        public bool Built, DonglimAlive=true, SeokhyeonCoop;
        public int Readiness, Remaining, FirstGroup, Survivors, Extra, NamelessLost, RescueDelayDays, RescuedCount, LostCount;
    }

    public partial class SurvivalState
    {
        public BrokenPathProgress BrokenPath=new BrokenPathProgress();
        public bool BrokenPathScenePending => BrokenPath!=null && !string.IsNullOrEmpty(BrokenPath.Scene);
        public bool BrokenPathActive => BrokenPath!=null && BrokenPath.Stage>=1 && BrokenPath.Stage<4;
        public bool BrokenPathFieldWork => BrokenPath!=null && BrokenPath.Stage==2;
        public int Clock => day*24+hour;
        public static bool IsLargeMaterial(string id) => id=="ladder" || id=="steel_plank";
        // One large piece per trip, one more for each helper walking with you.
        public int CarryCapacity => 1+(BrokenPath?.Helpers??0);
        public int CarriedLarge => Backpack.Count("ladder")+Backpack.Count("steel_plank");

        // The far side worsens with time since the bridge broke; the player only ever hears it over the radio.
        public int FarState(SurvivalDefinitions data)
        {
            var d=data.brokenPath; int elapsed=Clock-BrokenPath.StartTime;
            return elapsed<d.steadyHours?0:elapsed<d.shakingHours?1:2;
        }
        int FarPeople(SurvivalDefinitions data) => BrokenPath.CoreChoice=="A"?data.brokenPath.peopleA+1-BrokenPath.NamelessLost:data.brokenPath.peopleB-BrokenPath.NamelessLost;
        public string FarReport(SurvivalDefinitions data)
        {
            var reports=BrokenPath.CoreChoice=="A"?data.brokenPath.farReportsA:data.brokenPath.farReportsB;
            return BrokenPathText(reports[FarState(data)],data);
        }
        public string BrokenPathText(string text,SurvivalDefinitions data)
        {
            var d=data.brokenPath; var p=BrokenPath; var method=d.Method(p.Method);
            return Script((text??"").Replace("{Remaining}",p.Remaining.ToString()).Replace("{FirstGroup}",p.FirstGroup.ToString()).Replace("{Survivors}",p.Survivors.ToString())
                .Replace("{Rescued}",p.RescuedCount.ToString()).Replace("{Lost}",p.LostCount.ToString()).Replace("{Extra}",p.Extra.ToString()).Replace("{FarPeople}",FarPeople(data).ToString())
                .Replace("{MethodBuilt}",method?.builtLine??"").Replace("{Crossing}",method?.crossingName??"다리").Replace("{CarryLine}",method?.carryLine??"").Replace("{DoorLine}",method?.doorLine??"").Replace("{BLine}",p.Outcome=="B_Light"?d.lightLine:d.heavyLine));
        }
        public BrokenPathScene CurrentBrokenPathScene(SurvivalDefinitions data) => BrokenPathScenePending?data.brokenPath.Scene(BrokenPath.Scene):null;

        void StartBrokenPath()
        {
            BrokenPath=new BrokenPathProgress{Stage=1,Scene="core_choice"};
            Note("[3막 · 끊어진 길] 중계실에서 구조 요청 두 건을 받았다.");
        }

        public string ChooseBrokenPath(int index,SurvivalDefinitions data)
        {
            var scene=CurrentBrokenPathScene(data);
            if(scene==null || IsGameOver || index<0 || index>=scene.choices.Length) return "지금은 그 선택을 할 수 없다.";
            var d=data.brokenPath; var p=BrokenPath; string action=scene.choices[index].action;
            if(action=="core_A" || action=="core_B")
            {
                p.CoreChoice=action=="core_A"?"A":"B";
                // Helpers come only from the trust built in act 2, whichever side was chosen.
                int trust=LateTrust+CompanionTrust;
                p.Helpers=trust>=d.helperTrustTwo?2:trust>=d.helperTrustOne?1:0;
                p.StartTime=Clock; p.Scene="path_broken_"+p.CoreChoice;
                RecordFact("story",p.CoreChoice=="A"?"동림의 보호소 구조를 돕기로 했다. 석현의 중계 철수는 뒤로 밀렸다.":"석현의 중계 철수를 돕기로 했다. 보호소 구조는 동림이 맡은 인원으로만 진행됐다.");
                return Report(p.CoreChoice=="A"?"[핵심 선택] 동림의 구조를 돕는다.":"[핵심 선택] 석현의 중계 철수를 돕는다.");
            }
            if(action=="field")
            {
                p.Scene=""; p.Stage=2;
                p.Yard=new Inventory(); foreach(var stack in d.yard) p.Yard.Add(stack.id,stack.amount);
                UnlockLateStory("broken_path",data);
                UnlockLateStory("construction_site",data);
                RecordFact("story","성내천 보행교 가운데가 사라져 동림이 건너편에 고립됐다.");
                return Report(d.hint);
            }
            if(action=="A1" || action=="A2")
            {
                p.Outcome=action; p.YeongsukState="Rescued"; p.SeokhyeonCoop=false;
                if(action=="A2")
                {
                    p.RescuedCount=p.FirstGroup+1; p.LostCount=p.Remaining+p.NamelessLost; Vitals.AdjustMental(-5);
                    RecordFact("story","퇴로를 지켰다. 동림은 돌아왔고, 보호소에 남은 "+p.Remaining+"명은 데려오지 못했다.");
                }
                else
                {
                    p.Extra=Math.Max(1,p.Remaining-1); p.DonglimAlive=false;
                    p.RescuedCount=p.FirstGroup+1+p.Extra; p.LostCount=p.Remaining-p.Extra+p.NamelessLost; Vitals.AdjustMental(-20);
                    RecordFact("story","동림이 남은 사람을 데리러 다시 들어갔다. "+p.Extra+"명을 데리고 나왔지만 동림은 돌아오지 못했다.");
                }
                p.Scene="result_"+action;
                return Report(action=="A2"?"퇴로를 지켰다.":"동림을 다시 들여보냈다.");
            }
            if(action.StartsWith("next:")) { p.Scene=action.Substring(5); return lastReport; }
            // finish
            p.Scene=""; p.Stage=4;
            if(CurrentBeat(data)?.id=="core_choice") AdvanceLateStory(data);
            string summary=p.Outcome=="A1"?"동림을 잃었다. 보호소에서 "+p.RescuedCount+"명을 데려왔다. 석현은 중계실을 떠났다.":p.Outcome=="A2"?"동림과 함께 보호소에서 "+p.RescuedCount+"명을 데려왔다. 석현은 떠났다.":"동림을 "+(p.Outcome=="B_Light"?"데려왔다.":"중상인 채로 데려왔다.")+" 영숙 씨는 돌아오지 않았다.";
            RecordFact("story","「끊어진 길」을 마쳤다. "+summary);
            return Report("[3막 · 끊어진 길] "+summary+SpendBrokenPathNight(data));
        }
        // The closing scenes end the next morning ("이튿날 아침", "다음 날"), so the clock and place follow them:
        // the evening is spent with the rescued in the shelter basement, the night is slept there, and the shelter feeds everyone.
        string SpendBrokenPathNight(SurvivalDefinitions data)
        {
            int awake=hour>=8 && hour<22?22-hour:0;
            AdvanceTime(awake,data,false,true);
            int sleep=(8-hour+24)%24;
            AdvanceTime(sleep,data,true,true);
            if(IsGameOver) return "";
            string place=BrokenPath.Outcome.StartsWith("A") && !IsLocationErased("relay_room")?"relay_room":"shelter";
            if(IsLocationErased(place)) return "";
            if(CurrentLocation!=place)
            {
                string previous=CurrentLocation;
                CurrentLocation=place; AwayFromHome=true;
                FinishDeferredErasure(previous,data);
                MarkVisited(place);
            }
            Vitals.Hunger=Math.Min(Vitals.Hunger,20); UpdateHungerPenalty(data.TEMP_BALANCE);
            Thirst=0; HydrationHours=0;
            return "\n\n[DAY "+day.ToString("00")+" 08:00 · "+data.Location(place).name+"] 구한 사람들과 주민회관 지하에서 밤을 보냈다. 저녁과 아침은 배식소에서 함께 먹었다.";
        }

        public string SurveyBrokenPath(string id,SurvivalDefinitions data)
        {
            var survey=data.brokenPath.Survey(id);
            if(!CanAct || !BrokenPathFieldWork || CurrentLocation!="seongnae_bridge" || survey==null || BrokenPath.Surveyed.Contains(id) || BrokenPath.Built) return "지금은 조사할 수 없다.";
            AdvanceTime(1,data); if(IsGameOver) return Report("조사를 마칠 힘이 없다.");
            BrokenPath.Surveyed.Add(id);
            RecordFact("story",id switch
            {
                "gap"=>"보행교의 끊어진 길이를 쟀다.",
                "pier"=>"보행교에 남은 교각이 버틸지 살폈다.",
                "far"=>"무전으로 건너편의 인원과 부상자를 확인했다.",
                "detour"=>"둑 아래로 하천 바닥을 우회하는 길을 확인했다.",
                _=>"보행교 현장을 조사했다."
            });
            // On route B the far side has already lost Yeongsuk's building, so some surveys need their own text.
            return Report(BrokenPathText(BrokenPath.CoreChoice=="B" && !string.IsNullOrEmpty(survey.bodyB)?survey.bodyB:survey.body,data));
        }
        public string ChooseBrokenPathMethod(string id,SurvivalDefinitions data)
        {
            var method=data.brokenPath.Method(id);
            if(!BrokenPathFieldWork || BrokenPath.Built || method==null || IsGameOver) return "지금은 계획을 바꿀 수 없다.";
            BrokenPath.Method=id;
            RecordFact("story","보행교를 잇는 방법으로 "+Particle(method.name,"을/를")+" 정했다.");
            return Report(method.name+"으로 정했다. "+method.summary);
        }
        public string TakeYardMaterial(string id,SurvivalDefinitions data)
        {
            var item=data.Item(id);
            if(!CanAct || !BrokenPathFieldWork || CurrentLocation!="construction_site" || item==null || BrokenPath.Yard.Count(id)<1) return "가져갈 자재가 없다.";
            bool large=IsLargeMaterial(id);
            if(large && CarriedLarge>=CarryCapacity) return "큰 자재는 한 번에 "+CarryCapacity+"개까지 들 수 있다. 보행교에 먼저 내려놓자.";
            int amount=large?1:BrokenPath.Yard.Count(id);
            BrokenPath.Yard.Transfer(Backpack,id,amount);
            return Report(item.name+" "+amount+"개를 챙겼다."+(large?" 큰 자재 "+CarriedLarge+" / "+CarryCapacity:""));
        }
        public string DropAtBridge(SurvivalDefinitions data)
        {
            if(!CanAct || !BrokenPathFieldWork || CurrentLocation!="seongnae_bridge" || CarriedLarge==0) return "내려놓을 큰 자재가 없다.";
            var moved=new List<string>();
            foreach(var id in new[]{"ladder","steel_plank"}) { int count=Backpack.Count(id); if(count>0 && Backpack.Transfer(BrokenPath.Site,id,count)) moved.Add(data.Item(id).name+" "+count+"개"); }
            RecordFact("story","보행교 끝에 "+Particle(string.Join(", ",moved),"을/를")+" 내려놓았다.");
            return Report("보행교 끝에 "+Particle(string.Join(", ",moved),"을/를")+" 내려놓았다.");
        }
        // Have/need for one material of the chosen plan: large pieces count what is stacked at the bridge, small ones what is carried.
        public int BrokenPathHave(string id) => IsLargeMaterial(id)?BrokenPath.Site.Count(id):Backpack.Count(id);
        public bool CanBuildBrokenPath(SurvivalDefinitions data)
        {
            var method=data.brokenPath.Method(BrokenPath.Method);
            if(!CanAct || !BrokenPathFieldWork || BrokenPath.Built || CurrentLocation!="seongnae_bridge" || method==null) return false;
            foreach(var cost in method.large) if(BrokenPath.Site.Count(cost.id)<cost.amount) return false;
            foreach(var cost in method.small) if(Backpack.Count(cost.id)<cost.amount) return false;
            return true;
        }
        public string BuildBrokenPath(SurvivalDefinitions data)
        {
            if(!CanBuildBrokenPath(data)) return "계획한 자재가 모두 현장에 있어야 한다.";
            var d=data.brokenPath; var p=BrokenPath; var method=d.Method(p.Method);
            AdvanceTime(method.BuildHours(p.Helpers),data); if(IsGameOver) return Report("시공을 마칠 힘이 남지 않았다.");
            foreach(var cost in method.large) p.Site.Remove(cost.id,cost.amount);
            foreach(var cost in method.small) Backpack.Remove(cost.id,cost.amount);
            p.Built=true; p.Stage=3;
            // Hidden readiness: the method, a stretcher, two helpers, and how long the far side has held.
            int far=FarState(data);
            p.Readiness=method.score+(Backpack.Count("stretcher")>0?1:0)+(p.Helpers>=2?1:0)+(far==0?1:far==1?0:-1);
            p.Remaining=d.remainingByReadiness[Mathf.Clamp(p.Readiness,0,4)];
            if(p.CoreChoice=="A") p.FirstGroup=Math.Max(1,d.peopleA-p.NamelessLost-p.Remaining);
            else
            {
                p.Survivors=Math.Max(0,d.peopleB-p.NamelessLost-p.Remaining);
                p.Outcome=p.Readiness>=2?"B_Light":"B_Heavy"; p.YeongsukState="Missing"; p.SeokhyeonCoop=true;
                p.RescuedCount=p.Survivors; p.LostCount=d.peopleB-p.Survivors; Vitals.AdjustMental(-10);
            }
            p.Scene="evac_"+p.CoreChoice;
            RecordFact("story","성내천 보행교에 "+Particle(method.name,"을/를")+" 놓았다.");
            return Report(Particle(method.name,"을/를")+" 놓았다. 건너편에서 사람들이 움직이기 시작했다.");
        }
        // Waiting is never resolved for the player: after the far side turns critical, each new day costs one unnamed person.
        void TickBrokenPathDay(SurvivalDefinitions data)
        {
            if(!BrokenPathFieldWork || BrokenPath.Built || FarState(data)<2) return;
            BrokenPath.RescueDelayDays++;
            if(BrokenPath.NamelessLost>=data.brokenPath.maxNamelessLost) return;
            BrokenPath.NamelessLost++;
            RecordFact("story",data.brokenPath.decayNote);
        }
        // Saves from before act 3 gain the new places and an empty quest.
        public void UpgradeBrokenPath(SurvivalDefinitions data)
        {
            if(BrokenPath==null) BrokenPath=new BrokenPathProgress();
            if(BrokenPath.Surveyed==null) BrokenPath.Surveyed=new List<string>();
            if(BrokenPath.Site==null) BrokenPath.Site=new Inventory();
            if(BrokenPath.Yard==null) BrokenPath.Yard=new Inventory();
            BrokenPath.Scene=BrokenPath.Scene??""; BrokenPath.CoreChoice=BrokenPath.CoreChoice??""; BrokenPath.Method=BrokenPath.Method??"";
            BrokenPath.Outcome=BrokenPath.Outcome??""; BrokenPath.YeongsukState=BrokenPath.YeongsukState??"";
            if(LateFlags.Contains("broken_path") && !LateFlags.Contains("construction_site")) LateFlags.Add("construction_site");
        }
        public void UpgradeStoryLocations(SurvivalDefinitions data)
        {
            if(Locations==null || schemaVersion<5 || schemaVersion>13) return;
            foreach(var place in data.locations)
                if(!Locations.Exists(x=>x!=null && x.id==place.id))
                {
                    var progress=new LocationProgress { id=place.id };
                    foreach(var stack in place.loot) progress.remaining.Add(stack.id,stack.amount);
                    Locations.Add(progress);
                }
        }

        // Test entry: a real playthrough up to the first expedition, then straight to the act 3 choice.
        public static SurvivalState Act3TestStart(SurvivalDefinitions data,int trust,string nickname)
        {
            var s=NewGame(GameDifficulty.NORMAL,data,2131);
            s.Story.OpeningCompleted=true; s.PlayerNickname=nickname??"";
            s.Backpack.Add("radio",1); s.Story.HasRadio=true; s.ListenRadio(data);
            s.Travel("supply_site",data,()=>1); s.ApproachCrowd(data); s.ChooseCrowdResponse(true,data);
            s.ContinueCrowdStory(data); s.ContinueCrowdStory(data); s.ContinueCrowdStory(data);
            foreach(var id in new[]{"empty_house","school"}) if(!s.UnlockedLocations.Contains(id)) s.UnlockedLocations.Add(id);
            s.Travel("home",data,()=>1); s.day=2; s.hour=8; s.Travel("empty_house",data,()=>1); s.ResolveDongrimMeeting(false,data);
            s.Travel("home",data,()=>1); s.day=3; s.hour=8; s.Travel("school",data,()=>1); s.ResolveDongrimMeeting(false,data);
            s.day=4; s.hour=8; s.MeetCompanion(data); s.ContinueCompanion(data); s.Travel("home",data,()=>1);
            s.day=s.CompanionReadyDay; s.hour=8; s.Travel("school",data,()=>1); if(s.PendingDongrimMeeting) s.ResolveDongrimMeeting(false,data);
            s.MeetCompanion(data); s.ContinueCompanion(data); s.ContinueCompanion(data,true); s.ContinueCompanion(data);
            s.Travel("home",data,()=>1);
            if(s.Companion!=CompanionStep.Completed) throw new InvalidOperationException("Act 3 test entry could not finish the first expedition.");
            // The later story waits behind act 3; the relay room is where the choice is made.
            int resume=Array.FindIndex(data.lateStory.beats,x=>x.id=="core_choice");
            s.LateStep=resume>=0?resume:0; s.LateScenePending=false;
            s.UnlockLateStory("relay_room",data);
            s.UnlockLateStory("neighbor_moved",data);
            s.UnlockLateStory("pharmacy",data);
            s.Story.HasMetSeokhyeon=true;
            s.LateTrust=Math.Max(0,trust-s.CompanionTrust);
            s.day=21; s.hour=9; s.LateReadyDay=s.day;
            s.Vitals.Hunger=0; s.Thirst=0; s.HydrationHours=0; s.Sleepiness=0;
            s.HomeStorage.Add("water",6); s.Backpack.Add("water",2); s.HomeStorage.Add("cord",8); s.HomeStorage.Add("cloth",6); s.HomeStorage.Add("wood",4);
            s.StartBrokenPath();
            s.Note("[3막 테스트 진입] 중계실에서 동림과 석현이 기다린다.");
            return s;
        }
    }
}
