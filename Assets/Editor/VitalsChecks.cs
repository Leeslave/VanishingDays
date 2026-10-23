using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class VitalsChecks
    {
        [MenuItem("Vanishing Days/Validate health and mental stats")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0;
            Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
            Func<SurvivalState> fresh=()=>SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            var s=fresh();
            check(s.Vitals.CurrentMental==100 && s.Vitals.BaseMaxMental==100,"Initial mental health wrong.");
            s.Vitals.AdjustMental(-35); check(s.Vitals.CurrentMental==65 && s.Vitals.CurrentHP==500,"Mental consequence affected HP.");
            s.Vitals.AdjustMental(int.MaxValue); check(s.Vitals.CurrentMental==100,"Mental recovery exceeded maximum.");
            s.Vitals.AdjustMental(int.MinValue); check(s.Vitals.CurrentMental==0 && !s.IsGameOver,"Zero mental health caused an unrequested game over.");
            s=fresh(); s.Travel("store",data,()=>0); s.ResolveEncounter(true,data,()=>1);
            check(s.Vitals.Injuries.Count==1 && s.Vitals.CurrentHP==450,"New injury did not immediately clamp existing HP.");
            s=fresh(); s.Vitals.Hunger=data.TEMP_BALANCE.hungryAt-1; s.AdvanceTime(1,data);
            check(s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==450 && s.Vitals.CurrentHP==450,"Hunger cap did not reduce existing HP.");
            s=fresh(); s.Vitals.CurrentHP=200; s.Vitals.Hunger=data.TEMP_BALANCE.hungryAt-1; s.AdvanceTime(1,data);
            check(s.Vitals.CurrentHP==200,"Cap reduction subtracted HP already below the cap.");
            foreach(string medicine in new[]{"bandage","pills","antiseptic"})
            {
                s=fresh(); s.HomeStorage.Add(medicine,1); s.Vitals.CurrentHP=410;
                for(int i=0;i<3;i++)s.Vitals.Injuries.Add(new Injury{recoveryPenalty=.1f});
                s.Vitals.ClampToRecoveryCap(data.TEMP_BALANCE);
                check(s.Vitals.CurrentHP==350,"Screenshot fixture did not clamp 410 HP to 350.");
                s.TreatInjury(medicine,data);
                check(s.Vitals.Injuries.Count==2 && s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==400 && s.Vitals.CurrentHP==370,"Treatment did not restore injury plus a small amount of HP: "+medicine);
            }
            s=fresh(); s.Vitals.CurrentHP=499; s.Vitals.Injuries.Add(new Injury{recoveryPenalty=.1f}); s.TreatInjury("bandage",data);
            check(s.Vitals.CurrentHP==470 && s.Vitals.Injuries.Count==0,"Treatment restored HP lost before healing instead of only adding 20.");
            s=fresh(); s.Vitals.CurrentHP=445; s.Vitals.Hunger=70; s.Vitals.HungerPenalty=.1f;
            s.Vitals.Injuries.Add(new Injury{recoveryPenalty=.1f}); s.TreatInjury("bandage",data);
            check(s.Vitals.CurrentHP==420 && s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==450,"Treatment ignored the old cap or hunger cap.");
            s=fresh(); s.Vitals.CurrentHP=0; s.Vitals.Injuries.Add(new Injury{recoveryPenalty=.1f}); int bandages=s.HomeStorage.Count("bandage"); s.TreatInjury("bandage",data);
            check(s.IsGameOver && s.HomeStorage.Count("bandage")==bandages,"Treatment resurrected the player.");
            string path=Path.GetFullPath("Builds/Validation/vitals-save-test.json"); var store=new SessionSaveStore(path,data);
            foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            s=fresh(); s.Vitals.AdjustMental(-35); store.Save(s);
            check(store.TryLoad(out s,out _) && s.Vitals.CurrentMental==65,"Mental health was not persisted.");
            var old=fresh(); old.schemaVersion=5; old.Vitals.CurrentHP=410; old.Vitals.CurrentMental=0; old.Vitals.BaseMaxMental=0;
            for(int i=0;i<3;i++)old.Vitals.Injuries.Add(new Injury{recoveryPenalty=.1f});
            old.InstalledSecurity.Add("analog"); old.Locations.Find(x=>x.id=="store").remaining.Remove("tuna",1);
            File.WriteAllText(path,JsonUtility.ToJson(old));
            check(store.TryLoad(out s,out _) && s.schemaVersion==10 && s.Vitals.CurrentHP==350 && s.Vitals.CurrentMental==100 && s.Vitals.BaseMaxMental==100,"Old save did not initialize mental health/clamp HP.");
            check(s.Vitals.Injuries.Count==3 && s.InstalledSecurity.Contains("analog") && s.Locations.Find(x=>x.id=="store").remaining.Count("tuna")==0,"Vitals migration reset old progress.");
            s.Vitals.AdjustMental(-12); store.Save(s); check(store.TryLoad(out s,out _) && s.Vitals.CurrentMental==88,"Reload reset an existing mental value.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            s.Vitals.CurrentHP=410; File.WriteAllText(path,JsonUtility.ToJson(s));
            check(!store.TryLoad(out _,out _),"New save accepted HP above the recovery cap.");
            old.Vitals.CurrentHP=501; File.WriteAllText(path,JsonUtility.ToJson(old));
            check(!store.TryLoad(out _,out _),"Legacy migration concealed corrupt HP above the base maximum.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            Debug.Log("VD_VITALS_CHECKS_OK: "+checks+" assertions; immediate cap, injury+hunger interactions, treatment healing, mental bounds, save migration and corruption rejection.");
        }
    }
}
