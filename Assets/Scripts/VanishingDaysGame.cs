using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VanishingDays
{
    public class VanishingDaysGame : MonoBehaviour
    {
        [SerializeField, Tooltip("Play 시 타이틀을 건너뛰고 첫날 은신처에서 시작합니다.")]
        bool startInShelter;
        static readonly Color Ink=new Color32(14,21,24,255), Panel=new Color32(22,30,32,255), Paper=new Color32(222,216,198,255), Muted=new Color32(144,153,148,255), Amber=new Color32(211,173,110,255);
        SurvivalState state;
        RectTransform root;
        Font font;
        Texture2D art;
        string page="shelter", message="";
        int selected=-1;
        bool title=true, confirmRestart, readFinishedJournal;
        Action[] shortcuts=new Action[3];
        string SavePath { get { return Path.Combine(Application.persistentDataPath,"vanishing-days-v1.json"); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(FindFirstObjectByType<VanishingDaysGame>()==null) new GameObject("Vanishing Days").AddComponent<VanishingDaysGame>();
        }

        void Start()
        {
            Application.targetFrameRate=60;
            Application.runInBackground=true;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},20);
            if(font==null) font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            art=Resources.Load<Texture2D>("Art/SeoulErasure");
            var canvas=new GameObject("Diary Interface",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(900,1600); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            canvas.gameObject.AddComponent<Image>().color=Ink;
            root=Rect("Portrait viewport",canvas.transform,0,0,900,1600);
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);
            if(FindFirstObjectByType<EventSystem>()==null) new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            state=SurvivalState.NewGame();
            title=!startInShelter;
            Render();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vdSmoke")>=0) StartCoroutine(Smoke());
        }

        void Update()
        {
            var k=Keyboard.current; if(k==null || title) return;
            if(k.digit1Key.wasPressedThisFrame) shortcuts[0]?.Invoke();
            else if(k.digit2Key.wasPressedThisFrame) shortcuts[1]?.Invoke();
            else if(k.digit3Key.wasPressedThisFrame) shortcuts[2]?.Invoke();
        }

        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        RectTransform Box(string name,float x,float y,float w,float h,Color color,Transform parent=null)
        { var r=Rect(name,parent??root,x,y,w,h); var i=r.gameObject.AddComponent<Image>(); i.color=color; i.raycastTarget=false; return r; }
        Text Label(string text,float x,float y,float w,float h,int size,Color color,Transform parent=null,FontStyle style=FontStyle.Normal)
        {
            var r=Rect("Text",parent??root,x,y,w,h); var t=r.gameObject.AddComponent<Text>(); t.font=font; t.text=text; t.fontSize=size; t.fontStyle=style; t.color=color; t.supportRichText=true; t.raycastTarget=false; t.verticalOverflow=VerticalWrapMode.Truncate; t.horizontalOverflow=HorizontalWrapMode.Wrap; t.lineSpacing=1.18f; return t;
        }
        void Button(string text,float x,float y,float w,float h,Action action,bool enabled=true,bool accent=false,Transform parent=null)
        {
            var r=Box("Button "+text,x,y,w,h,accent?new Color32(57,63,52,255):Panel,parent);
            r.GetComponent<Image>().raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=r.GetComponent<Image>(); b.interactable=enabled;
            var colors=b.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.35f,1.3f,1.15f); colors.pressedColor=new Color(.75f,.7f,.6f); colors.disabledColor=new Color(.5f,.5f,.5f,.65f); b.colors=colors;
            b.onClick.AddListener(()=>action());
            Label(text,20,12,w-40,h-20,24,enabled?(accent?Amber:Paper):Muted,r);
            Box("Rule",0,h-1,w,1,enabled?new Color32(71,81,76,255):Panel,r);
        }
        void Picture(float x,float y,float w,float h,float alpha=1)
        {
            var r=Rect("Seoul · concept art",root,x,y,w,h); var image=r.gameObject.AddComponent<RawImage>(); image.texture=art; image.color=new Color(1,1,1,alpha); image.raycastTarget=false;
            if(art!=null) { float desired=w/h, actual=(float)art.width/art.height; image.uvRect=actual>desired?new UnityEngine.Rect((1-desired/actual)/2,0,desired/actual,1):new UnityEngine.Rect(0,(1-actual/desired)/2,1,actual/desired); }
        }
        void StoryBody(string text,float height=236,int size=28)
        {
            var area=Box("Story scroll",48,774,804,height,Color.clear);
            area.GetComponent<Image>().raycastTarget=true;
            area.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Story content",area,0,0,804,height);
            var label=Label(text,0,0,788,height,size,Paper,content);
            float contentHeight=Mathf.Max(height,label.preferredHeight+12);
            label.rectTransform.sizeDelta=new Vector2(788,contentHeight);
            content.sizeDelta=new Vector2(804,contentHeight);
            var scroll=area.gameObject.AddComponent<ScrollRect>(); scroll.content=content; scroll.viewport=area; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=40;
        }
        void Render()
        {
            foreach(Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            shortcuts=new Action[3];
            Box("Background",0,0,900,1600,Ink);
            if(title) { RenderTitle(); return; }
            Header(); Sidebar();
            if(confirmRestart) { RestartPage(); return; }
            if(state.finished && !(readFinishedJournal && page=="journal")) { Ending(); return; }
            if(page=="map") Map(); else if(page=="craft") Craft(); else if(page=="journal") Journal(); else if(page=="explore") Explore(); else Shelter();
            Label("VANISHING DAYS  /  선택 1 · 2 · 3  /  자동 저장",48,1560,804,28,16,Muted);
        }
        void RenderTitle()
        {
            Picture(0,0,900,1600);
            Box("Title panel",0,500,900,1100,new Color(0.035f,.055f,.065f,.90f));
            Label("A RECORD OF THINGS THAT DISAPPEAR",64,550,772,40,19,Amber);
            Box("Title rule",64,626,56,3,Amber);
            Label("소멸일지",56,674,788,140,96,Paper);
            Label("V A N I S H I N G   D A Y S",64,842,772,48,27,Muted);
            Label("매일 자정, 도시의 일부가 사라진다.",64,948,772,64,32,Paper);
            Label("남은 것을 모으고, 잃은 것을 만든다.\n그리고 사라지는 것들의 규칙을 기록한다.",64,1036,772,116,28,Muted);
            Button("첫 번째 기록 시작하기     →",64,1220,772,88,()=>NewGame(),true,true);
            Button("이전 일지 이어 읽기",64,1332,772,84,Load,File.Exists(SavePath));
            Label("서울, 누군가의 옥탑방\n07 DAYS  /  08 PLACES  /  ONE DIARY",64,1490,772,80,20,Muted);
        }
        void Header()
        {
            Label("소멸일지",48,38,400,66,42,Paper);
            Label("DAY "+state.day.ToString("00")+" / "+state.hour.ToString("00")+":00",564,48,288,48,30,Amber);
            Box("Header rule",48,117,804,1,new Color32(63,73,71,255));
            string[] values={"체력  "+state.health+" / 100","식량  "+state.food,"식수  "+state.water,"부품  "+state.scrap,"약품  "+state.medicine};
            for(int i=0;i<values.Length;i++) Label(values[i],48+i*166,145,i==0?180:150,42,22,i==0&&state.health<30?new Color32(218,120,99,255):Paper);
            string[] names={"은신처","도시 탐험","제작","관찰 일지"}; string[] ids={"shelter","map","craft","journal"};
            for(int i=0;i<4;i++) { string id=ids[i]; Button(names[i],48+i*206,210,186,64,()=>{page=id; message=""; Render();},true,page==id); }
        }
        void Sidebar()
        {
            Box("Survival dock",48,1405,804,140,new Color32(25,32,33,255));
            int alive=0; foreach(var d in state.districts) if(!d.erased) alive++;
            Label("장소 "+alive+"/8  ·  자정까지 "+(24-state.hour)+"시간  ·  밤마다 식량 2 / 식수 2",66,1420,768,36,20,Muted);
            Button("자정까지 기다린다   →",66,1474,478,58,()=>Apply(state.Midnight(),"shelter"),!state.finished,true);
            Button("처음부터",566,1474,268,58,()=>{confirmRestart=true; Render();});
        }
        void StoryTop(string kicker,string heading)
        {
            Picture(48,306,804,340,.84f);
            Box("Image caption",48,588,804,58,new Color(.035f,.055f,.065f,.86f));
            Label(kicker,68,601,764,32,19,Amber);
            Label(heading,48,684,804,64,38,Paper);
        }
        void Shelter()
        {
            StoryTop("SEOUL  /  EULJIRO  /  SAFEHOUSE",message==""?"사라진 골목의 아침":"오늘의 기록");
            StoryBody(message==""?state.lastReport:message);
            Choice(0,"도시로 나간다","남아 있는 장소와 물자를 확인한다",1040,()=>{page="map"; message=""; Render();});
            Choice(1,"작업대에 앉는다","생존에 필요한 설비를 만든다",1152,()=>{page="craft"; message=""; Render();});
            Choice(2,"상처를 치료한다","약품 1 · 1시간 / 체력 +25",1264,()=>Apply(state.Heal()),state.medicine>0&&state.health<100&&state.CanAct(1));
        }
        void Choice(int key,string titleText,string detail,float y,Action action,bool enabled=true)
        {
            shortcuts[key]=enabled?action:null;
            Button((key+1).ToString("00")+"    "+titleText+"\n        "+detail,48,y,804,94,action,enabled);
        }
        void Map()
        {
            Label("아직 지도에 남아 있는 곳들",48,310,804,60,36,Paper);
            Label("징후와 마지막 방문을 함께 살펴보자.",48,380,804,40,24,Muted);
            for(int i=0;i<state.districts.Count;i++)
            {
                int id=i; var d=state.districts[i]; int col=i%2,row=i/2; float x=48+col*414,y=454+row*226;
                Box("District",x,y,390,204,d.erased?new Color32(18,24,26,255):Panel);
                Label(d.erased?"지워진 장소":d.name,x+20,y+18,350,46,28,d.erased?Muted:Paper);
                Label(d.erased?"이곳에는 도착할 수 없다.":"마지막 방문 "+d.silence+"일 전"+(state.protectedDistrict==i?" · 보존 중":""),x+20,y+80,350,45,20,d.silence>=4?Amber:Muted);
                if(!d.erased) Button("살펴보기   →",x+20,y+134,350,54,()=>{selected=id; page="explore"; message=""; Render();});
            }
        }
        void Explore()
        {
            var d=state.districts[selected];
            StoryTop("CITY RECORD  /  "+(selected+1).ToString("00"),d.name);
            StoryBody(message==""?d.description+"\n"+d.clue:message);
            Choice(0,"조심스럽게 수색한다","3시간 · 부상 없이 소량의 물자",1040,()=>Apply(state.Visit(selected,false),"explore"),!d.erased&&state.CanAct(3));
            Choice(1,"안쪽까지 뒤진다","4시간 · 체력 -6 · 남은 물자 모두",1152,()=>Apply(state.Visit(selected,true),"explore"),!d.erased&&state.CanAct(4));
            Choice(2,"징후를 관찰하고 기록한다","1시간 · 소멸의 단서를 남긴다",1264,()=>Apply(state.Observe(selected),"explore"),!d.erased&&state.CanAct(1));
        }
        void Craft()
        {
            Label("사라진 것들을 대신하는 법",48,310,804,60,36,Paper);
            Label(message==""?"쓸모없는 부품도, 잃어버린 도시의 한 조각이 될 수 있다.":message,48,390,804,104,26,Muted);
            string[] names={"간이 정수기","옥상 텃밭","기록 송신기"}; string[] details={"부품 4 · 2시간\n밤의 소비 후, 매일 아침 식수 2를 생산한다.","부품 5 · 2시간\n밤의 소비 후, 매일 아침 식량 2를 생산한다.","부품 3 · 2시간\n장소 하나를 선택해 오늘 자정의 소멸을 막는다."};
            bool[] made={state.filter,state.garden,state.beacon}; int[] costs={4,5,3};
            for(int i=0;i<3;i++) { int r=i; float y=518+i*260; Box("Recipe",48,y,804,238,Panel); Label(names[i]+(made[i]?"   /   설치 완료":""),70,y+16,760,44,30,Paper); Label(details[i],70,y+74,760,84,23,Muted); Button(made[i]?"설치됨":"제작한다  →",70,y+170,760,54,()=>Apply(state.Craft(r),"craft"),!made[i]&&state.scrap>=costs[i]&&state.CanAct(2),true); }
            if(state.beacon) Button("기록을 보존할 장소 선택하기   →",48,1314,804,60,()=>{page="journal"; message=""; Render();},true,true);
        }
        void Journal()
        {
            Label("소멸 관찰 일지",48,310,804,60,36,Paper);
            Label("사건을 모으면 우연 사이에 규칙이 보일지도 모른다.",48,386,804,70,24,Muted);
            if(state.beacon)
            {
                Label("오늘의 보존 대상  /  눌러서 변경",48,476,804,38,22,Amber);
                int j=0; for(int i=0;i<state.districts.Count;i++) { if(state.districts[i].erased) continue; int id=i; Button(state.districts[i].name,48+(j%2)*414,530+(j/2)*72,390,62,()=>Apply(state.Protect(id),"journal"),!state.finished,state.protectedDistrict==i); j++; }
            }
            float top=state.beacon?876:510;
            var area=Box("Scrollable diary",48,top,804,1380-top,new Color32(25,32,33,255)); area.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Entries",area,0,0,804,Mathf.Max(1380-top,state.journal.Count*244+30));
            var scroll=area.gameObject.AddComponent<ScrollRect>(); scroll.content=content; scroll.viewport=area; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
            area.GetComponent<Image>().raycastTarget=true;
            float offset=18;
            for(int i=0;i<state.journal.Count;i++)
            {
                var entry=Label(state.journal[i],22,offset,760,214,24,i==0?Paper:Muted,content);
                float entryHeight=Mathf.Max(84,entry.preferredHeight+10);
                entry.rectTransform.sizeDelta=new Vector2(760,entryHeight);
                offset+=entryHeight+22;
                Box("Entry rule",22,offset,760,1,new Color32(53,64,61,255),content);
                offset+=22;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(1380-top,offset));
            Label("드래그하거나 마우스 휠로 넘깁니다.",48,top-38,804,30,18,Muted);
        }
        void Ending()
        {
            StoryTop("END OF RECORD  /  DAY "+state.day.ToString("00"),state.won?"아직, 기록할 내일이 있다":"마지막 장");
            StoryBody(state.lastReport,340,26);
            Button("새로운 일지를 펼친다   →",48,1152,804,84,NewGame,true,true);
            Button("남겨진 기록 읽기",48,1264,804,84,()=>{readFinishedJournal=true; page="journal"; Render();});
        }
        void RestartPage()
        {
            StoryTop("A NEW RECORD", "새 일지를 시작할까?");
            Label("현재 일지의 저장 내용이 새 기록으로 바뀐다.",48,774,804,180,28,Paper);
            Button("기록을 이어 간다",48,1152,804,84,()=>{confirmRestart=false; Render();},true,true);
            Button("새 일지를 시작한다",48,1264,804,84,NewGame);
        }
        void NewGame() { state=SurvivalState.NewGame(); title=false; confirmRestart=false; readFinishedJournal=false; page="shelter"; message=""; Save(); Render(); }
        void Apply(string text,string next="shelter") { message=text; page=next; Save(); Render(); }
        void Save()
        {
            try { string temp=SavePath+".tmp"; File.WriteAllText(temp,JsonUtility.ToJson(state,true)); File.Copy(temp,SavePath,true); File.Delete(temp); }
            catch(Exception e) { Debug.LogWarning("일지 저장 실패: "+e.Message); message+="\n저장하지 못했습니다. 저장 폴더의 여유 공간을 확인해 주세요."; }
        }
        void Load()
        {
            try { var loaded=JsonUtility.FromJson<SurvivalState>(File.ReadAllText(SavePath)); if(loaded==null||loaded.districts==null||loaded.districts.Count!=8||loaded.day<1||loaded.day>7) throw new InvalidDataException(); state=loaded; title=false; page="shelter"; message=""; Render(); }
            catch(Exception) { title=false; message="일지를 읽을 수 없어 새 기록을 준비했다."; state=SurvivalState.NewGame(); Render(); }
        }
        IEnumerator Smoke()
        {
            string dir=Path.GetFullPath("Builds/Verification"); Directory.CreateDirectory(dir);
            // Let the player splash screen finish before checking rendered UI.
            yield return new WaitForSeconds(4f);
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"01-title.png"));
            yield return null; title=false; page="shelter"; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"02-game.png"));
            yield return null; page="map"; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"03-map.png"));
            yield return null; selected=1; page="explore"; message=""; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"04-choice.png"));
            yield return null; page="craft"; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"05-craft.png"));
            yield return null; page="journal"; Render();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,"06-journal.png"));
            yield return null; Debug.Log("VD_SMOKE_OK"); Application.Quit();
        }
    }
}
