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
                    case 0:return "집을 거점으로 물과 식량을 준비하고, 낮에 가까운 곳부터 살피자.\n\n다섯 가지 기본 행동을 차례로 안내한다. 안내 화면을 읽거나 메뉴를 여는 동안에는 시간이 흐르지 않는다. 이동·수색·요리·수면에는 시간이 흐른다.\n\n언제든 안내를 건너뛰고 메뉴에서 다시 볼 수 있다.";
                    case 1:return state.NeighborStage>0?"옆집 노인과 첫 인사를 나눴다. 벽을 두 번 두드려 서로의 안부를 확인하기로 했다.\n\n이웃의 이야기는 매일 한 번씩 안부를 물으며 이어진다. 다음에는 답이 돌아오는지 귀를 기울이자.":"찬장에 옆집 노인에게 빌린 반찬 그릇이 있다. 아직 인기척이 들리는 옆집에 그릇을 돌려드려 보자.\n\n‘옆집 안부’에서 ‘그릇을 돌려드린다’를 직접 눌러 첫 인사를 나누자. 첫 인사는 시간이나 물품을 소모하지 않는다.";
                    case 2:return state.UnlimitedWater?"쉬움에서는 식수와 조리용 물이 무제한이다.\n\n다른 난이도에서는 식수 관리에서 생활용수 모으기(2시간·2병) → 물 끓이기(1시간·2병)를 반복한다. 정수기·집수통을 만들면 효율이 좋아진다.\n\n식수 관리 화면을 열어 확보 방법과 보유량을 확인하자.":"식수는 6시간마다 생수 1병을 자동으로 마신다. 목마르면 직접 마실 수도 있다.\n\n식수 관리에서 생활용수 모으기(2시간·2병) → 물 끓이기(1시간·2병)로 식수를 확보한다. 혼탁한 물은 바로 마실 수 없다.\n\n지금은 화면과 방법만 확인해도 된다.";
                    case 3:return "집 창고와 소지품은 별개다. 외출 중에는 가방에 챙긴 물과 음식만 사용할 수 있다.\n\n물품에서 수량을 정하고 ‘챙기기’를 누른다. 생수·음식·약품을 준비하고, 집에 돌아오면 남는 물자를 보관하자. 물품 이동에는 시간이 흐르지 않는다.\n\n물품 화면을 열어 두 보관 공간을 확인하자.";
                    case 4:return "외출 탭에서 지도를 열고 장소를 고른 뒤 ‘이동하기’를 눌러 출발한다. 지도를 보는 동안에는 시간이 흐르지 않는다.\n\n처음에는 편의점부터 살핀다. 도착한 곳에서 수색하면 물자를 얻고 새로운 길을 발견할 수 있다. 이동과 수색에는 시간이 흐르며 사람을 만날 수도 있다.\n\n지금은 지도를 확인하고 돌아와도 된다.";
                    case 5:return "집의 ‘잠을 잔다’에서 2·6·8시간을 고른다. 매일 오전 8시에 하루 수면을 평가하며, 6시간보다 부족하면 정신력을 잃는다.\n\n졸림이 높거나 식수가 없으면 경고가 뜬다. 긴 잠을 자기 전에는 물과 식량도 확인하자. 날짜가 바뀌면 그날 한 일이 기록지에 남는다.\n\n수면 화면을 확인한 뒤 안내를 마치자. 지금 잠들 필요는 없다.";
                    default:return "이제 물과 식량을 정리하고 가까운 곳부터 살펴보자.\n\n오늘의 안부는 생활 화면의 ‘옆집 안부’에서, 지나온 일은 ‘기록’에서 확인한다.\n\n안내는 메뉴의 ‘처음 생존 안내’에서 다시 볼 수 있다.";
                }
            }
        }
        void TutorialPage()
        {
            Hero("처음 생존 안내");
            Label((state.TutorialStep==0?"시작하기 전에":state.TutorialStep==6?"안내 완료":state.TutorialStep+" / 5")+" · "+TutorialTitles[state.TutorialStep],48,772,804,34,24,Amber);
            Paragraph(TutorialBody,824,246,25);
            if(state.TutorialStep==0)
                Button("안내 시작",48,1100,804,76,()=>{state.AdvanceTutorial();Save();Render();},state.CanAdvanceTutorial,true);
            else if(state.TutorialStep<6)
            {
                string[] targets={"","옆집 안부 확인","식수 관리 열기","물품 열기","지도 열기","수면 화면 열기"};
                Button(targets[state.TutorialStep],48,1090,804,76,OpenTutorialTarget,state.CanAct,true);
                Button(state.CanAdvanceTutorial?(state.TutorialStep==5?"안내 마치기":"확인했어요 · 다음 안내"):state.TutorialStep==1?"먼저 그릇을 돌려드리자":"먼저 안내한 화면을 열어보자",48,1182,804,76,()=>{state.AdvanceTutorial();Save();Render();},state.CanAdvanceTutorial);
            }
            if(state.TutorialActive) Button("안내 건너뛰기",48,1280,390,62,()=>{state.SkipTutorial();neighborInvitationPending=state.NeedsNeighborIntroduction;page="home";message="";Save();Render();});
            Button("생활로 돌아가기",state.TutorialActive?462:48,state.TutorialActive?1280:1160,state.TutorialActive?390:804,62,()=>{page="home";message="";Render();});
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
            Label("눌러서 다음 할 일을 확인한다.",152,120,600,34,22,Muted,card);
            Chevron(760,95,Amber,card);
        }
        // Shares the header strip with any survival warning, which gives up its right side.
        void TutorialReturnLink()
        {
            if(!state.TutorialActive || page=="tutorial") return;
            float x=48,w=804;
            var alert=root.Find("Survival alert") as RectTransform;
            if(alert!=null) { alert.sizeDelta=new Vector2(460,56); alert.Find("Fill").GetComponent<RectTransform>().sizeDelta=new Vector2(456,52); foreach(var label in alert.GetComponentsInChildren<Text>()) label.rectTransform.sizeDelta=new Vector2(396,48); foreach(Transform child in alert) if(child.name=="Chevron") child.gameObject.SetActive(false); x=520; w=332; }
            var link=Button("생존 안내로 돌아가기",x,376,w,56,()=>{page="tutorial";message="";Render();},true,true);
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
            ClickGameButton("생존 안내로 돌아가기");ClickGameButton("확인했어요");
            if(!store.TryLoad(out var loaded,out _) || loaded.TutorialStep!=2 || !loaded.TutorialActive || loaded.NeighborStage!=1)throw new Exception("Tutorial/event did not save.");
            state=loaded;page="tutorial";Render();
            foreach(string target in new[]{"식수 관리 열기","물품 열기","지도 열기","수면 화면 열기"})
            {
                ClickGameButton(target);ClickGameButton("생존 안내로 돌아가기");
                ClickGameButton(state.TutorialStep==5?"안내 마치기":"확인했어요");
            }
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
