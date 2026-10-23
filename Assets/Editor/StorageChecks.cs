using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class StorageChecks
    {
        [MenuItem("Vanishing Days/Validate storage transfers")]
        public static void Validate()
        {
            var data=SurvivalDefinitions.Load(); var s=SurvivalState.NewGame(GameDifficulty.NORMAL,data,123); int checks=0;
            Action<bool,string> check=(ok,why)=>{checks++; if(!ok) throw new Exception(why);};
            s.Backpack.Add("wood",10); s.Backpack.Add("rice",1234);
            int hour=s.hour, day=s.day; uint rng=s.RandomState;
            s.Transfer("wood",false,3,data);
            check(s.Backpack.Count("wood")==7 && s.HomeStorage.Count("wood")==3,"Partial deposit was not exact.");
            s.Transfer("wood",true,2,data);
            check(s.Backpack.Count("wood")==9 && s.HomeStorage.Count("wood")==1,"Partial withdrawal was not exact.");
            foreach(int quantity in new[]{0,-1,int.MinValue,10,int.MaxValue})
            {
                s.Transfer("wood",false,quantity,data);
                check(s.Backpack.Count("wood")==9 && s.HomeStorage.Count("wood")==1,"Invalid quantity mutated inventory.");
            }
            s.Transfer("rice",false,234,data);
            check(s.Backpack.Count("rice")==1000 && s.HomeStorage.Count("rice")==3234,"Rice lost gram precision.");
            s.Transfer("burner",true,1,data); check(s.HomeStorage.Count("burner")==1 && s.Backpack.Count("burner")==0,"Fixed home equipment moved.");
            s.Backpack.Add("weapon",1); s.Equip("weapon",data);
            s.Backpack.Add("radio",1); s.Story.HasRadio=true;
            var totals=data.items.ToDictionary(x=>x.id,x=>s.Backpack.Count(x.id)+s.HomeStorage.Count(x.id));
            s.StoreAll(data);
            check(s.Backpack.stacks.All(x=>x.amount==0),"Store-all missed an item.");
            foreach(var item in data.items) check(s.HomeStorage.Count(item.id)==totals[item.id],"Store-all did not conserve "+item.id);
            check(s.Equipment.Contains("weapon") && s.Strength(data)>0,"Store-all removed worn equipment.");
            check(s.CanUseRadio,"Home radio became inaccessible after storage.");
            check(s.hour==hour && s.day==day && s.RandomState==rng,"Storage changed time/RNG.");
            s.StoreAll(data); check(s.HomeStorage.Count("wood")==10,"Repeated store-all duplicated supplies.");
            s.Transfer("rice",true,data); check(s.Backpack.Count("rice")==4234,"Legacy all-quantity API changed.");
            s.CurrentLocation="store"; s.StoreAll(data); s.Transfer("rice",false,1,data);
            check(s.Backpack.Count("rice")==4234,"Storage allowed outside home.");
            s.CurrentLocation="home"; s.Vitals.CurrentHP=0; s.StoreAll(data); s.Transfer("rice",false,1,data);
            check(s.Backpack.Count("rice")==4234,"Storage allowed after death.");
            Debug.Log("VD_STORAGE_CHECKS_OK: "+checks+" assertions; exact quantities, invalid input, gram precision, conservation, equipment, radio and action gates.");
        }
    }
}
