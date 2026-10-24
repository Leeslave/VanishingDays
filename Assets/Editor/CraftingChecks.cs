using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class CraftingChecks
    {
        [MenuItem("Vanishing Days/Validate crafting tabs and equipment")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); int checks=0;
            Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
            Func<SurvivalState> fresh=()=>SurvivalState.NewGame(GameDifficulty.NORMAL,data,123);
            check(data.crafting.recipes.Count(x=>x.category=="weapons")==8,"Expected eight craftable weapons.");
            foreach(var recipe in data.crafting.recipes)
            {
                var s=fresh(); foreach(var cost in recipe.ingredients) s.HomeStorage.Add(cost.id,cost.amount);
                if(!string.IsNullOrEmpty(recipe.requiresFacility)) s.InstalledFacilities.Add(recipe.requiresFacility);
                var before=data.items.ToDictionary(x=>x.id,x=>s.HomeStorage.Count(x.id)); int hours=s.day*24+s.hour; uint rng=s.RandomState;
                check(s.CanCraft(recipe.id,data),"Recipe not craftable with requirements: "+recipe.id); s.Craft(recipe.id,data);
                check(s.day*24+s.hour==hours+recipe.hours && s.RandomState==rng,"Crafting time/RNG wrong: "+recipe.id);
                foreach(var cost in recipe.ingredients) check(s.HomeStorage.Count(cost.id)==before[cost.id]-cost.amount,"Recipe did not spend exact ingredients: "+recipe.id+"/"+cost.id);
                check(recipe.facility?s.HasFacility(recipe.id):s.HomeStorage.Count(recipe.result)==before[recipe.result]+recipe.amount,"Craft output missing: "+recipe.id);
                if(recipe.facility) { hours=s.hour; s.Craft(recipe.id,data); check(s.hour==hours && s.InstalledFacilities.Count(x=>x==recipe.id)==1,"Duplicate facility spent time or duplicated."); }
            }
            var state=fresh(); int hour=state.hour; state.Craft("club",data); check(state.hour==hour && state.HomeStorage.Count("club")==0,"Insufficient materials created an item.");
            foreach(var cost in data.crafting.Find("machete").ingredients) state.HomeStorage.Add(cost.id,cost.amount);
            state.Craft("machete",data); check(state.hour==hour && state.HomeStorage.Count("machete")==0,"Missing workbench bypassed.");
            state=fresh(); state.Backpack.Add("wood",2); state.Backpack.Add("tools",1); state.Craft("club",data); check(state.HomeStorage.Count("club")==0,"Crafting spent carried materials instead of home stock.");
            state.HomeStorage.Add("wood",2); state.HomeStorage.Add("tools",1); state.CurrentLocation="store"; state.AwayFromHome=true; state.Craft("club",data); check(state.HomeStorage.Count("wood")==2,"Crafting allowed away from home.");
            state.CurrentLocation="home";state.AwayFromHome=false; state.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; state.Craft("club",data); check(state.HomeStorage.Count("wood")==2,"Crafting bypassed pending encounter.");
            state.PendingEncounter=null;state.Vitals.CurrentHP=0;state.Craft("club",data);check(state.HomeStorage.Count("wood")==2,"Dead player crafted.");
            state=fresh(); state.HomeStorage.Add("club",1); state.HomeStorage.Add("machete",1); state.HomeStorage.Add("clothes",1);
            state.Equip("clothes",data); state.Equip("club",data); state.Equip("machete",data);
            check(state.Strength(data)==7 && state.Defense(data)==3 && state.Equipment.Count==2 && state.HomeStorage.Count("club")==1,"Weapon replacement stacked bonuses or lost old weapon.");
            state.Equip("machete",data); check(state.Strength(data)==0 && state.HomeStorage.Count("machete")==1,"Unequipping lost crafted weapon.");
            state.CurrentLocation="store";state.AwayFromHome=true;state.Backpack.Add("club",1);state.Backpack.Add("pipe_club",1);state.Equip("club",data);state.Equip("pipe_club",data);
            check(state.Backpack.Count("club")==1 && state.Strength(data)==4,"Away equipment replacement did not return old weapon to pack.");
            state.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; state.ResolveEncounter(true,data,()=>0);
            check(state.Vitals.MentalCap==75,"Crafted weapon bypassed armed-resistance moral consequences.");
            state=fresh();state.HomeStorage.Add("club",1);state.Equip("club",data);state.HomeStorage.Add("cloth",2);state.HomeStorage.Add("tape",1);
            check(!state.CanCraft("wrapped_club",data),"Equipped weapon silently consumed as material.");state.Equip("club",data);state.Craft("wrapped_club",data);
            check(state.HomeStorage.Count("club")==0 && state.HomeStorage.Count("wrapped_club")==1,"Weapon upgrade did not conserve lower-tier weapon.");
            state=fresh();state.InstalledFacilities.AddRange(new[]{"water_collector","water_filter"});int water=state.HomeStorage.Count("water");state.Craft("collect_water",data);state.Craft("purify_water",data);
            check(state.HomeStorage.Count("raw_water")==0 && state.HomeStorage.Count("water")==water+1 && state.hour==12,"Collection/purification loop failed.");
            state=fresh();state.HomeStorage.Add("raw_water",1);state.HomeStorage.Remove("water",8);state.HydrationHours=5;state.AdvanceTime(1,data);
            check(state.Vitals.CurrentHP==490 && state.HomeStorage.Count("raw_water")==1,"Raw water was drunk without purification.");
            state=fresh();state.InstalledFacilities.Add("bedroll");state.Vitals.AdjustMental(-50);state.Sleepiness=100;state.Rest(data,false,8);
            check(state.Sleepiness==0 && state.Vitals.CurrentMental==56 && state.SleepMentalRecovery==3,"Bedroll did not improve actual sleep.");
            state=fresh();foreach(var cost in data.crafting.Find("club").ingredients)state.HomeStorage.Add(cost.id,cost.amount);state.Vitals.CurrentHP=1;state.Vitals.Hunger=100;state.Craft("club",data);
            check(state.IsGameOver && state.HomeStorage.Count("club")==0 && state.HomeStorage.Count("wood")==0,"Fatal craft produced output or refunded used materials.");
            string path=Path.GetFullPath("Builds/Validation/crafting-save.json");var saves=new SessionSaveStore(path,data);
            foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            state=fresh();state.schemaVersion=11;state.Locations.Find(x=>x.id=="store").remaining.Remove("tuna",1);foreach(var location in state.Locations)location.remaining.stacks.RemoveAll(x=>new[]{"cloth","scrap","cord","tape","raw_water"}.Contains(x.id));
            state.InstalledSecurity.Add("door1");state.Vitals.AdjustMental(-17);File.WriteAllText(path,JsonUtility.ToJson(state));
            check(saves.TryLoad(out state,out _) && state.schemaVersion==12 && state.Locations.Find(x=>x.id=="store").remaining.Count("tuna")==0 && state.DoorLevel==1 && state.Vitals.CurrentMental==83,"Crafting migration reset old progress.");
            check(state.Locations.Find(x=>x.id=="empty_house").remaining.Count("cloth")==6,"Migration did not add new materials.");
            state.InstalledFacilities.Add("water_filter");state.HomeStorage.Add("club",1);state.Equip("club",data);state.Locations.Find(x=>x.id=="empty_house").remaining.Remove("cloth",6);saves.Save(state);
            check(saves.TryLoad(out state,out _) && state.HasFacility("water_filter") && state.Equipment.Contains("club") && state.Locations.Find(x=>x.id=="empty_house").remaining.Count("cloth")==0,"Reload lost crafting state or regenerated materials.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            state.InstalledFacilities.Add("unknown");File.WriteAllText(path,JsonUtility.ToJson(state));check(!saves.TryLoad(out _,out _),"Unknown facility accepted.");
            state.InstalledFacilities.Remove("unknown");state.Equipment.Add("machete");File.WriteAllText(path,JsonUtility.ToJson(state));check(!saves.TryLoad(out _,out _),"Multiple equipped weapons accepted.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            Debug.Log("VD_CRAFTING_CHECKS_OK: "+checks+" assertions; all recipes, facilities, upgrades, single weapon slot, water loop, sleep, action gates and v11 migration.");
        }
    }
}
