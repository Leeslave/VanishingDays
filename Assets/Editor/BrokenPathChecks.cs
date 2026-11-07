using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    // Act 3 「끊어진 길」: plays all four outcomes through the real state methods and checks the carry rule,
    // the readiness table, the far side running out of time, saves mid-stage and older saves.
    public static class BrokenPathChecks
    {
        static SurvivalDefinitions data; static int checks;
        static void Check(bool ok,string why) { checks++; if(!ok) throw new Exception("Broken path check failed: "+why); }
        static void Rest(SurvivalState s) { s.Vitals.Hunger=0; s.Thirst=0; s.HydrationHours=0; s.Sleepiness=0; }
        static void Valid(SurvivalState s,string where) { try { SessionSaveStore.Validate(s,data); checks++; } catch(Exception e) { throw new Exception("Broken path check failed: invalid state "+where+": "+e.Message); } }
        // JsonUtility cannot write null objects; the save store drops the empty encounter the same way.
        static SurvivalState Copy(SurvivalState s) { var c=JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(s)); if(c.PendingEncounter!=null && string.IsNullOrEmpty(c.PendingEncounter.kind)) c.PendingEncounter=null; return c; }
        static void Go(SurvivalState s,string id)
        {
            Rest(s); s.Travel(id,data,()=>1);
            Check(s.CurrentLocation==id && s.PendingEncounter==null && !s.PendingDongrimMeeting && !s.LateScenePending && !s.IsGameOver,"could not walk to "+id+" ("+s.lastReport+")");
            Valid(s,"after walking to "+id);
        }
        static void Choose(SurvivalState s,string action)
        {
            var scene=s.CurrentBrokenPathScene(data); Check(scene!=null,"no scene open for "+action);
            Check(!s.BrokenPathText(scene.body,data).Contains("{"),"unfilled placeholder in "+scene.id);
            int index=Array.FindIndex(scene.choices,x=>x.action==action); Check(index>=0,scene.id+" has no choice "+action);
            s.ChooseBrokenPath(index,data); Valid(s,"after "+scene.id+" → "+action);
        }
        // Play every remaining scene with its first choice until the stage closes.
        static void Close(SurvivalState s) { for(int guard=0;s.BrokenPathScenePending && guard<8;guard++) Choose(s,s.CurrentBrokenPathScene(data).choices[0].action); Check(s.BrokenPath.Stage==4 && !s.BrokenPathActive,"stage did not finish"); }
        static SurvivalState Begin(int trust,string side)
        {
            var s=SurvivalState.Act3TestStart(data,trust,"기록자"); Valid(s,"at the test entry");
            Check(s.BrokenPathScenePending && !s.CanAct && s.BrokenPath.Stage==1 && s.CurrentBrokenPathScene(data).id=="core_choice","test entry should open on the core choice");
            Choose(s,"core_"+side); Check(s.CurrentBrokenPathScene(data).id=="path_broken_"+side,"core choice did not lead to the call for help");
            Choose(s,"field"); Check(s.BrokenPathFieldWork && s.CanAct && s.IsLocationUnlocked("seongnae_bridge",data) && s.IsLocationUnlocked("construction_site",data),"field work did not open the bridge and the site");
            return s;
        }
        static void Plan(SurvivalState s,string methodId)
        {
            var method=data.brokenPath.Method(methodId);
            Go(s,"seongnae_bridge"); Rest(s); s.SurveyBrokenPath(method.requires,data);
            Check(s.BrokenPath.Surveyed.Contains(method.requires),"survey "+method.requires+" not recorded");
            s.ChooseBrokenPathMethod(methodId,data); Check(s.BrokenPath.Method==methodId,"method not chosen");
        }
        // Haul the method's large pieces in as few trips as the carry limit allows.
        static int Haul(SurvivalState s)
        {
            var method=data.brokenPath.Method(s.BrokenPath.Method); int trips=0;
            Func<bool> missing=()=>Array.Exists(method.large,c=>s.BrokenPath.Site.Count(c.id)<c.amount);
            while(missing() && trips<6)
            {
                Go(s,"construction_site"); trips++;
                foreach(var cost in method.large)
                    while(s.BrokenPath.Site.Count(cost.id)+s.Backpack.Count(cost.id)<cost.amount && s.CarriedLarge<s.CarryCapacity)
                    {
                        int before=s.CarriedLarge; s.TakeYardMaterial(cost.id,data);
                        Check(s.CarriedLarge==before+1,"could not lift "+cost.id+" ("+s.lastReport+")");
                    }
                Check(s.CarriedLarge<=s.CarryCapacity,"carried more large pieces than the limit");
                Go(s,"seongnae_bridge"); s.DropAtBridge(data); Check(s.CarriedLarge==0,"large pieces not set down");
            }
            Check(!missing(),"large materials never reached the bridge");
            return trips;
        }
        static void Build(SurvivalState s,bool stretcher)
        {
            var method=data.brokenPath.Method(s.BrokenPath.Method);
            foreach(var cost in method.small) s.Backpack.Add(cost.id,cost.amount);
            if(stretcher) s.Backpack.Add("stretcher",1);
            Rest(s); Check(s.CanBuildBrokenPath(data),"cannot build with every material present");
            s.BuildBrokenPath(data); Valid(s,"after building");
            var p=s.BrokenPath; int far=s.FarState(data);
            int expected=method.score+(stretcher?1:0)+(p.Helpers>=2?1:0)+(far==0?1:far==1?0:-1);
            Check(p.Built && p.Stage==3 && p.Readiness==expected,"readiness "+p.Readiness+" should be "+expected);
            Check(p.Remaining==data.brokenPath.remainingByReadiness[Mathf.Clamp(expected,0,4)],"remaining does not follow the readiness table");
            foreach(var cost in method.large) Check(p.Site.Count(cost.id)==0,"large pieces not used up");
            Check(s.CurrentBrokenPathScene(data).id=="evac_"+p.CoreChoice,"evacuation scene did not open");
        }
        // Waits in the open, a few hours at a time, with food and water topped up so only the far side changes.
        static void Wait(SurvivalState s,int hours) { for(int left=hours;left>0;left-=6) { Rest(s); s.AdvanceTime(Math.Min(6,left),data); } Rest(s); Check(!s.IsGameOver && s.PendingEncounter==null,"waiting ended the run"); }

        [MenuItem("Vanishing Days/Validate act 3 broken path")]
        public static void Validate()
        {
            data=SurvivalDefinitions.Load(); checks=0; var d=data.brokenPath;
            string path=Path.GetFullPath("Builds/Validation/broken-path-save.json"); var saves=new SessionSaveStore(path,data);
            Action clean=()=>{foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);}; clean();
            try
            {
                Check(d.remainingByReadiness[0]>d.remainingByReadiness[4],"readiness should lower who is left behind");
                // Helpers come from act 2 trust only.
                foreach(var pair in new[]{(0,0),(d.helperTrustOne,1),(d.helperTrustTwo,2)})
                {
                    var t=SurvivalState.Act3TestStart(data,pair.Item1,"기록자"); Choose(t,"core_A");
                    Check(t.BrokenPath.Helpers==pair.Item2 && t.CarryCapacity==1+pair.Item2,"trust "+pair.Item1+" should bring "+pair.Item2+" helpers");
                }

                // A-2: Dongrim's rescue, the full bridge with two helpers and a stretcher, holding the way back.
                var s=Begin(d.helperTrustTwo,"A");
                Check(!s.TryBeginLateStory(data) && !s.LateScenePending,"later story ran during act 3");
                Plan(s,"bridge");
                // Mid-stage save: the plan, the stacked pieces and the yard all come back.
                Go(s,"construction_site"); s.TakeYardMaterial("ladder",data); s.TakeYardMaterial("ladder",data); Go(s,"seongnae_bridge"); s.DropAtBridge(data);
                saves.Save(s); Check(saves.TryLoad(out var restored,out string error),"mid-stage save did not load: "+error);
                Check(restored.BrokenPathFieldWork && restored.BrokenPath.Method=="bridge" && restored.BrokenPath.Site.Count("ladder")==2 && restored.BrokenPath.Yard.Count("ladder")==d.yard[0].amount-2 && restored.BrokenPath.Surveyed.Contains("pier") && restored.BrokenPath.StartTime==s.BrokenPath.StartTime && restored.BrokenPath.Helpers==2,"mid-stage progress lost on reload");
                s=restored;
                int trips=Haul(s); Check(trips==1,"two helpers should carry the planks in one trip, took "+trips);
                Build(s,true);
                Check(s.BrokenPath.FirstGroup==Math.Max(1,d.peopleA-s.BrokenPath.Remaining) && s.FarState(data)==0,"first group wrong");
                saves.Save(s); Check(saves.TryLoad(out restored,out error) && restored.CurrentBrokenPathScene(data)?.id=="evac_A","evacuation scene not restored: "+error);
                var a1=Copy(s);
                Choose(s,"A2"); Close(s);
                Check(s.BrokenPath.Outcome=="A2" && s.BrokenPath.DonglimAlive && !s.BrokenPath.SeokhyeonCoop && s.BrokenPath.YeongsukState=="Rescued","A-2 outcome wrong");
                Check(s.BrokenPath.RescuedCount==s.BrokenPath.FirstGroup+1 && s.BrokenPath.LostCount==s.BrokenPath.Remaining,"A-2 counts wrong");
                Check(s.lastReport.Contains("석현은 떠났다"),"A-2 summary missing the parting");
                saves.Save(s); Check(saves.TryLoad(out restored,out _) && restored.BrokenPath.Stage==4 && restored.BrokenPath.Outcome=="A2","finished stage not saved");

                // A-1: same evacuation, sending Dongrim back in.
                Choose(a1,"A1"); Close(a1);
                Check(a1.BrokenPath.Outcome=="A1" && !a1.BrokenPath.DonglimAlive && a1.BrokenPath.Extra>=1 && a1.BrokenPath.RescuedCount==a1.BrokenPath.FirstGroup+1+a1.BrokenPath.Extra,"A-1 outcome wrong");
                Check(a1.Vitals.CurrentMental<s.Vitals.CurrentMental,"losing Dongrim should weigh more than holding the line");
                var after=Copy(a1); after.day++; after.hour=9;
                Go(after,"school"); Check(!after.PendingDongrimMeeting,"Dongrim met after he was lost");

                // B-Light: the relay withdrawal, a plain ladder with one helper, built while the far side holds.
                var b=Begin(d.helperTrustOne,"B"); Plan(b,"ladder");
                // Carry rule: one helper means two large pieces per trip; small materials are not limited.
                Go(b,"construction_site"); b.TakeYardMaterial("ladder",data); b.TakeYardMaterial("steel_plank",data);
                string refused=b.TakeYardMaterial("ladder",data); Check(b.CarriedLarge==2 && refused.Contains("2개까지"),"third large piece should be refused");
                b.TakeYardMaterial("cargo_belt",data); Check(b.Backpack.Count("cargo_belt")==d.yard[2].amount,"belts should come all at once");
                Go(b,"seongnae_bridge"); b.DropAtBridge(data);
                Haul(b); Build(b,false);
                Check(b.BrokenPath.Outcome=="B_Light" && b.BrokenPath.Readiness>=2 && b.BrokenPath.Survivors==d.peopleB-b.BrokenPath.Remaining,"B-Light outcome wrong");
                Close(b);
                Check(b.BrokenPath.YeongsukState=="Missing" && b.BrokenPath.SeokhyeonCoop && b.BrokenPath.DonglimAlive && b.lastReport.Contains("영숙"),"B-Light ending wrong");

                // B-Heavy: no helpers, the riverbed, and days lost before building. The far side decays only by radio and only to a cap.
                var h=Begin(0,"B"); Plan(h,"riverbed");
                Check(h.FarState(data)==0 && h.FarReport(data).Contains("[무전]"),"far side should start steady, over the radio");
                Wait(h,d.steadyHours); Check(h.FarState(data)==1,"far side should be shaking after "+d.steadyHours+"h");
                Wait(h,d.shakingHours-d.steadyHours); Check(h.FarState(data)==2 && !h.FarReport(data).Contains("{"),"far side should be critical");
                var decay=Copy(h);
                Wait(decay,24*(d.maxNamelessLost+2));
                Check(decay.BrokenPath.NamelessLost==d.maxNamelessLost && decay.BrokenPath.RescueDelayDays>d.maxNamelessLost,"unnamed losses should stop at "+d.maxNamelessLost+" while delay days keep counting");
                Valid(decay,"after long neglect");
                Wait(h,24); Check(h.BrokenPath.NamelessLost>=1 && h.BrokenPath.RescueDelayDays>=1,"a critical day should cost someone");
                int trips0=Haul(h); Check(trips0==2,"alone, two ladders take two trips, took "+trips0);
                Build(h,false);
                Check(h.BrokenPath.Outcome=="B_Heavy" && h.BrokenPath.Readiness<2 && h.BrokenPath.Survivors==Math.Max(0,d.peopleB-h.BrokenPath.NamelessLost-h.BrokenPath.Remaining),"B-Heavy outcome wrong");
                Close(h); Check(h.lastReport.Contains("중상"),"B-Heavy summary wrong");

                // Saves from before act 3 gain the two places and an empty quest.
                var old=SurvivalState.Act3TestStart(data,0,"기록자"); old.BrokenPath=new BrokenPathProgress();
                old.Locations.RemoveAll(x=>x.id=="construction_site" || x.id=="seongnae_bridge");
                clean(); File.WriteAllText(path,JsonUtility.ToJson(old));
                Check(saves.TryLoad(out restored,out error) && restored.Locations.Count==data.locations.Length && restored.BrokenPath!=null && restored.BrokenPath.Stage==0 && !restored.BrokenPathActive,"older save did not upgrade: "+error);
                Check(!restored.IsLocationUnlocked("seongnae_bridge",data),"bridge open before act 3");

                // Corrupt stage data is refused.
                var bad=Copy(b); bad.BrokenPath.Stage=2;
                bool refusedBad=false; try { SessionSaveStore.Validate(bad,data); } catch(InvalidDataException) { refusedBad=true; }
                Check(refusedBad,"finished-then-reopened stage accepted");

                Debug.Log("VD_BROKEN_PATH_CHECKS_OK: "+checks+" assertions; A-1, A-2, B-Light, B-Heavy, carry limit, decay cap, saves.");
            }
            finally { clean(); }
        }
    }
}
