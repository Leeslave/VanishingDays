using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
#endif

namespace VanishingDays.Editor
{
    public static class SurvivalChecks
    {
#if UNITY_EDITOR
        [MenuItem("Vanishing Days/Validate survival v0.3")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load();
            int checks=ValidateData(data);
            string path=System.IO.Path.GetFullPath("Builds/Validation/survival-save-test.json");
            var saves=new SessionSaveStore(path,data);
            var state=SurvivalState.NewGame(GameDifficulty.HARD,data,123); state.PlayerNickname="저장검증"; state.Story.OpeningCompleted=true;
            state.UnlockedLocations.Add("school");
            state.Travel("school",data,()=>1); state.Search(data,()=>0);
            saves.Save(state);
            if(!saves.TryLoad(out var restored,out _) || restored.PlayerNickname!=state.PlayerNickname || restored.CurrentLocation!="school" || restored.PendingEncounter==null || restored.RandomState!=state.RandomState || restored.Difficulty!=GameDifficulty.HARD) throw new Exception("Native session/pending-encounter save round trip failed.");
            restored.ResolveEncounter(false,data); saves.Save(restored);
            File.WriteAllText(path,"corrupted");
            if(!saves.TryLoad(out var backup,out _) || backup.PendingEncounter==null) throw new Exception("Save backup recovery failed.");
            var legacy=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); legacy.MedicalLootVersion=0;
            foreach(var progress in legacy.Locations) progress.remaining.stacks.RemoveAll(x=>data.Item(x.id).injuriesTreated>0);
            legacy.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); legacy.Rest(data);
            File.WriteAllText(path,JsonUtility.ToJson(legacy));
            if(!saves.TryLoad(out var upgraded,out _) || upgraded.MedicalLootVersion!=1 || upgraded.InjuryRestProgress!=2 || upgraded.Locations.Find(x=>x.id=="store").remaining.Count("bandage")!=1 || upgraded.HomeStorage.Count("bandage")!=3) throw new Exception("Legacy medicine migration/rest progress failed.");
            upgraded.Locations.Find(x=>x.id=="store").remaining.Remove("bandage",1); saves.Save(upgraded);
            if(!saves.TryLoad(out var exhausted,out _) || exhausted.Locations.Find(x=>x.id=="store").remaining.Count("bandage")!=0) throw new Exception("Medicine replenished on reload.");
            foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
            Debug.Log("VD_SURVIVAL_CHECKS_OK: "+checks+" model assertions; native JSON save, pending choice, backup recovery, medicine migration/rest progress passed.");
        }
#endif
        public static int ValidateData(SurvivalDefinitions data)
        {
            data.Validate(); int checks=0;
            Action<bool,string> require=(value,reason)=>{checks++; if(!value) throw new Exception(reason);};
            // Existing action fixtures exercise already discovered routes. New-game unlocks are checked separately.
            Func<SurvivalState> fresh=()=>{ var value=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); value.UnlockedLocations.AddRange(new[]{"empty_house","school"}); return value; };
            int[] spam={10,5,3}, tuna={10,5,1}, rice={10000,3000,1000};
            for(int i=0;i<3;i++)
            {
                var initial=SurvivalState.NewGame((GameDifficulty)i,data,1);
                require(initial.HomeStorage.Count("spam")==spam[i] && initial.HomeStorage.Count("tuna")==tuna[i] && initial.HomeStorage.Count("rice")==rice[i],"Difficulty food allocation wrong.");
                require(initial.HomeStorage.Count("burner")==1 && initial.Backpack.Count("radio")+initial.HomeStorage.Count("radio")==0,"Initial burner/radio wrong.");
                require(initial.HomeStorage.Count("bandage")==new[]{5,3,1}[i] && initial.Backpack.Count("bandage")==0,"Difficulty bandages wrong.");
                require(initial.Vitals.CurrentHP==500 && initial.Vitals.BaseMaxHP==500 && initial.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==500,"Initial HP wrong.");
                require(initial.CurrentLocation=="home" && initial.CalendarDate==new DateTime(2031,2,26) && initial.Locations.FindAll(x=>initial.IsLocationUnlocked(x.id,data)).Count==2,"Initial home/date/locations wrong.");
            }
            var s=fresh(); s.Vitals.CurrentHP=200; s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f });
            require(s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==450 && s.Vitals.BaseMaxHP==500,"Recovery cap -10% example failed.");
            s.Vitals.Heal(9999,data.TEMP_BALANCE); require(s.Vitals.CurrentHP==450,"Healing exceeded cap.");
            s.Vitals.CurrentHP=500; s.Vitals.Heal(1,data.TEMP_BALANCE); require(s.Vitals.CurrentHP==450,"Healing must enforce the lowered maximum HP.");
            s.Vitals.Damage(500); require(s.IsGameOver,"HP zero did not cause game over.");
            s.Vitals.Heal(500,data.TEMP_BALANCE); require(s.IsGameOver,"Healing resurrected game-over session.");
            int day=s.day; s.Rest(data); s.Travel("school",data); s.Cook("rice",data); require(s.day==day && s.CurrentLocation=="home" && s.HomeStorage.Count("rice")==3000,"Dead player could act.");
            s=fresh(); s.Cook("spam",data); require(s.HomeStorage.Count("spam_meal")==1 && s.HomeStorage.Count("spam")==4 && s.HomeStorage.Count("rice")==2900,"Cooking failed to consume ingredients and store output.");
            s.Vitals.Hunger=80; s.Eat("spam_meal",data); require(s.HomeStorage.Count("spam_meal")==0 && s.Vitals.Hunger<80,"Eating failed.");
            s.Transfer("tuna",true,data); require(s.Backpack.Count("tuna")==5 && s.HomeStorage.Count("tuna")==0,"Pack/home separation failed.");
            s.Transfer("tuna",false,data); require(s.HomeStorage.Count("tuna")==5 && s.Backpack.Count("tuna")==0,"Deposit failed.");
            s.HomeStorage.Remove("rice",2900); int before=s.hour; s.Cook("rice",data); require(s.hour==before,"Invalid cooking spent time.");
            s=fresh(); s.hour=23; s.AdvanceTime(2,data); require(s.day==2 && s.hour==1 && s.CalendarDate==new DateTime(2031,2,27),"Calendar rollover failed.");
            s=fresh(); s.hour=8; s.Rest(data,true); require(s.IsNight(data.TEMP_BALANCE),"Waiting for night failed."); s.Rest(data,true); require(!s.IsNight(data.TEMP_BALANCE) && s.day==2,"Waiting for dawn failed.");
            s=fresh(); s.Vitals.Hunger=100; s.Vitals.CurrentHP=1; s.Rest(data); require(s.IsGameOver,"Starvation cannot cause game over.");
            s=fresh(); s.Vitals.Hunger=100; s.Vitals.CurrentHP=10; s.Eat("spam",data); require(!s.IsGameOver,"Starving player cannot use food before time ticks.");
            s=fresh(); s.Vitals.Hunger=100; s.Vitals.CurrentHP=1; s.Travel("store",data,()=>1); require(s.IsGameOver && !s.AwayFromHome,"Fatal departure produced invalid away state.");
            s=fresh(); s.Travel("store",data,()=>1); s.Search(data,()=>1); s.Search(data,()=>1); require(s.Backpack.Count("tuna")==1 && s.Backpack.Count("ramen")==1,"Finite store pool duplicated loot.");
            s=fresh(); s.Travel("empty_house",data,()=>1); s.Search(data,()=>1); require(s.Backpack.Count("wood")==1 && s.Backpack.Count("tools")==1,"Empty house resource pool wrong.");
            s=fresh(); s.Travel("school",data,()=>1); for(int i=0;i<data.TEMP_BALANCE.radioSearches;i++) s.Search(data,()=>1);
            require(s.Story.HasRadio && s.Backpack.Count("radio")==1,"School radio not guaranteed.");
            require(!s.Story.HasMetDongrim && !s.Story.HasHeardSPBroadcast,"Radio auto-triggered story.");
            s.Search(data,()=>1); require(s.Backpack.Count("radio")==1,"Radio awarded twice.");
            require(s.Story.CanReceiveSPBroadcast && !s.Story.CanTriggerDongrimFirstEncounter(false),"Radio story gates wrong.");
            s.ListenRadio(data); require(s.Story.HasHeardSPBroadcast,"Carried radio could not receive outside home.");
            s.Travel("home",data,()=>1); s.ListenRadio(data); require(s.Story.HasHeardSPBroadcast && s.Story.SupplyRumorUnlocked,"Approved broadcast did not set clue flag.");
            require(s.Story.CompleteDongrimRescue(true) && !s.Story.CompleteDongrimRescue(true),"Rescue completion not guarded/idempotent.");
            s=fresh(); require(!s.Story.CanReceiveSPBroadcast && !s.Story.CompleteDongrimRescue(true) && !s.Story.HasMetDongrim,"Radio prerequisite bypassed.");
            s=fresh(); s.Story.HasRadio=true; s.HomeStorage.Add("radio",1); s.Travel("store",data,()=>1); require(!s.CanUseRadio && s.Story.HasRadio,"Radio left at home could be used outside.");
            s=fresh(); s.Story.HasRadio=true; s.Backpack.Add("radio",1); s.hour=20; s.Travel("school",data,()=>0); require(s.Backpack.Count("radio")==1 && s.Story.HasRadio,"Mandatory radio could be permanently stolen.");
            s=fresh(); s.Backpack.Add("tuna",3); s.Travel("store",data,()=>0); require(s.PendingEncounter!=null,"Day street encounter missing.");
            before=s.hour; s.Search(data,()=>1); require(s.hour==before,"Unresolved encounter could be skipped.");
            s.ResolveEncounter(false,data); require(s.Vitals.CurrentHP==500 && s.Backpack.Count("tuna")==2 && s.PendingEncounter==null,"Surrender did not avoid injury.");
            s=fresh(); s.Travel("store",data,()=>0); s.ResolveEncounter(true,data,()=>0); require(s.Vitals.Injuries.Count==0,"Successful escape injured player.");
            s=fresh(); s.Travel("store",data,()=>0); s.ResolveEncounter(true,data,()=>1); require(s.Vitals.CurrentHP<500 && s.Vitals.Injuries.Count==1,"Failed escape missing damage/injury.");
            s=fresh(); s.hour=20; s.Backpack.Add("ramen",7); s.Travel("school",data,()=>0); require(s.Vitals.Injuries.Count==1 && s.Vitals.CurrentHP==500-data.TEMP_BALANCE.nightDamage && s.Backpack.Count("ramen")==3,"Night damage/loss wrong.");
            require(s.HomeStorage.Count("spam")==5,"Street robbery affected home supplies.");
            s=fresh(); s.CurrentLocation="store"; s.AwayFromHome=true; s.Search(data,()=>0); require(s.PendingEncounter!=null && s.PendingEncounter.context=="건물 안","Day indoor encounter missing.");
            s=fresh(); s.CurrentLocation="school"; s.AwayFromHome=true; s.hour=19; s.Search(data,()=>0); require(s.PendingEncounter==null && s.Vitals.Injuries.Count==1,"Night indoor event wrong.");
            s=fresh(); s.Travel("store",data,()=>1); s.Backpack.Add("ramen",5); s.Travel("home",data,()=>0); require(s.HomeStorage.Count("spam")==2 && s.Backpack.Count("ramen")==5 && !s.AwayFromHome,"Home raid affected wrong inventory or did not occur.");
            s=fresh(); double baseRisk=s.HomeRaidRisk(data); s.InstalledSecurity.Add("analog"); double analogRisk=s.HomeRaidRisk(data); s.InstalledSecurity.Clear(); s.InstalledSecurity.Add("digital"); require(analogRisk<baseRisk && s.HomeRaidRisk(data)>baseRisk,"Security attention tradeoff absent.");
            s=fresh(); s.HomeStorage.Add("wood",8); s.HomeStorage.Add("tools",5); s.InstallSecurity("analog",data); before=s.hour; s.InstallSecurity("analog",data); require(s.InstalledSecurity.Count==1 && s.hour==before,"Security duplication spent resources/time.");
            s=fresh(); s.Backpack.Add("clothes",1); s.Backpack.Add("weapon",1); s.Equip("clothes",data); s.Equip("weapon",data); require(s.Strength(data)>0 && s.Defense(data)>0 && s.Backpack.Count("weapon")==0,"Equipment did not modify stats/conserve item."); s.Equip("weapon",data); require(s.Strength(data)==0 && s.HomeStorage.Count("weapon")==1,"Unequip did not remove modifier/return item.");
            s=fresh(); var twin=fresh(); s.Travel("school",data); twin.Travel("school",data); require(s.RandomState==twin.RandomState && (s.PendingEncounter!=null)==(twin.PendingEncounter!=null),"Saved RNG is not deterministic.");
            s=fresh(); s.HomeStorage.Add("water",100); int hungerRate=data.TEMP_BALANCE.hungerPerHour; data.TEMP_BALANCE.hungerPerHour=0; s.AdvanceTime(24*10,data); data.TEMP_BALANCE.hungerPerHour=hungerRate; require(s.day==11 && !s.IsGameOver,"Arbitrary seven-day win/end remains.");
            foreach(string id in new[]{"bandage","pills","antiseptic"})
            {
                s=fresh(); s.HomeStorage.Add(id,1); int stock=s.HomeStorage.Count(id); before=s.hour;
                s.TreatInjury(id,data); require(s.HomeStorage.Count(id)==stock && s.hour==before,"Medicine wasted without injury.");
                s.Vitals.CurrentHP=200; s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f });
                s.TreatInjury(id,data); require(s.HomeStorage.Count(id)==stock-1 && s.Vitals.Injuries.Count==1 && s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==450 && s.Vitals.CurrentHP==200+data.Item(id).healing,"Medicine treatment/cap/conservation wrong: "+id);
                s.Travel("store",data,()=>1); before=s.hour; stock=s.HomeStorage.Count(id); s.TreatInjury(id,data);
                require(s.hour==before && s.HomeStorage.Count(id)==stock && s.Vitals.Injuries.Count==1,"Medicine at home usable outside.");
                s.Backpack.Add(id,1); s.TreatInjury(id,data); require(s.Backpack.Count(id)==0 && s.Vitals.Injuries.Count==0,"Carried medicine unusable outside.");
                s=fresh(); s.Travel("store",data,()=>0); s.Backpack.Add(id,1); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.TreatInjury(id,data);
                require(s.Backpack.Count(id)==1 && s.Vitals.Injuries.Count==1,"Unresolved encounter allowed treatment.");
                s=fresh(); s.Vitals.CurrentHP=0; s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.Backpack.Add(id,1); s.TreatInjury(id,data);
                require(s.IsGameOver && s.Backpack.Count(id)==1 && s.Vitals.Injuries.Count==1,"Dead player used medicine.");
                bool found=false; foreach(var place in data.locations) foreach(var loot in place.loot) if(loot.id==id && loot.amount>0) found=true;
                require(found,"Medicine missing from loot: "+id);
            }
            s=fresh(); s.Travel("store",data,()=>1); s.Search(data,()=>1); require(s.Backpack.Count("bandage")==1 && s.Backpack.Count("pills")==1,"Store did not yield medicines.");
            s=fresh(); s.Travel("empty_house",data,()=>1); s.Search(data,()=>1); require(s.Backpack.Count("antiseptic")==1,"House did not yield disinfectant.");
            s=fresh(); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f });
            s.Rest(data); require(s.Vitals.Injuries.Count==2 && s.InjuryRestProgress==data.TEMP_BALANCE.restHours,"Short rest healed injury too early.");
            s.Rest(data); s.Rest(data); require(s.Vitals.Injuries.Count==1 && s.InjuryRestProgress==0 && s.HomeStorage.Count("bandage")==3,"Sufficient rest did not heal exactly one injury without medicine.");
            s.Rest(data); s.AdvanceTime(1,data); require(s.InjuryRestProgress==0,"Activity did not interrupt continuous rest.");
            s=fresh(); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.Vitals.CurrentHP=200; s.Eat("spam",data); require(s.Vitals.Injuries.Count==1,"Food removed injury.");
            s=fresh(); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.Rest(data,true); require(s.Vitals.Injuries.Count==0 && s.InjuryRestProgress==0,"Long rest did not cure injury.");
            s=fresh(); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.InjuryRestProgress=5; s.Vitals.Hunger=100; s.Vitals.CurrentHP=1; s.Rest(data); require(s.IsGameOver && s.Vitals.Injuries.Count==1,"Fatal rest cured injury after death.");
            s=fresh(); s.MedicalLootVersion=0; foreach(var progress in s.Locations) progress.remaining.stacks.RemoveAll(x=>data.Item(x.id).injuriesTreated>0);
            s.UpgradeMedicalLoot(data); require(s.Locations.Find(x=>x.id=="store").remaining.Count("bandage")==1 && s.HomeStorage.Count("bandage")==3,"Legacy loot migration changed starting supplies or missed medicine.");
            s.Locations.Find(x=>x.id=="store").remaining.Remove("bandage",1); s.UpgradeMedicalLoot(data); require(s.Locations.Find(x=>x.id=="store").remaining.Count("bandage")==0,"Migration replenished exhausted medicine.");
            s=fresh(); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); s.Vitals.Hunger=70; s.Vitals.HungerPenalty=data.TEMP_BALANCE.hungryCapPenalty;
            s.TreatInjury("bandage",data); require(s.Vitals.Injuries.Count==0 && s.Vitals.RecoverableMaxHP(data.TEMP_BALANCE)==450,"Medicine removed hunger penalty.");
            s=fresh(); s.HomeStorage.Remove("bandage",3); s.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); before=s.hour; s.TreatInjury("bandage",data); require(s.Vitals.Injuries.Count==1 && s.hour==before,"Treatment without medicine succeeded.");
            s.TreatInjury("spam",data); require(s.HomeStorage.Count("spam")==5 && s.Vitals.Injuries.Count==1,"Food accepted as medicine.");
            return checks;
        }
    }
}
