using System;
using System.Collections.Generic;

namespace VanishingDays
{
    [Serializable] public class Inventory
    {
        public List<ItemStack> stacks=new List<ItemStack>();
        public int Count(string id) => stacks.Find(x=>x.id==id)?.amount??0;
        public void Add(string id,int amount) { if(amount<=0) return; var stack=stacks.Find(x=>x.id==id); if(stack==null) stacks.Add(new ItemStack(id,amount)); else stack.amount+=amount; }
        public bool Remove(string id,int amount) { if(amount<=0 || Count(id)<amount) return false; stacks.Find(x=>x.id==id).amount-=amount; return true; }
        public bool Has(ItemStack[] cost) { foreach(var item in cost) if(Count(item.id)<item.amount) return false; return true; }
        public bool Spend(ItemStack[] cost) { if(!Has(cost)) return false; foreach(var item in cost) Remove(item.id,item.amount); return true; }
        public bool Transfer(Inventory target,string id,int count) { if(target==null || target==this || !Remove(id,count)) return false; target.Add(id,count); return true; }
    }
    [Serializable] public class Injury { public string kind="부상"; public float recoveryPenalty; }
    [Serializable] public class PlayerVitals
    {
        public int CurrentHP=500, BaseMaxHP=500, Hunger;
        public List<Injury> Injuries=new List<Injury>();
        public float HungerPenalty;
        public int RecoverableMaxHP(SurvivalBalance tuning)
        {
            double penalty=HungerPenalty; foreach(var injury in Injuries) penalty+=injury.recoveryPenalty;
            penalty=Math.Max(0,Math.Min(tuning.maxRecoveryPenalty,penalty));
            return (int)Math.Round(BaseMaxHP*(1-penalty),MidpointRounding.AwayFromZero);
        }
        public void Heal(int amount,SurvivalBalance tuning) { int cap=RecoverableMaxHP(tuning); if(CurrentHP<=0 || CurrentHP>=cap || amount<=0) return; CurrentHP=Math.Min(cap,CurrentHP+amount); }
        public void Damage(int amount) { if(amount>0) CurrentHP=Math.Max(0,CurrentHP-amount); }
    }
    [Serializable] public class StoryFlags
    {
        public bool OpeningCompleted, HasRadio, HasHeardSPBroadcast, DongrimRescueCompleted, HasMetDongrim, HasMetJiwoo, JoinedLiberationFront, HasMetSeokhyeon;
        public bool CanReceiveSPBroadcast => HasRadio;
        // No automatic trigger: an approved, independent crowd-rescue condition must be supplied later.
        public bool CanTriggerDongrimFirstEncounter(bool approvedCrowdRescueCondition) => HasRadio && approvedCrowdRescueCondition && !DongrimRescueCompleted && !HasMetDongrim;
        public bool CompleteDongrimRescue(bool approvedCrowdRescueCondition) { if(!CanTriggerDongrimFirstEncounter(approvedCrowdRescueCondition)) return false; DongrimRescueCompleted=true; HasMetDongrim=true; return true; }
    }
    [Serializable] public class LocationProgress { public string id; public int searches; public Inventory remaining=new Inventory(); }
    [Serializable] public class Encounter { public string kind, context; }
    [Serializable] public class SurvivalState
    {
        public int schemaVersion=3;
        public string PlayerNickname="", ProtagonistScriptName="안재현";
        public GameDifficulty Difficulty=GameDifficulty.NORMAL;
        public int day=1, hour=8;
        public int InjuryRestProgress;
        public int MedicalLootVersion;
        public string CurrentLocation="home";
        public uint RandomState=1;
        public PlayerVitals Vitals=new PlayerVitals();
        public Inventory Backpack=new Inventory(), HomeStorage=new Inventory();
        public StoryFlags Story=new StoryFlags();
        public List<LocationProgress> Locations=new List<LocationProgress>();
        public List<string> InstalledSecurity=new List<string>(), Equipment=new List<string>(), journal=new List<string>();
        public Encounter PendingEncounter;
        public bool AwayFromHome;
        public string lastReport="송파구 자취방. 남은 식량을 확인하고 오늘을 보낼 방법을 정하자.";
        public bool IsGameOver => Vitals.CurrentHP<=0;
        public DateTime CalendarDate => new DateTime(2031,2,26).AddDays(day-1);
        public bool IsNight(SurvivalBalance balance) => hour<balance.dayStarts || hour>=balance.nightStarts;
        public bool CanAct => !IsGameOver && PendingEncounter==null;
        public bool CanUseRadio => Story.HasRadio && (Backpack.Count("radio")>0 || (CurrentLocation=="home" && HomeStorage.Count("radio")>0));
        public static SurvivalState NewGame(GameDifficulty difficulty,SurvivalDefinitions definitions,uint seed=0)
        {
            if(definitions==null) throw new ArgumentNullException(nameof(definitions));
            var state=new SurvivalState { Difficulty=difficulty, MedicalLootVersion=1, hour=definitions?.TEMP_BALANCE.startHour??8, RandomState=seed==0?(uint)Guid.NewGuid().GetHashCode():seed };
            if(state.RandomState==0) state.RandomState=1;
            state.HomeStorage.Add("spam",difficulty==GameDifficulty.EASY?10:difficulty==GameDifficulty.HARD?3:5);
            state.HomeStorage.Add("tuna",difficulty==GameDifficulty.EASY?10:difficulty==GameDifficulty.HARD?1:5);
            state.HomeStorage.Add("rice",difficulty==GameDifficulty.EASY?10000:difficulty==GameDifficulty.HARD?1000:3000);
            state.HomeStorage.Add("burner",1);
            state.HomeStorage.Add("bandage",difficulty==GameDifficulty.EASY?5:difficulty==GameDifficulty.HARD?1:3);
            if(definitions!=null) foreach(var location in definitions.locations)
            {
                var progress=new LocationProgress { id=location.id };
                if(location.loot!=null) foreach(var stack in location.loot) progress.remaining.Add(stack.id,stack.amount);
                state.Locations.Add(progress);
            }
            state.Note("기록지 #001. 나는 오늘도 살아남아야 한다."); return state;
        }
        public void UpgradeMedicalLoot(SurvivalDefinitions data)
        {
            if(MedicalLootVersion>=1) return;
            foreach(var location in data.locations)
                foreach(var loot in location.loot)
                    if(data.Item(loot.id).injuriesTreated>0)
                    {
                        var pool=Locations.Find(x=>x.id==location.id).remaining;
                        // A present zero-count stack is exhausted and must stay exhausted.
                        if(!pool.stacks.Exists(x=>x.id==loot.id)) pool.Add(loot.id,loot.amount);
                    }
            MedicalLootVersion=1;
        }
        public void Note(string text) { journal.Insert(0,CalendarDate.ToString("yyyy-MM-dd")+" · "+hour.ToString("00")+":00  "+text); lastReport=text; }
        public int Strength(SurvivalDefinitions data) { int value=0; foreach(var id in Equipment) value+=data.Item(id)?.strength??0; return value; }
        public int Defense(SurvivalDefinitions data) { int value=0; foreach(var id in Equipment) value+=data.Item(id)?.defense??0; return value; }
        public int Protection(SurvivalDefinitions data) { int value=0; foreach(var id in InstalledSecurity) value+=data.Security(id)?.protection??0; return value; }
        public int Attention(SurvivalDefinitions data) { int value=0; foreach(var id in InstalledSecurity) value+=data.Security(id)?.attention??0; return value; }
        public double HomeRaidRisk(SurvivalDefinitions data) { var b=data.TEMP_BALANCE; return Math.Max(0,Math.Min(b.maxRaidChance,b.homeRaidChance+Attention(data)*b.attentionRisk-Protection(data)*b.protectionReduction)); }
        double Roll(Func<double> random)
        {
            if(random!=null) return random();
            uint value=RandomState; value^=value<<13; value^=value>>17; value^=value<<5; RandomState=value==0?1:value;
            return RandomState/((double)uint.MaxValue+1);
        }
        public void AdvanceTime(int hours,SurvivalDefinitions data,bool resting=false)
        {
            if(IsGameOver || hours<=0) return;
            var b=data.TEMP_BALANCE;
            if(!resting) InjuryRestProgress=0;
            for(int i=0;i<hours && !IsGameOver;i++)
            {
                hour++; if(hour>=24) { hour=0; day++; }
                Vitals.Hunger=Math.Min(100,Vitals.Hunger+b.hungerPerHour);
                UpdateHungerPenalty(b);
                if(b.clampWhenPenaltyAdded) Vitals.CurrentHP=Math.Min(Vitals.CurrentHP,Vitals.RecoverableMaxHP(b));
                if(Vitals.Hunger>=b.starvingAt) Vitals.Damage(b.starvationDamage);
                if(resting && !IsGameOver && Vitals.Injuries.Count>0)
                {
                    InjuryRestProgress++;
                    if(InjuryRestProgress>=b.injuryRestHours) { Vitals.Injuries.RemoveAt(0); InjuryRestProgress=0; }
                }
                if(Vitals.Injuries.Count==0) InjuryRestProgress=0;
            }
        }
        void UpdateHungerPenalty(SurvivalBalance b) { Vitals.HungerPenalty=Vitals.Hunger>=b.starvingAt?b.starvingCapPenalty:Vitals.Hunger>=b.hungryAt?b.hungryCapPenalty:0; }
        public string Rest(SurvivalDefinitions data,bool untilNextPeriod=false)
        {
            if(!CanAct || CurrentLocation!="home") return "집에서 쉴 수 있다.";
            int hours=data.TEMP_BALANCE.restHours;
            if(untilNextPeriod) { int target=IsNight(data.TEMP_BALANCE)?data.TEMP_BALANCE.dayStarts:data.TEMP_BALANCE.nightStarts; hours=(target-hour+24)%24; if(hours==0) hours=24; }
            int injuries=Vitals.Injuries.Count;
            AdvanceTime(hours,data,true); if(!IsGameOver) Vitals.Heal(data.TEMP_BALANCE.restHealing,data.TEMP_BALANCE);
            return Report(IsGameOver?"더 이상 몸을 일으킬 수 없다.":"집에서 "+hours+"시간 쉬었다. "+(injuries>Vitals.Injuries.Count?"부상 "+(injuries-Vitals.Injuries.Count)+"곳이 나았다. ":"")+(Vitals.Injuries.Count>0?"다음 부상 회복까지 연속 휴식 "+(data.TEMP_BALANCE.injuryRestHours-InjuryRestProgress)+"시간이 더 필요하다.":"남은 식량과 몸 상태를 확인하자."));
        }
        public string Travel(string destination,SurvivalDefinitions data,Func<double> random=null)
        {
            if(!CanAct || destination==CurrentLocation || data.Location(destination)==null) return "지금은 이동할 수 없다.";
            bool returning=destination=="home";
            int cost=returning?data.Location(CurrentLocation).travelHours:data.Location(destination).travelHours;
            if(CurrentLocation=="home") AwayFromHome=true;
            AdvanceTime(cost,data); if(IsGameOver) { AwayFromHome=CurrentLocation!="home"; return Report("돌아갈 힘이 남지 않았다."); }
            CurrentLocation=destination;
            Report(data.Location(destination).name+"에 도착했다. "+(IsNight(data.TEMP_BALANCE)?"밤이다. 만남은 드물지만 발견되면 위험하다.":"낮이다. 다른 생존자와 마주칠 수 있다."));
            CheckEncounter(false,data,random);
            if(returning)
            {
                if(AwayFromHome && Roll(random)<HomeRaidRisk(data)) Note("집을 비운 사이 누군가 들어왔다. "+LoseSupplies(HomeStorage,data.TEMP_BALANCE.homeLoss,data));
                AwayFromHome=false;
            }
            return lastReport;
        }
        public string Search(SurvivalDefinitions data,Func<double> random=null)
        {
            if(!CanAct || CurrentLocation=="home") return "지금은 수색할 수 없다.";
            var location=data.Location(CurrentLocation); var progress=Locations.Find(x=>x.id==CurrentLocation);
            AdvanceTime(location.searchHours,data); if(IsGameOver) return Report("수색을 계속할 힘이 없다.");
            progress.searches++; var found=new List<string>();
            // TEMP_BALANCE: finite pools, at most one unit of each resource per search.
            foreach(var stack in progress.remaining.stacks) if(stack.amount>0) { stack.amount--; Backpack.Add(stack.id,1); found.Add(data.Item(stack.id).name+" "+data.Quantity(stack.id,1)); }
            if(CurrentLocation=="school" && progress.searches>=data.TEMP_BALANCE.radioSearches && !Story.HasRadio) { Backpack.Add("radio",1); Story.HasRadio=true; found.Add("라디오 1개"); }
            Report(found.Count>0?"수색해서 챙긴 물품: "+string.Join(", ",found)+".":"살펴본 곳에는 더 챙길 물자가 없었다.");
            CheckEncounter(true,data,random); return lastReport;
        }
        void CheckEncounter(bool inside,SurvivalDefinitions data,Func<double> random)
        {
            if(IsGameOver) return;
            var b=data.TEMP_BALANCE; bool night=IsNight(b);
            double chance=inside?(night?b.nightInsideChance:b.dayInsideChance):(night?b.nightStreetChance:b.dayStreetChance);
            if(Roll(random)>=chance) return;
            if(night) { DamageAndInjure(Math.Max(1,b.nightDamage-Defense(data)),data); Note("전문 약탈꾼에게 발견됐다. 부상을 입고 물품을 잃었다. "+LoseSupplies(Backpack,b.nightLoss,data)); }
            else { PendingEncounter=new Encounter { kind="hungry",context=inside?"건물 안":"길거리" }; Note(PendingEncounter.context+"에서 굶주린 사람이 물품을 요구한다."); }
        }
        public string ResolveEncounter(bool resist,SurvivalDefinitions data,Func<double> random=null)
        {
            if(IsGameOver || PendingEncounter==null) return "현재 대응할 일이 없다.";
            PendingEncounter=null;
            if(!resist) return Report("요구에 순순히 응했다. "+LoseSupplies(Backpack,data.TEMP_BALANCE.dayLoss,data));
            double escape=Math.Min(.95,data.TEMP_BALANCE.escapeChance+Strength(data)*data.TEMP_BALANCE.strengthEscapeBonus);
            if(Roll(random)<escape) return Report("상대의 손을 뿌리치고 벗어났다.");
            DamageAndInjure(Math.Max(1,data.TEMP_BALANCE.dayDamage-Defense(data)),data);
            return Report("빠져나오려다 부상을 입었다. "+LoseSupplies(Backpack,data.TEMP_BALANCE.dayLoss,data));
        }
        void DamageAndInjure(int damage,SurvivalDefinitions data) { InjuryRestProgress=0; Vitals.Damage(damage); Vitals.Injuries.Add(new Injury { recoveryPenalty=data.TEMP_BALANCE.injuryCapPenalty }); if(data.TEMP_BALANCE.clampWhenPenaltyAdded) Vitals.CurrentHP=Math.Min(Vitals.CurrentHP,Vitals.RecoverableMaxHP(data.TEMP_BALANCE)); }
        string LoseSupplies(Inventory source,int units,SurvivalDefinitions data)
        {
            var lost=new List<string>(); int riceUnit=data.TEMP_BALANCE.riceLossUnit;
            foreach(var stack in source.stacks)
            {
                if(units<=0) break;
                if(stack.amount<=0 || data.Item(stack.id).protectedItem) continue;
                int count=Math.Min(stack.amount,stack.id=="rice"?units*riceUnit:units);
                source.Remove(stack.id,count); lost.Add(data.Item(stack.id).name+" "+data.Quantity(stack.id,count));
                units-=stack.id=="rice"?(count+riceUnit-1)/riceUnit:count;
            }
            return lost.Count==0?"빼앗길 물품은 없었다.":"잃은 물품: "+string.Join(", ",lost)+".";
        }
        public string Cook(string recipeId,SurvivalDefinitions data)
        {
            var recipe=data.Recipe(recipeId);
            if(!CanAct || CurrentLocation!="home" || recipe==null || HomeStorage.Count("burner")==0 || !HomeStorage.Has(recipe.ingredients)) return "집의 재료와 조리 도구를 확인하자.";
            HomeStorage.Spend(recipe.ingredients); AdvanceTime(recipe.hours,data);
            if(IsGameOver) return Report("조리를 마칠 힘이 남지 않았다.");
            HomeStorage.Add(recipe.result,1); return Report(recipe.name+"를 마쳤다. 만들어 둔 음식을 먹거나 보관할 수 있다.");
        }
        public string Eat(string itemId,SurvivalDefinitions data)
        {
            var item=data.Item(itemId); var inventory=Backpack.Count(itemId)>0?Backpack:CurrentLocation=="home"?HomeStorage:null;
            if(!CanAct || item==null || !item.consumable || inventory==null || inventory.Count(itemId)==0) return "지금 먹을 수 있는 음식이 없다.";
            inventory.Remove(itemId,1); Vitals.Hunger=Math.Max(0,Vitals.Hunger-item.hungerRelief); UpdateHungerPenalty(data.TEMP_BALANCE);
            Vitals.Heal(item.healing,data.TEMP_BALANCE); AdvanceTime(data.TEMP_BALANCE.eatHours,data);
            return Report(item.name+"을 먹었다.");
        }
        public string TreatInjury(string itemId,SurvivalDefinitions data)
        {
            var item=data.Item(itemId);
            var source=Backpack.Count(itemId)>0?Backpack:CurrentLocation=="home"?HomeStorage:null;
            if(!CanAct || item==null || item.injuriesTreated<=0 || Vitals.Injuries.Count==0 || source==null || source.Count(itemId)==0) return "부상과 사용할 수 있는 약품을 확인하자.";
            int treated=Math.Min(item.injuriesTreated,Vitals.Injuries.Count);
            source.Remove(itemId,1); Vitals.Injuries.RemoveRange(0,treated); InjuryRestProgress=0;
            AdvanceTime(data.TEMP_BALANCE.treatmentHours,data);
            return Report(item.name+" 1개로 부상 "+treated+"곳을 치료했다. 부상으로 낮아진 회복 상한이 복구됐다.");
        }
        public string Transfer(string itemId,bool toBackpack,SurvivalDefinitions data)
        {
            if(!CanAct || CurrentLocation!="home" || data.Item(itemId)==null || itemId=="burner") return "집에서 물품을 정리할 수 있다.";
            var source=toBackpack?HomeStorage:Backpack; var target=toBackpack?Backpack:HomeStorage; int count=source.Count(itemId);
            if(count==0 || !source.Transfer(target,itemId,count)) return "옮길 물품이 없다.";
            return Report(data.Item(itemId).name+" "+data.Quantity(itemId,count)+(toBackpack?"을 몸에 챙겼다.":"을 집에 보관했다."));
        }
        public string InstallSecurity(string id,SurvivalDefinitions data)
        {
            var device=data.Security(id);
            if(!CanAct || CurrentLocation!="home" || device==null || InstalledSecurity.Contains(id) || !HomeStorage.Has(device.ingredients)) return "설치 조건을 확인하자.";
            HomeStorage.Spend(device.ingredients); AdvanceTime(device.hours,data); if(!IsGameOver) InstalledSecurity.Add(id);
            return Report(IsGameOver?"작업을 끝낼 힘이 없다.":device.name+"을 마쳤다. 침입 저항과 바깥에서 보이는 주목도를 함께 살펴야 한다.");
        }
        public string Equip(string id,SurvivalDefinitions data)
        {
            var item=data.Item(id);
            if(!CanAct || item==null || (item.strength==0 && item.defense==0)) return "착용할 장비가 아니다.";
            if(Equipment.Contains(id)) { Equipment.Remove(id); (CurrentLocation=="home"?HomeStorage:Backpack).Add(id,1); return Report(item.name+"을 벗어 보관했다."); }
            var source=Backpack.Count(id)>0?Backpack:CurrentLocation=="home"?HomeStorage:null;
            if(source==null || !source.Remove(id,1)) return "소지한 장비가 없다.";
            Equipment.Add(id); return Report(item.name+"을 착용했다.");
        }
        public string ListenRadio(SurvivalDefinitions data)
        {
            if(!CanAct || !CanUseRadio) return "지금 사용할 수 있는 라디오가 없다.";
            if(string.IsNullOrWhiteSpace(data.approvedSPBroadcast)) return Report("라디오를 켰다. 지금은 수신되는 방송이 없다.");
            Story.HasHeardSPBroadcast=true; return Report(data.approvedSPBroadcast);
        }
        string Report(string text) { Note(text); return text; }
    }
}
