using System;

namespace VanishingDays
{
    public partial class SurvivalState
    {
        public int HydrationHours, Thirst;
        public bool UnlimitedWater => Difficulty==GameDifficulty.EASY;
        public int AccessibleWater => Backpack.Count("water")+(CurrentLocation=="home"?HomeStorage.Count("water"):0);
        public bool NeedsWaterWarning => !UnlimitedWater && (Thirst>=60 || AccessibleWater==0);
        public bool NeedsSleepWarning => Sleepiness>=60;
        public bool CanDrink => CanAct && !UnlimitedWater && AccessibleWater>0 && (Thirst>0 || HydrationHours>0);
        public string DrinkWater()
        {
            if(!CanDrink) return "지금 마실 수 있는 생수가 없거나 아직 목마르지 않다.";
            var source=CurrentLocation=="home" && HomeStorage.Count("water")>0?HomeStorage:Backpack;
            source.Remove("water",1); Thirst=0; HydrationHours=0;
            RecordFact("story","생수 1병을 마셔 목마름을 해소했다.");
            return Report("생수 1병(500ml)을 마셨다. 목마름 0 / 100 · 다음 식수까지 6시간.");
        }
        public ItemStack[] CookingIngredients(RecipeDefinition recipe) => UnlimitedWater?Array.FindAll(recipe.ingredients,x=>x.id!="water"):recipe.ingredients;
        public bool CanCook(RecipeDefinition recipe,SurvivalDefinitions data) => CanAct && CurrentLocation=="home" && recipe!=null && HomeStorage.Count("burner")>0 && HomeStorage.Has(CookingIngredients(recipe));
        void TickWater(SurvivalDefinitions data)
        {
            if(IsGameOver || UnlimitedWater) return;
            Thirst=Math.Min(100,Thirst+10);
            HydrationHours++;
            if(HydrationHours<data.TEMP_BALANCE.waterIntervalHours) return;
            HydrationHours=0;
            var source=CurrentLocation=="home" && HomeStorage.Count("water")>0?HomeStorage:Backpack;
            if(source.Remove("water",1)) { Thirst=0; Note("[식수] 생수 1병(500ml)을 마셨다. 목마름 0 / 100."); }
            else { Vitals.Damage(data.TEMP_BALANCE.dehydrationDamage); RecordFact("shortage","마실 물이 부족해 체력을 잃었다."); Note("[식수 부족] 목마름 "+Thirst+" / 100. 마실 물이 없어 체력 -"+data.TEMP_BALANCE.dehydrationDamage+". 외출할 때는 물을 챙겨야 한다."); }
        }
        public string WaterStatus(SurvivalDefinitions data)
        {
            if(UnlimitedWater) return "식수 무제한 · 쉬움 · 가스는 충분하다.";
            if(Backpack.Count("water")+(CurrentLocation=="home"?HomeStorage.Count("water"):0)==0) return "마실 물 없음 · "+(data.TEMP_BALANCE.waterIntervalHours-HydrationHours)+"시간 후 체력 -"+data.TEMP_BALANCE.dehydrationDamage+(CurrentLocation=="home"?"":" · 집의 물은 귀가 후 사용");
            return "물 · 소지 "+Backpack.Count("water")+"병"+(CurrentLocation=="home"?" / 집 "+HomeStorage.Count("water")+"병":" · 집의 물은 귀가 후 사용")+" · "+(data.TEMP_BALANCE.waterIntervalHours-HydrationHours)+"시간 후 1병 필요";
        }
        public void UpgradeWaterSupply(SurvivalDefinitions data)
        {
            HydrationHours=0;
            if(!UnlimitedWater && HomeStorage.Count("water")+Backpack.Count("water")==0)
            {
                HomeStorage.Add("water",Difficulty==GameDifficulty.HARD?3:6); Backpack.Add("water",Difficulty==GameDifficulty.HARD?1:2);
            }
            foreach(var place in data.locations)
            {
                var pool=Locations.Find(x=>x.id==place.id).remaining;
                foreach(var loot in place.loot) if(loot.id=="water" && !pool.stacks.Exists(x=>x.id=="water")) pool.Add("water",loot.amount);
            }
        }
    }
}
