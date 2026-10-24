using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        Texture2D survivalArt;
        string vitalsNotice="";
        void AtlasPicture(float x,float y,float w,float h,UnityEngine.Rect uv,Transform parent=null)
        {
            if(survivalArt==null) return;
            var image=Rect("Survival illustration",parent??root,x,y,w,h).gameObject.AddComponent<RawImage>();
            image.texture=survivalArt; image.uvRect=uv; image.raycastTarget=false;
        }
        void ArtIcon(int index,float x,float y,float w,float h,Transform parent=null)
        {
            AtlasPicture(x,y,w,h,new UnityEngine.Rect((index%4)*.25f,index<4?.25f:0,.25f,.25f),parent);
        }
        int ButtonIcon(string text)
        {
            if(text.Contains("식수") || text.Contains("마시") || text.Contains("물을") || text.Contains("용수") || text.Contains("정수")) return 0;
            if(text.Contains("잠") || text.Contains("잔다") || text.Contains("수면") || text.Contains("머무른다")) return 1;
            if(text.Contains("물품") || text.Contains("챙기") || text.Contains("보관")) return 2;
            if(text.Contains("기록") || text.Contains("메뉴")) return 3;
            if(text.Contains("외출") || text.Contains("지도") || text.Contains("돌아") || text.Contains("수색")) return 4;
            if(text.Contains("제작") || text.Contains("생활") || text.Contains("무기") || text.Contains("설치") || text.Contains("보강")) return 5;
            if(text.Contains("치료")) return 6;
            if(text.Contains("음식") || text.Contains("요리") || text.Contains("먹") || text.Contains("조리")) return 7;
            return -1;
        }
        int RecipeIcon(string id) => id.Contains("water")?0:id=="bedroll"?1:id=="make_bandage"?6:5;
        int ItemIcon(string id) => id.Contains("water")?0:definitions.Item(id).injuriesTreated>0?6:definitions.Item(id).consumable || id=="rice"?7:definitions.Item(id).strength>0 || id=="wood" || id=="tools" || id=="scrap" || id=="tape"?5:2;
        void SurvivalWarning()
        {
            if(state.IsGameOver) return;
            bool water=state.NeedsWaterWarning, sleep=state.NeedsSleepWarning;
            if(!water && !sleep) return;
            var strip=Box("Survival alert",48,427,804,27,new Color32(85,49,36,255));
            string warning=(water?(state.AccessibleWater==0?"식수 없음 · 물 확보 필요":"갈증 경고 · 물을 마셔야 한다"):"")+(water && sleep?"  /  ":"")+(sleep?"수면 부족 · 졸림 "+state.Sleepiness:"");
            Label("!  "+warning+"  ›",8,1,788,26,18,Amber,strip);
            strip.GetComponent<Image>().raycastTarget=true;
            var button=strip.gameObject.AddComponent<Button>(); button.targetGraphic=strip.GetComponent<Image>();
            button.interactable=state.CanAct && !state.HasLifeNotice;
            button.onClick.AddListener(()=>{page=water?"water":state.CurrentLocation=="home"?"sleep":"map";message="";Render();});
        }
        void WaterPage()
        {
            Hero("식수 확보와 관리");
            Paragraph(string.IsNullOrEmpty(message)?"생수 · 소지 "+state.Backpack.Count("water")+"병 / 집 "+state.HomeStorage.Count("water")+"병\n혼탁한 물 · 집 "+state.HomeStorage.Count("raw_water")+"병\n설비 없이 생활용수 2병을 모으고, 부르스타로 끓여 마실 수 있다. 혼탁한 물은 바로 마실 수 없다.":message,770,210,25);
            Choice(0,"지금 물을 마신다","생수 1병 · 시간 소모 없음 · 목마름 해소",996,()=>Apply(state.DrinkWater(),"water"),state.CanDrink);
            Choice(1,"생활용수를 모은다","2시간 · 혼탁한 물 2병 · 집에서 반복 가능",1100,()=>Apply(state.Craft("gather_water",definitions),"water"),state.CanCraft("gather_water",definitions));
            Choice(2,"물을 끓인다","1시간 · 혼탁한 물 2병 → 생수 2병 · 부르스타 필요",1204,()=>Apply(state.Craft("boil_water",definitions),"water"),state.CanCraft("boil_water",definitions));
            Button(state.CurrentLocation=="home"?"정수기·집수통 제작":"집으로 돌아갈 길 확인",48,1318,804,62,()=>{if(state.CurrentLocation=="home"){craftTab=2;page="craft";}else{selectedLocation="home";page="location";}message="";Render();});
        }
        void VitalsNotice()
        {
            if(string.IsNullOrEmpty(vitalsNotice)) return;
            var card=Box("Vitals notification",70,474,760,200,new Color32(48,39,30,250));
            card.GetComponent<Image>().raycastTarget=true;
            ArtIcon(state.NeedsWaterWarning?0:1,16,16,64,64,card);
            Label("생존 상태 경고",98,14,636,36,26,Amber,card);
            Label(vitalsNotice,98,50,636,84,20,Paper,card);
            Button("확인",570,142,170,48,()=>{vitalsNotice="";Render();},true,true,card);
        }
#if UNITY_EDITOR || VD_UI_VERIFICATION
        System.Collections.IEnumerator SmokeWaterAccess()
        {
            string dir=System.IO.Path.GetFullPath("Builds/WaterAccessVerification");System.IO.Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true;state.PlayerNickname="식수 검증";state.HomeStorage.Remove("water",8);state.Sleepiness=60;
            Render();yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"01-SHELTER-WARNINGS.png"));yield return null;
            if(survivalArt==null || !root.Find("Survival alert"))throw new System.Exception("Missing illustrated UI or warning.");
            ClickGameButton("식수 관리");ClickGameButton("생활용수를 모은다");
            if(state.HomeStorage.Count("raw_water")!=2 || string.IsNullOrEmpty(vitalsNotice))throw new System.Exception("Gather button or alert failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"02-NOTIFICATION.png"));yield return null;
            ClickGameButton("확인");ClickGameButton("물을 끓인다");ClickGameButton("지금 물을 마신다");
            if(state.HomeStorage.Count("water")!=1 || state.Thirst!=0 || state.HydrationHours!=0 || state.NeedsWaterWarning)throw new System.Exception("Boil/drink UI failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"03-WATER-ACCESS.png"));yield return null;
            ClickGameButton("생활");ClickGameButton("잠을 잔다");ClickGameButton("6시간 잔다");
            if(state.NeedsSleepWarning)throw new System.Exception("Sleep warning did not clear through UI.");
            if(!store.TryLoad(out var loaded,out _) || loaded.Thirst!=0 || loaded.Sleepiness!=0)throw new System.Exception("New actions did not save.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"04-RECOVERED.png"));yield return null;
            Debug.Log("VD_WATER_ACCESS_UI_OK: illustrated shelter, warnings, notification, gather, boil, drink, sleep and autosave.");
            Application.Quit();
        }
#endif
    }
}
