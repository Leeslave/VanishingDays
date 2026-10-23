using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class WaterChecks
    {
        [MenuItem("Vanishing Days/Validate difficulty water supply")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0;
            Action<bool,string> check=(ok,why)=>{checks++; if(!ok)throw new Exception(why);};
            Func<GameDifficulty,SurvivalState> fresh=d=>SurvivalState.NewGame(d,data,123);
            int hunger=data.TEMP_BALANCE.hungerPerHour; data.TEMP_BALANCE.hungerPerHour=0;
            try
            {
                var s=fresh(GameDifficulty.EASY); s.AdvanceTime(48,data);
                check(s.UnlimitedWater && s.HomeStorage.Count("water")==0 && s.HydrationHours==0 && s.Vitals.CurrentHP==500,"Easy water was limited or caused damage.");
                foreach(var recipe in data.recipes)
                {
                    s=fresh(GameDifficulty.EASY); foreach(var ingredient in recipe.ingredients) if(ingredient.id!="water") s.HomeStorage.Add(ingredient.id,ingredient.amount);
                    check(s.CanCook(recipe,data),"Easy cooking required water."); s.Cook(recipe.id,data); check(s.HomeStorage.Count(recipe.result)==1 && s.HomeStorage.Count("water")==0,"Easy recipe spent finite water.");
                }
                foreach(var difficulty in new[]{GameDifficulty.NORMAL,GameDifficulty.HARD})
                {
                    s=fresh(difficulty); int water=s.HomeStorage.Count("water");
                    check(!s.UnlimitedWater && water==(difficulty==GameDifficulty.NORMAL?8:4),"Difficulty initial water wrong.");
                    s.AdvanceTime(5,data); check(s.HomeStorage.Count("water")==water && s.HydrationHours==5,"Water consumed too early.");
                    s.AdvanceTime(1,data); check(s.HomeStorage.Count("water")==water-1 && s.HydrationHours==0 && s.Vitals.CurrentHP==500,"Water did not consume exactly at six hours.");
                    s.AdvanceTime(18,data,true); check(s.HomeStorage.Count("water")==water-4,"Sleeping/multi-hour action bypassed drinking.");
                }
                s=fresh(GameDifficulty.NORMAL); s.HomeStorage.Remove("water",8); s.Backpack.Add("water",1); s.AdvanceTime(6,data);
                check(s.Backpack.Count("water")==0 && s.Vitals.CurrentHP==500,"Home did not fall back to carried water.");
                s=fresh(GameDifficulty.NORMAL); s.CurrentLocation="store"; s.AwayFromHome=true; s.AdvanceTime(6,data);
                check(s.HomeStorage.Count("water")==8 && s.Vitals.CurrentHP==490 && s.HydrationHours==0,"Away drinking used home water or lacked shortage damage.");
                s.Backpack.Add("water",1); s.AdvanceTime(6,data); check(s.Backpack.Count("water")==0 && s.Vitals.CurrentHP==490,"Carried water did not protect against dehydration.");
                s=fresh(GameDifficulty.NORMAL); s.HomeStorage.Remove("water",8); s.Vitals.CurrentHP=10; s.AdvanceTime(12,data);
                check(s.IsGameOver && s.hour==14,"Fatal dehydration did not stop the action.");
                s=fresh(GameDifficulty.NORMAL); s.Cook("rice",data);
                check(s.HomeStorage.Count("water")==7 && s.HomeStorage.Count("rice")==2900 && s.HomeStorage.Count("rice_meal")==1,"Normal cooking did not spend one bottle plus food.");
                s.HomeStorage.Remove("water",7); int hour=s.hour; s.Cook("rice",data);
                check(s.hour==hour && s.HomeStorage.Count("rice")==2900 && s.HomeStorage.Count("rice_meal")==1,"Dry cooking spent time or ingredients.");
                s=fresh(GameDifficulty.NORMAL); s.Transfer("water",true,2,data); check(s.HomeStorage.Count("water")==6 && s.Backpack.Count("water")==2,"Water quantity transfer failed.");
                s.StoreAll(data); check(s.HomeStorage.Count("water")==8 && s.Backpack.Count("water")==0,"Store-all missed water.");
                s=fresh(GameDifficulty.NORMAL); s.Travel("store",data,()=>1);
                for(int i=0;i<4;i++) s.Search(data,()=>1);
                check(s.Backpack.Count("water")==4 && s.Locations.Find(x=>x.id=="store").remaining.Count("water")==0,"Exploration water was not finite/exact.");
                hour=s.hour; s.Search(data,()=>1); check(s.Backpack.Count("water")==4 && s.hour==hour,"Empty water pool regenerated or spent time.");
                foreach(var place in data.locations) if(place.id!="home" && !place.storyOnly) check(Array.Exists(place.loot,x=>x.id=="water" && x.amount>0),"Missing water exploration source: "+place.id);
                string path=Path.GetFullPath("Builds/Validation/water-save.json"); var saves=new SessionSaveStore(path,data);
                foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
                s=fresh(GameDifficulty.NORMAL); s.schemaVersion=8; s.HomeStorage.Remove("water",8);
                foreach(var place in s.Locations) place.remaining.stacks.RemoveAll(x=>x.id=="water");
                s.Locations.Find(x=>x.id=="store").remaining.Remove("tuna",1); s.Vitals.AdjustMental(-30); File.WriteAllText(path,JsonUtility.ToJson(s));
                check(saves.TryLoad(out s,out _) && s.schemaVersion==10 && s.HomeStorage.Count("water")==6 && s.Backpack.Count("water")==2 && s.Vitals.CurrentMental==70,"Legacy water upgrade lost progress or initial supply.");
                check(s.Locations.Find(x=>x.id=="store").remaining.Count("water")==4 && s.Locations.Find(x=>x.id=="store").remaining.Count("tuna")==0,"Migration replenished old loot or missed water.");
                s.Locations.Find(x=>x.id=="store").remaining.Remove("water",4); s.HydrationHours=5; saves.Save(s);
                check(saves.TryLoad(out s,out _) && s.HydrationHours==5 && s.Locations.Find(x=>x.id=="store").remaining.Count("water")==0 && s.HomeStorage.Count("water")==6,"Reload regenerated exhausted water or reset timer.");
                s.AdvanceTime(1,data); check(s.HomeStorage.Count("water")==5 && s.HydrationHours==0,"Restored drinking timer was ignored.");
                foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
                s.HydrationHours=6; File.WriteAllText(path,JsonUtility.ToJson(s)); check(!saves.TryLoad(out _,out _),"Invalid drinking timer accepted.");
                foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
                s=fresh(GameDifficulty.NORMAL); s.HomeStorage.Remove("water",8);
                s.AdvanceTime(5,data); check(s.Thirst==50 && s.Vitals.CurrentHP==500,"Thirst did not rise hourly before drinking time.");
                s.AdvanceTime(1,data); check(s.Thirst==60 && s.Vitals.CurrentHP==490,"Missed drinking reset thirst or changed shortage damage.");
                s.AdvanceTime(10,data); check(s.Thirst==100,"Thirst did not clamp at 100 while water was unavailable.");
                s.HomeStorage.Add("water",1); s.AdvanceTime(2,data);
                check(s.Thirst==0 && s.HomeStorage.Count("water")==0,"Drinking did not quench accumulated thirst.");
                s=fresh(GameDifficulty.NORMAL); s.AdvanceTime(6,data); check(s.Thirst==0 && s.HomeStorage.Count("water")==7,"Scheduled drinking failed to quench thirst.");
                s=fresh(GameDifficulty.EASY); s.AdvanceTime(24,data); check(s.Thirst==0,"Unlimited water accumulated thirst.");
                s=fresh(GameDifficulty.NORMAL); s.schemaVersion=9; s.HydrationHours=4; s.Vitals.AdjustMental(-21); File.WriteAllText(path,JsonUtility.ToJson(s));
                check(saves.TryLoad(out s,out _) && s.schemaVersion==10 && s.Thirst==40 && s.HydrationHours==4 && s.HomeStorage.Count("water")==8 && s.Vitals.CurrentMental==79,"v9 upgrade reset hydration, inventory or mental health.");
                saves.Save(s); check(saves.TryLoad(out s,out _) && s.Thirst==40,"Thirst did not persist in new saves.");
                foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
                foreach(int value in new[]{-1,101}) { s.Thirst=value; File.WriteAllText(path,JsonUtility.ToJson(s)); check(!saves.TryLoad(out _,out _),"Out-of-range thirst accepted."); }
                s=fresh(GameDifficulty.EASY); s.Thirst=1; File.WriteAllText(path,JsonUtility.ToJson(s)); check(!saves.TryLoad(out _,out _),"Easy accepted finite thirst.");
                foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            }
            finally { data.TEMP_BALANCE.hungerPerHour=hunger; }
            Debug.Log("VD_WATER_CHECKS_OK: "+checks+" assertions; easy unlimited, finite hydration/cooking/loot, carry/store, dehydration and v8 migration.");
        }
    }
}
