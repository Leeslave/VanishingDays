using System;
using System.Collections;
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
            Clear();
            if(page=="continue") { ContinuePage(); return; }
            Header(); Dock();
            if(confirmRestart) { RestartPage(); return; }
            if(state.IsGameOver && page!="menu") { GameOver(); return; }
            if(state.PendingEncounter!=null) { EncounterPage(); return; }
            if(state.HasActiveCrowdIncident) { CrowdIncidentPage(); return; }
            switch(page)
            {
                case "map": Map(); break;
                case "location": LocationDetails(); break;
                case "radio": RadioPage(); break;
                case "inventory": InventoryPage(); break;
                case "kitchen": Kitchen(); break;
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
            string hunger=state.Vitals.Hunger>=definitions.TEMP_BALANCE.starvingAt?"심한 굶주림":state.Vitals.Hunger>=definitions.TEMP_BALANCE.hungryAt?"허기짐":"버틸 만함";
            Label("허기 "+state.Vitals.Hunger+" · "+hunger+"  /  부상 "+state.Vitals.Injuries.Count,48,270,804,38,23,Muted);
            Label("힘 "+state.Strength(definitions)+"  ·  방어 "+state.Defense(definitions),48,313,804,34,23,Muted);
            string[] names={"생활","외출","물품","기록"}, ids={"home","map","inventory","journal"};
            for(int i=0;i<4;i++) { string id=ids[i]; Button(names[i],48+i*206,362,186,62,()=>{page=id; message=""; Render();},state.CanAct,page==id); }
        }
        void Dock()
        {
            Box("Dock",48,1412,804,138,Panel);
            Label(state.CurrentLocation=="home"?"송파구 자취방  ·  물과 가스는 충분하다.":"소지 물품과 집의 보관 물품은 별개다.",66,1422,768,38,22,Muted);
            bool home=state.CurrentLocation=="home";
            Button(home?(state.IsNight(definitions.TEMP_BALANCE)?"아침까지 머무른다":"밤까지 머무른다"):"집으로 돌아간다",66,1477,540,58,()=>{if(home) Apply(state.Rest(definitions,true)); else SelectLocation("home");},state.CanAct,true);
            Button("메뉴",626,1477,208,58,()=>{page="menu"; Render();},state.CanAct || state.IsGameOver);
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
            Choice(1,"집에서 쉰다",definitions.TEMP_BALANCE.restHours+"시간 · 연속 "+definitions.TEMP_BALANCE.injuryRestHours+"시간마다 부상 1곳 회복",1122,()=>Apply(state.Rest(definitions)));
            Choice(2,state.Story.HasRadio?"라디오를 켠다":"정비와 보안",state.Story.HasRadio?"SP 기지국의 방송을 수신한다":"집의 침입 저항과 외부 주목도를 확인한다",1234,()=>{if(state.Story.HasRadio) OpenRadio(); else {page="security"; message=""; Render();}});
        }
        void Explore()
        {
            var location=definitions.Location(state.CurrentLocation);
            if(location.storyOnly) { SupplySitePage(); return; }
            Hero(location.name); Paragraph(string.IsNullOrEmpty(message)?location.description:message,790,180);
            Choice(0,"주변을 수색한다",location.searchHours+"시간 · 안쪽에도 다른 사람이 있을 수 있다",1010,()=>Apply(state.Search(definitions)));
            Choice(1,"소지 물품을 확인한다","식량과 도구를 정리한다",1122,()=>{page="inventory"; Render();});
            Choice(2,"집으로 돌아간다",location.travelHours+"시간 · 귀갓길에도 조우할 수 있다",1234,()=>SelectLocation("home"));
        }
        void Map()
        {
            Label("송파구 일대",48,462,804,60,36,Paper);
            Label("장소를 눌러 정보를 확인하세요.  /  약도",48,528,804,38,24,Muted);
            var map=Rect("Neighborhood map",root,48,586,804,690).gameObject.AddComponent<NeighborhoodMapGraphic>();
            map.CurrentLocation=state.CurrentLocation; map.SupplyRumorUnlocked=state.Story.SupplyRumorUnlocked; map.raycastTarget=false;
            foreach(var location in definitions.locations.Where(x=>state.IsLocationUnlocked(x.id,definitions)))
            {
                Vector2 p=NeighborhoodMapGraphic.Position(location.id); float x=48+p.x*804,y=586+p.y*690;
                bool current=location.id==state.CurrentLocation;
                var hit=Box("지도:"+location.id,x-100,y-44,200,116,Color.clear); hit.GetComponent<Image>().raycastTarget=true;
                var button=hit.gameObject.AddComponent<Button>(); button.targetGraphic=hit.GetComponent<Image>(); button.onClick.AddListener(()=>SelectLocation(location.id));
                Box("Map label",x-100,y+40,200,38,Ink);
                var label=Label(string.IsNullOrEmpty(location.mapLabel)?location.name:location.mapLabel,x-100,y+43,200,34,25,current?Amber:Paper); label.alignment=TextAnchor.MiddleCenter;
                if(current) { var here=Label("현재 위치",x-100,y+80,200,30,20,Amber); here.alignment=TextAnchor.MiddleCenter; }
            }
            Label("● 현재 위치   ○ 탐색 가능한 장소",48,1296,804,36,23,Amber);
            Label("장소 선택 → 위치 정보 → 이동하기",48,1346,804,36,24,Muted);
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
            string resources=location.storyOnly?"확인되지 않은 제보 · 일반 파밍 불가\n낮에 현장을 살피고 접근 여부를 선택한다.":location.id=="home"?"요리 · 물자 보관 · 휴식 · 부상 치료":"수색 "+location.searchHours+"시간 / 회\n발견 가능: "+LootNames(location);
            Paragraph(resources+"\n\n"+risk,952,234,25);
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
            bool available=state.CanAct && state.CurrentLocation=="home" && state.CanUseRadio;
            Apply(state.ListenRadio(definitions),available?"radio":"home");
        }
        void RadioPage()
        {
            if(!state.Story.SupplyRumorUnlocked || !state.CanUseRadio || state.CurrentLocation!="home") { Home(); return; }
            var script=definitions.radioStory;
            var broadcast=state.LastRadioBroadcastId==script.firstBroadcast.id?script.firstBroadcast:script.generalBroadcast;
            Label("라디오 수신 기록",48,462,804,56,36,Paper);
            Label(broadcast.title,48,540,804,54,25,Amber);
            Label("방송 본문 · 위아래로 스크롤",48,584,804,24,18,Muted);
            Paragraph(broadcast.body,610,510,27);
            Paragraph("[정보 카드]\n"+script.clueCard,1142,134,23);
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
            Paragraph(body,622,state.Crowd.Step==CrowdStoryStep.Panic?430:550,28);
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
        void Kitchen()
        {
            Label("부르스타 앞에서",48,462,804,60,36,Paper);
            Paragraph(string.IsNullOrEmpty(message)?"물을 올리고 남은 재료를 확인한다. 만든 음식은 먹거나 집에 보관할 수 있다.":message,542,110,25);
            for(int i=0;i<definitions.recipes.Length;i++)
            {
                var recipe=definitions.recipes[i]; float y=680+i*172; Box(recipe.name,48,y,804,154,Panel);
                Label(recipe.name,68,y+10,760,36,28,Paper); Label(Cost(recipe.ingredients)+" · "+recipe.hours+"시간",68,y+52,760,32,21,Muted);
                bool home=state.CurrentLocation=="home";
                Button("조리한다",68,y+96,360,48,()=>Apply(state.Cook(recipe.id,definitions),"kitchen"),home && state.HomeStorage.Has(recipe.ingredients),true);
                Button("먹는다 · "+(state.HomeStorage.Count(recipe.result)+state.Backpack.Count(recipe.result)),452,y+96,380,48,()=>Apply(state.Eat(recipe.result,definitions),"kitchen"),state.Backpack.Count(recipe.result)>0 || (home && state.HomeStorage.Count(recipe.result)>0));
            }
        }
        string Cost(ItemStack[] ingredients) { var costs=new string[ingredients.Length]; for(int i=0;i<costs.Length;i++) costs[i]=definitions.Item(ingredients[i].id).name+" "+definitions.Quantity(ingredients[i].id,ingredients[i].amount); return string.Join(" · ",costs); }
        void InventoryPage()
        {
            Label("소지품과 집의 물자",48,462,804,60,36,Paper);
            Paragraph(string.IsNullOrEmpty(message)?"약품 1개로 부상 1곳을 치료한다. 집에 둔 물건은 귀가해야 사용할 수 있다.":message,542,108,25);
            ScrollArea("Inventory",678,694,out var content); float y=12;
            foreach(var item in definitions.items.OrderByDescending(x=>x.injuriesTreated))
            {
                int pack=state.Backpack.Count(item.id), home=state.HomeStorage.Count(item.id); bool equipped=state.Equipment.Contains(item.id);
                if(pack+home==0 && !equipped) continue;
                Label(item.name+(equipped?" · 착용 중":""),18,y,768,40,28,Paper,content);
                Label("소지 "+definitions.Quantity(item.id,pack)+" / 집 "+definitions.Quantity(item.id,home)+(item.injuriesTreated>0?" · 부상 치료 "+definitions.TEMP_BALANCE.treatmentHours+"시간":""),18,y+48,768,36,23,Muted,content);
                float buttonX=18;
                if(item.id!="burner" && state.CurrentLocation=="home")
                {
                    Button("챙기기",buttonX,y+96,230,52,()=>Apply(state.Transfer(item.id,true,definitions),"inventory"),home>0,false,content); buttonX+=250;
                    Button("보관",buttonX,y+96,230,52,()=>Apply(state.Transfer(item.id,false,definitions),"inventory"),pack>0,false,content); buttonX+=250;
                }
                if(item.injuriesTreated>0) Button("치료",buttonX,y+96,230,52,()=>Apply(state.TreatInjury(item.id,definitions),"inventory"),state.CanAct && state.Vitals.Injuries.Count>0 && (pack>0 || (state.CurrentLocation=="home" && home>0)),true,content);
                else if(item.consumable) Button("먹기",buttonX,y+96,230,52,()=>Apply(state.Eat(item.id,definitions),"inventory"),pack>0 || (state.CurrentLocation=="home" && home>0),true,content);
                else if(item.strength>0 || item.defense>0) Button(equipped?"벗기":"착용",buttonX,y+96,230,52,()=>Apply(state.Equip(item.id,definitions),"inventory"),true,true,content);
                y+=178;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(694,y));
        }
        void Security()
        {
            Label("집을 지키는 방법",48,462,804,60,36,Paper);
            Paragraph("침입 저항 "+state.Protection(definitions)+" · 외부 주목 "+state.Attention(definitions)+"\n"+(string.IsNullOrEmpty(message)?"고급 설비가 눈에 띄면 물자가 있는 집으로 보일 수 있다.":message),542,156,25);
            for(int i=0;i<definitions.security.Length;i++)
            {
                var device=definitions.security[i]; float y=744+i*260; bool installed=state.InstalledSecurity.Contains(device.id);
                Box(device.name,48,y,804,236,Panel); Label(device.name,68,y+14,764,42,30,Paper);
                Label("침입 저항 +"+device.protection+" / 외부 주목 +"+device.attention+"\n"+Cost(device.ingredients)+" · "+device.hours+"시간",68,y+70,764,86,23,Muted);
                Button(installed?"설치됨":"보강한다",68,y+170,764,54,()=>Apply(state.InstallSecurity(device.id,definitions),"security"),state.CurrentLocation=="home" && !installed && state.HomeStorage.Has(device.ingredients),true);
            }
        }
        void Journal()
        {
            Label("생존 기록",48,462,804,60,36,Paper); Label("작성자 : "+state.PlayerNickname,48,540,804,48,26,Muted);
            ScrollArea("Journal",620,752,out var content); float y=18;
            foreach(var entry in state.journal) { var text=Label(entry,18,y,768,200,25,Paper,content); float h=Mathf.Max(88,text.preferredHeight+20); text.rectTransform.sizeDelta=new Vector2(768,h); y+=h+28; Box("Entry rule",18,y,768,1,new Color32(61,73,67,255),content); y+=28; }
            content.sizeDelta=new Vector2(804,Mathf.Max(752,y));
        }
        void EncounterPage()
        {
            Hero("누군가 물품을 요구한다"); Paragraph(state.PendingEncounter.context+"에서 굶주린 사람과 마주쳤다. 어떻게 대응할까?",790,180);
            Choice(0,"순순히 내어준다","물품을 내어주고 부상을 피한다",1060,()=>Apply(state.ResolveEncounter(false,definitions)));
            Choice(1,"저항한다","빠져나갈 수도 있지만 부상을 입을 수 있다",1190,()=>Apply(state.ResolveEncounter(true,definitions)));
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
            Button("집의 보안과 정비",48,780,804,84,()=>{page="security";message="";Render();},state.CurrentLocation=="home");
            Button("기록 저장",48,890,390,84,()=>{Save();message="현재 기록을 저장했다.";Render();},true,true);
            Button("저장 기록 불러오기",462,890,390,84,Load,store.Exists);
            Button("기록 이어가기",48,1010,804,84,()=>{page="home";Render();});
            Button("새 기록 시작",48,1130,804,84,()=>{confirmRestart=true;Render();});
            if(state.Story.HasRadio) Button("라디오 켜기",48,1250,804,84,OpenRadio,state.CanUseRadio && state.CurrentLocation=="home");
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
                Apply(state.Travel("home",definitions,()=>1)); state.hour=18; Apply(state.Travel("supply_site",definitions,()=>1));
                // A fresh story fixture verifies the alternate night-only presentation.
                state.Story.CrowdIncidentStarted=false; state.Story.CrowdIncidentResolved=false; state.Story.HasMetDongrim=false; state.Story.DongrimRescueCompleted=false; state.Story.DongrimContactHintReceived=false; state.Crowd=new CrowdStoryProgress(); page="home"; Render();
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
