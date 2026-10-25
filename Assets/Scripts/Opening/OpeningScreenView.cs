using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VanishingDays
{
    public sealed class OpeningScreenView : MonoBehaviour
    {
        static readonly Color Background=new Color32(11,17,19,255), Paper=new Color32(230,223,204,255), Muted=new Color32(155,163,157,255), Accent=new Color32(217,166,94,255),
            Card=new Color32(18,26,28,255), Edge=new Color32(38,50,49,255), Selected=new Color32(43,38,28,255), AccentText=new Color32(233,190,126,255), OnAccent=new Color32(20,17,11,255);
        RectTransform root;
        Font font;
        OpeningFlowController flow;
        InputField nickname;
        Button submit;
        public OpeningFlowController Flow => flow;
        public GameDifficulty SelectedDifficulty { get; private set; }=GameDifficulty.NORMAL;
        public event Action<string> OnOpeningCompleted;

        public void Initialize(Font textFont)
        {
            font=textFont;
            root=GetComponent<RectTransform>();
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f); root.sizeDelta=new Vector2(900,1600); root.anchoredPosition=Vector2.zero;
            flow=new OpeningFlowController(OpeningScriptData.Load());
            flow.ScreenChanged+=Render;
            flow.OnOpeningCompleted+=Complete;
            Render();
        }
        void Complete(string name) { OnOpeningCompleted?.Invoke(name); }
        void OnDestroy() { if(flow!=null) { flow.ScreenChanged-=Render; flow.OnOpeningCompleted-=Complete; } }
        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); return rect;
        }
        RectTransform Panel(string name,float x,float y,float w,float h,Color color,Transform parent=null)
        {
            var rect=Rect(name,parent??root,x,y,w,h); rect.gameObject.AddComponent<Image>().color=color; return rect;
        }
        Text Text(string value,float x,float y,float w,float h,int size,Color color,Transform parent=null)
        {
            var rect=Rect("Text",parent??root,x,y,w,h); var label=rect.gameObject.AddComponent<Text>(); label.font=font; label.text=value; label.fontSize=size; label.color=color; label.supportRichText=false; label.lineSpacing=1.35f; label.raycastTarget=false; label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Truncate; return label;
        }
        // primary: the solid Amber call to action; otherwise an Edge-framed card that turns Amber when selected.
        Button Button(string caption,float x,float w,Action click,float y=1410,float h=88,bool primary=true,bool selected=false)
        {
            var rect=Panel(caption,x,y,w,h,primary?Accent:selected?Accent:Edge);
            var face=Panel("Fill",2,2,w-4,h-4,primary?Accent:selected?Selected:Card,rect).GetComponent<Image>();
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=face;
            var colors=button.colors; colors.highlightedColor=new Color(1.2f,1.18f,1.1f); colors.pressedColor=new Color(.8f,.76f,.68f); colors.disabledColor=new Color(.5f,.5f,.5f,.6f); button.colors=colors;
            var label=Text(caption,28,0,w-56,h,28,primary?OnAccent:Paper,rect); label.alignment=TextAnchor.MiddleLeft; label.fontStyle=FontStyle.Bold;
            button.onClick.AddListener(()=>click()); return button;
        }
        RawImage AtlasIcon(Texture2D atlas,int index,float x,float y,float size,Transform parent)
        {
            if(atlas==null) return null;
            var image=Rect("Icon",parent,x,y,size,size).gameObject.AddComponent<RawImage>(); image.texture=atlas; image.raycastTarget=false;
            image.uvRect=new UnityEngine.Rect((index%4)*.25f,index<4?.25f:0,.25f,.25f); return image;
        }
        Texture2D fadeTexture;
        void Fade(float x,float y,float w,float h)
        {
            if(fadeTexture==null)
            {
                fadeTexture=new Texture2D(1,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
                for(int i=0;i<64;i++) fadeTexture.SetPixel(0,i,new Color(Background.r,Background.g,Background.b,Mathf.SmoothStep(0,1,1-i/63f)));
                fadeTexture.Apply();
            }
            var image=Rect("Fade",root,x,y,w,h).gameObject.AddComponent<RawImage>(); image.texture=fadeTexture; image.raycastTarget=false;
        }
        void ScrollBody(string body)
        {
            var area=Panel("Scrollable record",64,464,772,870,Color.clear); area.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Record content",area,0,0,772,870);
            var label=Text(body,0,0,756,870,30,Paper,content);
            float height=Mathf.Max(870,label.preferredHeight+24);
            label.rectTransform.sizeDelta=new Vector2(756,height); content.sizeDelta=new Vector2(772,height);
            var scroll=area.gameObject.AddComponent<ScrollRect>(); scroll.viewport=area; scroll.content=content; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45;
            if(height>870) Text("아래로 스크롤해서 기록을 읽어주세요.",64,1350,772,34,20,Muted);
        }
        void Render()
        {
            ReleaseNickname();
            foreach(Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            nickname=null; submit=null;
            var screen=flow.Current;
            Panel("Archive background",0,0,900,1600,Background);
            if(screen.kind=="Start")
            {
                var image=Rect("Seoul archive",root,0,0,900,940).gameObject.AddComponent<RawImage>();
                image.texture=Resources.Load<Texture2D>("Art/SeoulErasure"); image.raycastTarget=false;
                if(image.texture!=null)
                {
                    float portion=(900f/940)/(image.texture.width/(float)image.texture.height);
                    image.uvRect=new UnityEngine.Rect((1-portion)*.38f,0,portion,1);
                }
                Fade(0,440,900,502);
                Text("SEOUL · 2031",48,40,600,34,22,new Color32(201,194,175,255));
                Text("Vanishing Days",48,668,804,110,88,new Color32(239,233,216,255)).fontStyle=FontStyle.Bold;
                Text("배니싱 데이즈",48,790,240,40,28,Muted);
                Panel("Title rule",250,810,56,2,Edge);
                Text("소멸일지",322,790,300,40,28,Muted);
                Text("난이도",48,862,300,36,24,Muted).fontStyle=FontStyle.Bold;
                string[] names={"쉬움","보통","어려움"}, tags={"물 무제한","물 8병","물 4병"};
                for(int i=0;i<3;i++)
                {
                    var difficulty=(GameDifficulty)i; bool chosen=SelectedDifficulty==difficulty;
                    var option=Button(names[i],48+i*273,257,()=>{SelectedDifficulty=difficulty; Render();},906,128,false,chosen);
                    var name=option.transform.Find("Text").GetComponent<Text>(); name.rectTransform.anchoredPosition=new Vector2(28,-18); name.rectTransform.sizeDelta=new Vector2(200,44); name.alignment=TextAnchor.UpperLeft; name.fontSize=30;
                    if(chosen) Panel("Chosen",213,26,18,18,Accent,option.transform);
                    Text(tags[i],28,78,200,34,22,Muted,option.transform);
                }
                string initial=SelectedDifficulty==GameDifficulty.EASY?"스팸 10 · 참치 10 · 쌀 10kg":SelectedDifficulty==GameDifficulty.HARD?"스팸 3 · 참치 1 · 쌀 1kg":"스팸 5 · 참치 5 · 쌀 3kg";
                int bandages=SelectedDifficulty==GameDifficulty.EASY?5:SelectedDifficulty==GameDifficulty.HARD?1:3;
                string water=SelectedDifficulty==GameDifficulty.EASY?"물 무제한":"물 "+(SelectedDifficulty==GameDifficulty.HARD?4:8)+"병 · 식수와 조리에 사용";
                var loadout=Panel("Loadout",48,1062,804,300,Edge); Panel("Fill",2,2,800,296,Card,loadout);
                Text("시작 물품",28,20,400,30,20,Muted,loadout);
                var atlas=Resources.Load<Texture2D>("Art/SurvivalAtlas");
                string[] rows={initial,"붕대 "+bandages+"개 · 부르스타 1개",water}; int[] icons={7,6,0};
                for(int i=0;i<3;i++)
                {
                    AtlasIcon(atlas,icons[i],28,62+i*76,60,loadout);
                    Text(rows[i],112,62+i*76,664,60,26,i==2 && SelectedDifficulty==GameDifficulty.HARD?new Color32(231,160,138,255):Paper,loadout).alignment=TextAnchor.MiddleLeft;
                }
                Button(screen.button,48,804,flow.Next,1404,112);
                Text("행동 후 자동 저장",0,1538,900,34,21,Muted).alignment=TextAnchor.UpperCenter;
                return;
            }
            Text("VANISHING DAYS  /  ARCHIVE",64,70,772,38,20,Muted);
            Panel("Archive rule",64,132,772,2,new Color32(64,74,70,255));
            if(!string.IsNullOrEmpty(screen.date)) Text(screen.date,64,180,772,42,26,Accent);
            Text(screen.title,64,256,772,90,44,Paper);
            Text(flow.Author,64,364,772,56,26,Muted);
            if(screen.kind=="Nickname") RenderNickname();
            else
            {
                ScrollBody(flow.Body);
                if(screen.kind=="Question") { Button("예",64,374,()=>flow.Answer(true)); Button("아니오",462,374,()=>flow.Answer(false),1410,88,false); }
                else Button(screen.button,64,772,flow.Next);
            }
        }
        void RenderNickname()
        {
            Text(flow.Body,64,490,772,92,32,Paper);
            var field=Panel("Nickname input",64,668,772,108,new Color32(31,41,43,255));
            nickname=field.gameObject.AddComponent<InputField>(); nickname.targetGraphic=field.GetComponent<Image>();
            nickname.contentType=InputField.ContentType.Standard; nickname.lineType=InputField.LineType.SingleLine; nickname.characterValidation=InputField.CharacterValidation.None;
            // Validate the committed name, rather than rejecting jamo while the IME composes.
            nickname.characterLimit=0;
            var text=Text("",24,24,724,64,34,Paper,field); text.supportRichText=false;
            var placeholder=Text("닉네임 입력",24,24,724,64,34,Muted,field);
            nickname.textComponent=text; nickname.placeholder=placeholder; nickname.caretColor=Accent; nickname.customCaretColor=true;
            Text("한글 · 영문 · 숫자 1~12자",64,806,772,64,24,Muted);
            submit=Button("기록 시작",64,772,SubmitNickname); submit.interactable=false;
            nickname.onValueChanged.AddListener(value=>submit.interactable=OpeningFlowController.IsNicknameValid(value));
            nickname.onEndEdit.AddListener(value=>MobileInputViewport.Restore());
            StartCoroutine(FocusNickname());
        }
        void SubmitNickname()
        {
            if(nickname==null) return;
            var value=nickname.text;
            // InputField commits composition on focus loss; do not advance on uncommitted text.
            var input=EventSystem.current?.currentInputModule?.input;
            if(input!=null && input.compositionString.Length>0) return;
            if(!OpeningFlowController.IsNicknameValid(value)) return;
            ReleaseNickname();
            flow.SubmitNickname(value);
        }
        void ReleaseNickname()
        {
            if(nickname==null) return;
            nickname.DeactivateInputField();
            if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==nickname.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            MobileInputViewport.Restore();
        }
        IEnumerator FocusNickname()
        {
            yield return null;
            if(nickname!=null && nickname.isActiveAndEnabled) { nickname.Select(); nickname.ActivateInputField(); }
        }
#if UNITY_EDITOR
        // Development-only entry point. No jump/skip control is included in the player UI.
        public void EditorSubmitNickname(string name) { if(nickname!=null) nickname.text=name; SubmitNickname(); }
#endif
    }
}
