#if UNITY_EDITOR || VD_UI_VERIFICATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        IEnumerator SmokeBalance()
        {
            string dir=Path.GetFullPath("Builds/BalanceVerification");Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true;state.PlayerNickname="밸런스 검증";state.NeighborIntroductionSeen=true;neighborInvitationPending=false;
            state.InstalledFacilities.AddRange(new[]{"water_collector","water_filter"});page="water";Render();yield return null;
            ClickGameButton("물 확보");ClickGameButton("2시간 자동 집수 시작");
            if(state.hour!=8 || state.CollectorCycleHours!=2)throw new Exception("Collector UI did not start without time.");
            state.AdvanceTime(2,definitions);Save();Render();yield return null;
            if(!root.Find("Collection notice"))throw new Exception("Collector completion popup missing.");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"01-COLLECTION-NOTICE.png"));yield return null;
            ClickGameButton("물을 회수한다 · 0시간");
            if(state.HomeStorage.Count("raw_water")!=4 || state.hour!=10)throw new Exception("Collector UI collection wrong.");
            int water=state.HomeStorage.Count("water");ClickGameButton("끓이기 · 정수");ClickGameButton("생수 정수");
            if(state.HomeStorage.Count("water")!=water+2 || state.HomeStorage.Count("raw_water")!=0 || state.hour!=11)throw new Exception("Purify UI yield/time wrong.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"02-WATER-MANAGEMENT.png"));yield return null;
            state.CurrentLocation="school";state.AwayFromHome=true;state.UnlockedLocations.AddRange(new[]{"empty_house","school"});state.hour=16;page="home";message="";Render();yield return null;
            int searches=state.Locations.Find(x=>x.id=="school").searches;ClickGameButton("주변을 수색한다");
            if(!confirmNightSearch || state.hour!=16 || state.Locations.Find(x=>x.id=="school").searches!=searches)throw new Exception("Search warning did not stop time/loot.");
            if(!root.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("20:00") && x.text.Contains("18:00")))throw new Exception("Warning missing immediate/delayed arrival comparison.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"03-NIGHT-SEARCH-WARNING.png"));yield return null;
            ClickGameButton("수색을 멈추고 귀갓길 확인");
            if(state.hour!=16 || page!="location" || confirmNightSearch)throw new Exception("Cancelling search spent time.");
            state.Backpack.Add("soda",1);state.Thirst=60;state.HydrationHours=8;state.Vitals.Hunger=30;page="inventory";selectedInventoryItem="soda";Render();yield return null;
            ClickGameButton("마시기");
            if(state.Thirst!=20 || state.Vitals.Hunger!=20 || state.hour!=16 || state.Backpack.Count("soda")!=0)throw new Exception("Portable soda UI effect/time wrong.");
            if(!store.TryLoad(out var restored,out var error) || restored.Thirst!=20 || restored.schemaVersion!=13)throw new Exception("Balance UI autosave failed: "+error);
            Debug.Log("VD_SURVIVAL_BALANCE_UI_OK: timed tank notification, free recovery, purifier yield, night confirmation/cancel and instant soda autosave.");
            yield return null;Application.Quit();
        }
    }
}
#endif
