using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class OnboardingChecks
    {
        [MenuItem("Vanishing Days/Validate tutorial and neighbor introduction")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load();int count=0;
            Action<bool,string> check=(ok,why)=>{count++;if(!ok)throw new Exception(why);};
            string path=Path.GetFullPath("Builds/Validation/onboarding-save.json");var saves=new SessionSaveStore(path,data);
            Action clean=()=>{foreach(string f in new[]{path,path+".bak",path+".tmp"})if(File.Exists(f))File.Delete(f);};clean();
            try
            {
                foreach(var difficulty in new[]{GameDifficulty.EASY,GameDifficulty.NORMAL,GameDifficulty.HARD})
                {
                    var s=SurvivalState.NewGame(difficulty,data,123);int hour=s.hour,water=s.HomeStorage.Count("water");uint rng=s.RandomState;
                    check(s.NeedsNeighborIntroduction && !s.TutorialActive,"Fresh state missing optional introduction.");
                    s.BeginTutorial();check(s.TutorialStep==0 && s.AdvanceTutorial() && s.TutorialStep==1,"Welcome failed.");
                    check(!s.AdvanceTutorial() && !s.ViewTutorialTopic(1) && s.NeighborStage==0,"Tutorial auto-completed neighbor.");
                    s.GreetNeighbor(data);check(s.AdvanceTutorial() && !s.NeedsNeighborIntroduction && s.NeighborGreetings==1,"Actual greeting did not unlock lesson.");
                    s.GreetNeighbor(data);check(s.NeighborGreetings==1,"Same-day tutorial greeting duplicated event.");
                    for(int step=2;step<=5;step++)
                    {
                        check(!s.AdvanceTutorial(),"Unseen lesson advanced.");
                        check(s.ViewTutorialTopic(step) && !s.ViewTutorialTopic(step),"Topic marking not idempotent.");
                        saves.Save(s);check(saves.TryLoad(out s,out _) && s.TutorialActive && s.TutorialStep==step && s.CanAdvanceTutorial,"Save lost lesson progress.");
                        check(s.AdvanceTutorial(),"Viewed lesson did not advance.");
                    }
                    check(!s.TutorialActive && s.TutorialStep==6 && s.hour==hour && s.RandomState==rng && s.HomeStorage.Count("water")==water,"Tutorial changed simulation or failed completion.");
                    s.BeginTutorial();s.SkipTutorial();check(!s.TutorialActive && !s.NeedsNeighborIntroduction,"Replay reset actual neighbor progress.");
                    s.PendingEncounter=new Encounter{kind="hungry"};s.BeginTutorial();check(!s.TutorialActive,"Tutorial bypassed encounter.");
                    s.PendingEncounter=null;s.Vitals.CurrentHP=0;s.BeginTutorial();check(!s.TutorialActive,"Tutorial resurrected player.");
                }
                var old=SurvivalState.NewGame(GameDifficulty.NORMAL,data,456);old.OnboardingVersion=0;old.Vitals.AdjustMental(-13);old.HomeStorage.Remove("water",2);old.Locations.Find(x=>x.id=="store").remaining.Remove("water",1);
                File.WriteAllText(path,JsonUtility.ToJson(old));cleanBackup(path);
                check(saves.TryLoad(out old,out _) && old.OnboardingVersion==1 && !old.TutorialActive && old.NeedsNeighborIntroduction && old.Vitals.CurrentMental==87 && old.HomeStorage.Count("water")==6 && old.Locations.Find(x=>x.id=="store").remaining.Count("water")==3,"Old save upgrade reset progress or forced tutorial.");
                old.NeighborIntroductionSeen=true;saves.Save(old);check(saves.TryLoad(out old,out _) && !old.NeedsNeighborIntroduction && old.NeighborStage==0,"Invitation dismissal lost or auto-played story.");
                old.OnboardingVersion=0;old.NeighborStage=1;old.NeighborGreetings=1;old.NeighborLastDay=1;old.UpgradeOnboarding();check(!old.NeedsNeighborIntroduction,"Returning neighbor was introduced again.");
                foreach(int invalid in new[]{-1,7}) {old.TutorialStep=invalid;clean();File.WriteAllText(path,JsonUtility.ToJson(old));check(!saves.TryLoad(out _,out _),"Invalid tutorial step accepted.");}
            }
            finally{clean();}
            Debug.Log("VD_ONBOARDING_CHECKS_OK: "+count+" assertions; five topics, real greeting, skip/replay, save/resume, legacy preservation and action gates.");
        }
        static void cleanBackup(string path){foreach(string f in new[]{path+".bak",path+".tmp"})if(File.Exists(f))File.Delete(f);}
    }
}
