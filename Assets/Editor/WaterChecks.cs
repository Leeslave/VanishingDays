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
            Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
            Func<GameDifficulty,SurvivalState> fresh=d=>SurvivalState.NewGame(d,data,123);
            for(int i=0;i<3;i++)
            {
                var s=fresh((GameDifficulty)i); s.AdvanceTime(4,data);
                check(s.Vitals.Hunger==new[]{4,8,12}[i] && s.Thirst==new[]{0,20,32}[i] && s.Sleepiness==new[]{8,12,16}[i],"Hourly difficulty rates incorrect.");
            }
            int hunger=data.TEMP_BALANCE.hungerPerHour;data.TEMP_BALANCE.hungerPerHour=0;
            try
            {
                var easy=fresh(GameDifficulty.EASY);easy.AdvanceTime(48,data);
                check(easy.UnlimitedWater && easy.Thirst==0 && easy.HydrationHours==0 && easy.Vitals.CurrentHP==500,"Easy water no longer unlimited.");
                foreach(var recipe in data.recipes)
                {
                    easy=fresh(GameDifficulty.EASY);foreach(var item in recipe.ingredients)if(item.id!="water")easy.HomeStorage.Add(item.id,item.amount);
                    check(easy.CanCook(recipe,data),"Easy cooking requires finite water.");easy.Cook(recipe.id,data);check(easy.HomeStorage.Count("water")==0 && easy.HomeStorage.Count(recipe.result)==1,"Easy cooking failed.");
                }
                foreach(var difficulty in new[]{GameDifficulty.NORMAL,GameDifficulty.HARD})
                {
                    var s=fresh(difficulty);int stock=s.HomeStorage.Count("water"),interval=s.WaterInterval(data);
                    s.AdvanceTime(interval-1,data);check(s.HomeStorage.Count("water")==stock && s.HydrationHours==interval-1,"Early automatic drinking.");
                    s.AdvanceTime(1,data);check(s.HomeStorage.Count("water")==stock-1 && s.Thirst==0 && s.HydrationHours==0,"Difficulty drink schedule failed.");
                    s.AdvanceTime(interval*2,data,true);check(s.HomeStorage.Count("water")==stock-3,"Sleep skipped water consumption.");
                    s=fresh(difficulty);s.CurrentLocation="store";s.AwayFromHome=true;s.AdvanceTime(interval,data);
                    check(s.HomeStorage.Count("water")==stock && s.Vitals.CurrentHP==490,"Away drinking accessed home water.");
                    s.Backpack.Add("water",1);s.AdvanceTime(interval,data);check(s.Backpack.Count("water")==0 && s.Vitals.CurrentHP==490 && s.Thirst==0,"Carried drinking failed.");
                    s=fresh(difficulty);s.HomeStorage.Remove("water",stock);s.Vitals.CurrentHP=10;s.AdvanceTime(interval*2,data);
                    check(s.IsGameOver && s.hour==8+interval,"Fatal shortage did not stop action.");
                }
                var normal=fresh(GameDifficulty.NORMAL);normal.HomeStorage.Remove("water",8);normal.Backpack.Add("water",1);normal.AdvanceTime(10,data);
                check(normal.Backpack.Count("water")==0 && normal.Vitals.CurrentHP==500,"Home did not fall back to backpack.");
                normal=fresh(GameDifficulty.NORMAL);normal.HomeStorage.Remove("water",8);normal.AdvanceTime(21,data);
                check(normal.Thirst==100 && normal.Vitals.CurrentHP==480,"Accumulated thirst/shortage incorrect.");
                normal.HomeStorage.Add("water",1);normal.DrinkWater();check(normal.Thirst==0 && normal.HydrationHours==0,"Manual drink reset failed.");
                normal=fresh(GameDifficulty.NORMAL);normal.Cook("rice",data);check(normal.HomeStorage.Count("water")==7 && normal.HomeStorage.Count("rice")==2900,"Cooking cost changed.");
                normal=fresh(GameDifficulty.NORMAL);normal.Travel("store",data,()=>1);for(int i=0;i<4;i++)normal.Search(data,()=>1);
                check(normal.Locations.Find(x=>x.id=="store").remaining.Count("water")==0,"Water loot not finite.");
                int hour=normal.hour;normal.Search(data,()=>1);check(normal.hour==hour,"Empty search spent time.");
                string path=Path.GetFullPath("Builds/Validation/water-save.json");Directory.CreateDirectory(Path.GetDirectoryName(path));var saves=new SessionSaveStore(path,data);
                Action clean=()=>{foreach(string f in new[]{path,path+".bak",path+".tmp"})if(File.Exists(f))File.Delete(f);};
                clean();normal=fresh(GameDifficulty.NORMAL);normal.schemaVersion=8;normal.HomeStorage.Remove("water",8);
                foreach(var place in normal.Locations)place.remaining.stacks.RemoveAll(x=>x.id=="water");
                normal.Locations.Find(x=>x.id=="store").remaining.Remove("tuna",1);File.WriteAllText(path,JsonUtility.ToJson(normal));
                check(saves.TryLoad(out normal,out _) && normal.schemaVersion==13 && normal.HomeStorage.Count("water")==6 && normal.Backpack.Count("water")==2,"Legacy water migration failed.");
                check(normal.Locations.Find(x=>x.id=="store").remaining.Count("tuna")==0,"Migration regenerated old loot.");
                clean();normal=fresh(GameDifficulty.NORMAL);normal.schemaVersion=9;normal.HydrationHours=4;File.WriteAllText(path,JsonUtility.ToJson(normal));
                check(saves.TryLoad(out normal,out _) && normal.Thirst==40 && normal.HydrationHours==4,"Old thirst migration reset saved progress.");
                clean();normal=fresh(GameDifficulty.NORMAL);normal.HydrationHours=9;normal.Thirst=45;saves.Save(normal);
                check(saves.TryLoad(out normal,out _) && normal.HydrationHours==9 && normal.Thirst==45,"Hydration progress not saved.");
                normal.AdvanceTime(1,data);check(normal.HomeStorage.Count("water")==7 && normal.Thirst==0,"Loaded drink timer failed.");
                clean();normal.HydrationHours=10;File.WriteAllText(path,JsonUtility.ToJson(normal));check(!saves.TryLoad(out _,out _),"Invalid drink timer accepted.");
                foreach(int value in new[]{-1,101}){normal=fresh(GameDifficulty.NORMAL);normal.Thirst=value;File.WriteAllText(path,JsonUtility.ToJson(normal));check(!saves.TryLoad(out _,out _),"Invalid thirst accepted.");}
                easy=fresh(GameDifficulty.EASY);easy.Thirst=1;File.WriteAllText(path,JsonUtility.ToJson(easy));check(!saves.TryLoad(out _,out _),"Easy accepted thirst.");clean();
            }
            finally{data.TEMP_BALANCE.hungerPerHour=hunger;}
            Debug.Log("VD_WATER_CHECKS_OK: "+checks+" assertions; difficulty rates, finite water, cooking, away/sleep drinking, migration and invalid saves.");
        }
    }
}
