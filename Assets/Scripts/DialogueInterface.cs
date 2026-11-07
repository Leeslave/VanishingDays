using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    // Story text presentation, picked by who is speaking and how:
    //   A · Journal  — narration that ends in a choice: written onto the journal page one paragraph at a time.
    //   B · Scene    — people talking face to face: a portrait and one line at a time.
    //   C · Radio    — anything received over the air: an SP receiver log with source badges and static.
    public partial class VanishingDaysGame
    {
        static readonly Color JournalSoft=new Color32(58,55,47,255), JournalSpeaker=new Color32(90,63,34,255), JournalShade=new Color32(204,195,172,255),
            JournalRadio=new Color32(52,82,96,255), SceneNarration=new Color32(201,196,182,255);
        string sceneKey;
        int sceneStep;
        bool sceneLog;
        // Scenes read to the end this session open straight onto their choices when the page is redrawn.
        readonly HashSet<string> scenesRead=new HashSet<string>();
        // Set only while a scene is being stepped through; Space/Enter and tapping the scene call it.
        Action sceneAdvance;
        Texture2D portraitHatch;

        List<ScriptBlock> Blocks(string text) => ScriptBlocks.Parse(text,state.PlayerNickname);
        static Color Fade(Color color,float alpha) => new Color(color.r,color.g,color.b,color.a*alpha);

        // Draws a story scene from top down to choicesTop. Returns true once it has been read to the end and
        // the page should draw its choices; until then the scene owns the page down to the dock.
        bool SceneText(string text,float top,float choicesTop,ScenePresentation? forced=null)
        {
            var blocks=Blocks(text);
            var mode=forced??ScriptBlocks.PresentationFor(blocks);
            if(text!=sceneKey) { sceneKey=text; sceneStep=0; sceneLog=false; }
            if(mode==ScenePresentation.Radio) { RadioText(blocks,top,choicesTop-top-16,false,null); return true; }
            bool stepping=blocks.Count>2 && !scenesRead.Contains(text) && sceneStep<blocks.Count-1;
            if(!stepping)
            {
                scenesRead.Add(text);
                // A scene that was stepped through opens at its last lines, next to the choices they lead to.
                Transcript(blocks,top,choicesTop-top-16,mode==ScenePresentation.Journal,-1,blocks.Count>2);
                return true;
            }
            if(sceneLog)
            {
                sceneAdvance=()=>{sceneLog=false;Render();};
                SceneLog(blocks,top);
                return false;
            }
            sceneAdvance=()=>{ sceneStep++; if(sceneStep>=blocks.Count-1) scenesRead.Add(text); Render(); };
            if(mode==ScenePresentation.Scene) SceneStepper(blocks,top); else JournalStepper(blocks,top);
            return false;
        }
        void SkipScene()
        {
            if(sceneKey!=null) scenesRead.Add(sceneKey);
            sceneLog=false; Render();
        }

        // ---- A · journal page --------------------------------------------------------------------------

        void JournalStepper(List<ScriptBlock> blocks,float top)
        {
            int shown=sceneStep+1;
            Transcript(blocks.Take(shown).ToList(),top,1286-top,true,sceneStep,true);
            var count=Label(shown+" / "+blocks.Count,48,1302,200,88,24,Muted); count.alignment=TextAnchor.MiddleLeft;
            Button("모두 보기",268,1302,232,88,SkipScene);
            PrimaryButton("계속 읽기 ↓",524,1302,328,88,sceneAdvance);
        }

        // The whole scene as a page: speaker names in a gutter, quotes beside them, broadcasts boxed.
        // journal=true writes it on the diary paper (A); false on the dark panel (B, read back).
        RectTransform Transcript(List<ScriptBlock> blocks,float y,float height,bool journal,int active,bool scrollToEnd,int size=26)
        {
            Color fill=journal?JournalPaper:Panel;
            var area=ScrollArea("Scene text",y,height,fill,out var content);
            float cy=20;
            for(int i=0;i<blocks.Count;i++) cy+=TranscriptBlock(blocks[i],content,cy,journal,size,active>=0 && i<active?.62f:1f)+(journal?24:20);
            float needed=cy+4;
            // Short scenes get a page their own size instead of a large empty panel.
            if(needed<height-60) { height=Mathf.Max(needed,120); area.sizeDelta=new Vector2(804,height); }
            content.sizeDelta=new Vector2(804,Mathf.Max(height,needed));
            if(needed>height+4)
            {
                if(scrollToEnd) content.anchoredPosition=new Vector2(0,needed-height);
                else MoreBelow(area,fill,journal?JournalInk:Muted);
            }
            return area;
        }
        float TranscriptBlock(ScriptBlock block,Transform parent,float y,bool journal,int size,float alpha)
        {
            Color ink=journal?JournalInk:Paper, soft=journal?JournalSoft:SceneNarration;
            FontStyle weight=journal?FontStyle.Bold:FontStyle.Normal;
            switch(block.Kind)
            {
                case ScriptBlockKind.Speech:
                case ScriptBlockKind.Radio:
                {
                    bool radio=block.Kind==ScriptBlockKind.Radio;
                    var name=Label(block.Speaker,24,y+5,116,40,size-6,Fade(radio?(journal?JournalRadio:Cold):journal?JournalSpeaker:AmberText,alpha),parent); name.fontStyle=FontStyle.Bold;
                    float nameH=name.preferredHeight+2; name.rectTransform.sizeDelta=new Vector2(116,nameH);
                    if(radio) { Label("무전",24,y+5+nameH,116,28,size-9,Fade(journal?JournalRadio:Cold,alpha),parent); nameH+=28; }
                    var line=Label(block.Text,152,y,628,40,size+1,Fade(ink,alpha),parent); line.fontStyle=weight; line.lineSpacing=1.28f;
                    float h=line.preferredHeight+4; line.rectTransform.sizeDelta=new Vector2(628,h);
                    return Mathf.Max(h,nameH);
                }
                case ScriptBlockKind.Broadcast:
                {
                    var card=Box("Broadcast",24,y,756,40,Fade(journal?JournalShade:Raised,alpha),parent);
                    float cy=14;
                    if(block.Meta.Length>0) cy+=Badge(block.Meta,card,16,cy,724,Fade(Rust,alpha),Fade(Paper,alpha))+10;
                    var text=Label(block.Text,16,cy,724,40,size,Fade(ink,alpha),card); text.fontStyle=weight; text.lineSpacing=1.25f;
                    float h=text.preferredHeight+4; text.rectTransform.sizeDelta=new Vector2(724,h);
                    card.sizeDelta=new Vector2(756,cy+h+14);
                    return cy+h+14;
                }
                default:
                {
                    string body=block.Kind==ScriptBlockKind.Noise?"▒▒  "+block.Meta+"  ▒▒":block.ToString();
                    var text=Label(body,24,y,756,40,size,Fade(block.Kind==ScriptBlockKind.Noise?Faint:soft,alpha),parent); text.fontStyle=weight; text.lineSpacing=1.3f;
                    float h=text.preferredHeight+4; text.rectTransform.sizeDelta=new Vector2(756,h);
                    return h;
                }
            }
        }
        // A filled tag sized to its text; returns its height.
        float Badge(string text,Transform parent,float x,float y,float maxWidth,Color fill,Color ink,int size=19)
        {
            var box=Box("Badge",x,y,maxWidth,40,fill,parent);
            var label=Label(text,12,6,maxWidth-24,28,size,ink,box); label.fontStyle=FontStyle.Bold;
            float w=Mathf.Min(label.preferredWidth+28,maxWidth);
            label.rectTransform.sizeDelta=new Vector2(w-24,28);
            float h=Mathf.Max(28,label.preferredHeight); label.rectTransform.sizeDelta=new Vector2(w-24,h);
            box.sizeDelta=new Vector2(w,h+12);
            return h+12;
        }

        // ---- B · face-to-face scene --------------------------------------------------------------------

        void SceneStepper(List<ScriptBlock> blocks,float top)
        {
            var current=blocks[sceneStep];
            // The whole scene is a tap target; the portrait's own button sits on top of it.
            Pressable("장면 넘기기",0,top,900,1290-top,sceneAdvance,true,Color.clear,Color.clear);
            float portraitH=Mathf.Clamp(1396-top-560,0,300);
            if(portraitH>=120) Portrait(blocks,sceneStep,48,top,804,portraitH);
            else portraitH=0;
            float y=top+portraitH+(portraitH>0?22:0);
            if(sceneStep>0)
            {
                var previous=blocks[sceneStep-1];
                var ghost=Label(previous.Kind==ScriptBlockKind.Speech || previous.Kind==ScriptBlockKind.Radio?previous.Speaker+"   "+previous.Text:previous.ToString(),48,y,804,40,22,Faint);
                float gh=Mathf.Min(ghost.preferredHeight,92); ghost.rectTransform.sizeDelta=new Vector2(804,gh);
                y+=gh+14;
            }
            Box("Scene rule",48,y,804,2,Edge); y+=20;
            const float bottom=1284;
            Text line;
            switch(current.Kind)
            {
                case ScriptBlockKind.Speech:
                case ScriptBlockKind.Radio:
                {
                    bool radio=current.Kind==ScriptBlockKind.Radio;
                    var name=Label(current.Speaker+(radio?"  ·  무전":""),48,y,804,36,24,radio?Cold:current.IsPlayer?Muted:AmberText); name.fontStyle=FontStyle.Bold;
                    y+=46;
                    line=Label(current.Text,48,y,804,bottom-y,34,Paper); line.lineSpacing=1.3f;
                    break;
                }
                case ScriptBlockKind.Broadcast:
                {
                    var holder=Rect("Scene broadcast",root,24,y,852,bottom-y);
                    TranscriptBlock(current,holder,0,false,28,1);
                    line=null;
                    break;
                }
                default:
                    line=Label(current.ToString(),48,y,804,bottom-y,29,SceneNarration); line.lineSpacing=1.32f;
                    break;
            }
            // Long lines step down a size before they would be cut off.
            if(line!=null) for(int fit=line.fontSize;line.preferredHeight>bottom-y && fit>22;fit-=3) line.fontSize=fit-3;
            Progress(blocks.Count,sceneStep,48,1302,380);
            Button("건너뛰기",444,1302,180,88,SkipScene);
            PrimaryButton("다음 →",640,1302,212,88,sceneAdvance);
        }
        void Progress(int count,int step,float x,float y,float w)
        {
            if(count>16) { var label=Label((step+1)+" / "+count,x,y,w,88,24,Muted); label.alignment=TextAnchor.MiddleLeft; return; }
            float segment=Mathf.Min(28,(w-8*(count-1))/count);
            for(int i=0;i<count;i++) Box("Step",x+i*(segment+8),y+42,segment,4,i<step?Faint:i==step?Amber:Edge);
        }
        // The speaker's portrait; while the player or the narration has the line, the last person who spoke stays, dimmed.
        void Portrait(List<ScriptBlock> blocks,int step,float x,float y,float w,float h)
        {
            string speaker=null, id=null; bool dim=false, overAir=false;
            var current=blocks[step];
            if((current.Kind==ScriptBlockKind.Speech || current.Kind==ScriptBlockKind.Radio) && !current.IsPlayer)
            {
                overAir=current.Kind==ScriptBlockKind.Radio;
                if(!overAir) { id=ScriptBlocks.PortraitId(current.Speaker); if(id!=null) speaker=current.Speaker; }
            }
            if(speaker==null && !overAir)
                for(int i=step-1;i>=0;i--)
                {
                    var earlier=blocks[i];
                    if(earlier.Kind!=ScriptBlockKind.Speech || earlier.IsPlayer) continue;
                    id=ScriptBlocks.PortraitId(earlier.Speaker); if(id==null) continue;
                    speaker=earlier.Speaker; dim=true; break;
                }
            var frame=Box("Portrait",x,y,w,h,Panel);
            if(portraitHatch==null)
            {
                portraitHatch=new Texture2D(14,14,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point};
                for(int py=0;py<14;py++) for(int px=0;px<14;px++) portraitHatch.SetPixel(px,py,(px+py)%14<3?Edge:Panel);
                portraitHatch.Apply();
            }
            var hatch=Rect("Hatch",frame,0,0,w,h).gameObject.AddComponent<RawImage>(); hatch.texture=portraitHatch; hatch.uvRect=new UnityEngine.Rect(0,0,w/14f,h/14f); hatch.raycastTarget=false;
            if(id!=null)
            {
                // Drawn portraits replace the silhouette as they are added under Resources/Art/Portraits.
                var drawn=Resources.Load<Texture2D>("Art/Portraits/"+id);
                if(drawn!=null)
                {
                    var image=Rect("Portrait art",frame,0,0,w,h).gameObject.AddComponent<RawImage>(); image.texture=drawn; image.raycastTarget=false; image.color=new Color(1,1,1,dim?.4f:1);
                    float desired=w/h, actual=drawn.width/(float)drawn.height;
                    image.uvRect=actual>desired?new UnityEngine.Rect((1-desired/actual)/2,0,desired/actual,1):new UnityEngine.Rect(0,1-actual/desired,1,actual/desired);
                }
                else
                {
                    var figure=Rect("Silhouette",frame,0,0,w,h).gameObject.AddComponent<PortraitGraphic>();
                    figure.PortraitId=id; figure.raycastTarget=false; figure.color=new Color(1,1,1,dim?.4f:1);
                }
            }
            FadeToInk(x,y+h-56,w,56);
            string tag=overAir?"무전 수신 중":speaker==null?"":speaker+(dim?" · 듣는 중":"");
            if(tag.Length>0)
            {
                var tagBox=Box("Portrait tag",x+16,y+h-52,tag.Length*21+36,40,new Color(Ink.r,Ink.g,Ink.b,.8f));
                Label(tag,16,4,tag.Length*21+20,32,20,overAir?Cold:Muted,tagBox);
            }
            if(step>0)
            {
                var log=Pressable("지난 대화",x+w-176,y+16,160,60,()=>{sceneLog=true;Render();},true,Edge,new Color(Ink.r,Ink.g,Ink.b,.8f));
                var caption=Label("지난 대화",0,0,160,60,21,Paper,log.transform); caption.alignment=TextAnchor.MiddleCenter; caption.fontStyle=FontStyle.Bold;
            }
        }
        void SceneLog(List<ScriptBlock> blocks,float top)
        {
            Transcript(blocks.Take(sceneStep+1).ToList(),top,1286-top,false,-1,true);
            Button("장면으로 돌아가기",48,1302,804,88,()=>{sceneLog=false;Render();},true,true);
        }

        // ---- C · radio receiver ------------------------------------------------------------------------

        // transmission=true: the whole text is what came over the air, laid out as one receiver log.
        // false: a scene that quotes a broadcast; only the received parts are boxed, narration stays outside.
        RectTransform RadioText(List<ScriptBlock> blocks,float y,float height,bool transmission,string caption,int size=25)
        {
            Color fill=transmission?Panel:Ink;
            var area=ScrollArea("Radio text",y,height,fill,out var content);
            float cy=transmission?16:4;
            if(transmission && caption!=null) cy=ReceiverHeader(content,0,0,804,caption);
            for(int i=0;i<blocks.Count;i++)
            {
                var block=blocks[i];
                if(transmission) { cy+=RadioBlock(block,content,24,cy,756,size)+16; continue; }
                if(block.IsTransmission || block.Kind==ScriptBlockKind.Noise)
                {
                    // Consecutive received paragraphs share one receiver card.
                    var card=Box("Receiver",0,cy,804,40,Edge,content); var face=Box("Fill",2,2,800,36,Raised,card);
                    float inner=ReceiverHeader(card,0,0,804,block.Kind==ScriptBlockKind.Radio?"무전 · 수신":"SP 수신");
                    for(;i<blocks.Count && (blocks[i].IsTransmission || blocks[i].Kind==ScriptBlockKind.Noise);i++) inner+=RadioBlock(blocks[i],card,24,inner,756,size)+14;
                    i--;
                    card.sizeDelta=new Vector2(804,inner+6); face.sizeDelta=new Vector2(800,inner+2);
                    cy+=inner+6+20;
                    continue;
                }
                var text=Label(block.ToString(),0,cy,804,40,size+1,SceneNarration,content); text.lineSpacing=1.3f;
                float h=text.preferredHeight+4; text.rectTransform.sizeDelta=new Vector2(804,h); cy+=h+20;
            }
            float needed=cy+8;
            if(needed<height-60) { height=Mathf.Max(needed,120); area.sizeDelta=new Vector2(804,height); }
            content.sizeDelta=new Vector2(804,Mathf.Max(height,needed));
            if(needed>height+4) MoreBelow(area,fill,Muted);
            return area;
        }
        // "SP 수신 · 08:00 ····· ▮▮▯▯ 잡음 많음"; returns the y below it.
        float ReceiverHeader(Transform parent,float x,float y,float w,string caption)
        {
            Label(caption,x+24,y+18,w-260,30,20,Muted,parent);
            for(int i=0;i<4;i++) { float bar=8+i*6; Box("Signal",x+w-196+i*10,y+44-bar,6,bar,i<2?Muted:Edge,parent); }
            var noise=Label("잡음 많음",x+w-150,y+18,126,30,19,Muted,parent); noise.alignment=TextAnchor.UpperRight;
            Box("Receiver rule",x+24,y+58,w-48,2,Edge,parent);
            return y+76;
        }
        float RadioBlock(ScriptBlock block,Transform parent,float x,float y,float w,int size)
        {
            switch(block.Kind)
            {
                case ScriptBlockKind.Noise:
                {
                    var label=Label("▒▒▒  "+block.Meta+"  ▒▒▒",x,y,w,32,size-4,Faint,parent);
                    return label.preferredHeight+2;
                }
                case ScriptBlockKind.Section:
                {
                    var head=Label(block.Meta,x,y,w,30,20,Cold,parent); head.fontStyle=FontStyle.Bold;
                    float cy=y+38;
                    bool first=true;
                    foreach(var raw in block.Text.Split('\n'))
                    {
                        string line=raw.Trim(); if(line.Length==0) continue;
                        bool source=line.StartsWith("출처");
                        var text=Label(line,x,cy,w,40,source?size-5:first?size+1:size-3,source?Muted:first?Paper:Muted,parent);
                        if(first && !source) { text.fontStyle=FontStyle.Bold; first=false; }
                        float h=text.preferredHeight+2; text.rectTransform.sizeDelta=new Vector2(w,h); cy+=h+4;
                    }
                    return cy-y;
                }
                case ScriptBlockKind.Broadcast:
                {
                    float cy=y;
                    if(block.Meta.Length>0) cy+=Badge(block.Meta,parent,x,cy,w,Rust,Paper)+10;
                    if(block.Text.Length>0) { var text=Label(block.Text,x,cy,w,40,size,Paper,parent); text.lineSpacing=1.28f; float h=text.preferredHeight+4; text.rectTransform.sizeDelta=new Vector2(w,h); cy+=h; }
                    return cy-y;
                }
                case ScriptBlockKind.Speech:
                case ScriptBlockKind.Radio:
                {
                    var name=Label(block.Speaker,x,y+3,126,40,size-5,Cold,parent); name.fontStyle=FontStyle.Bold;
                    float nameH=name.preferredHeight; name.rectTransform.sizeDelta=new Vector2(126,nameH);
                    var text=Label(block.Text,x+138,y,w-138,40,size,Paper,parent); text.lineSpacing=1.28f;
                    float h=text.preferredHeight+4; text.rectTransform.sizeDelta=new Vector2(w-138,h);
                    return Mathf.Max(h,nameH);
                }
                default:
                {
                    var text=Label(block.Text,x,y,w,40,size,Paper,parent); text.lineSpacing=1.28f;
                    float h=text.preferredHeight+4; text.rectTransform.sizeDelta=new Vector2(w,h);
                    return h;
                }
            }
        }
#if UNITY_EDITOR || VD_UI_VERIFICATION
        // -vdDialogue: walks one scene of each presentation and captures it mid-scene and read to the end.
        IEnumerator SmokeDialogue()
        {
            string dir=Path.GetFullPath("Builds/DialogueVerification"); Directory.CreateDirectory(dir);
            state.Story.OpeningCompleted=true; state.PlayerNickname="수신자";
            state.Backpack.Add("radio",1); state.Story.HasRadio=true; state.ListenRadio(definitions);
            IEnumerator Shot(string name) { yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,name+".png")); yield return new WaitForSeconds(.4f); }
            void Expect(bool condition,string failure) { if(!condition) throw new InvalidOperationException("Dialogue UI: "+failure); }

            page="radio"; message=""; Render();
            Expect(root.Find("Radio text")!=null,"radio page is not a receiver log");
            yield return Shot("C-RADIO");

            state.Travel("supply_site",definitions,()=>1); state.ApproachCrowd(definitions); page="home"; message=""; Render();
            Expect(state.Crowd.Step==CrowdStoryStep.Panic && sceneAdvance!=null && shortcuts[0]==null,"panic narration should step with its choices hidden");
            sceneAdvance(); sceneAdvance();
            yield return Shot("A-JOURNAL-STEP");
            SkipScene();
            Expect(sceneAdvance==null && shortcuts[0]!=null,"choices missing after the journal scene was read");
            yield return Shot("A-JOURNAL-DONE");

            ClickGameButton("가방을 놓고");
            Expect(state.Crowd.Step==CrowdStoryStep.Rescue && sceneAdvance!=null,"rescue should open as a face-to-face scene");
            for(int i=0;i<5;i++) sceneAdvance();
            yield return Shot("B-SCENE-STEP");
            sceneLog=true; Render();
            yield return Shot("B-SCENE-LOG");
            ClickGameButton("다음",0);
            Expect(state.Crowd.Step!=CrowdStoryStep.Rescue,"skipping the scene did not reach its continue button");

            state.Crowd.Step=CrowdStoryStep.Introduction; state.Companion=CompanionStep.Completed; Render();
            ClickGameButton("기록하고 계속");
            int falseReport=Array.FindIndex(definitions.lateStory.beats,x=>x.id=="false_report");
            state.LateStep=falseReport; state.LateScenePending=true; page="home"; message=""; Render();
            Expect(sceneAdvance==null && shortcuts[0]!=null && root.Find("Radio text")!=null,"a broadcast scene should show the receiver card with its choices");
            yield return Shot("C-LATE-BROADCAST");

            state.LateStep=Array.FindIndex(definitions.lateStory.beats,x=>x.body.Contains("한지우:")); Render();
            Expect(sceneAdvance!=null,"a late scene with speech should step");
            sceneAdvance(); sceneAdvance();
            yield return Shot("B-LATE-STEP");
            SkipScene();
            yield return Shot("B-LATE-DONE");
            Debug.Log("VD_DIALOGUE_UI_OK: journal narration steps, face-to-face scenes step with portrait and log, broadcasts open as receiver logs, choices appear only after reading.");
            yield return null; Application.Quit();
        }
#endif
    }
}
