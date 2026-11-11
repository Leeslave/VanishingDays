using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        Texture2D survivalArt;
        string vitalsNotice="";
        RawImage AtlasPicture(float x,float y,float w,float h,UnityEngine.Rect uv,Transform parent=null)
        {
            if(survivalArt==null) return null;
            var image=Rect("Survival illustration",parent??root,x,y,w,h).gameObject.AddComponent<RawImage>();
            image.texture=survivalArt; image.uvRect=uv; image.raycastTarget=false; return image;
        }
        static UnityEngine.Rect IconRect(int index) => new UnityEngine.Rect((index%4)*.25f,index<4?.25f:0,.25f,.25f);
        void ArtIcon(int index,float x,float y,float w,float h,Transform parent=null) { AtlasPicture(x,y,w,h,IconRect(index),parent); }
        int ButtonIcon(string text)
        {
            // Navigation back to a screen and plain storing carry no pictogram; one icon per verb keeps them distinct.
            if(text.Contains("생활로") || text.Contains("생활 화면") || text=="보관" || text=="전체 보관") return -1;
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
            if(state.IsGameOver || state.HasEnded) return;
            bool water=state.NeedsWaterWarning, sleep=state.NeedsSleepWarning, ready=state.CollectorReady, hungry=Hungry, home=state.CurrentLocation=="home";
            if(!water && !sleep && !ready && !hungry) return;
            string warning=(water?(state.AccessibleWater==0?"식수 없음 · 물 확보 필요":"갈증 경고 · 물을 마셔야 한다"):"")+(water && sleep?"  /  ":"")+(sleep?"수면 부족 · 졸림 "+state.Sleepiness:"");
            if(ready) warning="집수통 가득 참 · 식수 관리에서 회수"+(water?" / 식수 부족":"");
            if(hungry) warning+=(warning.Length>0?"  /  ":"")+(Starving?"굶주림 · 체력이 줄고 있다":"허기 "+state.Vitals.Hunger+" · 식사 필요");
            // Water first, then sleep, then food: the most urgent need decides where the strip leads.
            string target=water || ready?"water":sleep?(home?"sleep":"map"):home?"kitchen":"inventory";
            var strip=Pressable("Survival alert",48,376,804,56,()=>{page=target;message="";Render();},state.CanAct && !LifeNoticeBlocks,EmberEdge,Alert).transform;
            Box("Alert mark",20,18,8,20,Amber,strip);
            Label(warning,44,4,736,48,22,AmberText,strip).alignment=TextAnchor.MiddleLeft;
            Chevron(770,16,Amber,strip);
        }
        // Hunger already lowers the HP cap from hungryAt and drains HP from starvingAt; warn at the same thresholds.
        bool Hungry => state.Vitals.Hunger>=definitions.TEMP_BALANCE.hungryAt;
        bool Starving => state.Vitals.Hunger>=definitions.TEMP_BALANCE.starvingAt;
        int waterTab;
        void WaterPage()
        {
            bool home=state.CurrentLocation=="home";
            if(!home)waterTab=0;
            Label("식수 관리",48,462,804,56,36,Paper);
            var summary=Box("Water summary",48,534,804,142,Panel);
            string[] titles={home?"마실 수 있는 생수":"소지한 생수","목마름",home?"혼탁한 물":"자동 음수 주기"};
            string[] values={state.UnlimitedWater?"무제한":state.AccessibleWater+"병",state.Thirst+" / 100",home?state.HomeStorage.Count("raw_water")+"병":state.UnlimitedWater?"무제한":state.WaterInterval(definitions)+"시간"};
            for(int i=0;i<3;i++)
            {
                Label(titles[i],20+i*264,14,244,32,21,Muted,summary);
                Label(values[i],20+i*264,50,244,44,30,i==1 && state.Thirst>=60?Amber:Paper,summary).fontStyle=FontStyle.Bold;
            }
            string hint=state.UnlimitedWater?"쉬움 · 식수와 조리용 물이 무제한입니다.":"생수 1병 = 500ml · "+(state.WaterInterval(definitions)-state.HydrationHours)+"시간 후 자동으로 1병을 마십니다.";
            Label(hint,20,104,764,30,20,Muted,summary);
            string[] tabs={"마시기","물 확보","끓이기 · 정수","설비"};
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;bool enabled=home || i==0;
                bool highlighted=waterTab==i || i==1 && state.CollectorGuideStep==2;
                var chip=Pressable(tabs[i],48+i*204,698,192,60,()=>SelectWaterTab(tab),enabled,highlighted?Amber:Edge,highlighted?EmberDim:Panel);
                Label(tabs[i],4,0,184,60,22,!enabled?Faint:highlighted?Amber:Paper,chip.transform).alignment=TextAnchor.MiddleCenter;
            }
            ScrollArea("Water management",780,592,out var content);float y=12;
            if(!string.IsNullOrEmpty(message))
            {
                var notice=Label(message,20,y,764,80,23,AmberText,content);
                float h=Mathf.Max(80,notice.preferredHeight);notice.rectTransform.sizeDelta=new Vector2(764,h);y+=h+24;
            }
            if(waterTab==0)
            {
                string counts=state.UnlimitedWater?"물 부족 없이 언제든 마실 수 있습니다.":"소지 "+state.Backpack.Count("water")+"병"+(home?" · 집 "+state.HomeStorage.Count("water")+"병":"")+"\n1병을 마시면 목마름이 0이 됩니다. 시간 소모 없음.";
                if(!state.UnlimitedWater && state.Thirst==0)counts+="\n지금은 목마르지 않습니다.";
                WaterAction(content,ref y,"water","지금 물을 마신다",counts,()=>Apply(state.DrinkWater(),"water"),state.CanDrink);
                WaterNote(content,ref y,home?"집에서는 창고의 생수를 먼저 마십니다.\n외출 전에는 물품 또는 외출 준비에서 생수를 챙기세요.":"외출 중에는 소지한 생수만 마실 수 있습니다.\n집의 물자는 귀가한 뒤 사용할 수 있습니다.");
                if(home)WaterAction(content,ref y,"raw_water","물 확보 방법 보기","생수가 부족하다면 생활용수를 모으거나 집수통을 사용하세요.",()=>SelectWaterTab(1),true);
                else WaterAction(content,ref y,"water","집으로 돌아갈 길 확인","이동 경로와 예상 소요 시간을 확인합니다.",()=>SelectLocation("home"),state.CanAct);
            }
            else if(waterTab==1)
            {
                if(!state.HasFacility("water_collector")) ManualWaterGathering(content,ref y);
                Label("자동 집수",20,y,764,42,30,Paper,content);y+=54;
                WaterNote(content,ref y,state.CollectorStatus+"\n외출·수면 중에도 모입니다. 가득 차면 멈추며 회수는 0시간입니다.");
                if(state.HasFacility("water_collector"))
                {
                    var settings=Box("Collector duration settings",12,y,780,184,Raised,content);
                    Label("집수 시간 선택 · 설정 0시간",20,14,740,34,23,Paper,settings);
                    int[] durations={2,5,10};
                    for(int i=0;i<durations.Length;i++)
                    {
                        int hours=durations[i];
                        var option=Pressable(hours+"시간 자동 집수 시작",20+i*250,62,240,62,()=>Apply(state.StartWaterCollection(hours),"water"),state.CanStartCollector,state.CollectorGuideStep==3?Amber:Edge,state.CollectorGuideStep==3?EmberDim:Panel,settings);
                        Label(hours+"시간",0,0,240,62,26,state.CanStartCollector?Amber:Faint,option.transform).alignment=TextAnchor.MiddleCenter;
                        Label("혼탁한 물 "+SurvivalState.CollectorYield(hours)+"병",20+i*250,136,240,32,21,Muted,settings).alignment=TextAnchor.MiddleCenter;
                    }
                    y+=204;
                    WaterAction(content,ref y,"water_collector","집수통 물 회수",state.CollectorReady?"혼탁한 물 "+state.CollectorCapacity+"병을 집 창고로 옮깁니다. · 0시간":"집수통이 가득 차면 회수할 수 있습니다.",()=>Apply(state.CollectWater(),"water"),state.CanCollectWater);
                }
                else WaterAction(content,ref y,"water_collector","집수통 제작 보기","설치하면 원하는 시간 동안 물을 자동으로 모을 수 있습니다.",OpenWaterCrafting,state.CanAct);
                if(state.HasFacility("water_collector")) ManualWaterGathering(content,ref y);
            }
            else if(waterTab==2)
            {
                WaterAction(content,ref y,"burner","물을 끓인다","부르스타 사용 · 1시간\n혼탁한 물 2병 → 생수 2병",()=>Apply(state.Craft("boil_water",definitions),"water"),state.CanCraft("boil_water",definitions));
                WaterAction(content,ref y,"water_filter","생수 정수",(state.WaterFilterLevel==0?"정수기 설치 필요":state.WaterFilterLevel+"단계 정수기 · "+definitions.crafting.Find("purify_water").hours+"시간")+"\n혼탁한 물 6병 → 생수 "+state.PurifiedWaterAmount+"병",()=>Apply(state.Craft("purify_water",definitions),"water"),state.CanCraft("purify_water",definitions));
                WaterNote(content,ref y,"정수기 · 1시간에 혼탁한 물 6병 처리\n간이 → 4병 · 그럴듯한 → 5병 · 괜찮은 → 6병\n끓이기는 1시간에 2병, 손실 없음. 혼탁한 물은 그대로 마실 수 없습니다.");
            }
            else
            {
                if(state.HasFacility("water_collector") && state.CanStartCollector)
                {
                    Button("집수 버튼 안내",20,y,764,58,()=>{state.CollectorGuideStep=1;Save();Render();},true,false,content);
                    y+=70;
                }
                WaterNote(content,ref y,"집수통은 물을 모으고 정수기는 혼탁한 물을 식수로 바꿉니다.\n제작에 필요한 재료는 집 창고에 보관해 두세요.");
                foreach(string id in new[]{"water_collector","water_filter","water_filter2","water_filter3"})
                {
                    var recipe=definitions.crafting.Find(id);bool installed=state.HasFacility(id);
                    WaterAction(content,ref y,id,recipe.name+(installed?" · 설치됨":" · 제작 보기"),recipe.description,OpenWaterCrafting,state.CanAct);
                }
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(592,y));
        }
        void OpenWaterCrafting() {craftTab=2;livingFilter=1;page="craft";message="";Render();}
        void WaterNote(Transform content,ref float y,string text)
        {
            var label=Label(text,20,y,764,88,23,Muted,content);
            float height=Mathf.Max(88,label.preferredHeight);label.rectTransform.sizeDelta=new Vector2(764,height);y+=height+26;
        }
        void WaterAction(Transform content,ref float y,string id,string title,string detail,System.Action action,bool enabled)
        {
            var card=Box("Water action:"+title,12,y,780,240,Raised,content);
            ItemPicture(id,20,20,68,68,card);
            Label(title,110,24,644,48,28,Paper,card).fontStyle=FontStyle.Bold;
            var text=Label(detail,20,104,740,72,23,Muted,card);
            float detailHeight=Mathf.Max(72,text.preferredHeight);text.rectTransform.sizeDelta=new Vector2(740,detailHeight);
            Button(title,20,196+detailHeight-72,740,66,action,enabled,true,card);
            float height=286+detailHeight-72;card.sizeDelta=new Vector2(780,height);y+=height+20;
        }
        void ManualWaterGathering(Transform content,ref float y)
        {
            WaterAction(content,ref y,"raw_water","생활용수를 모은다","설비·재료 불필요 · 2시간\n혼탁한 물 2병을 얻습니다. 마시기 전에 끓이거나 정수하세요.",()=>Apply(state.Craft("gather_water",definitions),"water"),state.CanCraft("gather_water",definitions));
        }
        void OpenWaterFromDock()
        {
            if(state.CurrentLocation=="home" && state.CollectorGuideStep>0)
            {
                if(state.CollectorGuideStep==1) { state.CollectorGuideStep=2; Save(); }
                waterTab=state.CollectorGuideStep==3?1:0;
            }
            OpenStoryPage("water");
        }
        void SelectWaterTab(int tab)
        {
            if(tab==1 && state.CollectorGuideStep==2)
            {
                state.CollectorGuideStep=3;
                Save();
            }
            waterTab=tab;
            OpenStoryPage("water");
        }
        void CollectorGuideHint()
        {
            if(state.CollectorGuideStep==0 || state.CurrentLocation!="home") return;
            string hint=state.CollectorGuideStep==1?"아래에서 ‘식수 관리’를 눌러 보세요.":
                state.CollectorGuideStep==2?"식수 관리에서 ‘물 확보’를 눌러 보세요.":"‘물 확보’에서 2·5·10시간 중 하나를 눌러 집수를 시작하세요.";
            // Sits in the header strip (376-432) like the tutorial link, never over the page title or the request badge.
            float x=48,w=804;
            var alert=root.Find("Survival alert") as RectTransform;
            if(alert!=null) { alert.sizeDelta=new Vector2(460,56); alert.Find("Fill").GetComponent<RectTransform>().sizeDelta=new Vector2(456,52); foreach(var label in alert.GetComponentsInChildren<Text>()) label.rectTransform.sizeDelta=new Vector2(396,48); foreach(Transform child in alert) if(child.name=="Chevron") child.gameObject.SetActive(false); x=520; w=332; }
            var strip=Box("Collector guide",x,376,w,56,EmberDim);
            Box("Collector guide edge",0,0,4,56,Amber,strip);
            var text=Label("집수 안내 "+state.CollectorGuideStep+"/3 · "+hint,16,0,w-24,56,alert!=null?18:22,AmberText,strip); text.alignment=TextAnchor.MiddleLeft;
        }
        void CollectorNotification()
        {
            if(!state.CollectorNoticePending) return;
            shortcuts=new System.Action[5];
            Box("Collection notice scrim",0,0,900,1600,new Color(0,0,0,.28f)).GetComponent<Image>().raycastTarget=true;
            Box("Collection notice",48,560,804,450,Raised);
            Label("집수통이 가득 찼다",80,600,740,54,34,Amber);
            Label("혼탁한 물 "+state.CollectorCapacity+"병을 모았다.\n집에서 시간 소모 없이 회수할 수 있다.\n회수하기 전까지 집수는 멈춘다.",80,678,740,148,26,Paper);
            Button(state.CurrentLocation=="home"?"물을 회수한다 · 0시간":"확인 · 귀가 후 회수",80,864,740,70,()=>{state.CollectorNoticePending=false;if(state.CanCollectWater)Apply(state.CollectWater(),"water");else{Save();Render();}},true,true);
        }
        void VitalsNotice()
        {
            if(string.IsNullOrEmpty(vitalsNotice)) return;
            // Sits just above the dock so the result it follows stays readable at the top of the page.
            var card=Box("Vitals notification",48,1160,804,224,Amber);
            card.GetComponent<Image>().raycastTarget=true;
            Box("Fill",2,2,800,220,EmberDim,card);
            ArtIcon(state.NeedsWaterWarning?0:state.NeedsSleepWarning?1:7,22,22,88,88,card);
            Label("생존 상태 경고",130,22,640,38,26,AmberText,card).fontStyle=FontStyle.Bold;
            Label(vitalsNotice,130,64,640,84,21,Paper,card);
            PrimaryButton("확인",604,150,180,56,()=>{vitalsNotice="";Render();},true,card);
        }
#if UNITY_EDITOR || VD_UI_VERIFICATION
        System.Collections.IEnumerator SmokeWaterAccess()
        {
            string dir=System.IO.Path.GetFullPath("Builds/WaterAccessVerification");System.IO.Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true;state.PlayerNickname="식수 검증";state.HomeStorage.Remove("water",8);state.Sleepiness=60;
            Render();yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"01-SHELTER-WARNINGS.png"));yield return null;
            if(survivalArt==null || !root.Find("Survival alert"))throw new System.Exception("Missing illustrated UI or warning.");
            ClickGameButton("식수 관리");ClickGameButton("물 확보");ClickGameButton("생활용수를 모은다");
            if(state.HomeStorage.Count("raw_water")!=2 || string.IsNullOrEmpty(vitalsNotice))throw new System.Exception("Gather button or alert failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"02-NOTIFICATION.png"));yield return null;
            ClickGameButton("확인");ClickGameButton("끓이기 · 정수");ClickGameButton("물을 끓인다");ClickGameButton("마시기");ClickGameButton("지금 물을 마신다");
            if(state.HomeStorage.Count("water")!=1 || state.Thirst!=0 || state.HydrationHours!=0 || state.NeedsWaterWarning)throw new System.Exception("Boil/drink UI failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"03-WATER-ACCESS.png"));yield return null;
            ClickGameButton("생활");ClickGameButton("잠자기");ClickGameButton("6시간 잔다");
            if(state.NeedsSleepWarning)throw new System.Exception("Sleep warning did not clear through UI.");
            if(!store.TryLoad(out var loaded,out string error) || loaded.Thirst!=6*definitions.Rates(state.Difficulty).thirstPerHour || loaded.Sleepiness!=0)throw new System.Exception("Hydration and sleep did not save: "+error);
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"04-RECOVERED.png"));yield return null;
            Debug.Log("VD_WATER_ACCESS_UI_OK: illustrated shelter, warnings, notification, gather, boil, drink, sleep and autosave.");
            Application.Quit();
        }
#endif
    }
}
