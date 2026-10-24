using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VanishingDays
{
    public class VanishingDaysGame : MonoBehaviour
    {
        [SerializeField, Tooltip("에디터 테스트 전용: 오프닝을 건너뜁니다.")]
        bool skipOpeningInEditor;
        OpeningScreenView openingView;
        static readonly Color Ink=new Color32(14,21,24,255), Panel=new Color32(22,30,32,255), Paper=new Color32(222,216,198,255), Muted=new Color32(144,153,148,255), Amber=new Color32(211,173,110,255);
        SurvivalState state;
        SurvivalDefinitions definitions;
        SessionSaveStore store;
        RectTransform root;
        Font font;
        Texture2D art;
        string page="home", message="", selectedLocation="home";
        bool confirmRestart;
        int selectedErasureDay;
        int selectedDiaryDay,selectedMorningDay;
        readonly Dictionary<string,int> transferQuantities=new Dictionary<string,int>();
        ScrollRect inventoryScroll;
        float inventoryScrollOffset;
        ScrollRect neighborhoodScroll;
        Vector2 neighborhoodOffset;
        bool neighborhoodPositioned;
        int craftTab,craftRenderedTab;
        ScrollRect craftScroll;
        readonly float[] craftOffsets=new float[3];
        Action[] shortcuts=new Action[3];
        string SavePath => store.Path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() { if(FindFirstObjectByType<VanishingDaysGame>()==null) new GameObject("Vanishing Days").AddComponent<VanishingDaysGame>(); }
        void Start()
        {
            Application.targetFrameRate=60; Application.runInBackground=true;
            definitions=SurvivalDefinitions.Load();
            store=new SessionSaveStore(Path.Combine(Application.persistentDataPath,"vanishing-days-v03.json"),definitions);
            font=Resources.Load<Font>("Fonts/NanumGothic-Regular");
            if(font==null) throw new InvalidOperationException("Bundled Korean font is missing: Resources/Fonts/NanumGothic-Regular.ttf");
            art=Resources.Load<Texture2D>("Art/SeoulErasure");
            var canvas=new GameObject("Diary Interface",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(900,1600); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            canvas.gameObject.AddComponent<Image>().color=Ink;
            root=Rect("Portrait viewport",canvas.transform,0,0,900,1600); root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);
            if(FindFirstObjectByType<EventSystem>()==null) new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var module=EventSystem.current.currentInputModule??EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if(module!=null && module.inputOverride==null) module.inputOverride=EventSystem.current.gameObject.AddComponent<OpeningInputBridge>();
            state=SurvivalState.NewGame(GameDifficulty.NORMAL,definitions);
#if UNITY_EDITOR || VD_UI_VERIFICATION
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdCraft")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/CraftingVerification/session.json"),definitions); StartCoroutine(SmokeCrafting()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdLife")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/DailyLifeVerification/session.json"),definitions); StartCoroutine(SmokeDailyLife()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdThirst")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/ThirstVerification/session.json"),definitions); StartCoroutine(SmokeThirst()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdWater")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/WaterVerification/session.json"),definitions); StartCoroutine(SmokeWater()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdSleepTrade")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/SleepTradeVerification/session.json"),definitions); StartCoroutine(SmokeSleepTrade()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdStorage")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/StorageVerification/session.json"),definitions); StartCoroutine(SmokeStorage()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdErasure")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/ErasureVerification/session.json"),definitions); StartCoroutine(SmokeErasure()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdVitals")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/VitalsVerification/session.json"),definitions); StartCoroutine(SmokeVitals()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdSmoke")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/SurvivalVerification/smoke-session.json"),definitions); BeginOpening(); StartCoroutine(Smoke()); return; }
            if(skipOpeningInEditor) { state.Story.OpeningCompleted=true; Render(); return; }
#endif
            if(store.Exists) { page="continue"; Render(); } else BeginOpening();
        }
        void Update()
        {
            var keyboard=Keyboard.current; if(keyboard==null || openingView!=null) return;
            if(keyboard.digit1Key.wasPressedThisFrame) shortcuts[0]?.Invoke();
            else if(keyboard.digit2Key.wasPressedThisFrame) shortcuts[1]?.Invoke();
            else if(keyboard.digit3Key.wasPressedThisFrame) shortcuts[2]?.Invoke();
        }
        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); return rect;
        }
        RectTransform Box(string name,float x,float y,float w,float h,Color color,Transform parent=null) { var rect=Rect(name,parent??root,x,y,w,h); var image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return rect; }
        Text Label(string text,float x,float y,float w,float h,int size,Color color,Transform parent=null)
        {
            var rect=Rect("Text",parent??root,x,y,w,h); var label=rect.gameObject.AddComponent<Text>(); label.font=font; label.text=text; label.fontSize=size; label.color=color; label.supportRichText=false; label.raycastTarget=false; label.lineSpacing=1.18f; label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Truncate; return label;
        }
        void Button(string text,float x,float y,float w,float h,Action click,bool enabled=true,bool accent=false,Transform parent=null)
        {
            var rect=Box(text,x,y,w,h,accent?new Color32(57,63,52,255):Panel,parent); rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>(); button.interactable=enabled;
            var colors=button.colors; colors.highlightedColor=new Color(1.3f,1.25f,1.1f); colors.pressedColor=new Color(.75f,.7f,.6f); colors.disabledColor=new Color(.5f,.5f,.5f,.65f); button.colors=colors;
            button.onClick.AddListener(()=>click());
            Label(text,18,12,w-36,h-20,h<64?22:26,enabled?(accent?Amber:Paper):Muted,rect);
            Box("Rule",0,h-1,w,1,new Color32(61,73,67,255),rect);
        }
        void Picture(float x,float y,float w,float h)
        {
            var image=Rect("Seoul",root,x,y,w,h).gameObject.AddComponent<RawImage>(); image.texture=art; image.raycastTarget=false;
            if(art!=null)
            {
                float desired=w/h, actual=art.width/(float)art.height;
                image.uvRect=actual>desired?new UnityEngine.Rect((1-desired/actual)/2,0,desired/actual,1):new UnityEngine.Rect(0,(1-actual/desired)/2,1,actual/desired);
            }
        }
        RectTransform ScrollArea(string name,float y,float height,out RectTransform content)
        {
            var area=Box(name,48,y,804,height,Panel); area.GetComponent<Image>().raycastTarget=true; area.gameObject.AddComponent<RectMask2D>();
            content=Rect("Content",area,0,0,804,height);
            var scroll=area.gameObject.AddComponent<ScrollRect>(); scroll.content=content; scroll.viewport=area; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45; return area;
        }
        void Paragraph(string text,float y,float height,int size=27)
        {
            ScrollArea("Scrollable text",y,height,out var content);
            var label=Label(text,18,12,768,height-24,size,Paper,content);
            float h=Mathf.Max(height,label.preferredHeight+30); content.sizeDelta=new Vector2(804,h); label.rectTransform.sizeDelta=new Vector2(768,h-24);
        }
        void Clear()
        {
            foreach(Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            shortcuts=new Action[3]; Box("Background",0,0,900,1600,Ink);
        }
        void Render()
        {
            if(craftScroll!=null && craftScroll.gameObject.activeInHierarchy) craftOffsets[craftRenderedTab]=craftScroll.content.anchoredPosition.y;
            craftScroll=null;
            if(neighborhoodScroll!=null && neighborhoodScroll.gameObject.activeInHierarchy) neighborhoodOffset=neighborhoodScroll.content.anchoredPosition;
            neighborhoodScroll=null;
            if(inventoryScroll!=null && inventoryScroll.gameObject.activeInHierarchy) inventoryScrollOffset=inventoryScroll.content.anchoredPosition.y;
            inventoryScroll=null;
            Clear();
            if(page=="continue") { ContinuePage(); return; }
            Header(); Dock();
            if(confirmRestart) { RestartPage(); return; }
            if(state.IsGameOver && page!="menu") { GameOver(); return; }
            if(state.PendingEncounter!=null) { EncounterPage(); return; }
            if(state.HasActiveCrowdIncident) { CrowdIncidentPage(); return; }
            if(state.PendingDongrimMeeting) { DongrimMeetingPage(); return; }
            if(state.HasActiveCompanion) { CompanionPage(); return; }
            if(page!="diaries" && page!="morning")
            {
                if(state.NextDiary!=null) { DiaryPage(true); return; }
                if(state.HasPendingMorning) { selectedMorningDay=0; MorningPage(); return; }
            }
            switch(page)
            {
                case "map": Map(); break;
                case "location": LocationDetails(); break;
                case "radio": RadioPage(); break;
                case "erasure": ErasureMapPage(); break;
                case "inventory": InventoryPage(); break;
                case "kitchen": Kitchen(); break;
                case "craft": CraftingPage(); break;
                case "sleep": SleepPage(); break;
                case "diaries": DiaryPage(false); break;
                case "morning": MorningPage(); break;
                case "neighbor": NeighborPage(); break;
                case "companion": CompanionPage(); break;
                case "security": Security(); break;
                case "journal": Journal(); break;
                case "menu": Menu(); break;
                default: if(state.CurrentLocation=="home") Home(); else Explore(); break;
            }
            Label("VANISHING DAYS  /  행동 후 자동 저장",48,1563,804,26,16,Muted);
        }
        void Header()
        {
            Label("소멸일지",48,28,420,60,40,Paper);
            Label("DAY "+state.day.ToString("00"),650,36,202,48,30,Amber);
            Label(state.CalendarDate.ToString("yyyy.MM.dd")+"  "+state.hour.ToString("00")+":00  ·  "+(state.IsNight(definitions.TEMP_BALANCE)?"밤":"낮")+"  ·  "+definitions.Location(state.CurrentLocation).name,48,102,804,40,24,Muted);
            Label("체력  "+state.Vitals.CurrentHP+" / "+state.Vitals.BaseMaxHP,48,158,470,48,30,Paper);
            int cap=state.Vitals.RecoverableMaxHP(definitions.TEMP_BALANCE);
            Label("회복 상한  "+cap,538,166,314,40,25,Amber);
            Box("HP blocked",48,226,804,20,new Color32(96,56,52,255));
            Box("HP recoverable",48,226,804*(cap/(float)state.Vitals.BaseMaxHP),20,new Color32(55,68,62,255));
            Box("Current HP",48,226,804*(Mathf.Min(state.Vitals.CurrentHP,cap)/(float)state.Vitals.BaseMaxHP),20,Amber);
            Label("허기 "+state.Vitals.Hunger+" · 목마름 "+state.Thirst+"/100 · 부상 "+state.Vitals.Injuries.Count+" · 힘 "+state.Strength(definitions)+" · 방어 "+state.Defense(definitions),48,264,804,32,22,state.Thirst>=60?Amber:Muted);
            Label("정신력 "+state.Vitals.CurrentMental+" / "+state.Vitals.MentalCap,48,304,400,32,23,Paper);
            Label("졸림 "+state.Sleepiness+" / 100 · 수면 "+state.SleepHours+" / 6h",448,306,404,30,21,Muted);
            Box("Mental blocked",48,343,804,12,new Color32(96,56,52,255));
            Box("Mental capacity",48,343,804*(state.Vitals.MentalCap/(float)state.Vitals.BaseMaxMental),12,new Color32(55,68,62,255));
            Box("Mental current",48,343,804*(state.Vitals.CurrentMental/(float)state.Vitals.BaseMaxMental),12,new Color32(129,161,177,255));
            string[] names={"생활","외출","물품","기록"}, ids={"home","map","inventory","journal"};
            for(int i=0;i<4;i++) { string id=ids[i]; Button(names[i],48+i*206,362,186,62,()=>{page=id; message=""; Render();},state.CanAct && !state.HasLifeNotice,page==id); }
        }
        void Dock()
        {
            Box("Dock",48,1412,804,138,Panel);
            Label(state.WaterStatus(definitions),66,1422,768,38,21,Muted);
            bool home=state.CurrentLocation=="home";
            Button(home?(state.IsNight(definitions.TEMP_BALANCE)?"아침까지 머무른다":"밤까지 머무른다"):"집으로 돌아간다",66,1477,420,58,()=>{if(home) Apply(state.Rest(definitions,true)); else SelectLocation("home");},state.CanAct && !state.HasLifeNotice,true);
            Button("제작",502,1477,190,58,()=>{page="craft";message="";Render();},home && state.CanAct && !state.HasLifeNotice);
            Button("메뉴",708,1477,126,58,()=>{page="menu"; Render();},(state.CanAct || state.IsGameOver) && !state.HasLifeNotice);
        }
        void Hero(string title)
        {
            Picture(48,454,804,220); Label(title,48,702,804,60,36,Paper);
        }
        void Choice(int index,string title,string detail,float y,Action action,bool enabled=true)
        {
            shortcuts[index]=enabled?action:null; Button((index+1).ToString("00")+"  "+title+"\n     "+detail,48,y,804,94,action,enabled);
        }
        string ReportText => string.IsNullOrEmpty(message)?state.lastReport:message;
        void Home()
        {
            Hero("송파구 자취방");
            Label("집: 스팸 "+state.HomeStorage.Count("spam")+" · 참치 "+state.HomeStorage.Count("tuna")+" · 쌀 "+definitions.Quantity("rice",state.HomeStorage.Count("rice")),48,768,804,34,23,Muted);
            Paragraph(string.IsNullOrEmpty(message) && state.journal.Count==1?definitions.Location("home").description:ReportText,820,150);
            Choice(0,"요리하고 식사한다","집에 보관한 식량을 사용한다",1010,()=>{page="kitchen"; message=""; Render();});
            Choice(1,"잠을 잔다","하루 6시간 필요 · 수면 시간을 선택한다",1122,()=>{page="sleep";message="";Render();});
            Choice(2,state.Story.HasRadio?"라디오를 켠다":"제작대를 확인한다",state.Story.HasRadio?"SP 기지국의 방송을 수신한다":"음식 · 무기 · 생활용품을 만든다",1234,()=>{if(state.Story.HasRadio) OpenRadio(); else {page="craft"; message=""; Render();}});
            Button("옆집 안부",48,1342,390,48,()=>{page="neighbor";message="";Render();});
            Button("동행 약속",462,1342,390,48,()=>{page="companion";message="";Render();},state.CompanionAvailable);
        }
        void Explore()
        {
            var location=definitions.Location(state.CurrentLocation);
            if(location.storyOnly) { SupplySitePage(); return; }
            Hero(location.name); Label(state.LootSummary(location.id,definitions),48,772,804,36,23,Amber);
            Paragraph(string.IsNullOrEmpty(message)?location.description:message,822,158);
            Choice(0,"주변을 수색한다",state.HasSearchWork(location.id,definitions)?location.searchHours+"시간 · 남은 물자를 확인한다":"물자 소진 · 지도에서 다음 목적지를 확인",1010,()=>Apply(state.Search(definitions)),state.HasSearchWork(location.id,definitions));
            Choice(1,"소지 물품을 확인한다","식량과 도구를 정리한다",1122,()=>{page="inventory"; Render();});
            Choice(2,"집으로 돌아간다",location.travelHours+"시간 · 귀갓길에도 조우할 수 있다",1234,()=>SelectLocation("home"));
            if(state.CompanionAvailable) Button("동행 약속 확인",48,1342,804,48,()=>{page="companion";message="";Render();});
        }
        void Map()
        {
            Label("송파구 일대",48,462,804,60,36,Paper);
            Label(state.WorldHint(definitions),48,530,804,76,22,Amber);
            var places=definitions.locations.Where(x=>state.IsLocationUnlocked(x.id,definitions)).ToArray();
            var viewport=Box("Map viewport",48,610,804,450,Panel); viewport.gameObject.AddComponent<RectMask2D>(); viewport.GetComponent<Image>().raycastTarget=true;
            var world=Rect("Map content",viewport,0,0,1440,900);
            neighborhoodScroll=viewport.gameObject.AddComponent<ScrollRect>(); neighborhoodScroll.content=world; neighborhoodScroll.viewport=viewport; neighborhoodScroll.horizontal=true; neighborhoodScroll.vertical=true; neighborhoodScroll.movementType=ScrollRect.MovementType.Clamped; neighborhoodScroll.scrollSensitivity=60;
            var map=world.gameObject.AddComponent<NeighborhoodMapGraphic>();
            map.CurrentLocation=state.CurrentLocation; map.VisibleLocations=places.Select(x=>x.id).ToArray(); map.raycastTarget=false;
            foreach(var location in places)
            {
                Vector2 p=NeighborhoodMapGraphic.Position(location.id); float x=p.x*1440,y=p.y*900;
                bool current=location.id==state.CurrentLocation;
                var hit=Box("지도:"+location.id,x-88,y-40,176,112,Color.clear,world); hit.GetComponent<Image>().raycastTarget=true;
                var button=hit.gameObject.AddComponent<Button>(); button.targetGraphic=hit.GetComponent<Image>(); button.onClick.AddListener(()=>SelectLocation(location.id));
                Box("Map label",x-88,y+29,176,36,Ink,world);
                var label=Label(string.IsNullOrEmpty(location.mapLabel)?location.name:location.mapLabel,x-88,y+30,176,34,21,current?Amber:Paper,world); label.alignment=TextAnchor.MiddleCenter;
            }
            if(!neighborhoodPositioned) { CenterNeighborhood(); neighborhoodPositioned=true; } else world.anchoredPosition=neighborhoodOffset;
            Button("←",48,1074,128,56,()=>PanNeighborhood(190,0));
            Button("↑",190,1074,128,56,()=>PanNeighborhood(0,-160));
            Button("↓",332,1074,128,56,()=>PanNeighborhood(0,160));
            Button("→",474,1074,128,56,()=>PanNeighborhood(-190,0));
            Button("현재 위치",616,1074,236,56,CenterNeighborhood);
            Label("지도를 끌거나 화살표로 이동 · 아래에서 장소 선택",48,1140,804,30,20,Muted);
            ScrollArea("Location records",1180,192,out var content); float row=10;
            foreach(var location in places)
            {
                bool meeting=state.Story.HasMetDongrim && location.id==state.DongrimMeetingLocation;
                Button(location.name+(location.id==state.CurrentLocation?" · 현재":"")+(meeting?" · 동림 약속":""),12,row,780,52,()=>SelectLocation(location.id),true,meeting,content);
                Label(state.LootSummary(location.id,definitions),30,row+57,744,32,21,Muted,content);
                row+=104;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(192,row));
        }
        void PanNeighborhood(float x,float y)
        {
            var p=neighborhoodScroll.content.anchoredPosition+new Vector2(x,y);
            neighborhoodOffset=new Vector2(Mathf.Clamp(p.x,-636,0),Mathf.Clamp(p.y,0,450));
            neighborhoodScroll.StopMovement(); neighborhoodScroll.content.anchoredPosition=neighborhoodOffset;
        }
        void CenterNeighborhood()
        {
            Vector2 p=NeighborhoodMapGraphic.Position(state.CurrentLocation);
            neighborhoodOffset=new Vector2(Mathf.Clamp(402-p.x*1440,-636,0),Mathf.Clamp(p.y*900-225,0,450));
            neighborhoodScroll.StopMovement(); neighborhoodScroll.content.anchoredPosition=neighborhoodOffset;
        }
        void SelectLocation(string id) { if(!state.CanAct || !state.IsLocationUnlocked(id,definitions)) return; selectedLocation=id; page="location"; message=""; Render(); }
        void LocationDetails()
        {
            var location=definitions.Location(selectedLocation); bool current=location.id==state.CurrentLocation;
            int hours=current?0:location.id=="home"?definitions.Location(state.CurrentLocation).travelHours:location.travelHours;
            var arrival=state.CalendarDate.AddHours(state.hour+hours); bool night=arrival.Hour<definitions.TEMP_BALANCE.dayStarts || arrival.Hour>=definitions.TEMP_BALANCE.nightStarts;
            Label("위치 정보  /  "+(current?"현재 위치":"이동 전 확인"),48,462,804,48,26,Muted);
            Label(location.name,48,532,804,64,42,Paper);
            Paragraph(location.description,624,230,28);
            Label(current?"지금 이곳에 머무르고 있다.":"이동 "+hours+"시간  ·  "+arrival.ToString("MM.dd HH:mm")+" 도착 예정",48,882,804,48,27,Amber);
            string risk=night?"밤에는 만남이 드물지만 약탈꾼에게 발견되면 위험하다.":"낮에는 길거리나 건물 안에서 다른 생존자와 마주칠 수 있다.";
            string resources=state.LootSummary(location.id,definitions,true)+(location.storyOnly?"\n낮에 현장을 살피고 접근 여부를 선택한다.":location.id=="home"?"\n요리 · 물자 보관 · 휴식 · 부상 치료":"\n수색 "+location.searchHours+"시간 / 회");
            if(state.Story.HasMetDongrim && location.id==state.DongrimMeetingLocation) resources+="\n\n"+state.DongrimHint(definitions);
            Label("잔량·위험 정보 · 위아래로 스크롤",48,938,804,28,18,Muted);
            Paragraph(resources+"\n\n"+risk,974,212,25);
            Button(current?"현재 위치":"이동하기",48,1212,804,76,()=>Apply(state.Travel(location.id,definitions)),!current && state.CanAct,true);
            Button("지도로 돌아가기",48,1310,390,70,()=>{page="map"; Render();});
            if(current) Button("생활 화면",462,1310,390,70,()=>{page="home"; Render();});
        }
        string LootNames(LocationDefinition location)
        {
            var names=new System.Collections.Generic.List<string>();
            foreach(var item in location.loot) names.Add(definitions.Item(item.id).name);
            return string.Join(" · ",names);
        }
        void OpenRadio()
        {
            if(state.Story.HasHeardSPBroadcast && state.CanUseRadio && state.MorningIssues.Count>0) { page="morning";selectedMorningDay=0;Render(); return; }
            bool available=state.CanAct && state.CanUseRadio;
            Apply(state.ListenRadio(definitions),available?"radio":"home");
        }
        void RadioPage()
        {
            if(!state.Story.SupplyRumorUnlocked || !state.CanUseRadio) { if(state.CurrentLocation=="home") Home(); else Explore(); return; }
            var script=definitions.radioStory;
            var broadcast=state.LastRadioBroadcastId==script.firstBroadcast.id?script.firstBroadcast:script.generalBroadcast;
            Label("라디오 수신 기록",48,462,804,56,36,Paper);
            Label(broadcast.title,48,540,804,54,25,Amber);
            Label("방송 본문 · 위아래로 스크롤",48,584,804,24,18,Muted);
            Paragraph(broadcast.body,610,460,27);
            Paragraph("[정보 카드]\n"+script.clueCard,1090,128,23);
            Button("전국 소멸 지도·수신 기록",48,1236,804,52,()=>{page="erasure";selectedErasureDay=0;Render();},state.ErasureNewsEnabled);
            Button("방송 기록 닫기",48,1310,390,70,()=>{page="home";message="";Render();},true,true);
            Button("제보 장소 지도 보기",462,1310,390,70,()=>{page="map";message="";Render();});
        }
        void SupplySitePage()
        {
            var script=definitions.radioStory;
            Label("제보 장소  /  현장 기록",48,462,804,48,26,Muted);
            Label(definitions.Location("supply_site").name,48,532,804,64,36,Paper);
            bool completed=state.Story.CrowdIncidentResolved, night=state.IsNight(definitions.TEMP_BALANCE);
            string text=completed?script.completed:night?script.night:state.Crowd.Step==CrowdStoryStep.Observing?script.observe:script.approach;
            Paragraph(text,622,350,28);
            bool approach=state.CanApproachCrowd(definitions);
            Choice(0,"한발 물러나 상황을 살핀다","입구와 인파의 움직임을 확인한다",1000,()=>Apply(state.ObserveSupplySite(definitions)),approach);
            Choice(1,"배분 구역 가까이 간다",completed?"이미 현장을 확인했다":night?"현장 접근은 낮에 가능하다":"사람들이 모인 입구로 접근한다",1112,()=>Apply(state.ApproachCrowd(definitions)),approach);
            Choice(2,"돌아간다","지도를 확인하고 다음 행선지를 정한다",1224,()=>{page="map";message="";Render();});
        }
        void CrowdIncidentPage()
        {
            var script=definitions.radioStory; string title,body;
            switch(state.Crowd.Step)
            {
                case CrowdStoryStep.Panic: title="출구가 보이지 않는다"; body=script.panic; break;
                case CrowdStoryStep.Rescue: title="좁은 통로"; body=script.rescue; break;
                case CrowdStoryStep.Disappearance: title="사라진 골목"; body=script.disappearance; break;
                default:title="최동림"; body=script.introduction; break;
            }
            Label("현장 기록",48,462,804,48,26,Muted); Label(title,48,532,804,64,40,Paper);
            Paragraph(state.Script(body),622,state.Crowd.Step==CrowdStoryStep.Panic?430:550,28);
            if(state.Crowd.Step==CrowdStoryStep.Panic)
            {
                Choice(0,"가방을 놓고 몸을 틀어 빠져나온다","소지품 일부를 잃지만 부상을 피한다",1100,()=>Apply(state.ChooseCrowdResponse(true,definitions)));
                Choice(1,"가방을 붙잡고 버틴다","물품을 지키지만 부상을 입는다",1220,()=>Apply(state.ChooseCrowdResponse(false,definitions)));
            }
            else
            {
                if(state.Crowd.Step==CrowdStoryStep.Rescue) Paragraph(state.Crowd.Consequence,1192,94,22);
                Button(state.Crowd.Step==CrowdStoryStep.Introduction?"기록하고 계속한다":"다음",48,1310,804,70,()=>Apply(state.ContinueCrowdStory(definitions)),true,true);
            }
        }
        void Kitchen() { craftTab=0; CraftingPage(); }
        void CraftingPage()
        {
            Label("제작",48,462,804,60,36,Paper);
            string[] tabs={"음식","무기","생활용품"};
            for(int i=0;i<tabs.Length;i++) { int tab=i; Button(tabs[i],48+i*276,536,252,62,()=>{craftTab=tab;page="craft";message="";Render();},true,craftTab==i); }
            string[] notes={state.UnlimitedWater?"집의 식재료로 요리한다. 쉬움은 식수·조리용 물이 무제한이다.":"집의 식재료와 물로 요리한다. 요리 1회에 물 1병이 필요하다.","하위 무기를 재료로 상위 무기를 만든다. 집 창고의 재료를 사용하며, 장착한 재료 무기는 먼저 벗어야 한다. 무기는 1개만 장착한다.","문 강화·정수·작업대·침구를 만든다. 설치한 설비는 집에 남고 해당 작업이나 생존 효과를 제공한다."};
            Paragraph(string.IsNullOrEmpty(message)?notes[craftTab]:message,614,114,24);
            var area=ScrollArea("Craft recipes",748,624,out var content); craftScroll=area.GetComponent<ScrollRect>(); craftRenderedTab=craftTab; float y=12;
            if(craftTab==0)
            {
                foreach(var recipe in definitions.recipes)
                {
                    Label(recipe.name,18,y,768,40,28,Paper,content);
                    Label(Materials(state.CookingIngredients(recipe))+" · "+recipe.hours+"시간",18,y+46,768,58,22,Muted,content);
                    Button("조리한다",18,y+106,366,54,()=>Apply(state.Cook(recipe.id,definitions),"craft"),state.CanCook(recipe,definitions),true,content);
                    Button("먹는다 · "+(state.HomeStorage.Count(recipe.result)+state.Backpack.Count(recipe.result)),402,y+106,384,54,()=>Apply(state.Eat(recipe.result,definitions),"craft"),state.Backpack.Count(recipe.result)>0 || state.CurrentLocation=="home" && state.HomeStorage.Count(recipe.result)>0,false,content);
                    y+=184;
                }
            }
            else
            {
                if(craftTab==2) foreach(var device in definitions.security.Where(x=>x.id.StartsWith("door"))) { SecurityCraftRow(device,content,y); y+=234; }
                foreach(var recipe in definitions.crafting.recipes.Where(x=>x.category==(craftTab==1?"weapons":"living")))
                {
                    bool installed=recipe.facility && state.HasFacility(recipe.id);
                    bool facilityReady=string.IsNullOrEmpty(recipe.requiresFacility) || state.HasFacility(recipe.requiresFacility);
                    Label(recipe.name,18,y,768,40,28,Paper,content);
                    Label(recipe.description,18,y+46,768,60,22,Muted,content);
                    Label((recipe.ingredients.Length==0?"재료 소모 없음 · 집수 작업":Materials(recipe.ingredients))+" · "+recipe.hours+"시간",18,y+108,768,60,22,Muted,content);
                    string status=installed?"설치 완료":!facilityReady?"필요 설비: "+definitions.crafting.Find(recipe.requiresFacility).name:state.CanCraft(recipe.id,definitions)?"제작 가능":"집의 재료 부족";
                    Label(status,18,y+180,500,36,21,installed?Muted:Amber,content);
                    Button(installed?"설치됨":recipe.facility?"설치한다":"제작한다",540,y+174,246,58,()=>Apply(state.Craft(recipe.id,definitions),"craft"),state.CanCraft(recipe.id,definitions),true,content);
                    y+=254;
                }
                if(craftTab==2) foreach(var device in definitions.security.Where(x=>!x.id.StartsWith("door"))) { SecurityCraftRow(device,content,y); y+=234; }
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(624,y));
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(craftOffsets[craftTab],0,content.sizeDelta.y-624));
        }
        string Materials(ItemStack[] costs) => string.Join(" · ",costs.Select(x=>definitions.Item(x.id).name+" "+definitions.Quantity(x.id,state.HomeStorage.Count(x.id))+" / "+definitions.Quantity(x.id,x.amount)));
        void SecurityCraftRow(SecurityDefinition device,Transform content,float y)
        {
            bool installed=state.InstalledSecurity.Contains(device.id), prerequisite=string.IsNullOrEmpty(device.requiresSecurity) || state.InstalledSecurity.Contains(device.requiresSecurity);
            Label(device.name,18,y,768,40,28,Paper,content);
            Label("침입 저항 +"+device.protection+" · 외부 주목 +"+device.attention+" · "+device.hours+"시간",18,y+46,768,32,22,Muted,content);
            Label(Materials(device.ingredients),18,y+86,768,60,22,Muted,content);
            Label(installed?"보강 완료":!prerequisite?"선행: "+definitions.Security(device.requiresSecurity).name:state.CanInstallSecurity(device.id,definitions)?"보강 가능":"집의 재료 부족",18,y+168,500,36,21,Amber,content);
            Button(installed?"설치됨":"보강한다",540,y+158,246,58,()=>Apply(state.InstallSecurity(device.id,definitions),"craft"),state.CanInstallSecurity(device.id,definitions),true,content);
        }
        string Cost(ItemStack[] ingredients) { var costs=new string[ingredients.Length]; for(int i=0;i<costs.Length;i++) costs[i]=definitions.Item(ingredients[i].id).name+" "+definitions.Quantity(ingredients[i].id,ingredients[i].amount); return string.Join(" · ",costs); }
        void InventoryPage()
        {
            Label("소지품과 집의 물자",48,462,804,60,36,Paper);
            bool atHome=state.CurrentLocation=="home";
            Paragraph(string.IsNullOrEmpty(message)?"수량을 입력하고 챙기기·보관을 누른다. 쌀은 g 단위다. 약품으로 부상 치료와 체력 회복이 가능하다.":message,542,108,25);
            Button("전체 보관",48,666,264,52,()=>Apply(state.StoreAll(definitions),"inventory"),atHome && state.CanAct && definitions.items.Any(x=>x.id!="burner" && state.Backpack.Count(x.id)>0),true);
            Label(atHome?"소지품을 집으로 · 착용 장비 제외":"물품 정리는 집에서 가능하다",330,676,522,34,22,Muted);
            var area=ScrollArea("Inventory",734,638,out var content); inventoryScroll=area.GetComponent<ScrollRect>(); float y=12;
            foreach(var item in definitions.items.OrderByDescending(x=>x.injuriesTreated))
            {
                int pack=state.Backpack.Count(item.id), home=state.HomeStorage.Count(item.id); bool equipped=state.Equipment.Contains(item.id);
                if(pack+home==0 && !equipped) continue;
                Label(item.name+(equipped?" · 착용 중":""),18,y,768,40,28,Paper,content);
                Label("소지 "+definitions.Quantity(item.id,pack)+" / 집 "+definitions.Quantity(item.id,home)+(item.id=="water"?" · 500ml · 식수 자동 소비":item.injuriesTreated>0?" · 치료 "+definitions.TEMP_BALANCE.treatmentHours+"시간 · 체력 +"+item.healing:""),18,y+48,768,36,23,Muted,content);
                float buttonX=18;
                bool canTransfer=item.id!="burner" && atHome;
                if(canTransfer)
                {
                    Label("수량"+(item.id=="rice"?" (g)":""),18,y+107,170,34,22,Muted,content);
                    Button("−",192,y+96,66,52,()=>ChangeTransferQuantity(item.id,-1,Math.Max(pack,home)),true,false,content);
                    var inputRect=Box("수량:"+item.id,270,y+96,202,52,Ink,content); inputRect.GetComponent<Image>().raycastTarget=true;
                    var input=inputRect.gameObject.AddComponent<InputField>(); input.targetGraphic=inputRect.GetComponent<Image>();
                    var text=Label("",8,4,186,44,24,Paper,inputRect); text.alignment=TextAnchor.MiddleCenter;
                    input.textComponent=text; input.contentType=InputField.ContentType.IntegerNumber; input.keyboardType=TouchScreenKeyboardType.NumberPad; input.characterLimit=9;
                    input.text=TransferQuantity(item.id).ToString();
                    input.onValueChanged.AddListener(value=>transferQuantities[item.id]=int.TryParse(value,out int quantity)?Math.Max(0,quantity):0);
                    Button("+",484,y+96,66,52,()=>ChangeTransferQuantity(item.id,1,Math.Max(pack,home)),true,false,content);
                    Label(item.id=="rice"?"1g 단위":"1개 단위",578,y+107,208,34,22,Muted,content);
                    Button("챙기기",buttonX,y+158,230,52,()=>Apply(state.Transfer(item.id,true,TransferQuantity(item.id),definitions),"inventory"),home>0,false,content); buttonX+=250;
                    Button("보관",buttonX,y+158,230,52,()=>Apply(state.Transfer(item.id,false,TransferQuantity(item.id),definitions),"inventory"),pack>0,false,content); buttonX+=250;
                }
                float actionY=y+(canTransfer?158:96);
                if(item.injuriesTreated>0) Button("치료",buttonX,actionY,230,52,()=>Apply(state.TreatInjury(item.id,definitions),"inventory"),state.CanAct && state.Vitals.Injuries.Count>0 && (pack>0 || (atHome && home>0)),true,content);
                else if(item.consumable) Button("먹기",buttonX,actionY,230,52,()=>Apply(state.Eat(item.id,definitions),"inventory"),pack>0 || (atHome && home>0),true,content);
                else if(item.strength>0 || item.defense>0) Button(equipped?"벗기":"착용",buttonX,actionY,230,52,()=>Apply(state.Equip(item.id,definitions),"inventory"),true,true,content);
                y+=canTransfer?240:178;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(638,y));
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(inventoryScrollOffset,0,content.sizeDelta.y-638));
        }
        int TransferQuantity(string id) => transferQuantities.TryGetValue(id,out int quantity)?quantity:1;
        void ChangeTransferQuantity(string id,int change,int maximum)
        {
            transferQuantities[id]=Mathf.Clamp(TransferQuantity(id)+change,1,Math.Max(1,maximum)); Render();
        }
        void Security() { craftTab=2; CraftingPage(); }
        void DongrimMeetingPage()
        {
            Label("최동림 · "+(state.TotalDongrimMeetings+1)+"번째 만남",48,462,804,60,36,Paper);
            Paragraph(state.DongrimDialogue(definitions),550,500,27);
            Label("대화 본문 · 위아래로 스크롤",48,1060,804,26,18,Muted);
            Choice(0,"식량을 건네고 붕대를 받는다","소지한 참치캔·스팸·라면 중 1개 ↔ 붕대 1개",1100,()=>Apply(state.ResolveDongrimMeeting(true,definitions)),state.CanTradeWithDongrim);
            Choice(1,"확인한 길과 소식을 나눈다","물자 소모 없이 대화 · 새로운 길과 다음 약속 기록",1220,()=>Apply(state.ResolveDongrimMeeting(false,definitions)));
        }
        void Journal()
        {
            Label("생존 기록",48,462,804,60,36,Paper); Label("작성자 : "+state.PlayerNickname,48,540,420,48,26,Muted);
            if(state.ErasureNewsEnabled) Button("전국 소멸 지도",490,530,362,58,()=>{page="erasure";selectedErasureDay=0;Render();});
            Button("날짜별 일기",48,606,252,52,()=>{page="diaries";selectedDiaryDay=0;Render();});
            Button("아침 수신함",324,606,252,52,()=>{page="morning";selectedMorningDay=0;Render();},state.CanUseRadio && state.MorningIssues.Count>0);
            Button("동행 약속",600,606,252,52,()=>{page="companion";message="";Render();},state.CompanionAvailable);
            ScrollArea("Journal",684,688,out var content); float y=18;
            foreach(var entry in state.journal) { var text=Label(entry,18,y,768,200,25,Paper,content); float h=Mathf.Max(88,text.preferredHeight+20); text.rectTransform.sizeDelta=new Vector2(768,h); y+=h+28; Box("Entry rule",18,y,768,1,new Color32(61,73,67,255),content); y+=28; }
            content.sizeDelta=new Vector2(804,Mathf.Max(688,y));
        }
        void DiaryPage(bool pending)
        {
            var entry=pending?state.NextDiary:state.Diaries.Find(x=>x.day==selectedDiaryDay)??state.Diaries.LastOrDefault();
            Label(pending?"하루의 기록":"날짜별 기록지",48,462,804,60,36,Paper);
            Paragraph(entry?.body??"아직 완성된 하루 기록이 없다. 날짜가 바뀌면 오늘 실제로 겪은 일을 정리한다.",542,548,27);
            Label("본문은 위아래로 스크롤해서 읽을 수 있다",48,1090,804,20,16,Muted);
            ScrollArea("Diary dates",1110,148,out var content); float y=10;
            foreach(var date in state.Diaries)
            {
                int id=date.day;
                Button("기록지 #"+id.ToString("000")+(date.read?" · 확인":" · 새 기록"),12,y,780,48,()=>{selectedDiaryDay=id;page="diaries";Render();},true,date==entry,content); y+=58;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(148,y));
            Button(pending?"읽고 계속한다":"확인하고 닫는다",48,1298,390,76,()=>{if(pending && entry!=null) state.ReadDiary(entry.day); else {foreach(var date in state.Diaries)date.read=true;page="journal";} Save();Render();},true,true);
            Button("기록 모아 보기",462,1298,390,76,()=>{page="diaries";selectedDiaryDay=entry?.day??0;Render();});
        }
        void MorningPage()
        {
            if(!state.CanUseRadio)
            {
                Label("아침 수신함",48,462,804,60,36,Paper); Paragraph("집에 둔 라디오는 외부에서 들을 수 없다. 라디오를 챙기거나 집에 돌아와 수신 기록을 확인하자.",550,450);
                Button("돌아가기",48,1298,804,76,()=>{page="journal";Render();}); return;
            }
            var issue=state.MorningIssues.Find(x=>x.day==selectedMorningDay)??state.MorningIssues.Find(x=>!x.read)??state.MorningIssues.LastOrDefault();
            Label("아침 수신함",48,462,804,60,36,Paper);
            Label(issue==null?"아침 8시에 다음 소식이 준비된다.":"DAY "+issue.day.ToString("00")+" · "+(issue.kind=="first"?"첫 물자 제보":"SP 소식")+" · "+(issue.read?"확인한 방송":"미청취"),48,536,804,44,24,Amber);
            Label("방송 본문 · 위아래로 스크롤",48,580,804,18,15,Muted);
            Paragraph(issue?.body??"아직 준비된 아침 소식이 없다.",598,492,26);
            ScrollArea("Morning dates",1110,148,out var content); float y=10;
            foreach(var date in state.MorningIssues)
            {
                int id=date.day;
                Button("DAY "+id.ToString("00")+" · "+(date.kind=="first"?"첫 방송":date.kind=="legacy"?"이전 수신":"아침 소식")+(date.read?" · 확인":" · 미청취"),12,y,780,48,()=>{selectedMorningDay=id;page="morning";Render();},true,date==issue,content); y+=58;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(148,y));
            Button("이날 방송 확인",48,1298,390,76,()=>{if(issue!=null)state.ReadMorning(issue.day,definitions);state.DismissMorning();if(page=="morning")page="home";Save();Render();},issue!=null,true);
            Button("나머지는 보관·닫기",462,1298,390,76,()=>{state.DismissMorning();if(page=="morning")page="home";Save();Render();});
        }
        void NeighborPage()
        {
            Label("옆집의 안부",48,462,804,60,36,Paper);
            string text=state.NeighborStage==0?"찬장에 빌려 쓰던 반찬 그릇이 남아 있다. 옆집 노인에게 돌려드리며 안부를 물을 수 있다.":state.NeighborStage==2?definitions.lifeStory.neighborQuiet:"벽을 두 번 두드리고 두 번 답하는 작은 인사. 오늘의 안부를 물을 수 있다.";
            Paragraph(string.IsNullOrEmpty(message)?text:message,550,540,27);
            Label("대화 본문 · 위아래로 스크롤",48,1092,804,22,17,Muted);
            Button(state.NeighborStage==0?"그릇을 돌려드린다":"벽을 두 번 두드린다",48,1120,804,70,()=>Apply(state.GreetNeighbor(definitions),"neighbor"),state.CanAct && state.CurrentLocation=="home" && state.NeighborLastDay<state.day && state.NeighborStage!=2,true);
            Button("문 앞에서 직접 확인한다 · 1시간",48,1212,804,70,()=>Apply(state.CheckNeighbor(definitions),"neighbor"),state.CanAct && state.CurrentLocation=="home" && state.NeighborStage==2);
            Button("생활로 돌아가기",48,1310,804,70,()=>{page="home";message="";Render();});
        }
        void CompanionPage()
        {
            bool active=state.HasActiveCompanion;
            Label(state.Companion==CompanionStep.Jiwoo?"처음 만난 한지우":active?"함께 돌아오는 사람":"동행 약속",48,462,804,60,36,Paper);
            Paragraph(active?state.CompanionText(definitions):string.IsNullOrEmpty(message)?state.CompanionHint:message,550,570,27);
            Label("대화 본문 · 위아래로 스크롤",48,1128,804,26,18,Muted);
            if(state.Companion==CompanionStep.Decision)
            {
                Choice(0,"오늘은 먼저 돌아간다","동림의 상태를 살피고 함께 철수 · 1시간",1160,()=>Apply(state.ContinueCompanion(definitions,true),"companion"));
                Choice(1,"입구의 표식만 더 확인한다","안쪽 진입 없이 기록하고 귀환 · 2시간",1272,()=>Apply(state.ContinueCompanion(definitions,false),"companion"));
            }
            else if(active) Button(state.Companion==CompanionStep.Jiwoo?"각자 집에서 준비한다":state.Companion==CompanionStep.Briefing?"동림과 탐사를 시작한다 · 2시간":"귀환을 알리고 해산한다",48,1298,804,76,()=>Apply(state.ContinueCompanion(definitions),"companion"),true,true);
            else
            {
                Button("약속 장소에서 만난다",48,1212,804,76,()=>Apply(state.MeetCompanion(definitions),"companion"),state.CanMeetCompanion(definitions),true);
                Button("고등학교 지도",48,1310,390,70,()=>SelectLocation("school"),state.IsLocationUnlocked("school",definitions));
                Button("생활로 돌아가기",462,1310,390,70,()=>{page="home";message="";Render();});
            }
        }
        void ErasureBulletinPage()
        {
            var report=state.NextUnreadErasureNews;
            Label("새날의 소멸 속보",48,462,804,60,36,Paper);
            Label(new DateTime(2031,2,26).AddDays(report.day-1).ToString("yyyy.MM.dd")+" · DAY "+report.day+" · SP 기지국",48,540,804,48,24,Amber);
            Paragraph(state.ErasureBody(report,definitions),610,530,27);
            Choice(0,"확인하고 계속한다","오늘의 수신 기록을 일지에 남긴다",1170,()=>AcknowledgeErasure(false));
            Choice(1,"전국 소멸 지도를 본다","소멸 제보 위치와 날짜별 수신 기록",1280,()=>AcknowledgeErasure(true));
        }
        void AcknowledgeErasure(bool map)
        {
            selectedErasureDay=state.NextUnreadErasureNews.day;
            state.AcknowledgeErasureNews(); page=map?"erasure":"home"; message=""; Save(); Render();
        }
        void ErasureMapPage()
        {
            if(!state.ErasureNewsEnabled) { Journal(); return; }
            Label("전국 소멸 수신 기록",48,462,550,60,34,Paper);
            Button("기록으로",620,462,232,54,()=>{page="journal";Render();});
            var reports=state.ErasureHistory.Where(state.CanViewErasureReport).ToList();
            var selected=reports.Find(x=>x.day==selectedErasureDay)??reports.LastOrDefault();
            var places=reports.Where(x=>!string.IsNullOrEmpty(x.locationId)).Select(x=>definitions.erasureNews.Find(x.locationId)).ToArray();
            var map=Rect("Korea erasure map",root,48,540,804,270).gameObject.AddComponent<KoreaErasureMapGraphic>();
            map.Places=places; map.SelectedId=selected?.locationId??""; map.raycastTarget=false;
            Label("서울\n"+places.Count(x=>x.region=="서울")+"건",64,580,190,96,24,Muted);
            Label("지방\n"+places.Count(x=>x.kind=="town")+"개 읍",640,580,190,96,24,Muted);
            Label("대한민국 개략도\n표시는 대략적인 위치",610,718,230,64,18,Muted);
            Label(selected==null?"아직 수신된 소식이 없다":"DAY "+selected.day+" · "+state.ErasureHeadline(selected,definitions),48,824,804,40,22,Amber);
            Paragraph(selected==null?"가락시장 사건 이후 날짜가 바뀔 때마다 새로운 소멸 제보를 수신한다. 받은 소식은 이 지도와 일지에 남는다.":state.ErasureBody(selected,definitions),876,202,23);
            Label("날짜별 기록 · 목록을 위아래로 스크롤",48,1090,804,28,18,Muted);
            ScrollArea("Erasure history",1128,244,out var content); float y=12;
            foreach(var report in reports.AsEnumerable().Reverse())
            {
                var captured=report;
                Button("DAY "+report.day+"  "+state.ErasureHeadline(report,definitions),12,y,780,64,()=>{selectedErasureDay=captured.day;Render();},true,selected==report,content); y+=80;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(244,y));
        }
        void EncounterPage()
        {
            if(state.PendingEncounter.kind=="trader") { StreetTraderPage(); return; }
            // lastReport holds the search/arrival result that triggered this encounter.
            Hero("누군가 물품을 요구한다"); Paragraph(state.lastReport+"\n\n"+state.PendingEncounter.context+"에서 굶주린 사람과 마주쳤다. 어떻게 대응할까?",790,188);
            Choice(0,"순순히 내어준다","물품을 내어주고 부상을 피한다",1000,()=>Apply(state.ResolveEncounter(false,definitions)));
            Choice(1,"저항한다",state.HasWeapon(definitions)?"무기 저항 · 사망 시 정신력 상한 -25":"빠져나갈 수도 있지만 부상을 입을 수 있다",1112,()=>Apply(state.ResolveEncounter(true,definitions)));
            Choice(2,"상대의 식량을 강탈한다","참치 1개 획득 · 정신력 상한 -10 (회복 불가)",1224,()=>Apply(state.RobHungrySurvivor()));
        }
        void SleepPage()
        {
            Label("잠들기 전",48,462,804,60,36,Paper);
            Paragraph("졸림 "+state.Sleepiness+" / 100\n오늘 수면 "+state.SleepHours+" / 6시간\n\n매일 오전 8시에 지난 하루를 평가한다. 부족한 수면 1시간당 정신력 -3. 6시간 초과 수면은 시간당 정신력 +"+state.SleepMentalRecovery+".\n"+(state.HasFacility("bedroll")?"보강 침구 사용 · 졸림 시간당 -16\n":"")+"\n살해·강탈로 줄어든 정신력 상한은 수면으로 회복되지 않는다. 연속 6시간 수면은 부상 1곳도 회복한다.",542,428,26);
            Choice(0,"2시간 잔다","짧게 눈을 붙인다",1000,()=>Apply(state.Rest(definitions,false,2)));
            Choice(1,"6시간 잔다","하루에 필요한 수면을 채운다",1112,()=>Apply(state.Rest(definitions,false,6)));
            Choice(2,"8시간 잔다","충분히 자고 정신력을 일부 회복한다",1224,()=>Apply(state.Rest(definitions,false,8)));
        }
        void StreetTraderPage()
        {
            Label("길 위의 행상인",48,462,804,60,36,Paper);
            Paragraph("낡은 가방을 멘 사람이 물물교환을 제안했다. 한 가지 거래를 마치면 서로 길을 떠난다.\n\n집의 물자가 아닌 지금 소지한 물품으로만 교환할 수 있다.",542,220,26);
            for(int i=0;i<SurvivalState.StreetOffers.Length;i++)
            {
                int index=i; var offer=SurvivalState.StreetOffers[i]; float y=790+i*142;
                Button(definitions.Item(offer.give).name+" "+definitions.Quantity(offer.give,offer.cost)+" → "+definitions.Item(offer.receive).name+" "+definitions.Quantity(offer.receive,offer.amount),48,y,804,72,()=>Apply(state.StreetTrade(index,definitions)),state.CanStreetTrade(index),true);
                Label("소지 "+definitions.Item(offer.give).name+" "+definitions.Quantity(offer.give,state.Backpack.Count(offer.give)),66,y+80,768,34,22,Muted);
            }
            Button("거래하지 않고 지나간다",48,1244,804,76,()=>Apply(state.LeaveStreetTrader()));
        }
        void GameOver()
        {
            Hero("기록이 멈췄다"); Paragraph("체력이 바닥났다. 더는 몸을 움직일 수 없다.\n\n"+state.lastReport,790,240);
            Button("새로운 기록",48,1130,804,84,()=>{confirmRestart=true;Render();},true,true);
            Button("남겨진 일지",48,1250,804,84,()=>{Clear(); Header(); Journal(); Button("돌아가기",48,1450,804,70,Render);});
        }
        void Menu()
        {
            Label("기록을 정리한다",48,470,804,60,36,Paper); Paragraph(ReportText,560,170);
            Button("제작 · 음식 / 무기 / 생활용품",48,780,804,84,()=>{page="craft";message="";Render();},state.CurrentLocation=="home");
            Button("기록 저장",48,890,390,84,()=>{Save();message="현재 기록을 저장했다.";Render();},true,true);
            Button("저장 기록 불러오기",462,890,390,84,Load,store.Exists);
            Button("기록 이어가기",48,1010,804,84,()=>{page="home";Render();});
            Button("새 기록 시작",48,1130,804,84,()=>{confirmRestart=true;Render();});
            if(state.Story.HasRadio) Button("라디오 켜기",48,1250,804,84,OpenRadio,state.CanUseRadio);
        }
        void RestartPage()
        {
            Hero("새 기록을 시작할까?"); Paragraph("새 오프닝을 마치면 현재 저장이 새 기록으로 바뀐다.",790,180);
            Button("현재 기록으로 돌아간다",48,1100,804,84,()=>{confirmRestart=false;Render();},true,true);
            Button("새로운 기록 시작",48,1230,804,84,BeginOpening);
        }
        void ContinuePage()
        {
            Picture(48,120,804,460); Label("Vanishing Days",48,680,804,100,56,Paper);
            Paragraph(string.IsNullOrEmpty(message)?"남겨 둔 기록이 있다.":message,840,170);
            Button("기록 이어 읽기",48,1120,804,88,Load,true,true);
            Button("새 기록 시작",48,1250,804,88,BeginOpening);
        }
        void BeginOpening()
        {
            confirmRestart=false; message=""; Clear();
            var host=new GameObject("Opening",typeof(RectTransform),typeof(OpeningScreenView)); host.transform.SetParent(root,false);
            openingView=host.GetComponent<OpeningScreenView>(); openingView.OnOpeningCompleted+=StartMainGame; openingView.Initialize(font);
        }
        void StartMainGame(string playerNickname)
        {
            if(openingView==null) return;
            var difficulty=openingView.SelectedDifficulty;
            openingView.OnOpeningCompleted-=StartMainGame; openingView.gameObject.SetActive(false); Destroy(openingView.gameObject); openingView=null;
            state=SurvivalState.NewGame(difficulty,definitions); state.PlayerNickname=playerNickname; state.Story.OpeningCompleted=true;
            transferQuantities.Clear(); inventoryScrollOffset=0; inventoryScroll=null;
            neighborhoodPositioned=false; neighborhoodScroll=null;
            confirmRestart=false; page="home"; message=""; Save(); Render();
        }
        void Apply(string text,string next="home") { message=text; page=next; Save(); Render(); }
        void Save() { try { store.Save(state); } catch(Exception e) { Debug.LogWarning("Save failed: "+e.Message); message="기록을 저장하지 못했다. 저장 공간을 확인해 주세요."; } }
        void Load()
        {
            if(store.TryLoad(out var restored,out var error)) { state=restored; confirmRestart=false; page="home"; message=""; Render(); }
            else { Debug.LogWarning("Load failed: "+error); page="continue"; message="남겨 둔 기록을 읽을 수 없다. 기존 파일은 보관돼 있다."; Render(); }
        }
#if UNITY_EDITOR || VD_UI_VERIFICATION
        IEnumerator SmokeCrafting()
        {
            string dir=Path.GetFullPath("Builds/CraftingVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true;state.PlayerNickname="제작 검증";
            foreach(string id in new[]{"wood","tools","scrap","cloth","cord","tape","water"})state.HomeStorage.Add(id,40);
            state.HomeStorage.Add("raw_water",2); Render();yield return null;ClickGameButton("제작");
            if(!root.GetComponentsInChildren<Button>().Any(x=>x.name=="음식") || !root.GetComponentsInChildren<Button>().Any(x=>x.name=="무기") || !root.GetComponentsInChildren<Button>().Any(x=>x.name=="생활용품"))throw new InvalidOperationException("Craft tabs missing.");
            ClickGameButton("조리한다");if(state.HomeStorage.Count("rice_meal")!=1)throw new InvalidOperationException("Food tab cooking failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"FOOD.png"));
            Action<string,bool> make=(id,security)=>
            {
                string title=security?definitions.Security(id).name:definitions.crafting.Find(id).name;
                var label=craftScroll.content.GetComponentsInChildren<Text>().First(x=>x.text==title);
                float top=-label.rectTransform.anchoredPosition.y;
                craftScroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(top-20,0,craftScroll.content.sizeDelta.y-624));
                float offset=security?158:174;
                var button=craftScroll.content.GetComponentsInChildren<Button>().First(x=>Mathf.Abs(x.GetComponent<RectTransform>().anchoredPosition.y-(label.rectTransform.anchoredPosition.y-offset))<1);
                if(!button.interactable)throw new InvalidOperationException("Craft UI unexpectedly locked: "+id);button.onClick.Invoke();
            };
            ClickGameButton("무기");make("club",false);make("wrapped_club",false);make("nail_bat",false);
            if(state.HomeStorage.Count("club")!=0 || state.HomeStorage.Count("wrapped_club")!=0 || state.HomeStorage.Count("nail_bat")!=1)throw new InvalidOperationException("Weapon upgrade chain failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"WEAPONS.png"));
            ClickGameButton("물품");ClickGameButton("착용");if(state.Strength(definitions)!=6)throw new InvalidOperationException("Crafted weapon equip failed.");
            ClickGameButton("제작");ClickGameButton("생활용품");make("workbench",false);make("water_filter",false);make("water_collector",false);make("collect_water",false);
            int water=state.HomeStorage.Count("water");make("purify_water",false);
            if(state.NextDiary!=null)ClickGameButton("읽고 계속");
            if(state.HomeStorage.Count("raw_water")!=2 || state.HomeStorage.Count("water")!=water+1 || craftTab!=2)throw new InvalidOperationException("Water crafting or tab restoration after diary failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"WATER-SURVIVAL.png"));
            state.Vitals.Hunger=0;make("bedroll",false);make("door1",true);
            if(!state.HasFacility("bedroll") || state.SleepMentalRecovery!=3 || state.DoorLevel!=1)throw new InvalidOperationException("Living utility effects/door upgrade failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DOOR-UPGRADE.png"));
            ClickGameButton("무기");make("machete",false);ClickGameButton("물품");ClickGameButton("착용");
            if(state.Strength(definitions)!=7 || state.HomeStorage.Count("nail_bat")!=1 || state.Equipment.Count!=1)throw new InvalidOperationException("Weapon replacement stacked or lost old weapon.");
            Save();if(!store.TryLoad(out var loaded,out _) || !loaded.HasFacility("water_filter") || loaded.DoorLevel!=1 || loaded.Strength(definitions)!=7)throw new InvalidOperationException("Crafting autosave failed.");
            Debug.Log("VD_CRAFTING_UI_OK: three tabs, cooking, weapon upgrade chain/equip/replacement, workbench, collection/purification, bedroll, door upgrade, diary return and autosave.");
            yield return null;Application.Quit();
        }
        IEnumerator SmokeDailyLife()
        {
            string dir=Path.GetFullPath("Builds/DailyLifeVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="하루 기록자"; Render(); yield return null;
            ClickGameButton("옆집 안부"); ClickGameButton("그릇을 돌려"); yield return null;
            if(state.NeighborStage!=1) throw new InvalidOperationException("Neighbor first meeting UI failed.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"NEIGHBOR.png"));
            ClickGameButton("생활로 돌아"); state.Story.HasRadio=true; state.Backpack.Add("radio",1); state.hour=23;
            Apply(state.Rest(definitions,false,9)); yield return null;
            if(!root.GetComponentsInChildren<Text>().Any(x=>x.text=="하루의 기록") || state.MorningIssues.Count!=1) throw new InvalidOperationException("Sleep did not queue diary before morning.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DAILY-DIARY.png"));
            ClickGameButton("읽고 계속"); yield return null;
            if(!root.GetComponentsInChildren<Text>().Any(x=>x.text=="아침 수신함") || state.Story.SupplyRumorUnlocked) throw new InvalidOperationException("First morning order/first-radio gate failed.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"FIRST-MORNING.png"));
            ClickGameButton("이날 방송 확인");
            if(!state.Story.SupplyRumorUnlocked || state.Story.HasMetDongrim) throw new InvalidOperationException("Morning first broadcast skipped market story.");
            state.day=3; state.hour=7; state.Vitals.Hunger=0; state.AdvanceTime(1,definitions); Render(); yield return null;
            if(state.ErasureHistory.Count!=1 || state.Story.CrowdIncidentResolved) throw new InvalidOperationException("Morning news remained gated by Garak.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"REGULAR-MORNING.png"));
            ClickGameButton("이날 방송 확인");
            state.Travel("supply_site",definitions,()=>1); state.ApproachCrowd(definitions); state.ChooseCrowdResponse(true,definitions);
            state.ContinueCrowdStory(definitions); state.ContinueCrowdStory(definitions); state.ContinueCrowdStory(definitions);
            state.UnlockedLocations.AddRange(new[]{"empty_house","school"}); state.Travel("home",definitions,()=>1);
            state.day=4; state.hour=8; state.Vitals.Hunger=0; Apply(state.Travel("empty_house",definitions,()=>1)); ClickGameButton("확인한 길과 소식");
            state.Travel("home",definitions,()=>1); state.day=5; state.hour=8; state.Vitals.Hunger=0;
            Apply(state.Travel("school",definitions,()=>1)); ClickGameButton("확인한 길과 소식");
            state.day=6; state.hour=8; page="companion"; message=""; Render(); ClickGameButton("약속 장소에서 만난다"); yield return null;
            if(!state.Story.HasMetJiwoo || state.Companion!=CompanionStep.Jiwoo) throw new InvalidOperationException("Jiwoo UI did not start.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"JIWOO.png"));
            Save(); if(!store.TryLoad(out var reloaded,out _) || reloaded.Companion!=CompanionStep.Jiwoo) throw new InvalidOperationException("Jiwoo active dialogue save failed.");
            state=reloaded; Render(); ClickGameButton("각자 집에서 준비");
            state.Travel("home",definitions,()=>1); state.Cook("rice",definitions); state.Transfer("rice_meal",true,1,definitions);
            state.day=state.CompanionReadyDay; state.hour=8; state.Vitals.Hunger=0; state.Travel("school",definitions,()=>1);
            page="companion"; message=""; Render(); ClickGameButton("약속 장소에서 만난다"); ClickGameButton("동림과 탐사를 시작"); yield return null;
            if(state.Companion!=CompanionStep.Decision || state.CompanionTrust!=1) throw new InvalidOperationException("Prepared companion expedition UI failed.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"EXPEDITION-CHOICE.png"));
            Save(); if(!store.TryLoad(out reloaded,out _) || reloaded.Companion!=CompanionStep.Decision) throw new InvalidOperationException("Expedition choice save failed.");
            state=reloaded; Render(); ClickGameButton("오늘은 먼저 돌아"); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"RETURN-TOGETHER.png"));
            ClickGameButton("귀환을 알리고 해산");
            if(state.Companion!=CompanionStep.Completed || state.Story.JoinedLiberationFront || state.CurrentLocation!="school") throw new InvalidOperationException("Expedition moved/joined player or did not complete.");
            Apply(state.Travel("home",definitions,()=>1)); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"HOME-AFTER-EXPEDITION.png"));
            if(state.CurrentLocation!="home" || state.HomeStorage.Count("burner")!=1 || !store.TryLoad(out reloaded,out _) || reloaded.CompanionTrust!=3) throw new InvalidOperationException("Original home/storage or expedition autosave failed.");
            state=SurvivalState.NewGame(GameDifficulty.NORMAL,definitions); state.hour=23; state.Vitals.Hunger=100; state.Vitals.CurrentHP=1; state.AdvanceTime(1,definitions); page="home"; Render();
            if(!root.GetComponentsInChildren<Text>().Any(x=>x.text=="기록이 멈췄다")) throw new InvalidOperationException("Diary displaced death screen.");
            Debug.Log("VD_DAILY_LIFE_UI_OK: neighbor, actual diary before morning, first broadcast gate, pre-Garak news, Jiwoo, saved expedition choices, home return and death priority.");
            yield return null; Application.Quit();
        }
        IEnumerator SmokeThirst()
        {
            string dir=Path.GetFullPath("Builds/ThirstVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="목마름 검증";
            state.HomeStorage.Remove("water",8); state.AdvanceTime(5,definitions); page="home"; Render(); yield return null;
            if(!root.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("목마름 50/100"))) throw new InvalidOperationException("Thirst HUD missing or incorrect.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"THIRST-50.png"));
            state.AdvanceTime(1,definitions); Render(); yield return null;
            var label=root.GetComponentsInChildren<Text>().First(x=>x.text.Contains("목마름 60/100"));
            if(label.color!=Amber || state.Vitals.CurrentHP!=490) throw new InvalidOperationException("Thirst warning/shortage damage failed.");
            state.AdvanceTime(4,definitions); Render(); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"THIRST-100.png"));
            state.HomeStorage.Add("water",1); state.AdvanceTime(2,definitions); Save(); Render(); yield return null;
            if(!root.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("목마름 0/100")) || state.Thirst!=0 || state.HomeStorage.Count("water")!=0) throw new InvalidOperationException("Drinking did not update thirst HUD.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"THIRST-QUENCHED.png"));
            if(!store.TryLoad(out var loaded,out _) || loaded.Thirst!=0 || loaded.HydrationHours!=0 || loaded.Vitals.CurrentHP!=490) throw new InvalidOperationException("Thirst UI save/load failed.");
            Debug.Log("VD_THIRST_UI_OK: visible 50/60/100 thirst, shortage warning and HP, drinking resets to zero, saved state.");
            yield return null; Application.Quit();
        }
        IEnumerator SmokeWater()
        {
            string dir=Path.GetFullPath("Builds/WaterVerification"); Directory.CreateDirectory(dir);
            state=SurvivalState.NewGame(GameDifficulty.EASY,definitions,123); state.Story.OpeningCompleted=true; state.PlayerNickname="물 검증";
            page="kitchen"; Render(); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"EASY-UNLIMITED.png"));
            ClickGameButton("조리한다");
            if(state.HomeStorage.Count("rice_meal")!=1 || state.HomeStorage.Count("water")!=0 || state.HydrationHours!=0) throw new InvalidOperationException("Easy UI cooking required finite water.");
            state=SurvivalState.NewGame(GameDifficulty.NORMAL,definitions,123); state.Story.OpeningCompleted=true; state.PlayerNickname="물 검증";
            page="inventory"; message=""; Render(); yield return null;
            var input=root.GetComponentsInChildren<InputField>().First(x=>x.name=="수량:water"); input.text="2";
            var button=inventoryScroll.content.GetComponentsInChildren<Button>().First(x=>x.name=="챙기기" && Mathf.Abs(x.GetComponent<RectTransform>().anchoredPosition.y-(input.GetComponent<RectTransform>().anchoredPosition.y-62))<1);
            inventoryScroll.content.anchoredPosition=new Vector2(0,inventoryScroll.content.sizeDelta.y-638); button.onClick.Invoke(); yield return null;
            if(state.HomeStorage.Count("water")!=6 || state.Backpack.Count("water")!=2) throw new InvalidOperationException("Water quantity carry UI failed.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CARRY-WATER.png"));
            page="kitchen"; message=""; Render(); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"NORMAL-WATER-COST.png"));
            ClickGameButton("조리한다");
            if(state.HomeStorage.Count("water")!=5 || state.HomeStorage.Count("rice_meal")!=1) throw new InvalidOperationException("Normal cooking UI did not consume water.");
            state.HydrationHours=5; state.AdvanceTime(1,definitions);
            if(state.HomeStorage.Count("water")!=4 || state.Backpack.Count("water")!=2 || state.HydrationHours!=0) throw new InvalidOperationException("Automatic drinking failed.");
            Apply(state.Travel("store",definitions,()=>1)); Apply(state.Search(definitions,()=>1));
            if(state.Backpack.Count("water")!=3 || !state.lastReport.Contains("생수")) throw new InvalidOperationException("Exploration did not award/show water.");
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"FOUND-WATER.png"));
            state.HydrationHours=5; state.AdvanceTime(1,definitions); Save();
            if(!store.TryLoad(out var loaded,out _) || loaded.Backpack.Count("water")!=2 || loaded.HomeStorage.Count("water")!=4 || loaded.HydrationHours!=0) throw new InvalidOperationException("Water autosave failed.");
            state=SurvivalState.NewGame(GameDifficulty.NORMAL,definitions,123); state.HomeStorage.Remove("water",8); page="kitchen"; message=""; Render(); yield return null;
            if(root.GetComponentsInChildren<Button>().Any(x=>x.name=="조리한다" && x.interactable)) throw new InvalidOperationException("Waterless cooking buttons enabled.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"NO-WATER.png"));
            Debug.Log("VD_WATER_UI_OK: easy unlimited cooking, quantity carry, normal cooking cost, automatic drinking, exploration water, shortage warning and autosave.");
            yield return null; Application.Quit();
        }
        IEnumerator SmokeSleepTrade()
        {
            string dir=Path.GetFullPath("Builds/SleepTradeVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="생존 검증";
            foreach(var place in definitions.locations) if(!place.requiresSupplyRumor && !state.UnlockedLocations.Contains(place.id)) state.UnlockedLocations.Add(place.id);
            page="map"; Render(); yield return null;
            Vector2 start=neighborhoodScroll.content.anchoredPosition;
            ClickGameButton("→"); ClickGameButton("↑");
            if(neighborhoodScroll.content.anchoredPosition.x>=start.x || neighborhoodScroll.content.anchoredPosition.y>=start.y) throw new InvalidOperationException("Right/up map arrows failed.");
            ClickGameButton("←"); ClickGameButton("↓");
            if(Vector2.Distance(neighborhoodScroll.content.anchoredPosition,start)>1) throw new InvalidOperationException("Left/down map arrows failed.");
            for(int i=0;i<12;i++) { ClickGameButton("→"); ClickGameButton("↓"); }
            if(neighborhoodOffset!=new Vector2(-636,450)) throw new InvalidOperationException("Map did not clamp at edges.");
            ClickGameButton("현재 위치"); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"PAN-MAP.png"));
            ClickGameButton("지도:store"); yield return null;
            if(page!="location" || root.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("발견 가능:") || x.text.Contains("최초 물량"))) throw new InvalidOperationException("Map target selection/hidden loot failed.");
            page="home"; Render(); ClickGameButton("잠을 잔다"); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SLEEP-CHOICES.png"));
            state.Vitals.AdjustMental(-50); ClickGameButton("8시간 잔다");
            if(state.SleepHours!=8 || state.Vitals.CurrentMental!=54 || state.Sleepiness!=0) throw new InvalidOperationException("Eight-hour sleep UI failed.");
            state.Vitals.Hunger=0; state.Travel("store",definitions,()=>.27); Render(); yield return null;
            if(state.PendingEncounter?.kind!="trader") throw new InvalidOperationException("Trader UI encounter gate failed.");
            state.Backpack.Add("tuna",2); Render(); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"STREET-TRADER.png"));
            ClickGameButton("참치");
            if(state.PendingEncounter!=null || state.Backpack.Count("tuna")!=0 || state.Backpack.Count("bandage")!=1) throw new InvalidOperationException("Actual trader button exchange failed.");
            state.PendingEncounter=new Encounter{kind="hungry",context="길거리"}; Render(); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MORAL-CHOICE.png"));
            ClickGameButton("상대의 식량");
            if(state.Vitals.MentalCap!=90 || root.Find("Mental current")==null) throw new InvalidOperationException("Moral cap/HUD bar failed.");
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MENTAL-CAP.png"));
            if(!store.TryLoad(out var loaded,out _) || loaded.SleepHours!=8 || loaded.Vitals.MentalCap!=90 || loaded.Vitals.CurrentMental!=44) throw new InvalidOperationException("Sleep/trade/moral UI autosave failed.");
            Debug.Log("VD_SLEEP_TRADE_UI_OK: four map arrows and bounds, selection, hidden loot, 8h sleep/recovery, trader exchange, moral cap bar and autosave.");
            yield return null; Application.Quit();
        }
        IEnumerator SmokeStorage()
        {
            string dir=Path.GetFullPath("Builds/StorageVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="보관 검증"; state.Story.HasRadio=true;
            foreach(var item in definitions.items) if(item.id!="burner") state.Backpack.Add(item.id,item.id=="radio"?1:item.id=="rice"?700:7);
            state.Equip("weapon",definitions); page="inventory"; Render(); yield return null;
            Func<string,InputField> input=id=>root.GetComponentsInChildren<InputField>().First(x=>x.name=="수량:"+id);
            Func<string,string,Button> action=(id,caption)=>inventoryScroll.content.GetComponentsInChildren<Button>().First(x=>x.name==caption && Mathf.Abs(x.GetComponent<RectTransform>().anchoredPosition.y-(input(id).GetComponent<RectTransform>().anchoredPosition.y-62))<1);
            float offset=-input("wood").GetComponent<RectTransform>().anchoredPosition.y-180;
            inventoryScroll.content.anchoredPosition=new Vector2(0,offset); yield return null;
            input("wood").text="3"; int homeWood=state.HomeStorage.Count("wood"); int startHour=state.hour;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"QUANTITY.png"));
            action("wood","보관").onClick.Invoke(); yield return null;
            if(state.Backpack.Count("wood")!=4 || state.HomeStorage.Count("wood")!=homeWood+3 || Mathf.Abs(inventoryScroll.content.anchoredPosition.y-offset)>1) throw new InvalidOperationException("Partial storage or scroll preservation failed.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"STORED-SCROLL.png"));
            action("wood","챙기기").onClick.Invoke(); yield return null;
            if(state.Backpack.Count("wood")!=7 || Mathf.Abs(inventoryScroll.content.anchoredPosition.y-offset)>1) throw new InvalidOperationException("Partial withdrawal or scroll preservation failed.");
            input("wood").text=""; action("wood","보관").onClick.Invoke(); yield return null;
            if(state.Backpack.Count("wood")!=7 || Mathf.Abs(inventoryScroll.content.anchoredPosition.y-offset)>1) throw new InvalidOperationException("Invalid quantity changed supplies/scroll.");
            ClickGameButton("전체 보관"); yield return null;
            if(state.Backpack.stacks.Any(x=>x.amount>0) || !state.Equipment.Contains("weapon") || state.hour!=startHour || Mathf.Abs(inventoryScroll.content.anchoredPosition.y-offset)>1) throw new InvalidOperationException("Store-all changed equipment/time/scroll or missed supplies.");
            if(!store.TryLoad(out var loaded,out _) || loaded.HomeStorage.Count("wood")!=homeWood+7 || !loaded.Equipment.Contains("weapon")) throw new InvalidOperationException("Storage autosave failed.");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"ALL-STORED.png"));
            Debug.Log("VD_STORAGE_UI_OK: exact quantity deposit/withdrawal, empty input, pixel scroll preservation, all supplies stored, equipment/time unchanged, autosave.");
            yield return null; Application.Quit();
        }
        IEnumerator SmokeErasure()
        {
            string dir=Path.GetFullPath("Builds/ErasureVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="수신자";
            state.Backpack.Add("radio",1); state.Story.HasRadio=true; state.ListenRadio(definitions);
            state.Travel("supply_site",definitions,()=>1); state.ApproachCrowd(definitions); state.ChooseCrowdResponse(true,definitions);
            state.ContinueCrowdStory(definitions); state.ContinueCrowdStory(definitions); state.ContinueCrowdStory(definitions);
            state.hour=23; state.AdvanceTime(1,definitions); Render();
            yield return new WaitForSeconds(1f); yield return new WaitForEndOfFrame();
            if(!state.HasUnreadErasureNews || !root.GetComponentsInChildren<Text>().Any(x=>x.text=="새날의 소멸 속보")) throw new InvalidOperationException("Daily erasure bulletin is missing.");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DAILY-BULLETIN.png"));
            ClickGameButton("전국 소멸 지도를");
            if(page!="erasure" || state.HasUnreadErasureNews) throw new InvalidOperationException("Erasure acknowledgement/map button failed.");
            for(int i=0;i<49 && !(state.ErasureHistory.Any(x=>definitions.erasureNews.Find(x.locationId)?.region=="서울") && state.ErasureHistory.Any(x=>definitions.erasureNews.Find(x.locationId)?.kind=="town"));i++)
            {
                state.day++; state.GenerateDailyErasureNews(definitions); Render();
                ClickGameButton("확인하고 계속");
            }
            var seoul=state.ErasureHistory.First(x=>definitions.erasureNews.Find(x.locationId).region=="서울");
            var town=state.ErasureHistory.First(x=>definitions.erasureNews.Find(x.locationId).kind=="town");
            page="erasure"; selectedErasureDay=seoul.day; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SEOUL-MAP.png"));
            var map=root.GetComponentInChildren<KoreaErasureMapGraphic>();
            if(map==null || map.GetComponent<CanvasRenderer>().GetMesh().vertexCount<250) throw new InvalidOperationException("National map did not render.");
            selectedErasureDay=town.day; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"RURAL-MAP.png"));
            Save(); if(!store.TryLoad(out var restored,out _) || restored.ErasureHistory.Count!=state.ErasureHistory.Count || restored.HasUnreadErasureNews) throw new InvalidOperationException("Erasure UI save/read state failed.");
            page="journal"; Render(); ClickGameButton("전국 소멸 지도");
            if(page!="erasure") throw new InvalidOperationException("Journal erasure map entry failed.");
            Debug.Log("VD_ERASURE_UI_OK: midnight bulletin, acknowledgement, Seoul and rural reports, country map geometry, history/journal link and saved read state.");
            yield return null; Application.Quit();
        }
        IEnumerator SmokeVitals()
        {
            string dir=Path.GetFullPath("Builds/VitalsVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="검증";
            state.Vitals.CurrentHP=410;
            for(int i=0;i<3;i++) state.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f });
            state.Vitals.ClampToRecoveryCap(definitions.TEMP_BALANCE); Render();
            yield return new WaitForSeconds(1f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,"HP-CAPPED.png"));
            if(state.Vitals.CurrentHP!=350 || !root.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("정신력 100 / 100"))) throw new InvalidOperationException("Capped HP/mental HUD failed.");
            page="inventory"; Render(); yield return null; ClickGameButton("치료");
            if(state.Vitals.CurrentHP!=370 || state.Vitals.Injuries.Count!=2 || state.Vitals.RecoverableMaxHP(definitions.TEMP_BALANCE)!=400) throw new InvalidOperationException("Treatment UI did not heal injury and 20 HP.");
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"HP-TREATED.png"));
            state.Vitals.AdjustMental(-35); page="home"; Render(); Save();
            if(!store.TryLoad(out var loaded,out _) || loaded.Vitals.CurrentMental!=65 || loaded.Vitals.CurrentHP!=370) throw new InvalidOperationException("Vitals UI save round trip failed.");
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MENTAL-HUD.png"));
            Debug.Log("VD_VITALS_UI_OK: 410 to 350 clamp, treatment button to 370 HP/400 cap and two injuries, mental HUD and save/load.");
            yield return null; Application.Quit();
        }
        IEnumerator Smoke()
        {
            string dir=Path.GetFullPath("Builds/SurvivalVerification"); Directory.CreateDirectory(dir);
            byte[] previousSave=File.Exists(SavePath)?File.ReadAllBytes(SavePath):null;
            try
            {
                yield return new WaitForSeconds(4f);
                const string koreanGlyphs="소멸일지 한글 기록자 가나다 힣";
                font.RequestCharactersInTexture(koreanGlyphs,26,FontStyle.Normal);
                foreach(char character in koreanGlyphs)
                    if(character!=' ' && (!font.GetCharacterInfo(character,out var glyph,26,FontStyle.Normal) || glyph.glyphWidth<=0))
                        throw new InvalidOperationException("Bundled Korean glyph failed to render: "+character);
                Debug.Log("VD_KOREAN_FONT_OK: bundled font rasterized Korean glyphs in "+(Application.isEditor?"Editor Play":"Windows Player"));
                if(openingView==null) throw new InvalidOperationException("Opening did not start.");
                while(openingView!=null)
                {
                    var flow=openingView.Flow;
                    string id=flow.CurrentScreenId;
                    yield return null; yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir,id+".png"));
                    if(id=="OPEN_00")
                    {
                        OpeningButton("어려움").onClick.Invoke();
                        if(openingView.SelectedDifficulty!=GameDifficulty.HARD) throw new InvalidOperationException("Difficulty selection failed.");
                        yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DIFFICULTY-HARD.png"));
                        OpeningButton("쉬움").onClick.Invoke();
                        if(openingView.SelectedDifficulty!=GameDifficulty.EASY) throw new InvalidOperationException("Easy selection failed.");
                        OpeningButton("보통").onClick.Invoke();
                    }
                    if(id=="OPEN_07")
                    {
                        var scroll=openingView.GetComponentInChildren<ScrollRect>();
                        if(scroll.content.rect.height<=scroll.viewport.rect.height) throw new InvalidOperationException("Long record must scroll.");
                        scroll.verticalNormalizedPosition=0;
                        yield return null; yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(dir,id+"-bottom.png"));
                    }
                    if(flow.Current.kind=="Nickname")
                    {
                        var input=openingView.GetComponentInChildren<InputField>();
                        input.text="   ";
                        if(OpeningButton("기록 시작").interactable) throw new InvalidOperationException("Blank nickname button enabled.");
                        input.text=""; input.ActivateInputField(); yield return null;
                        foreach(char c in "  기록자A1  ") input.ProcessEvent(new Event { type=EventType.KeyDown, character=c });
                        input.ForceLabelUpdate();
                        if(input.text!="  기록자A1  ") throw new InvalidOperationException("Korean committed text input failed: "+input.text);
                        if(input.textComponent.text.Trim()!="기록자A1" || input.placeholder.enabled) throw new InvalidOperationException("Committed nickname is not visible.");
                        var keyboard=Keyboard.current;
                        if(keyboard!=null)
                        {
                            var ime=UnityEngine.InputSystem.LowLevel.IMECompositionEvent.Create(keyboard.deviceId,"한",UnityEngine.InputSystem.LowLevel.InputState.currentTime);
                            InputSystem.QueueEvent(ref ime); yield return null;
                            var bridge=EventSystem.current.currentInputModule.input;
                            if(bridge.compositionString!="한") throw new InvalidOperationException("IME composition bridge failed.");
                            if(!input.textComponent.text.Contains("한")) throw new InvalidOperationException("IME composition is not visible.");
                            ime=UnityEngine.InputSystem.LowLevel.IMECompositionEvent.Create(keyboard.deviceId,"",UnityEngine.InputSystem.LowLevel.InputState.currentTime);
                            InputSystem.QueueEvent(ref ime); yield return null;
                        }
                        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"OPEN_11-input.png"));
                        OpeningButton("기록 시작").onClick.Invoke();
                    }
                    else OpeningButton(flow.Current.kind=="Question"?"예":flow.Current.button).onClick.Invoke();
                }
                if(state.PlayerNickname!="기록자A1" || page!="home") throw new InvalidOperationException("Main game/profile handoff failed.");
                var restored=JsonUtility.FromJson<SurvivalState>(File.ReadAllText(SavePath));
                if(restored.PlayerNickname!=state.PlayerNickname) throw new InvalidOperationException("Nickname save round trip failed.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MAIN.png"));
                if(state.Vitals.CurrentHP!=500 || state.CurrentLocation!="home" || state.Story.HasRadio || state.HomeStorage.Count("spam")!=5) throw new InvalidOperationException("v0.3 starting state mismatch.");
                page="kitchen"; Render(); yield return null;
                ClickGameButton("조리한다",1);
                if(state.HomeStorage.Count("spam_meal")!=1) throw new InvalidOperationException("Cooking button failed.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"KITCHEN.png"));
                ClickGameButton("먹는다");
                if(state.HomeStorage.Count("spam_meal")!=0) throw new InvalidOperationException("Eating button failed.");
                Apply(state.Transfer("tuna",true,definitions),"inventory");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"INVENTORY.png"));
                page="map"; Render(); yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MAP-INITIAL.png"));
                if(root.GetComponentInChildren<NeighborhoodMapGraphic>().VisibleLocations.Length!=2) throw new InvalidOperationException("New game revealed locked routes.");
                Apply(state.Travel("store",definitions,()=>1)); Apply(state.Search(definitions,()=>1));
                if(!state.IsLocationUnlocked("empty_house",definitions) || state.IsLocationUnlocked("school",definitions) || state.LootPercent("store",definitions)!=100) throw new InvalidOperationException("First route/loot unlock failed.");
                Apply(state.Travel("empty_house",definitions,()=>1)); Apply(state.Search(definitions,()=>1)); Apply(state.Travel("home",definitions,()=>1));
                state.Vitals.Hunger=0; state.hour=8;
                page="map"; Render(); yield return null; yield return new WaitForEndOfFrame();
                var mapGraphic=root.GetComponentInChildren<NeighborhoodMapGraphic>();
                var mapRenderer=mapGraphic==null?null:mapGraphic.GetComponent<CanvasRenderer>();
                if(mapRenderer==null || mapRenderer.cull || mapRenderer.materialCount==0) throw new InvalidOperationException("Map renderer is missing/invisible.");
                var mapMesh=mapRenderer.GetMesh();
                if(mapMesh==null || mapMesh.vertexCount<1000 || mapMesh.triangles.Length<1000) throw new InvalidOperationException("Road/building/marker geometry was not rendered.");
                ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MAP.png"));
                Debug.Log("VD_MAP_RENDER_OK: actual CanvasRenderer has road/building/marker mesh in "+(Application.isEditor?"Editor Play":"Windows Player"));
                int selectionHour=state.hour; uint selectionRandom=state.RandomState;
                ClickGameButton("지도:school");
                if(page!="location" || state.CurrentLocation!="home" || state.hour!=selectionHour || state.RandomState!=selectionRandom || state.AwayFromHome) throw new InvalidOperationException("Map selection caused travel/time/RNG changes.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"LOCATION.png"));
                ClickGameButton("이동하기");
                if(state.CurrentLocation!="school" || state.hour!=(selectionHour+3)%24) throw new InvalidOperationException("Explicit travel button failed.");
                if(state.PendingEncounter!=null) Apply(state.ResolveEncounter(false,definitions));
                for(int i=0;i<definitions.TEMP_BALANCE.radioSearches;i++) Apply(state.Search(definitions,()=>1));
                if(!state.Story.HasRadio || state.Story.HasMetDongrim) throw new InvalidOperationException("Radio gate failed.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SCHOOL-RADIO.png"));
                Apply(state.Travel("home",definitions,()=>1)); Apply(state.Eat("tuna",definitions));
                Apply(state.Travel("empty_house",definitions,()=>1));
                Apply(state.Search(definitions,()=>1)); Apply(state.Search(definitions,()=>1)); Apply(state.Travel("home",definitions,()=>1));
                Apply(state.Transfer("wood",false,definitions)); Apply(state.Transfer("tools",false,definitions)); Apply(state.InstallSecurity("analog",definitions),"security");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SECURITY.png"));
                page="inventory"; Render(); ClickGameButton("착용"); ClickGameButton("착용");
                if(state.Strength(definitions)<=0 || state.Defense(definitions)<=0) throw new InvalidOperationException("Equipment buttons did not apply modifiers.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"EQUIPMENT.png"));
                state.hour=8; Apply(state.Travel("store",definitions,()=>0));
                if(state.PendingEncounter==null) throw new InvalidOperationException("Day encounter missing.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DAY-ENCOUNTER.png"));
                ClickGameButton("순순히 내어준다");
                if(state.PendingEncounter!=null) throw new InvalidOperationException("Day choice did not resolve.");
                state.Vitals.CurrentHP=200; state.Vitals.Hunger=0; state.Vitals.HungerPenalty=0; state.Vitals.Injuries.Clear(); state.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); page="home"; Render();
                if(state.Vitals.RecoverableMaxHP(definitions.TEMP_BALANCE)!=450) throw new InvalidOperationException("HUD recovery cap wrong.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"RECOVERY-CAP.png"));
                Save();
                if(!store.TryLoad(out var saved,out _) || saved.Backpack.Count("radio")!=1 || saved.Story.HasMetDongrim || saved.Vitals.CurrentHP!=200) throw new InvalidOperationException("v0.3 full save/load failed.");
                state.CurrentLocation="home"; state.AwayFromHome=false; state.PendingEncounter=null; state.Backpack.Add("bandage",1); page="inventory"; Render();
                foreach(var inventoryScroll in root.GetComponentsInChildren<ScrollRect>()) if(inventoryScroll.name=="Inventory") inventoryScroll.verticalNormalizedPosition=1;
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MEDICINE.png"));
                int bandagesBefore=state.Backpack.Count("bandage")+state.HomeStorage.Count("bandage"); ClickGameButton("치료");
                if(state.Vitals.Injuries.Count!=0 || state.Vitals.RecoverableMaxHP(definitions.TEMP_BALANCE)!=500 || state.Backpack.Count("bandage")+state.HomeStorage.Count("bandage")!=bandagesBefore-1) throw new InvalidOperationException("Medicine UI failed.");
                state.Vitals.Injuries.Add(new Injury { recoveryPenalty=.1f }); Apply(state.Rest(definitions));
                Save(); if(!store.TryLoad(out var resting,out _) || resting.InjuryRestProgress!=2) throw new InvalidOperationException("Rest progress save failed.");
                Apply(state.Rest(definitions)); Apply(state.Rest(definitions)); if(state.Vitals.Injuries.Count!=0) throw new InvalidOperationException("Rest UI recovery failed.");
                Debug.Log("VD_MEDICINE_MAP_UI_OK: map selection preserves time/location/RNG; explicit movement; medicine button; 6h rest; saved rest progress.");
                state.hour=8; state.Vitals.Hunger=0; state.Vitals.HungerPenalty=0; page="home"; Render();
                int radioHour=state.hour; uint radioRandom=state.RandomState;
                ClickGameButton("라디오를 켠다");
                if(page!="radio" || !state.Story.SupplyRumorUnlocked || state.Story.HasMetDongrim || state.hour!=radioHour || state.RandomState!=radioRandom) throw new InvalidOperationException("First radio UI failed/unexpected time or story progression.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"RADIO-FIRST.png"));
                foreach(var scroll in root.GetComponentsInChildren<ScrollRect>()) if(scroll.content.rect.height>scroll.viewport.rect.height) scroll.verticalNormalizedPosition=0;
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"RADIO-FIRST-bottom.png"));
                ClickGameButton("제보 장소 지도 보기");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MAP-RUMOR.png"));
                ClickGameButton("지도:supply_site");
                if(state.Story.SupplySiteVisited || state.hour!=radioHour || state.RandomState!=radioRandom) throw new InvalidOperationException("Rumor info selection mutated game.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SUPPLY-INFO.png"));
                ClickGameButton("이동하기");
                if(state.PendingEncounter!=null) Apply(state.ResolveEncounter(false,definitions));
                if(!state.Story.SupplySiteVisited || state.Story.CrowdIncidentStarted || state.Story.HasMetDongrim) throw new InvalidOperationException("Site arrival auto-started story.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SUPPLY-APPROACH.png"));
                ClickGameButton("한발 물러나");
                Save(); if(!store.TryLoad(out var observing,out _) || observing.Crowd.Step!=CrowdStoryStep.Observing) throw new InvalidOperationException("Observation save failed.");
                ClickGameButton("배분 구역 가까이");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CROWD-PANIC.png"));
                int beforeChoice=state.Backpack.Count("radio"); ClickGameButton("가방을 붙잡고");
                if(state.Crowd.Step!=CrowdStoryStep.Rescue || state.Backpack.Count("radio")!=beforeChoice || !state.HasActiveCrowdIncident) throw new InvalidOperationException("Rescue UI choice failed.");
                Save(); if(!store.TryLoad(out var rescue,out _) || rescue.Crowd.Step!=CrowdStoryStep.Rescue || rescue.Crowd.BagChoice!="hold") throw new InvalidOperationException("Rescue state restore failed.");
                state=rescue; Render();
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CROWD-RESCUE.png"));
                ClickGameButton("다음");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CROWD-DISAPPEARANCE.png"));
                ClickGameButton("다음");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DONGRIM.png"));
                ClickGameButton("기록하고 계속한다");
                if(!state.CanAct || !state.Story.HasMetDongrim || !state.Story.CrowdIncidentResolved || state.Story.JoinedLiberationFront || state.Story.HasMetJiwoo || state.PlayerNickname!="기록자A1") throw new InvalidOperationException("Meeting completion/profile/scope failed.");
                Apply(state.Travel("home",definitions,()=>1)); state.day=state.NextDongrimDay; state.hour=8; state.Vitals.Hunger=0;
                Apply(state.Travel("empty_house",definitions,()=>1));
                if(!state.PendingDongrimMeeting) throw new InvalidOperationException("Second Dongrim meeting missing.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DONGRIM-SECOND.png"));
                state.Backpack.Add("tuna",1); Render(); ClickGameButton("식량을 건네고");
                if(state.TotalDongrimMeetings!=2 || !state.IsLocationUnlocked("hardware",definitions)) throw new InvalidOperationException("Second meeting button/unlock failed.");
                Apply(state.Travel("home",definitions,()=>1)); state.day=state.NextDongrimDay; state.hour=8; state.Vitals.Hunger=0;
                Apply(state.Travel("school",definitions,()=>1));
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DONGRIM-THIRD.png"));
                ClickGameButton("확인한 길과 소식");
                if(state.TotalDongrimMeetings!=3 || !state.IsLocationUnlocked("shelter",definitions)) throw new InvalidOperationException("Third meeting button/unlock failed.");
                page="map"; Render(); yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MAP-EXPANDED.png"));
                foreach(var scroll in root.GetComponentsInChildren<ScrollRect>()) scroll.verticalNormalizedPosition=0;
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"MAP-EXPANDED-bottom.png"));
                SelectLocation("empty_house");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"LOOT-REMAINING.png"));
                foreach(var scroll in root.GetComponentsInChildren<ScrollRect>()) scroll.verticalNormalizedPosition=0;
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"LOOT-REMAINING-bottom.png"));
                Apply(state.Travel("home",definitions,()=>1)); state.HomeStorage.Add("wood",20); state.HomeStorage.Add("tools",10);
                ClickGameButton("제작");
                for(int upgrade=0;upgrade<3;upgrade++) ClickGameButton("보강한다");
                if(state.DoorLevel!=3) throw new InvalidOperationException("Door upgrade buttons failed.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DOOR-UPGRADES.png"));
                foreach(var scroll in root.GetComponentsInChildren<ScrollRect>()) scroll.verticalNormalizedPosition=0;
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DOOR-UPGRADES-bottom.png"));
                state.day=state.NextDongrimDay; state.hour=8; state.Vitals.Hunger=0;
                foreach(var stack in state.Locations.Find(x=>x.id=="empty_house").remaining.stacks) stack.amount=0;
                Apply(state.Travel("empty_house",definitions,()=>1));
                if(!state.PendingDongrimMeeting) throw new InvalidOperationException("Patrol did not revisit exhausted place.");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DONGRIM-PATROL.png"));
                ClickGameButton("확인한 길과 소식");
                if(state.TotalDongrimMeetings!=4 || state.HasSearchWork("empty_house",definitions)) throw new InvalidOperationException("Patrol replenished loot/failed meeting.");
                Debug.Log("VD_WORLD_PROGRESSION_UI_OK: gradual discovery, loot quantities/exhaustion, second/third Dongrim meetings, eight routes, three door upgrades and recurring patrol at an exhausted site.");
                Apply(state.Travel("home",definitions,()=>1)); state.hour=18; Apply(state.Travel("supply_site",definitions,()=>1));
                // A fresh story fixture verifies the alternate night-only presentation.
                state.Story.CrowdIncidentStarted=false; state.Story.CrowdIncidentResolved=false; state.Story.HasMetDongrim=false; state.Story.DongrimRescueCompleted=false; state.Story.DongrimContactHintReceived=false; state.Crowd=new CrowdStoryProgress(); state.DongrimMeetings=0; state.LastDongrimMeetingDay=0; page="home"; Render();
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SUPPLY-NIGHT.png"));
                foreach(var button in root.GetComponentsInChildren<Button>()) if(button.name.Contains("배분 구역 가까이") && button.interactable) throw new InvalidOperationException("Night approach button enabled.");
                Debug.Log("VD_RADIO_STORY_UI_OK: home broadcast scroll, rumor card and map unlock, explicit travel, observation, panic/response, mid-rescue restore, disappearance, Dongrim meeting, free survival and night gate.");
                yield return null; Debug.Log("VD_SURVIVAL_UI_OK: 9:16 HUD, 500 HP, cooking/eating buttons, storage, four places, school radio, delayed story gate, security, encounter choice, 450 cap and session save/load.");
                Debug.Log("VD_OPENING_UI_OK: all screens, long-record scroll, disabled blank submit, Korean committed text, IME composition, nickname binding and main-game handoff.");
            }
            finally
            {
                if(previousSave!=null) File.WriteAllBytes(SavePath,previousSave); else if(File.Exists(SavePath)) File.Delete(SavePath);
                Application.Quit();
            }
        }
        Button OpeningButton(string caption)
        {
            foreach(var button in openingView.GetComponentsInChildren<Button>()) if(button.gameObject.name==caption) return button;
            throw new InvalidOperationException("Opening button missing: "+caption);
        }
        void ClickGameButton(string caption,int index=0)
        {
            foreach(var button in root.GetComponentsInChildren<Button>())
                if(button.interactable && button.gameObject.name.Contains(caption)) { if(index--==0) { button.onClick.Invoke(); return; } }
            throw new InvalidOperationException("Game button missing: "+caption);
        }
#endif
    }
}
