using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class SleepTradeChecks
    {
        [MenuItem("Vanishing Days/Validate sleep morality and street trade")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0;
            Action<bool,string> check=(ok,why)=>{checks++; if(!ok) throw new Exception(why);};
            Func<SurvivalState> fresh=()=>SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            int hungerRate=data.TEMP_BALANCE.hungerPerHour; data.TEMP_BALANCE.hungerPerHour=0;
            try
            {
                var s=fresh(); s.AdvanceTime(18,data);
                check(s.Sleepiness==72 && s.Vitals.CurrentMental==100,"Awake sleepiness/assessment timing wrong.");
                s.AdvanceTime(6,data,true);
                check(s.hour==8 && s.SleepHours==0 && s.Sleepiness==0 && s.Vitals.CurrentMental==100,"Six hours crossing midnight did not meet requirement.");
                s=fresh(); s.AdvanceTime(24,data); check(s.Vitals.CurrentMental==82 && s.Sleepiness==96,"No-sleep daily penalty wrong.");
                s.AdvanceTime(24,data); check(s.Vitals.CurrentMental==64 && s.Sleepiness==100,"Repeated deprivation did not accumulate/clamp.");
                s=fresh(); s.AdvanceTime(20,data); s.AdvanceTime(4,data,true); check(s.Vitals.CurrentMental==94,"Partial sleep did not charge only missing hours.");
                s=fresh(); s.Vitals.AdjustMental(-50); s.AdvanceTime(16,data); s.Rest(data,false,8);
                check(s.Vitals.CurrentMental==54 && s.Sleepiness==0,"Extra sleep did not restore two mental points per hour.");
                s=fresh(); s.AdvanceTime(18,data); s.Rest(data); s.Rest(data); s.Rest(data);
                check(s.Vitals.CurrentMental==100 && s.SleepHours==0,"Split sleep was not accumulated.");
                s=fresh(); s.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; s.RobHungrySurvivor();
                check(s.Vitals.MentalCap==90 && s.Vitals.CurrentMental==90 && s.Backpack.Count("tuna")==1 && s.PendingEncounter==null,"Robbery moral cost/reward wrong.");
                s.RobHungrySurvivor(); check(s.Backpack.Count("tuna")==1 && s.Vitals.MentalCap==90,"Resolved robbery repeated.");
                s.Vitals.AdjustMental(int.MaxValue); s.AdvanceTime(8,data,true);
                check(s.Vitals.CurrentMental==90 && s.Vitals.MentalCap==90,"Sleep restored permanent moral cap.");
                s=fresh(); s.Backpack.Add("weapon",1); s.Equip("weapon",data); s.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; s.ResolveEncounter(true,data,()=>0);
                check(s.Vitals.MentalCap==75 && s.lastReport.Contains("숨"),"Armed killing did not lower cap.");
                s=fresh(); s.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; s.ResolveEncounter(true,data,()=>0);
                check(s.Vitals.MentalCap==100,"Unarmed escape was treated as a killing.");
                s=fresh(); s.Backpack.Add("weapon",1); s.Equip("weapon",data); s.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; int roll=0; s.ResolveEncounter(true,data,()=>roll++==0?0:.9);
                check(s.Vitals.MentalCap==100,"Every armed escape became fatal.");
                s.Vitals.MoralHarm(int.MaxValue); check(s.Vitals.MentalCap==0 && s.Vitals.CurrentMental==0 && !s.IsGameOver,"Moral cap underflow or unrequested death.");
                s=fresh(); s.Travel("store",data,()=>.27);
                check(s.PendingEncounter?.kind=="trader" && !s.CanAct,"Rare daylight street trader did not block normal actions.");
                check(!s.CanStreetTrade(0),"Trader accepted home supplies.");
                s.StreetTrade(0,data); check(s.PendingEncounter!=null && s.Backpack.Count("bandage")==0,"Insufficient trade mutated state.");
                s.Backpack.Add("tuna",2); s.StreetTrade(-1,data); s.StreetTrade(99,data); check(s.Backpack.Count("tuna")==2,"Invalid offer spent supplies.");
                s.StreetTrade(0,data); check(s.Backpack.Count("tuna")==0 && s.Backpack.Count("bandage")==1 && s.PendingEncounter==null,"Trade did not conserve promised amounts/end encounter.");
                s.StreetTrade(0,data); check(s.Backpack.Count("bandage")==1,"Trade could repeat after departure.");
                foreach(int index in new[]{1,2})
                {
                    s=fresh(); var offer=SurvivalState.StreetOffers[index]; s.PendingEncounter=new Encounter{kind="trader",context="길거리"}; s.Backpack.Add(offer.give,offer.cost); s.StreetTrade(index,data);
                    check(s.Backpack.Count(offer.give)==0 && s.Backpack.Count(offer.receive)==offer.amount,"Alternative offer wrong.");
                }
                s=fresh(); s.Travel("store",data,()=>0); check(s.PendingEncounter?.kind=="hungry","Ordinary encounter replaced by trader.");
                s=fresh(); s.hour=20; s.Travel("store",data,()=>.09); check(s.PendingEncounter==null && s.Vitals.Injuries.Count==1,"Night spawned trader instead of danger.");
                s=fresh(); s.Travel("store",data,()=>1); s.Search(data,()=>.22); check(s.PendingEncounter?.kind=="hungry","Indoor search spawned street trader.");
                s=fresh(); s.Travel("store",data,()=>.27); var before=s.RandomState; int hour=s.hour; s.LeaveStreetTrader();
                check(s.CanAct && s.hour==hour && s.RandomState==before && s.Backpack.stacks.Count==0,"Declining trader spent time/supplies.");
                foreach(var location in data.locations)
                {
                    string summary=s.LootSummary(location.id,data,true);
                    foreach(var loot in location.loot) check(!summary.Contains(data.Item(loot.id).name),"Loot types leaked before searching.");
                }
                string path=Path.GetFullPath("Builds/Validation/sleep-trade-save.json"); var saves=new SessionSaveStore(path,data);
                foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
                s=fresh(); s.schemaVersion=7; s.Vitals.AdjustMental(-21); File.WriteAllText(path,JsonUtility.ToJson(s));
                check(saves.TryLoad(out s,out _) && s.schemaVersion==10 && s.Vitals.CurrentMental==79 && s.SleepHours==6,"Old save lost mental health or received retroactive sleep debt.");
                s.Sleepiness=67; s.SleepHours=4; s.Vitals.MoralHarm(10); s.PendingEncounter=new Encounter{kind="trader",context="길거리"}; saves.Save(s);
                check(saves.TryLoad(out s,out _) && s.Sleepiness==67 && s.SleepHours==4 && s.Vitals.MentalCap==90 && s.PendingEncounter.kind=="trader","Sleep/moral cap/pending trader not persisted.");
                foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
                s.Sleepiness=101; File.WriteAllText(path,JsonUtility.ToJson(s)); check(!saves.TryLoad(out _,out _),"Corrupt sleepiness accepted.");
                s.Sleepiness=67; s.Vitals.CurrentMental=91; File.WriteAllText(path,JsonUtility.ToJson(s)); check(!saves.TryLoad(out _,out _),"Mental health above reduced cap accepted.");
                foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            }
            finally { data.TEMP_BALANCE.hungerPerHour=hungerRate; }
            Debug.Log("VD_SLEEP_TRADE_CHECKS_OK: "+checks+" assertions; daily sleep, recovery, moral cap, rare street trades, hidden loot and v7 migration.");
        }
    }
}
