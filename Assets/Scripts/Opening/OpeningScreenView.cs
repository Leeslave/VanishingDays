using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VanishingDays
{
    public sealed class OpeningScreenView : MonoBehaviour
    {
        static readonly Color Background=new Color32(17,23,25,255), Paper=new Color32(226,221,206,255), Muted=new Color32(151,159,151,255), Accent=new Color32(211,173,110,255);
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
        Button Button(string caption,float x,float w,Action click,float y=1410)
        {
            var rect=Panel(caption,x,y,w,88,new Color32(51,60,51,255));
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>();
            var colors=button.colors; colors.highlightedColor=new Color(1.25f,1.2f,1.1f); colors.disabledColor=new Color(.45f,.45f,.45f,.6f); button.colors=colors;
            Text(caption,24,23,w-48,52,28,Accent,rect); button.onClick.AddListener(()=>click()); return button;
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
                var image=Rect("Seoul archive",root,0,0,900,650).gameObject.AddComponent<RawImage>();
                image.texture=Resources.Load<Texture2D>("Art/SeoulErasure"); image.raycastTarget=false;
                if(image.texture!=null)
                {
                    float portion=(900f/650)/(image.texture.width/(float)image.texture.height);
                    image.uvRect=new UnityEngine.Rect((1-portion)/2,0,portion,1);
                }
                Text("Vanishing Days",64,820,772,100,64,Paper);
                Text("배니싱 데이즈",64,940,772,60,32,Muted);
                Text("난이도",64,1090,772,42,26,Muted);
                string[] names={"쉬움","보통","어려움"};
                for(int i=0;i<3;i++)
                {
                    var difficulty=(GameDifficulty)i;
                    var option=Button(names[i]+(SelectedDifficulty==difficulty?" ✓":""),64+i*266,240,()=>{SelectedDifficulty=difficulty; Render();},1160);
                    if(SelectedDifficulty==difficulty) option.targetGraphic.color=new Color32(69,78,59,255);
                }
                string initial=SelectedDifficulty==GameDifficulty.EASY?"스팸 10 · 참치 10 · 쌀 10kg":SelectedDifficulty==GameDifficulty.HARD?"스팸 3 · 참치 1 · 쌀 1kg":"스팸 5 · 참치 5 · 쌀 3kg";
                int bandages=SelectedDifficulty==GameDifficulty.EASY?5:SelectedDifficulty==GameDifficulty.HARD?1:3;
                string water=SelectedDifficulty==GameDifficulty.EASY?"물 무제한":"물 "+(SelectedDifficulty==GameDifficulty.HARD?4:8)+"병 · 식수와 조리에 사용";
                Text(initial+"\n붕대 "+bandages+"개 · 부르스타 1개\n"+water,64,1280,772,104,25,Muted);
                Button(screen.button,64,772,flow.Next);
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
                if(screen.kind=="Question") { Button("예",64,374,()=>flow.Answer(true)); Button("아니오",462,374,()=>flow.Answer(false)); }
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
