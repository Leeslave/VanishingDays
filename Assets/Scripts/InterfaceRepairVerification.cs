#if UNITY_EDITOR || VD_UI_VERIFICATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        IEnumerator SmokeInterfaceRepair()
        {
            string folder=Path.GetFullPath("Builds/InterfaceRepairVerification");Directory.CreateDirectory(folder);
            state=SurvivalState.NewGame(GameDifficulty.NORMAL,definitions,123);
            state.Story.OpeningCompleted=true;state.NeighborIntroductionSeen=true;state.BeginTutorial();page="tutorial";Render();
            int startHour=state.hour;uint rng=state.RandomState;
            ClickGameButton("안내 시작");ClickGameButton("옆집 안부 확인");ClickGameButton("그릇을 돌려드린다");
            if(state.TutorialStep!=2)throw new Exception("Greeting did not advance the tutorial automatically.");
            ClickGameButton("다음 안내 보기");ClickGameButton("식수 관리 열기");
            if(state.TutorialStep!=3)throw new Exception("Water lesson still required a confirmation click.");
            yield return null;CaptureQa(Path.Combine(folder,"01-DRINKING.png"));
            var focused=root.GetComponentsInChildren<Button>().First(x=>x.name=="지금 물을 마신다").gameObject;
            EventSystem.current.SetSelectedGameObject(focused);
            ClickGameButton("다음 안내 보기");
            if(EventSystem.current.currentSelectedGameObject!=null)throw new Exception("Screen rebuild retained its previous selected object.");
            yield return null;Update();
            ClickGameButton("물품 열기");ClickGameButton("다음 안내 보기");
            ClickGameButton("지도 열기");ClickGameButton("다음 안내 보기");
            ClickGameButton("수면 화면 열기");
            if(state.TutorialActive || state.TutorialStep!=6 || state.hour!=startHour || state.RandomState!=rng)throw new Exception("Automatic lessons changed game time/RNG or failed to finish.");
            ClickGameButton("생존 안내 완료");Save();
            if(!store.TryLoad(out var loaded,out _) || loaded.TutorialViewed!=state.TutorialViewed || loaded.TutorialStep!=6)throw new Exception("Completed lesson progress was lost on save.");
            state.BeginTutorial();page="tutorial";Render();ClickGameButton("안내 시작");
            if(state.TutorialActive || state.TutorialStep!=6)throw new Exception("Completed lessons repeated on replay.");
            // Reproduce the pseudo-null reference from the reported Update stack trace.
            var destroyed=new GameObject("Destroyed selected object");EventSystem.current.SetSelectedGameObject(destroyed);
            DestroyImmediate(destroyed);Update();
            page="water";waterTab=1;message="";Render();
            yield return null;CaptureQa(Path.Combine(folder,"02-GATHERING.png"));
            ClickGameButton("생활용수를 모은다");vitalsNotice="";
            if(state.HomeStorage.Count("raw_water")!=2)throw new Exception("Gathering action failed after categorization.");
            ClickGameButton("끓이기 · 정수");
            yield return null;CaptureQa(Path.Combine(folder,"03-PURIFICATION.png"));
            int water=state.HomeStorage.Count("water");ClickGameButton("물을 끓인다");
            if(state.HomeStorage.Count("water")!=water+2 || state.HomeStorage.Count("raw_water")!=0)throw new Exception("Boiling action failed after categorization.");
            ClickGameButton("설비");yield return null;CaptureQa(Path.Combine(folder,"04-EQUIPMENT.png"));
            state.InstalledFacilities.Add("water_collector");ClickGameButton("물 확보");ClickGameButton("2시간 자동 집수 시작");
            if(state.CollectorCycleHours!=2)throw new Exception("Collector cycle could not be started from its category.");
            yield return null;CaptureQa(Path.Combine(folder,"05-COLLECTOR.png"));
            Debug.Log("VD_INTERFACE_REPAIR_OK: automatic lessons and replay/save, destroyed selection regression, four water categories, gathering/boiling/collection and rendered screenshots.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }
}
#endif
