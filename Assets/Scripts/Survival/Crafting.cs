using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    [Serializable] public class CraftDefinition
    {
        public string id,name,category,description,result,requiresFacility;
        public int amount=1,hours; public bool facility;
        public ItemStack[] ingredients;
    }
    [Serializable] public class CraftingCatalog
    {
        public CraftDefinition[] recipes;
        public CraftDefinition Find(string id) => Array.Find(recipes,x=>x.id==id);
        public static CraftingCatalog Load()
        {
            var asset=Resources.Load<TextAsset>("Survival/Crafting");
            if(asset==null) throw new InvalidOperationException("Missing crafting catalog.");
            return JsonUtility.FromJson<CraftingCatalog>(asset.text);
        }
        public void Validate(SurvivalDefinitions data)
        {
            if(recipes==null || recipes.Length<12) throw new InvalidOperationException("Incomplete crafting catalog.");
            var ids=new HashSet<string>();
            foreach(var recipe in recipes)
            {
                if(recipe==null || string.IsNullOrWhiteSpace(recipe.id) || !ids.Add(recipe.id) || string.IsNullOrWhiteSpace(recipe.name) || string.IsNullOrWhiteSpace(recipe.description) || recipe.hours<1 || recipe.category!="weapons" && recipe.category!="living" || recipe.ingredients==null || !recipe.facility && (data.Item(recipe.result)==null || recipe.amount<1)) throw new InvalidOperationException("Invalid crafting recipe.");
                foreach(var cost in recipe.ingredients) if(cost==null || data.Item(cost.id)==null || cost.amount<1 || recipe.ingredients.Count(x=>x.id==cost.id)!=1) throw new InvalidOperationException("Invalid crafting ingredients.");
                if(recipe.ingredients.Length==0 && recipe.id!="collect_water" && recipe.id!="gather_water") throw new InvalidOperationException("Crafting requires materials.");
                if(!string.IsNullOrEmpty(recipe.requiresFacility) && (Find(recipe.requiresFacility)==null || !Find(recipe.requiresFacility).facility || recipe.requiresFacility==recipe.id)) throw new InvalidOperationException("Invalid crafting facility requirement.");
            }
        }
    }
    public partial class SurvivalState
    {
        public List<string> InstalledFacilities=new List<string>();
        public bool HasFacility(string id) => InstalledFacilities.Contains(id);
        public int SleepMentalRecovery => HasFacility("bedroll")?3:2;
        public int SleepinessRecovery => HasFacility("bedroll")?16:12;
        public bool HasWeapon(SurvivalDefinitions data) => Equipment.Exists(x=>data.Item(x).strength>0);
        public bool CanCraft(string id,SurvivalDefinitions data)
        {
            var recipe=data.crafting.Find(id);
            return CanAct && CurrentLocation=="home" && recipe!=null && (id!="boil_water" || HomeStorage.Count("burner")>0) && (!recipe.facility || !HasFacility(id)) && (string.IsNullOrEmpty(recipe.requiresFacility) || HasFacility(recipe.requiresFacility)) && HomeStorage.Has(recipe.ingredients);
        }
        public string Craft(string id,SurvivalDefinitions data)
        {
            var recipe=data.crafting.Find(id);
            if(!CanCraft(id,data)) return "집에 보관한 재료와 필요한 설비를 확인하자. 장착한 재료 무기는 먼저 벗어 보관해야 한다.";
            HomeStorage.Spend(recipe.ingredients); RecordFact("story",recipe.name+" 작업을 시작했다.");
            AdvanceTime(recipe.hours,data);
            if(IsGameOver) return Report("제작 도중 쓰러졌다. 사용한 재료로 작업을 마치지 못했다.");
            if(recipe.facility) InstalledFacilities.Add(id); else HomeStorage.Add(recipe.result,recipe.amount);
            string text=recipe.facility?recipe.name+"를 집에 설치했다.":data.Item(recipe.result).name+" "+data.Quantity(recipe.result,recipe.amount)+"을 만들어 집에 보관했다.";
            RecordFact("story",text); return Report(text);
        }
        public void UpgradeCrafting(SurvivalDefinitions data)
        {
            InstalledFacilities=new List<string>();
            var additions=new HashSet<string>{"scrap","cloth","cord","tape","raw_water"};
            foreach(var place in data.locations)
            {
                var pool=Locations.Find(x=>x.id==place.id).remaining;
                foreach(var stack in place.loot) if(additions.Contains(stack.id) && !pool.stacks.Exists(x=>x.id==stack.id)) pool.Add(stack.id,stack.amount);
            }
        }
    }
}
