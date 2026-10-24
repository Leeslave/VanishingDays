using System;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class WaterAccessChecks
    {
        [MenuItem("Vanishing Days/Validate renewable water and warnings")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int count=0;
            Action<bool,string> check=(ok,why)=>{count++;if(!ok)throw new Exception(why);};
            int hunger=data.TEMP_BALANCE.hungerPerHour;data.TEMP_BALANCE.hungerPerHour=0;
            try
            {
                foreach(var difficulty in new[]{GameDifficulty.NORMAL,GameDifficulty.HARD})
                {
                    var s=SurvivalState.NewGame(difficulty,data,123);s.HomeStorage.Remove("water",s.HomeStorage.Count("water"));
                    check(s.NeedsWaterWarning && s.CanCraft("gather_water",data),"Empty supply did not warn or allow gathering.");
                    for(int i=0;i<4;i++){s.Craft("gather_water",data);s.Craft("boil_water",data);}
                    check(s.HomeStorage.Count("water")==6 && s.Vitals.CurrentHP==500 && s.HomeStorage.Count("raw_water")==0,"Renewable water failed to outpace consumption.");
                    s.AdvanceTime(2,data);int water=s.HomeStorage.Count("water"),hour=s.hour;uint random=s.RandomState;
                    check(s.CanDrink,"Carried/home drinking unavailable.");s.DrinkWater();
                    check(s.Thirst==0 && s.HydrationHours==0 && s.hour==hour && s.RandomState==random && s.HomeStorage.Count("water")==water-1,"Manual drinking cost or reset wrong.");
                    s.DrinkWater();check(s.HomeStorage.Count("water")==water-1,"Drinking while hydrated wasted water.");
                    s.CurrentLocation="store";s.AwayFromHome=true;
                    check(s.NeedsWaterWarning && !s.CanDrink && !s.CanCraft("gather_water",data) && !s.CanCraft("boil_water",data),"Away actions used home water/equipment.");
                    s.Backpack.Add("water",1);s.Thirst=70;s.HydrationHours=1;s.DrinkWater();
                    check(s.Backpack.Count("water")==0 && s.Thirst==0 && s.HomeStorage.Count("water")==water-1,"Away drink did not use pack exclusively.");
                    s.Sleepiness=59;check(!s.NeedsSleepWarning,"Sleep warning too early.");s.Sleepiness=60;check(s.NeedsSleepWarning,"Missing sleep warning.");
                    s.CurrentLocation="home";s.AwayFromHome=false;s.Rest(data,false,6);check(!s.NeedsSleepWarning,"Sleep warning did not clear.");
                    s.Thirst=60;check(s.NeedsWaterWarning,"Thirst threshold missing.");s.DrinkWater();check(!s.NeedsWaterWarning,"Drink warning did not clear.");
                    s.PendingEncounter=new Encounter{kind="hungry"};check(!s.CanDrink && !s.CanCraft("gather_water",data),"Water actions bypassed encounter.");
                    s.PendingEncounter=null;s.Vitals.CurrentHP=0;check(!s.CanDrink && !s.CanCraft("gather_water",data),"Dead player used water actions.");
                }
                var easy=SurvivalState.NewGame(GameDifficulty.EASY,data,123);check(!easy.NeedsWaterWarning && !easy.CanDrink,"Unlimited water warned or spent bottles.");
            }
            finally{data.TEMP_BALANCE.hungerPerHour=hunger;}
            Debug.Log("VD_WATER_ACCESS_OK: "+count+" assertions; renewable supply, difficulty, manual drinking, warnings and action gates.");
        }
    }
}
