using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        bool neighborInvitationPending;
        static readonly string[] TutorialTitles={"혼자 남은 첫날","옆집에 첫 인사","마실 물을 확보하기","외출 전에 가방 챙기기","지도와 수색","잠과 하루의 기록","이제 하루를 시작하자"};
        string TutorialBody
        {
            get
            {
                switch(state.TutorialStep)
                {
                    case 0:return "집을 거점으로 물과 식량을 준비하고, 낮에 가까운 곳부터 살피자.\n\n다섯 가지 기본 행동을 차례로 안내한다. 안내 중에는 요리하거나 수면을 연습해도 시계가 멈춘다. 요리와 식사는 실제 물자를 사용한다. 아래 버튼 설명에서 생활 화면의 조작을 모두 확인할 수 있다.\n\n언제든 안내를 건너뛰고 메뉴에서 다시 볼 수 있다.";
                    case 1:return state.NeighborStage>0?"옆집 노인과 첫 인사를 나눴다. 벽을 두 번 두드려 서로의 안부를 확인하기로 했다.\n\n이웃의 이야기는 매일 한 번씩 안부를 물으며 이어진다. 다음에는 답이 돌아오는지 귀를 기울이자.":"찬장에 옆집 노인에게 빌린 반찬 그릇이 있다. 아직 인기척이 들리는 옆집에 그릇을 돌려드려 보자.\n\n‘옆집 안부’에서 ‘그릇을 돌려드린다’를 직접 눌러 첫 인사를 나누자. 첫 인사는 시간이나 물품을 소모하지 않는다.";
                    case 2:return (state.UnlimitedWater?"쉬움에서는 식수와 조리용 물이 무제한이다.":"생수는 "+state.WaterInterval(definitions)+"시간마다 1병씩 자동으로 마신다. 목마르면 직접 마실 수도 있다.")+"\n\n물이 떨어지면 생활용수를 모아 끓이자. 집수통이나 정수 설비는 제작에서 차차 만들면 된다.\n\n식수 관리 화면을 열어 보자.";
                    case 3:return "집 창고와 소지품은 별개다. 외출 중에는 가방에 챙긴 물과 음식만 사용할 수 있다.\n\n물품에서 수량을 정하고 ‘챙기기’를 누른다. 생수·음식·약품을 준비하고, 집에 돌아오면 남은 소지품은 자동으로 보관된다. 물품 이동에는 시간이 흐르지 않는다.\n\n물품 화면을 열어 두 보관 공간을 확인하자.";
                    case 4:return "하단 ‘외출 · 지도’에서 외투에 넣어 둔 검침 지도를 펼친다. 장소를 고르고 ‘이동하기’를 누르면 출발한다. 지도를 보는 동안에는 시간이 흐르지 않는다.\n\n처음에는 편의점부터 살핀다. 수색하면 물자를 얻고 새로 확인한 길을 지도에 표시할 수 있다. 안내가 끝난 뒤에는 이동과 수색에 시간이 흐르며 사람을 만날 수도 있다.\n\n지금은 지도를 확인하고 돌아와도 된다.";
                    case 5:return "집의 ‘잠을 잔다’에서 2·6·8시간을 고른다. 매일 오전 8시에 하루 수면을 평가하며, 6시간보다 부족하면 정신력을 잃는다.\n\n졸림이 높거나 식수가 없으면 경고가 뜬다. 긴 잠을 자기 전에는 물과 식량도 확인하자. 날짜가 바뀌면 그날 한 일이 기록지에 남는다.\n\n수면 시간을 하나 눌러 연습하거나, 화면 아래에서 수면을 건너뛰고 안내를 마칠 수 있다.";
                    default:return "이제 물과 식량을 정리하고 가까운 곳부터 살펴보자.\n\n오늘의 안부는 생활 화면의 ‘옆집 안부’에서, 지나온 일은 ‘기록’에서 확인한다.\n\n안내는 메뉴의 ‘처음 생존 안내’에서 다시 볼 수 있다.";
                }
            }
        }
        // The tutorial step the player last saw on the tutorial page; a higher current step means one was just finished.
        int tutorialSeenStep;
        void TutorialPage()
        {
            tutorialSeenStep=state.TutorialStep;
            Hero("처음 생존 안내");
            Label((state.TutorialStep==0?"시작하기 전에":state.TutorialStep==6?"안내 완료":state.TutorialStep+" / 5")+" · "+TutorialTitles[state.TutorialStep],48,772,804,34,24,Amber);
            Paragraph(TutorialBody,824,246,25);
            if(state.TutorialStep==0)
            {
                Button("안내 시작",48,1100,804,76,()=>{state.AdvanceTutorial();Save();Render();},state.CanAdvanceTutorial,true);
                Button("전체 버튼 설명",48,1190,804,62,()=>OpenStoryPage("tutorial-controls"));
            }
            else if(state.TutorialStep<6)
            {
                string[] targets={"","옆집 안부 확인","식수 관리 열기","물품 열기","지도 열기","수면 화면 열기"};
                Button(targets[state.TutorialStep],48,1090,804,76,OpenTutorialTarget,state.CanAct,true);
                Label(state.TutorialStep==1?"그릇을 돌려드리면 다음 안내로 자동 진행됩니다.":"안내한 화면을 확인하면 다음 안내로 자동 진행됩니다.",48,1182,804,76,24,Muted);
            }
            if(state.TutorialActive) Button("안내 건너뛰기",48,1280,390,62,()=>{state.SkipTutorial();neighborInvitationPending=state.NeedsNeighborIntroduction;page="home";message="";Save();Render();});
            Button("생활로 돌아가기",state.TutorialActive?462:48,state.TutorialActive?1280:1160,state.TutorialActive?390:804,62,()=>{page="home";message="";Render();});
        }
        void TutorialControlsPage()
        {
            Label("처음 생존 안내 · 버튼 설명",48,462,804,58,36,Paper);
            string guide=
                "생활\n요리하고 식사한다 · 집 재료로 음식을 만든다. 조리한다와 먹는다로 나뉜다.\n외출 준비 · 물품을 챙긴다 · 수량을 정하고 가방에 넣는다.\n제작대를 확인한다 · 제작 목록과 필요한 재료를 본다.\n라디오를 켠다 · 라디오를 얻은 뒤 첫 제보와 아침 소식을 받는다.\n잠을 잔다 · 2·6·8시간 중 하나를 누르거나 잠들지 않고 돌아간다.\n옆집 안부 · 그릇 반환, 두드리기, 문 앞 확인으로 이웃과 연락한다.\n동행 약속 · 다음 만남의 날짜와 장소를 확인하고 지도나 만남으로 이어간다.\n설치 설비 · 집에 설치한 장비와 보안을 확인한다.\n\n화면 하단\n식수 관리 · 마시기에서 생수를 마시고, 물 확보에서 생활용수를 모으거나 집수를 시작·회수한다. 끓이기 · 정수에서 식수로 바꾸고, 설비에서 집수통과 정수기를 확인한다.\n잠자기 / 지도 · 집에서는 수면, 밖에서는 지도를 연다.\n외출 · 지도 / 집으로 돌아간다 · 집에서는 지도, 밖에서는 귀가를 준비한다.\n생활 · 현재 있는 곳의 기본 화면으로 돌아간다.\n제작 · 음식, 무기, 생활용품과 후반 거점 설비를 둘러본다. 조리·제작·설치·보강 버튼으로 재료를 사용한다.\n물품 · 물건을 누르면 정보와 먹기·치료·착용·벗기가 열린다. 집에서는 수량 칸이나 ±로 조절하고 챙기기·보관·전체 보관을 쓴다.\n기록 · 날짜별 일기, 아침 수신함, 동행 약속과 획득한 방송·지도를 다시 읽는다.\n\n외출과 기록\n지도 점 / 장소 목록 · 위치를 선택한다. 화살표는 화면을 옮기고 현재 위치는 내 위치로 돌아온다.\n이동하기 / 준비 완료 · 출발 · 목적지와 예상 시각을 확인한 뒤 실제로 출발한다.\n주변을 수색한다 · 현재 장소를 뒤져 물자를 찾는다. 야간 경고에서는 수색을 강행하거나 취소할 수 있다.\n메뉴 · 저장, 불러오기, 생존 안내, 스토리 열람과 새 기록을 연다.\n대화의 다음·계속 읽기 · 한 단락을 넘긴다. 건너뛰기는 선택지까지 이동하고, 지난 대화는 이미 읽은 대사를 펼친다.";
            Paragraph(guide,548,720,24);
            Button("생존 안내로 돌아가기",48,1290,804,72,()=>OpenStoryPage("tutorial"),true,true);
        }
        void OpenTutorialTarget()
        {
            if(state.TutorialStep==1)
            {
                if(state.CurrentLocation!="home") {selectedLocation="home";page="location";}
                else {state.NeighborIntroductionSeen=true;neighborInvitationPending=false;page="neighbor";Save();}
            }
            else if(state.TutorialStep==5 && state.CurrentLocation!="home") {selectedLocation="home";page="location";}
            else page=state.TutorialStep==2?"water":state.TutorialStep==3?"inventory":state.TutorialStep==4?"map":"sleep";
            message="";Render();
        }
        void TutorialHomeCard()
        {
            var card=Pressable("Tutorial guide",48,776,804,214,()=>{page="tutorial";message="";Render();},true,Amber,EmberDim).transform;
            ArtIcon(3,24,26,104,104,card);
            Label("생존 안내 · "+(state.TutorialStep==0?"시작":state.TutorialStep+" / 5"),152,26,620,34,23,AmberText,card).fontStyle=FontStyle.Bold;
            Label(TutorialTitles[state.TutorialStep],152,68,600,44,30,Paper,card).fontStyle=FontStyle.Bold;
            Label("눌러서 다음 미완료 안내를 확인한다.",152,120,600,34,22,Muted,card);
            Chevron(760,95,Amber,card);
        }
        // Shares the header strip with any survival warning, which gives up its right side.
        void TutorialReturnLink()
        {
            if(page=="tutorial-controls") return;
            if((!state.TutorialActive && !(state.TutorialStep==6 && page=="sleep")) || page=="tutorial") return;
            float x=48,w=804;
            var alert=root.Find("Survival alert") as RectTransform;
            if(alert!=null) { alert.sizeDelta=new Vector2(460,56); alert.Find("Fill").GetComponent<RectTransform>().sizeDelta=new Vector2(456,52); foreach(var label in alert.GetComponentsInChildren<Text>()) label.rectTransform.sizeDelta=new Vector2(396,48); foreach(Transform child in alert) if(child.name=="Chevron") child.gameObject.SetActive(false); x=520; w=332; }
            // Only a finished step is announced as a call to action; otherwise the link is a quiet way back.
            bool finished=!state.TutorialActive || state.TutorialStep>tutorialSeenStep;
            var link=Button(!state.TutorialActive?"생존 안내 완료":finished?(state.TutorialStep-1)+"단계 완료 · 다음 안내 보기":"생존 안내 "+state.TutorialStep+"/5 보기",x,376,w,56,()=>{page="tutorial";message="";Render();},true,finished);
            foreach(var label in link.GetComponentsInChildren<Text>()) label.fontSize=21;
        }
        void NeighborIntroductionPage()
        {
            Hero("벽 너머의 인기척");
            Paragraph("조용한 복도에서 작은 기침 소리가 들린다.\n\n찬장에 빌려 쓰던 반찬 그릇이 남아 있다. 옆집 노인은 무사할까. 그릇을 돌려드리며 한 번 안부를 물어보자.\n\n첫 인사에는 시간이나 물품이 들지 않는다.",790,300,27);
            Button("옆집 안부를 물어본다",48,1140,804,80,()=>{state.NeighborIntroductionSeen=true;neighborInvitationPending=false;Save();page="neighbor";message="";Render();},true,true);
            Button("지금은 생활부터 정리한다",48,1250,804,70,()=>{state.NeighborIntroductionSeen=true;neighborInvitationPending=false;Save();page="home";message="";Render();});
        }
#if UNITY_EDITOR || VD_UI_VERIFICATION
        IEnumerator SmokeTutorial()
        {
            string dir=Path.GetFullPath("Builds/TutorialVerification");Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true;state.BeginTutorial();page="tutorial";Render();
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"01-WELCOME.png"));yield return null;
            int hour=state.hour;uint rng=state.RandomState;
            ClickGameButton("안내 시작");ClickGameButton("옆집 안부 확인");
            if(state.NeighborStage!=0 || state.CanAdvanceTutorial)throw new Exception("Opening neighbor page completed event automatically.");
            ClickGameButton("그릇을 돌려드린다");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"02-NEIGHBOR.png"));yield return null;
            ClickGameButton("다음 안내 보기");
            if(!store.TryLoad(out var loaded,out _) || loaded.TutorialStep!=2 || !loaded.TutorialActive || loaded.NeighborStage!=1)throw new Exception("Tutorial/event did not save.");
            state=loaded;page="tutorial";Render();
            foreach(string target in new[]{"식수 관리 열기","물품 열기","지도 열기"})
            {
                ClickGameButton(target);
                ClickGameButton("다음 안내 보기");
            }
            ClickGameButton("수면 화면 열기"); ClickGameButton("6시간 잔다");
            if(state.TutorialActive || state.TutorialStep!=6 || state.hour!=hour || state.RandomState!=rng || state.NeighborGreetings!=1)throw new Exception("Tutorial changed time/RNG or duplicated neighbor meeting.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"03-COMPLETE.png"));yield return null;
            state=SurvivalState.NewGame(GameDifficulty.NORMAL,definitions);state.Story.OpeningCompleted=true;Save();Load();
            if(!root.Find("Text") || !neighborInvitationPending)throw new Exception("Existing save invitation missing.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"04-NEIGHBOR-INVITATION.png"));yield return null;
            ClickGameButton("지금은 생활부터");Load();
            if(neighborInvitationPending || !state.NeighborIntroductionSeen || state.NeighborStage!=0)throw new Exception("Dismissed invitation repeated or auto-played event.");
            page="menu";Render();ClickGameButton("처음 생존 안내");ClickGameButton("안내 건너뛰기");
            if(state.TutorialActive)throw new Exception("Tutorial skip failed.");
            Debug.Log("VD_TUTORIAL_UI_OK: welcome, actual neighbor button, five topics, resume, completion, skip/replay, one-time invitation and no time/RNG cost.");
            yield return null;Application.Quit();
        }
#endif
    }
}
