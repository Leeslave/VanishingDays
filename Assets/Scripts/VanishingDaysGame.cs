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
    public partial class VanishingDaysGame : MonoBehaviour
    {
        [SerializeField, Tooltip("에디터 테스트 전용: 오프닝을 건너뜁니다.")]
        bool skipOpeningInEditor;
        OpeningScreenView openingView;
        // Charcoal-and-fog palette: everything ash-grey, Amber reserved for the one action or warning that matters now.
        static readonly Color Ink=new Color32(11,17,19,255), Panel=new Color32(18,26,28,255), Raised=new Color32(26,36,39,255), Edge=new Color32(38,50,49,255), Track=new Color32(36,48,47,255), NavBar=new Color32(14,21,23,255),
            Paper=new Color32(230,223,204,255), Muted=new Color32(155,163,157,255), Faint=new Color32(110,119,114,255),
            Amber=new Color32(217,166,94,255), AmberText=new Color32(233,190,126,255), EmberDim=new Color32(43,38,28,255), EmberEdge=new Color32(92,74,44,255), OnAmber=new Color32(20,17,11,255),
            Cold=new Color32(143,176,192,255), Rust=new Color32(90,47,41,255), RustDark=new Color32(58,36,32,255), Alert=new Color32(74,42,33,255),
            JournalPaper=new Color32(220,212,191,255), JournalInk=new Color32(27,26,22,255);
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
            survivalArt=Resources.Load<Texture2D>("Art/SurvivalAtlas");
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
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdTutorial")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/TutorialVerification/session.json"),definitions); StartCoroutine(SmokeTutorial()); return; }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdSupply")>=0) { store=new SessionSaveStore(Path.GetFullPath("Builds/WaterAccessVerification/session.json"),definitions); StartCoroutine(SmokeWaterAccess()); return; }
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
        // A 2px Edge frame around a fill; the fill is the raycast and tint target.
        Button Pressable(string name,float x,float y,float w,float h,Action click,bool enabled,Color frame,Color fill,Transform parent=null)
        {
            var rect=Box(name,x,y,w,h,frame,parent);
            var face=Box("Fill",2,2,w-4,h-4,fill,rect).GetComponent<Image>(); face.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=face; button.interactable=enabled;
            var colors=button.colors; colors.highlightedColor=new Color(1.25f,1.22f,1.15f); colors.pressedColor=new Color(.8f,.76f,.68f); colors.disabledColor=new Color(.62f,.62f,.62f,.7f); button.colors=colors;
            if(click!=null) button.onClick.AddListener(()=>click());
            return button;
        }
        Button Button(string text,float x,float y,float w,float h,Action click,bool enabled=true,bool accent=false,Transform parent=null)
        {
            var button=Pressable(text,x,y,w,h,click,enabled,accent?(enabled?Amber:EmberEdge):Edge,accent?EmberDim:Panel,parent);
            var rect=(RectTransform)button.transform;
            int icon=ButtonIcon(text);
            bool illustrated=survivalArt!=null && icon>=0 && w>=180 && h>=48;
            float iconSize=h>=80?62:40;
            if(illustrated) ArtIcon(icon,12,(h-iconSize)/2,iconSize,iconSize,rect);
            float inset=illustrated?iconSize+28:22;
            var label=Label(text,inset,4,w-inset-16,h-8,h<64?22:26,enabled?(accent?AmberText:Paper):Faint,rect);
            label.fontStyle=FontStyle.Bold; label.alignment=text.Contains("\n")?TextAnchor.UpperLeft:TextAnchor.MiddleLeft;
            if(text.Contains("\n")) label.rectTransform.anchoredPosition=new Vector2(inset,-12);
            else if(w<160 && !illustrated) { label.alignment=TextAnchor.MiddleCenter; label.rectTransform.anchoredPosition=new Vector2(4,-4); label.rectTransform.sizeDelta=new Vector2(w-8,h-8); }
            return button;
        }
        // The single solid Amber call to action on a screen.
        Button PrimaryButton(string text,float x,float y,float w,float h,Action click,bool enabled=true,Transform parent=null)
        {
            var button=Pressable(text,x,y,w,h,click,enabled,enabled?Amber:EmberEdge,enabled?Amber:EmberDim,parent);
            var label=Label(text,26,4,w-52,h-8,28,enabled?OnAmber:Faint,button.transform); label.fontStyle=FontStyle.Bold; label.alignment=TextAnchor.MiddleLeft;
            return button;
        }
        void Chevron(float x,float y,Color color,Transform parent)
        {
            for(int i=0;i<2;i++)
            {
                var stroke=Box("Chevron",x,y+(i==0?0:12),16,3,color,parent);
                stroke.pivot=new Vector2(1,.5f); stroke.anchoredPosition=new Vector2(x+14,-(y+12)); stroke.localRotation=Quaternion.Euler(0,0,i==0?-45:45);
            }
        }
        Texture2D hatchTexture,fadeTexture;
        // Recovery that is blocked (injury, hunger, moral damage) is drawn as erased, rust-hatched ground.
        RawImage Hatched(string name,float x,float y,float w,float h,Transform parent=null)
        {
            if(hatchTexture==null)
            {
                hatchTexture=new Texture2D(12,12,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point};
                for(int py=0;py<12;py++) for(int px=0;px<12;px++) hatchTexture.SetPixel(px,py,(px+py)%12<5?Rust:RustDark);
                hatchTexture.Apply();
            }
            var image=Rect(name,parent??root,x,y,w,h).gameObject.AddComponent<RawImage>(); image.texture=hatchTexture; image.uvRect=new UnityEngine.Rect(0,0,w/12f,h/12f); image.raycastTarget=false; return image;
        }
        // Illustrations dissolve downward into the ink ground, the way the city dissolves into fog.
        void FadeToInk(float x,float y,float w,float h,bool upward=false)
        {
            if(fadeTexture==null)
            {
                fadeTexture=new Texture2D(1,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
                for(int i=0;i<64;i++) { float t=1-i/63f; fadeTexture.SetPixel(0,i,new Color(Ink.r,Ink.g,Ink.b,Mathf.SmoothStep(0,1,t))); }
                fadeTexture.Apply();
            }
            var image=Rect("Fade",root,x,y,w,h).gameObject.AddComponent<RawImage>(); image.texture=fadeTexture; image.raycastTarget=false;
            if(upward) image.uvRect=new UnityEngine.Rect(0,1,1,-1);
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
        RectTransform ScrollArea(string name,float y,float height,out RectTransform content) => ScrollArea(name,y,height,Panel,out content);
        RectTransform ScrollArea(string name,float y,float height,Color fill,out RectTransform content)
        {
            var area=Box(name,48,y,804,height,fill); area.GetComponent<Image>().raycastTarget=true; area.gameObject.AddComponent<RectMask2D>();
            content=Rect("Content",area,0,0,804,height);
            var scroll=area.gameObject.AddComponent<ScrollRect>(); scroll.content=content; scroll.viewport=area; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45; return area;
        }
        // journal: the diary-page treatment for what the survivor writes down about the current place.
        void Paragraph(string text,float y,float height,int size=27,bool journal=false)
        {
            ScrollArea("Scrollable text",y,height,journal?JournalPaper:Panel,out var content);
            var label=Label(text,24,16,756,height-32,size,journal?JournalInk:Paper,content); label.lineSpacing=journal?1.3f:1.18f;
            // Dark glyphs on the light page blend thin in linear color space; bold keeps them legible.
            if(journal) label.fontStyle=FontStyle.Bold;
            float h=Mathf.Max(height,label.preferredHeight+36); content.sizeDelta=new Vector2(804,h); label.rectTransform.sizeDelta=new Vector2(756,h-32);
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
            if(neighborInvitationPending && page=="home" && state.CurrentLocation=="home" && state.NeedsNeighborIntroduction && !state.TutorialActive) { NeighborIntroductionPage(); return; }
            int topic=page=="water"?2:page=="inventory"?3:page=="map"?4:page=="sleep"?5:0;
            if(state.ViewTutorialTopic(topic)) Save();
            switch(page)
            {
                case "tutorial": TutorialPage(); break;
                case "map": Map(); break;
                case "location": LocationDetails(); break;
                case "radio": RadioPage(); break;
                case "erasure": ErasureMapPage(); break;
                case "inventory": InventoryPage(); break;
                case "kitchen": Kitchen(); break;
                case "craft": CraftingPage(); break;
                case "sleep": SleepPage(); break;
                case "water": WaterPage(); break;
                case "diaries": DiaryPage(false); break;
                case "morning": MorningPage(); break;
                case "neighbor": NeighborPage(); break;
                case "companion": CompanionPage(); break;
                case "security": Security(); break;
                case "journal": Journal(); break;
                case "menu": Menu(); break;
                default: if(state.CurrentLocation=="home") Home(); else Explore(); break;
            }
            TutorialReturnLink();
            VitalsNotice();
        }
        // Header (0-440): day and clock, then the three-layer HP and mental bars and the body-state chips.
        // Pages own 454-1396; the dock and tab bar own everything below.
        void Header()
        {
            bool night=state.IsNight(definitions.TEMP_BALANCE);
            Label("DAY",48,48,64,30,21,Muted);
            var day=Label(state.day.ToString("00"),104,22,130,66,56,Paper); day.fontStyle=FontStyle.Bold;
            Box("Header rule",242,34,2,54,Edge);
            Label(state.CalendarDate.ToString("yyyy.MM.dd"),268,28,420,30,21,Muted);
            Box(night?"Night":"Day",268,72,12,12,night?Cold:Amber);
            var clock=Label(state.hour.ToString("00")+":00  ·  "+(night?"밤":"낮")+"  ·  "+definitions.Location(state.CurrentLocation).name,292,60,460,38,25,Paper); clock.fontStyle=FontStyle.Bold;
            var menu=Pressable("메뉴",764,16,88,88,()=>{page="menu"; Render();},(state.CanAct || state.IsGameOver) && !state.HasLifeNotice,Ink,Ink);
            for(int i=0;i<3;i++) Box("Menu line",24,26+i*16,i==2?26:40,4,menu.interactable?Paper:Faint,menu.transform);

            int cap=state.Vitals.RecoverableMaxHP(definitions.TEMP_BALANCE);
            Label("체력  "+state.Vitals.CurrentHP+" / "+state.Vitals.BaseMaxHP,48,122,500,42,28,Paper).fontStyle=FontStyle.Bold;
            var capLabel=Label("회복 상한  "+cap,552,128,300,34,22,Amber); capLabel.alignment=TextAnchor.UpperRight;
            Hatched("HP blocked",48,170,804,16);
            Box("HP recoverable",48,170,804*(cap/(float)state.Vitals.BaseMaxHP),16,Track);
            Box("Current HP",48,170,804*(Mathf.Min(state.Vitals.CurrentHP,cap)/(float)state.Vitals.BaseMaxHP),16,Amber);
            Label("정신력 "+state.Vitals.CurrentMental+" / "+state.Vitals.MentalCap,48,198,500,40,26,Paper).fontStyle=FontStyle.Bold;
            var sleep=Label("수면 "+state.SleepHours+" / 6h",552,204,300,32,21,Muted); sleep.alignment=TextAnchor.UpperRight;
            Hatched("Mental blocked",48,244,804,10);
            Box("Mental capacity",48,244,804*(state.Vitals.MentalCap/(float)state.Vitals.BaseMaxMental),10,Track);
            Box("Mental current",48,244,804*(state.Vitals.CurrentMental/(float)state.Vitals.BaseMaxMental),10,Cold);

            VitalChip(0,"허기 "+state.Vitals.Hunger,state.Vitals.Hunger/100f,new Color32(201,194,175,255),false);
            VitalChip(1,"목마름 "+state.Thirst+"/100",state.Thirst/100f,Amber,state.Thirst>=60);
            VitalChip(2,"졸림 "+state.Sleepiness+"/100",state.Sleepiness/100f,Cold,state.NeedsSleepWarning);
            var body=VitalChip(3,"부상 "+state.Vitals.Injuries.Count,-1,Paper,false);
            Label("힘 "+state.Strength(definitions)+" · 방어 "+state.Defense(definitions),16,48,170,28,19,Muted,body);
            SurvivalWarning();
        }
        RectTransform VitalChip(int index,string text,float fill,Color barColor,bool warning)
        {
            var chip=Box("Vital chip",48+index*207,276,195,86,warning?EmberEdge:Panel);
            if(warning) Box("Fill",2,2,191,82,EmberDim,chip);
            var label=Label(text,16,12,170,34,22,warning?Amber:Paper,chip); label.fontStyle=warning?FontStyle.Bold:FontStyle.Normal;
            if(fill>=0) { Box("Track",16,64,163,4,warning?EmberEdge:Track,chip); Box("Level",16,64,163*Mathf.Clamp01(fill),4,barColor,chip); }
            return chip;
        }
        // Dock (1412-1496) holds water, crafting and the time-advancing action; the tab bar (1508-1600) sits under the thumb.
        void Dock()
        {
            bool home=state.CurrentLocation=="home", ready=state.CanAct && !state.HasLifeNotice, thirsty=state.NeedsWaterWarning;
            var water=Pressable("식수 관리",48,1412,204,84,()=>{page="water";message="";Render();},ready,thirsty?Amber:EmberEdge,thirsty?EmberDim:Raised);
            Label("식수 관리",18,10,180,34,22,ready?AmberText:Faint,water.transform).fontStyle=FontStyle.Bold;
            int carried=state.Backpack.Count("water"), stored=state.HomeStorage.Count("water");
            Label(state.UnlimitedWater?"무제한":carried+(home?stored:0)==0?"마실 물 없음":"소지 "+carried+(home?" · 집 "+stored:""),18,46,180,30,19,thirsty?Amber:Muted,water.transform);
            Button("제작",264,1412,150,84,()=>{page="craft";message="";Render();},home && ready);
            PrimaryButton(home?(state.IsNight(definitions.TEMP_BALANCE)?"아침까지 머무른다":"밤까지 머무른다"):"집으로 돌아간다",426,1412,426,84,()=>{if(home) Apply(state.Rest(definitions,true)); else SelectLocation("home");},ready);
            Box("Tab bar",0,1504,900,96,NavBar); Box("Tab rule",0,1504,900,2,Edge);
            string[] names={"생활","외출","물품","기록"}, ids={"home","map","inventory","journal"};
            int[] icons={1,4,2,3};
            for(int i=0;i<4;i++)
            {
                string id=ids[i];
                bool active=page==id || (id=="map" && page=="location");
                var tab=Pressable(names[i],i*225,1506,225,94,()=>{page=id; message=""; selectedInventoryItem=null; Render();},ready,Color.clear,Color.clear);
                if(active) Box("Active tab",76,0,73,4,Amber,tab.transform);
                var icon=AtlasPicture(88,10,48,48,IconRect(icons[i]),tab.transform);
                if(icon!=null) icon.color=active?Color.white:new Color(1,1,1,.5f);
                var label=Label(names[i],0,60,225,28,21,active?Amber:ready?Muted:Faint,tab.transform); label.alignment=TextAnchor.UpperCenter; label.fontStyle=FontStyle.Bold;
            }
        }
        // Full-bleed illustration (384-754, rising behind the header strip) with the place name set into its fog.
        void Hero(string title,string caption=null,bool cautionCaption=false)
        {
            if(survivalArt!=null && state.CurrentLocation=="home") AtlasPicture(0,384,900,370,new UnityEngine.Rect(0,.51f,1,.411f));
            else Picture(0,384,900,370);
            FadeToInk(0,374,900,40,true);
            FadeToInk(0,574,900,182);
            root.Find("Survival alert")?.SetAsLastSibling();
            Label(title,48,caption==null?684:650,804,60,44,Paper).fontStyle=FontStyle.Bold;
            if(caption!=null) Label(caption,48,712,804,34,22,cautionCaption?Amber:new Color32(183,189,182,255));
        }
        void Choice(int index,string title,string detail,float y,Action action,bool enabled=true)
        {
            shortcuts[index]=enabled?action:null;
            var rect=(RectTransform)Pressable((index+1).ToString("00")+"  "+title+"\n     "+detail,48,y,804,94,action,enabled,Edge,Panel).transform;
            int icon=ButtonIcon(title);
            float inset=26;
            if(survivalArt!=null && icon>=0) { ArtIcon(icon,12,11,72,72,rect); inset=104; }
            Label((index+1).ToString("00"),inset,16,44,30,21,enabled?Amber:Faint,rect).fontStyle=FontStyle.Bold;
            var name=Label(title,inset+42,10,804-inset-100,40,28,enabled?Paper:Faint,rect); name.fontStyle=FontStyle.Bold;
            Label(detail,inset,52,804-inset-60,34,21,enabled?Muted:Faint,rect);
            Chevron(760,35,enabled?Faint:Edge,rect);
        }
        string ReportText => string.IsNullOrEmpty(message)?state.lastReport:message;
        void Home()
        {
            Hero("송파구 자취방","집 · 스팸 "+state.HomeStorage.Count("spam")+" · 참치 "+state.HomeStorage.Count("tuna")+" · 쌀 "+definitions.Quantity("rice",state.HomeStorage.Count("rice")));
            float badgeRight=852;
            if(state.CompanionAvailable) badgeRight=HeroBadge("동행 약속",badgeRight,()=>{page="companion";message="";Render();},true,false)-12;
            HeroBadge("옆집 안부"+(state.NeighborStage==0?" · 첫 인사":""),badgeRight,()=>{state.NeighborIntroductionSeen=true;neighborInvitationPending=false;Save();page="neighbor";message="";Render();},state.CanAct,state.NeighborStage==0);
            if(state.TutorialActive) TutorialHomeCard();
            else Paragraph(string.IsNullOrEmpty(message) && state.journal.Count==1?definitions.Location("home").description:ReportText,776,214,25,true);
            Choice(0,"요리하고 식사한다","집에 보관한 식량을 사용한다",1010,()=>{page="kitchen"; message=""; Render();});
            Choice(1,"잠을 잔다","하루 6시간 필요 · 수면 시간을 선택한다",1122,()=>{page="sleep";message="";Render();});
            Choice(2,state.Story.HasRadio?"라디오를 켠다":"제작대를 확인한다",state.Story.HasRadio?"SP 기지국의 방송을 수신한다":"음식 · 무기 · 생활용품을 만든다",1234,()=>{if(state.Story.HasRadio) OpenRadio(); else {page="craft"; message=""; Render();}});
        }
        // A pill set over the illustration's top-right corner; returns its left edge so badges can line up.
        float HeroBadge(string text,float right,Action click,bool enabled,bool highlighted)
        {
            float w=text.Length*23+(highlighted?70:48);
            var badge=Pressable(text,right-w,474,w,60,click,enabled,new Color32(58,71,68,255),new Color(Ink.r,Ink.g,Ink.b,.86f));
            float x=24;
            if(highlighted) { Box("Notice dot",24,24,12,12,Amber,badge.transform); x=46; }
            Label(text,x,4,w-x-12,52,22,enabled?Paper:Faint,badge.transform).alignment=TextAnchor.MiddleLeft;
            return right-w;
        }
        void Explore()
        {
            var location=definitions.Location(state.CurrentLocation);
            if(location.storyOnly) { SupplySitePage(); return; }
            Hero(location.name,state.LootSummary(location.id,definitions),true);
            Paragraph(string.IsNullOrEmpty(message)?location.description:message,776,214,25,true);
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
                    ArtIcon(7,18,y,42,42,content); Label(recipe.name,72,y,714,40,28,Paper,content);
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
                    ArtIcon(RecipeIcon(recipe.id),18,y,42,42,content); Label(recipe.name,72,y,714,40,28,Paper,content);
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
        string selectedInventoryItem;
        int inventoryFilter;
        static readonly string[] InventoryFilters={"전체","식량","물","의약","장비 · 재료"};
        int InventoryCategory(ItemDefinition item) => item.id.Contains("water")?2:item.injuriesTreated>0 || (item.healing>0 && item.hungerRelief==0)?3:item.consumable || item.id=="rice"?1:4;
        // Items are a grid of tiles; choosing one opens a sheet with its quantity stepper and actions.
        void InventoryPage()
        {
            bool atHome=state.CurrentLocation=="home";
            Label("물품",48,462,560,60,44,Paper).fontStyle=FontStyle.Bold;
            Button("전체 보관",652,464,200,64,()=>Apply(state.StoreAll(definitions),"inventory"),atHome && state.CanAct && definitions.items.Any(x=>x.id!="burner" && state.Backpack.Count(x.id)>0));
            if(string.IsNullOrEmpty(message)) Label(atHome?"소지품과 집의 물자 · 전체 보관은 착용 장비 제외":"물품 정리는 집에서 가능하다",48,528,804,34,22,Muted);
            else Label(message,48,522,804,62,21,AmberText);
            var owned=definitions.items.OrderByDescending(x=>x.injuriesTreated).Where(x=>state.Backpack.Count(x.id)+state.HomeStorage.Count(x.id)>0 || state.Equipment.Contains(x.id)).ToList();
            float chipX=48;
            for(int i=0;i<InventoryFilters.Length;i++)
            {
                int filter=i; bool active=inventoryFilter==i;
                string caption=InventoryFilters[i]+(i==0?" "+owned.Count:"");
                float w=caption.Length*22+44;
                var chip=Pressable("분류:"+InventoryFilters[i],chipX,592,w,60,()=>{inventoryFilter=filter;Render();},true,active?Paper:Edge,active?Paper:Panel);
                var label=Label(caption,0,4,w,52,21,active?OnAmber:new Color32(201,194,175,255),chip.transform); label.alignment=TextAnchor.MiddleCenter; label.fontStyle=FontStyle.Bold;
                chipX+=w+12;
            }
            var visible=owned.Where(x=>inventoryFilter==0 || InventoryCategory(x)==inventoryFilter).ToList();
            var area=ScrollArea("Inventory",672,724,Color.clear,out var content); inventoryScroll=area.GetComponent<ScrollRect>();
            const float tileW=257,tileH=208,gap=16;
            for(int i=0;i<visible.Count;i++)
            {
                var item=visible[i]; string id=item.id;
                int pack=state.Backpack.Count(id), home=state.HomeStorage.Count(id); bool equipped=state.Equipment.Contains(id), selected=selectedInventoryItem==id;
                var tile=Pressable("물품:"+id,(i%3)*(tileW+gap),(i/3)*(tileH+gap),tileW,tileH,()=>{selectedInventoryItem=id;Render();},true,selected?Amber:Edge,selected?EmberDim:Panel,content).transform;
                ArtIcon(ItemIcon(id),18,18,88,88,tile);
                if(equipped) { Box("Equipped",tileW-92,18,74,36,Amber,tile); var tag=Label("착용",0,0,74,36,19,OnAmber,tile.GetChild(tile.childCount-1)); tag.alignment=TextAnchor.MiddleCenter; tag.fontStyle=FontStyle.Bold; }
                Label(item.name,18,118,tileW-36,38,26,Paper,tile).fontStyle=FontStyle.Bold;
                Label(pack+home==0?"착용 중":"소지 "+definitions.Quantity(id,pack)+" · 집 "+definitions.Quantity(id,home),18,160,tileW-36,32,20,selected?new Color32(201,194,175,255):Muted,tile);
            }
            if(visible.Count==0) Label("이 분류에 해당하는 물품이 없다.",0,24,804,40,23,Muted,content);
            content.sizeDelta=new Vector2(804,Mathf.Max(724,Mathf.Ceil(visible.Count/3f)*(tileH+gap)));
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(inventoryScrollOffset,0,content.sizeDelta.y-724));
            var chosen=owned.Find(x=>x.id==selectedInventoryItem);
            if(chosen==null) selectedInventoryItem=null; else InventorySheet(chosen,atHome);
        }
        void InventorySheet(ItemDefinition item,bool atHome)
        {
            string id=item.id;
            int pack=state.Backpack.Count(id), home=state.HomeStorage.Count(id); bool equipped=state.Equipment.Contains(id);
            bool canTransfer=id!="burner" && atHome;
            var actions=new List<Action<float,float,float>>();
            if(canTransfer)
            {
                actions.Add((x,y,w)=>Button("챙기기",x,y,w,96,()=>Apply(state.Transfer(id,true,TransferQuantity(id),definitions),"inventory"),home>0));
                actions.Add((x,y,w)=>Button("보관",x,y,w,96,()=>Apply(state.Transfer(id,false,TransferQuantity(id),definitions),"inventory"),pack>0));
            }
            if(id=="water") actions.Add((x,y,w)=>PrimaryButton("마시기",x,y,w,96,()=>Apply(state.DrinkWater(),"inventory"),state.CanDrink));
            else if(item.injuriesTreated>0) actions.Add((x,y,w)=>PrimaryButton("치료",x,y,w,96,()=>Apply(state.TreatInjury(id,definitions),"inventory"),state.CanAct && state.Vitals.Injuries.Count>0 && (pack>0 || (atHome && home>0))));
            else if(item.consumable) actions.Add((x,y,w)=>PrimaryButton("먹기",x,y,w,96,()=>Apply(state.Eat(id,definitions),"inventory"),pack>0 || (atHome && home>0)));
            else if(item.strength>0 || item.defense>0) actions.Add((x,y,w)=>PrimaryButton(equipped?"벗기":"착용",x,y,w,96,()=>Apply(state.Equip(id,definitions),"inventory")));
            float height=188+(canTransfer?112:0)+(actions.Count>0?96:0)+48, top=1504-height;
            Pressable("Sheet scrim",0,0,900,top,()=>{selectedInventoryItem=null;Render();},true,new Color(.02f,.04f,.04f,.82f),Color.clear);
            Box("Sheet",0,top,900,height,Raised); Box("Sheet rule",0,top,900,2,new Color32(51,65,63,255)); Box("Sheet handle",414,top+14,72,8,new Color32(58,71,68,255));
            ArtIcon(ItemIcon(id),48,top+44,120,120);
            Label(item.name,196,top+44,480,52,40,new Color32(239,233,216,255)).fontStyle=FontStyle.Bold;
            var counts=Label(pack+home==0?"착용 중":"소지 "+definitions.Quantity(id,pack)+" · 집 "+definitions.Quantity(id,home),196,top+102,560,34,24,new Color32(201,194,175,255));
            string detail=id=="water"?"500ml · 식수 자동 소비":item.injuriesTreated>0?"부상 "+item.injuriesTreated+"개 치료 · "+definitions.TEMP_BALANCE.treatmentHours+"시간 · 체력 +"+item.healing:item.strength>0 || item.defense>0?(item.strength>0?"힘 +"+item.strength:"")+(item.strength>0 && item.defense>0?" · ":"")+(item.defense>0?"방어 +"+item.defense:"")+(equipped?" · 착용 중":""):!atHome && id!="burner"?"물품 정리는 집에서 가능하다":"";
            Label(detail,196,top+138,620,32,21,Muted);
            var close=Pressable("시트 닫기",780,top+30,72,72,()=>{selectedInventoryItem=null;Render();},true,Raised,Raised);
            Label("×",0,0,72,72,40,Muted,close.transform).alignment=TextAnchor.MiddleCenter;
            float rowY=top+188;
            if(canTransfer)
            {
                Label("수량"+(id=="rice"?" (g)":""),48,rowY+26,120,36,22,Muted).fontStyle=FontStyle.Bold;
                Button("−",156,rowY,88,88,()=>ChangeTransferQuantity(id,-1,Math.Max(pack,home)));
                var inputRect=Box("수량:"+id,252,rowY,170,88,Ink); inputRect.GetComponent<Image>().raycastTarget=true;
                var input=inputRect.gameObject.AddComponent<InputField>(); input.targetGraphic=inputRect.GetComponent<Image>();
                var text=Label("",8,8,154,72,30,Paper,inputRect); text.alignment=TextAnchor.MiddleCenter;
                input.textComponent=text; input.contentType=InputField.ContentType.IntegerNumber; input.keyboardType=TouchScreenKeyboardType.NumberPad; input.characterLimit=9;
                input.text=TransferQuantity(id).ToString();
                input.onValueChanged.AddListener(value=>transferQuantities[id]=int.TryParse(value,out int quantity)?Math.Max(0,quantity):0);
                Button("+",430,rowY,88,88,()=>ChangeTransferQuantity(id,1,Math.Max(pack,home)));
                Label(id=="rice"?"1g 단위":"1개 단위",542,rowY+28,300,34,21,Muted);
                rowY+=112;
            }
            float buttonW=(804-16*(actions.Count-1))/(float)Math.Max(1,actions.Count);
            for(int i=0;i<actions.Count;i++) actions[i](48+i*(buttonW+16),rowY,buttonW);
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
            Label("기록을 정리한다",48,470,804,60,36,Paper); Paragraph(ReportText,560,140);
            Button(state.TutorialActive?"생존 안내 이어 보기":"처음 생존 안내",48,708,804,52,()=>{if(!state.TutorialActive)state.BeginTutorial();page="tutorial";message="";Save();Render();},state.CanAct);
            Button("제작 · 음식 / 무기 / 생활용품",48,780,804,84,()=>{page="craft";message="";Render();},state.CurrentLocation=="home");
            Button("기록 저장",48,890,390,84,()=>{if(Save()) message="현재 기록을 저장했다.";Render();},true,true);
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
            state.BeginTutorial(); neighborInvitationPending=false; vitalsNotice=""; waterWasWarning=sleepWasWarning=false;
            transferQuantities.Clear(); inventoryScrollOffset=0; inventoryScroll=null;
            neighborhoodPositioned=false; neighborhoodScroll=null;
            confirmRestart=false; page="tutorial"; message=""; Save(); Render();
        }
        bool waterWasWarning,sleepWasWarning;
        void Apply(string text,string next="home")
        {
            vitalsNotice="";
            if(state.NeedsWaterWarning && !waterWasWarning) vitalsNotice="마실 물이 부족하다. 하단 식수 관리에서 물을 확보하거나 마시자.";
            if(state.NeedsSleepWarning && !sleepWasWarning) vitalsNotice+=(vitalsNotice.Length>0?"\n":"")+"졸림이 높다. 집에서 잠을 자야 한다.";
            waterWasWarning=state.NeedsWaterWarning; sleepWasWarning=state.NeedsSleepWarning;
            message=text; page=next; Save(); Render();
        }
        bool Save() { try { store.Save(state); return true; } catch(Exception e) { Debug.LogWarning("Save failed: "+e.Message); message="기록을 저장하지 못했다. 저장 공간을 확인해 주세요."; return false; } }
        void Load()
        {
            if(store.TryLoad(out var restored,out var error)) { state=restored; confirmRestart=false; neighborInvitationPending=state.NeedsNeighborIntroduction; page=state.TutorialActive?"tutorial":"home"; message="";vitalsNotice="";waterWasWarning=sleepWasWarning=false; Render(); }
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
                // A craft can cross midnight; the day's diary then takes over the screen until it is read.
                if(state.NextDiary!=null)ClickGameButton("읽고 계속");
            };
            ClickGameButton("무기");make("club",false);make("wrapped_club",false);make("nail_bat",false);
            if(state.HomeStorage.Count("club")!=0 || state.HomeStorage.Count("wrapped_club")!=0 || state.HomeStorage.Count("nail_bat")!=1)throw new InvalidOperationException("Weapon upgrade chain failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"WEAPONS.png"));
            ClickGameButton("물품");ClickGameButton("물품:nail_bat");ClickGameButton("착용");if(state.Strength(definitions)!=6)throw new InvalidOperationException("Crafted weapon equip failed.");
            ClickGameButton("제작");ClickGameButton("생활용품");make("workbench",false);make("water_filter",false);make("water_collector",false);make("collect_water",false);
            int water=state.HomeStorage.Count("water");make("purify_water",false);
            if(state.NextDiary!=null)ClickGameButton("읽고 계속");
            if(state.HomeStorage.Count("raw_water")!=2 || state.HomeStorage.Count("water")!=water+3 || craftTab!=2)throw new InvalidOperationException("Water crafting or tab restoration after diary failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"WATER-SURVIVAL.png"));
            state.Vitals.Hunger=0;make("bedroll",false);make("door1",true);
            if(!state.HasFacility("bedroll") || state.SleepMentalRecovery!=3 || state.DoorLevel!=1)throw new InvalidOperationException("Living utility effects/door upgrade failed.");
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"DOOR-UPGRADE.png"));
            ClickGameButton("무기");make("machete",false);ClickGameButton("물품");ClickGameButton("물품:machete");ClickGameButton("착용");
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
            page="inventory"; message=""; Render(); ClickGameButton("물품:water"); yield return null;
            var input=root.GetComponentsInChildren<InputField>().First(x=>x.name=="수량:water"); input.text="2";
            root.GetComponentsInChildren<Button>().First(x=>x.name=="챙기기").onClick.Invoke(); yield return null;
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
            Func<string,string,Button> action=(id,caption)=>root.GetComponentsInChildren<Button>().First(x=>x.name==caption);
            var woodTile=inventoryScroll.content.GetComponentsInChildren<Button>().First(x=>x.name=="물품:wood").GetComponent<RectTransform>();
            float offset=Mathf.Clamp(-woodTile.anchoredPosition.y-120,0,inventoryScroll.content.sizeDelta.y-724);
            if(offset<=0) throw new InvalidOperationException("Storage fixture must scroll the item grid.");
            inventoryScroll.content.anchoredPosition=new Vector2(0,offset); yield return null;
            ClickGameButton("물품:wood"); yield return null;
            if(Mathf.Abs(inventoryScroll.content.anchoredPosition.y-offset)>1) throw new InvalidOperationException("Opening the item sheet lost the grid scroll.");
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
            page="inventory"; Render(); yield return null; ClickGameButton("물품:bandage"); ClickGameButton("치료");
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
                if(state.PlayerNickname!="기록자A1" || page!="tutorial" || !state.TutorialActive) throw new InvalidOperationException("Main game/profile/tutorial handoff failed.");
                ClickGameButton("안내 건너뛰기");ClickGameButton("지금은 생활부터");
                var restored=JsonUtility.FromJson<SurvivalState>(File.ReadAllText(SavePath));
                if(restored.PlayerNickname!=state.PlayerNickname) throw new InvalidOperationException("Nickname save round trip failed.");
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdOpeningOnly")>=0)
                {
                    Debug.Log("VD_TUTORIAL_OPENING_UI_OK: actual opening, nickname, tutorial handoff, skip, neighbor invitation and profile save.");
                    Application.Quit();yield break;
                }
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
                if(!state.IsLocationUnlocked("empty_house",definitions) || state.IsLocationUnlocked("school",definitions) || state.Locations.Find(x=>x.id=="store").searches!=1 || state.LootPercent("store",definitions)<=0 || state.LootPercent("store",definitions)>=100) throw new InvalidOperationException("First route/finite-loot unlock failed.");
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
                foreach(var diary in state.Diaries) diary.read=true;
                if(state.HasPendingMorning) state.DismissMorning();
                page="inventory"; Render(); ClickGameButton("물품:clothes"); ClickGameButton("착용"); ClickGameButton("물품:weapon"); ClickGameButton("착용");
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
                state.CurrentLocation="home"; state.AwayFromHome=false; state.PendingEncounter=null; state.Backpack.Add("bandage",1); page="inventory"; selectedInventoryItem="bandage"; Render();
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
                ClickGameButton("제작"); ClickGameButton("생활용품");
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
