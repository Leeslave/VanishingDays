using System;
using UnityEngine;

namespace VanishingDays
{
    public enum GameDifficulty { EASY, NORMAL, HARD }
    [Serializable] public class ItemDefinition { public string id, name, unit; public bool consumable, protectedItem; public int hungerRelief, healing, strength, defense, injuriesTreated; }
    [Serializable] public class ItemStack
    {
        public string id; public int amount;
        public ItemStack() { }
        public ItemStack(string itemId,int count) { id=itemId; amount=count; }
    }
    [Serializable] public class LocationDefinition { public string id, name, description, mapLabel, requiresLocation; public int travelHours, searchHours, requiresSearches, requiresDongrimMeetings; public bool requiresSupplyRumor, storyOnly; public ItemStack[] loot; }
    [Serializable] public class DongrimFollowupDefinition { public string secondMeeting, thirdMeeting, patrolMeeting, secondCompleted, thirdCompleted, patrolCompleted; }
    [Serializable] public class RadioBroadcastDefinition { public string id, title, body; }
    [Serializable] public class CrowdBalance { public string status; public int dropBagLoss, keepBagLoss, dropBagDamage, keepBagDamage; }
    [Serializable] public class RadioStoryDefinition
    {
        public RadioBroadcastDefinition firstBroadcast, generalBroadcast;
        public string clueCard, approach, observe, night, panic, rescue, disappearance, introduction, completed;
        public CrowdBalance TEMP_BALANCE;
    }
    [Serializable] public class RecipeDefinition { public string id, name, result; public int hours; public ItemStack[] ingredients; }
    [Serializable] public class SecurityDefinition { public string id, name, requiresSecurity; public int protection, attention, hours; public ItemStack[] ingredients; }
    [Serializable] public class SurvivalBalance
    {
        public string status;
        public int startHour, dayStarts, nightStarts, restHours, hungerPerHour, hungryAt, starvingAt, starvationDamage, restHealing, eatHours, radioSearches;
        public float hungryCapPenalty, starvingCapPenalty, injuryCapPenalty, maxRecoveryPenalty;
        public int dayDamage, nightDamage, dayLoss, nightLoss, homeLoss, riceLossUnit, injuryRestHours, treatmentHours;
        public int waterIntervalHours, dehydrationDamage;
        public float dayStreetChance, nightStreetChance, dayInsideChance, nightInsideChance, escapeChance, strengthEscapeBonus, homeRaidChance, attentionRisk, protectionReduction, maxRaidChance;
    }
    [Serializable] public class SurvivalDefinitions
    {
        public int version;
        public SurvivalBalance TEMP_BALANCE;
        public ItemDefinition[] items;
        public LocationDefinition[] locations;
        public RecipeDefinition[] recipes;
        public SecurityDefinition[] security;
        public RadioStoryDefinition radioStory;
        public DongrimFollowupDefinition dongrimFollowup;
        [NonSerialized] public ErasureNewsCatalog erasureNews;
        public ItemDefinition Item(string id) => Array.Find(items,x=>x.id==id);
        public LocationDefinition Location(string id) => Array.Find(locations,x=>x.id==id);
        public RecipeDefinition Recipe(string id) => Array.Find(recipes,x=>x.id==id);
        public SecurityDefinition Security(string id) => Array.Find(security,x=>x.id==id);
        public static SurvivalDefinitions Load()
        {
            var asset=Resources.Load<TextAsset>("Survival/SurvivalDefinitions");
            if(asset==null) throw new InvalidOperationException("Survival definitions missing.");
            var data=JsonUtility.FromJson<SurvivalDefinitions>(asset.text); data.erasureNews=ErasureNewsCatalog.Load(); data.Validate(); return data;
        }
        public void Validate()
        {
            if(version!=5 || TEMP_BALANCE==null || locations==null || locations.Length!=8 || items==null || recipes==null || security==null) throw new InvalidOperationException("Invalid v0.5 definitions.");
            if(erasureNews==null) throw new InvalidOperationException("Missing erasure catalog.");
            erasureNews.Validate();
            foreach(var id in new[]{"home","store","empty_house","school","supply_site","pharmacy","hardware","shelter"}) if(Location(id)==null) throw new InvalidOperationException("Missing location: "+id);
            if(dongrimFollowup==null) throw new InvalidOperationException("Missing Dongrim follow-up.");
            foreach(var text in new[]{dongrimFollowup.secondMeeting,dongrimFollowup.thirdMeeting,dongrimFollowup.patrolMeeting,dongrimFollowup.secondCompleted,dongrimFollowup.thirdCompleted,dongrimFollowup.patrolCompleted}) if(string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Missing follow-up script.");
            foreach(var place in locations) if((!string.IsNullOrEmpty(place.requiresLocation) && (Location(place.requiresLocation)==null || place.requiresLocation==place.id || place.requiresSearches<1)) || place.requiresDongrimMeetings<0) throw new InvalidOperationException("Invalid location unlock.");
            foreach(var device in security) if(!string.IsNullOrEmpty(device.requiresSecurity) && (Security(device.requiresSecurity)==null || device.requiresSecurity==device.id)) throw new InvalidOperationException("Invalid security prerequisite.");
            if(!Location("supply_site").requiresSupplyRumor || !Location("supply_site").storyOnly || Location("supply_site").loot.Length!=0) throw new InvalidOperationException("Invalid story location.");
            if(radioStory?.firstBroadcast==null || radioStory.generalBroadcast==null || radioStory.TEMP_BALANCE==null || string.IsNullOrWhiteSpace(radioStory.firstBroadcast.body) || string.IsNullOrWhiteSpace(radioStory.generalBroadcast.body) || string.IsNullOrWhiteSpace(radioStory.clueCard) || radioStory.firstBroadcast.id==radioStory.generalBroadcast.id) throw new InvalidOperationException("Missing radio story data.");
            foreach(var text in new[]{radioStory.firstBroadcast.id,radioStory.firstBroadcast.title,radioStory.generalBroadcast.id,radioStory.generalBroadcast.title,radioStory.approach,radioStory.observe,radioStory.night,radioStory.panic,radioStory.rescue,radioStory.disappearance,radioStory.introduction,radioStory.completed}) if(string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Incomplete radio/crowd script.");
            var crowd=radioStory.TEMP_BALANCE;
            if(crowd.dropBagLoss<0 || crowd.keepBagLoss<0 || crowd.dropBagDamage<0 || crowd.keepBagDamage<0 || crowd.dropBagDamage>=crowd.keepBagDamage || crowd.dropBagLoss<=crowd.keepBagLoss) throw new InvalidOperationException("Invalid crowd choice balance.");
            if(TEMP_BALANCE.injuryRestHours<1 || TEMP_BALANCE.treatmentHours<0) throw new InvalidOperationException("Invalid recovery balance.");
            if(TEMP_BALANCE.waterIntervalHours<1 || TEMP_BALANCE.dehydrationDamage<0 || Item("water")==null) throw new InvalidOperationException("Invalid water balance.");
            foreach(var id in new[]{"bandage","pills","antiseptic"}) if(Item(id)==null || Item(id).injuriesTreated<1 || Item(id).consumable) throw new InvalidOperationException("Invalid medicine: "+id);
            foreach(var id in new[]{"bandage","pills","antiseptic"}) if(Item(id).healing<1) throw new InvalidOperationException("Missing treatment healing: "+id);
            if(TEMP_BALANCE.radioSearches<1 || TEMP_BALANCE.dayStreetChance<=TEMP_BALANCE.nightStreetChance || TEMP_BALANCE.dayInsideChance<=TEMP_BALANCE.nightInsideChance || TEMP_BALANCE.nightDamage<=TEMP_BALANCE.dayDamage || TEMP_BALANCE.riceLossUnit<1) throw new InvalidOperationException("Invalid day/night/radio balance.");
            foreach(var place in locations) if(place.loot!=null) foreach(var item in place.loot) if(Item(item.id)==null || item.amount<0) throw new InvalidOperationException("Invalid loot.");
            foreach(var recipe in recipes) { if(Item(recipe.result)==null) throw new InvalidOperationException("Invalid recipe result."); CheckIngredients(recipe.ingredients); }
            foreach(var device in security) CheckIngredients(device.ingredients);
        }
        void CheckIngredients(ItemStack[] ingredients) { if(ingredients==null || ingredients.Length==0) throw new InvalidOperationException("Missing cost."); foreach(var item in ingredients) if(Item(item.id)==null || item.amount<=0) throw new InvalidOperationException("Invalid cost."); }
        public string Quantity(string id,int count)
        {
            var item=Item(id); if(item==null) return "?";
            return id=="rice"?(count/1000f).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"kg":count+item.unit;
        }
    }
}
