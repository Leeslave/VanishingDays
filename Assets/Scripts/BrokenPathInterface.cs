using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VanishingDays
{
    // Act 3 「끊어진 길」 screens: the story scenes, the bridge as a work site, the plan comparison and the construction yard.
    public partial class VanishingDaysGame
    {
        [SerializeField, Tooltip("에디터 테스트 전용: 3막 「끊어진 길」 핵심 선택부터 시작합니다.")]
        bool startAtAct3InEditor;

        void BrokenPathScenePage()
        {
            var scene=state.CurrentBrokenPathScene(definitions);
            Label(scene.chapter,48,462,804,40,24,Amber);
            Label(scene.title,48,506,804,60,40,Paper).fontStyle=FontStyle.Bold;
            int count=scene.choices.Length; float top=1302-112*(count-1);
            if(!SceneText(state.BrokenPathText(scene.body,definitions),584,top-4)) return;
            for(int i=0;i<count;i++)
            {
                int index=i; var choice=scene.choices[i];
                bool field=choice.action=="field";
                int walk=definitions.Location("seongnae_bridge").travelHours;
                string detail=state.BrokenPathText(choice.detail,definitions)+(field?" · "+walk+"시간 이동"+(NightAt(walk)?" · 야간 도착":""):"");
                Choice(i,choice.label,detail,top+112*i,()=>
                {
                    string result=state.ChooseBrokenPath(index,definitions);
                    if(field && state.BrokenPathFieldWork && state.CurrentLocation!="seongnae_bridge") result+="\n\n"+state.Travel("seongnae_bridge",definitions);
                    Apply(result);
                });
            }
        }

        // Results heard over the radio stay radio logs; everything else is written on the journal page.
        void FieldText(string text,float y,float height)
        {
            var blocks=Blocks(text);
            if(ScriptBlocks.PresentationFor(blocks)==ScenePresentation.Radio) RadioText(blocks,y,height,false,null,24);
            else Paragraph(text,y,height,24,true);
        }
        string MaterialLine(BrokenPathMethod method)
        {
            string Part(ItemStack cost) => definitions.Item(cost.id).name+" "+Math.Min(state.BrokenPathHave(cost.id),cost.amount)+"/"+cost.amount;
            // A missing carried item that is made at home names its recipe, so the player knows where it comes from.
            string Carried(ItemStack cost)
            {
                var recipe=state.BrokenPathHave(cost.id)<cost.amount?Array.Find(definitions.crafting.recipes,x=>x.result==cost.id):null;
                return Part(cost)+(recipe==null?"":" (집에서 제작: "+recipe.name+")");
            }
            string large=string.Join(" · ",method.large.Select(Part)), small=string.Join(" · ",method.small.Select(Carried));
            return "보행교에 쌓음  "+large+(small.Length>0?"\n몸에 지님  "+small:"");
        }

        void BrokenPathBridgePage(LocationDefinition location)
        {
            var d=definitions.brokenPath; var p=state.BrokenPath;
            Label("3막 · 끊어진 길",48,462,804,36,22,Amber);
            Label(location.name,48,498,804,56,38,Paper).fontStyle=FontStyle.Bold;
            if(!state.BrokenPathFieldWork)
            {
                Paragraph(string.IsNullOrEmpty(message)?location.description:message,584,400,25,true);
                Choice(0,"지도를 확인한다","다음 행선지를 정한다",1010,()=>{page="map";message="";Render();});
                Choice(1,"집으로 돌아간다",location.travelHours+"시간",1122,()=>RequestDeparture("home"));
                return;
            }
            var method=d.Method(p.Method);
            Label("지원 인력 "+p.Helpers+"명 · 큰 자재는 한 번에 "+state.CarryCapacity+"개"+(state.CarriedLarge>0?" · 지금 "+state.CarriedLarge+"개를 들고 있다":""),48,558,804,34,22,state.CarriedLarge>0?AmberText:Muted);
            // The far side is only ever heard, never shown as a number.
            if(string.IsNullOrEmpty(message)) RadioText(Blocks(state.FarReport(definitions)),600,256,true,"무전 · 보호소 쪽");
            else
            {
                FieldText(message,600,256);
                // Long radio exchanges are read on their own page instead of a line at a time.
                if(Blocks(message).Count>3) { string log=message; Button("전체 읽기",692,556,160,40,()=>{fieldLog=log;page="field-log";Render();}); }
            }
            Label(method==null?"계획: 아직 정하지 않았다":"계획: "+method.name+" · 시공 "+method.BuildHours(p.Helpers)+"시간",48,866,804,36,25,method==null?Muted:Paper).fontStyle=FontStyle.Bold;
            Label(method==null?"계획 비교에서 방법을 고르면 필요한 자재가 여기에 표시된다.":MaterialLine(method),48,906,804,66,21,Muted);
            for(int i=0;i<d.surveys.Length;i++)
            {
                var survey=d.surveys[i]; bool done=p.Surveyed.Contains(survey.id);
                Button(survey.label+(done?" · 확인":" · 1시간"),48+(i%2)*414,984+(i/2)*76,390,66,()=>Apply(state.SurveyBrokenPath(survey.id,definitions)),!done && state.CanAct);
            }
            Button("계획 비교 · 방법 정하기",48,1144,390,72,()=>{page="bridge-plan";message="";Render();},state.CanAct,method==null);
            Button("자재 내려놓기"+(state.CarriedLarge>0?" · "+state.CarriedLarge+"개":""),462,1144,390,72,()=>Apply(state.DropAtBridge(definitions)),state.CanAct && state.CarriedLarge>0,state.CarriedLarge>0);
            PrimaryButton(method==null?"시공한다":"시공한다 · "+method.BuildHours(p.Helpers)+"시간",48,1230,804,76,()=>Apply(state.BuildBrokenPath(definitions)),state.CanBuildBrokenPath(definitions));
            int toSite=definitions.Location("construction_site").travelHours;
            Button("공사 현장으로 · "+toSite+"시간"+(NightAt(toSite)?" · 야간 도착 · 약탈꾼 주의":""),48,1320,804,68,()=>RequestDeparture("construction_site"),state.CanAct);
        }

        string fieldLog="";
        void FieldLogPage()
        {
            Label("3막 · 끊어진 길",48,462,804,36,22,Amber);
            Label("현장 기록",48,498,804,56,38,Paper).fontStyle=FontStyle.Bold;
            var blocks=Blocks(fieldLog);
            if(ScriptBlocks.PresentationFor(blocks)==ScenePresentation.Radio) RadioText(blocks,568,708,true,null,24);
            else Transcript(blocks,568,708,true,-1,false);
            PrimaryButton("보행교 화면으로",48,1300,804,76,()=>{page="home";message=fieldLog;Render();});
        }
        void BrokenPathPlanPage()
        {
            if(!state.BrokenPathFieldWork) { page="home"; Explore(); return; }
            var d=definitions.brokenPath; var p=state.BrokenPath;
            Label("계획 비교",48,462,560,60,36,Paper).fontStyle=FontStyle.Bold;
            Button("보행교로",652,462,200,60,()=>{page="home";message="";Render();});
            Label("조사하지 않은 항목은 ?로 남는다. 시공 전까지는 바꿀 수 있다.",48,530,804,34,21,Muted);
            bool gap=p.Surveyed.Contains("gap"), far=p.Surveyed.Contains("far");
            for(int i=0;i<d.methods.Length;i++)
            {
                var method=d.methods[i]; bool chosen=p.Method==method.id, ground=p.Surveyed.Contains(method.requires);
                float y=578+i*266;
                var card=Box("Plan card",48,y,804,256,chosen?Amber:Edge); Box("Fill",2,2,800,252,chosen?EmberDim:Panel,card);
                Label(method.name,24,14,540,40,28,chosen?AmberText:Paper,card).fontStyle=FontStyle.Bold;
                Label(method.summary,24,56,560,58,20,Muted,card);
                string cost=gap?string.Join(", ",method.large.Concat(method.small).Select(x=>definitions.Item(x.id).name+" "+x.amount)):"?";
                string terrain=ground?(method.requires=="pier"?"남은 교각이 버틴다":"둑 아래로 건널 수 있다"):(method.requires=="pier"?"교각 상태 ?":"우회 가능성 ?");
                string lines="자재  "+cost+"\n시공  "+method.BuildHours(p.Helpers)+"시간 · "+terrain+"\n건너기  "+method.crossing+" · 철수 "+method.retreat+"\n부상자  "+(far?method.injured:"?");
                var detail=Label(lines,24,124,756,124,21,Paper,card); detail.lineSpacing=1.2f;
                int index=i;
                Button(chosen?"정한 방법":"이 방법으로",604,14,180,60,()=>Apply(state.ChooseBrokenPathMethod(d.methods[index].id,definitions),"bridge-plan"),!chosen && !p.Built,!chosen,card);
            }
        }

        void ConstructionSitePage(LocationDefinition location)
        {
            var p=state.BrokenPath;
            Label(p.Stage==0?"2막 · 자재 창고":"3막 · 끊어진 길",48,462,804,36,22,Amber);
            Label(location.name,48,498,804,56,38,Paper).fontStyle=FontStyle.Bold;
            Paragraph(string.IsNullOrEmpty(message)?location.description:message,568,196,24,true);
            if(!state.BrokenPathFieldWork)
            {
                Choice(0,"지도를 확인한다","다음 행선지를 정한다",1010,()=>{page="map";message="";Render();});
                Choice(1,"집으로 돌아간다",location.travelHours+"시간",1122,()=>RequestDeparture("home"));
                return;
            }
            Label("남은 자재  사다리 "+p.Yard.Count("ladder")+" · 강철 발판 "+p.Yard.Count("steel_plank")+" · 고정 벨트 "+p.Yard.Count("cargo_belt"),48,782,804,36,24,Paper);
            Label("큰 자재 "+state.CarriedLarge+" / "+state.CarryCapacity+" 들고 있음 · 지원 인력 "+p.Helpers+"명이 함께 나른다",48,824,804,34,21,state.CarriedLarge>=state.CarryCapacity?AmberText:Muted);
            bool room=state.CarriedLarge<state.CarryCapacity;
            Choice(0,"사다리를 든다","큰 자재 · 남은 "+p.Yard.Count("ladder")+"개",890,()=>Apply(state.TakeYardMaterial("ladder",definitions)),state.CanAct && room && p.Yard.Count("ladder")>0);
            Choice(1,"강철 발판을 든다","큰 자재 · 남은 "+p.Yard.Count("steel_plank")+"개",1002,()=>Apply(state.TakeYardMaterial("steel_plank",definitions)),state.CanAct && room && p.Yard.Count("steel_plank")>0);
            Choice(2,"고정 벨트를 챙긴다","일반 소지품 · 남은 "+p.Yard.Count("cargo_belt")+"개 모두",1114,()=>Apply(state.TakeYardMaterial("cargo_belt",definitions)),state.CanAct && p.Yard.Count("cargo_belt")>0);
            int toBridge=definitions.Location("seongnae_bridge").travelHours;
            PrimaryButton("보행교로 나른다 · "+toBridge+"시간"+(NightAt(toBridge)?" · 야간 도착":""),48,1236,804,76,()=>RequestDeparture("seongnae_bridge"),state.CanAct);
            Label("집에 돌아가면 소지품은 집에 보관된다. 큰 자재는 보행교에 내려놓자.",48,1328,804,34,20,Muted);
        }

        void BeginAct3Test()
        {
            state=SurvivalState.Act3TestStart(definitions,4,string.IsNullOrWhiteSpace(state?.PlayerNickname)?"재현":state.PlayerNickname);
            page="home"; message=""; scenesRead.Clear(); Save(); Render();
        }

#if UNITY_EDITOR || VD_UI_VERIFICATION
        // -vdAct3: plays route A to the end through the real buttons, then route B, capturing each step.
        IEnumerator SmokeAct3()
        {
            string dir=Path.GetFullPath("Builds/Act3Verification"); Directory.CreateDirectory(dir);
            IEnumerator Shot(string name) { Render(); yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(dir,name+".png")); yield return new WaitForSeconds(.4f); }
            void Expect(bool condition,string failure) { if(!condition) throw new InvalidOperationException("Act 3 UI: "+failure); }
            void Go(string id) { state.Vitals.Hunger=0; state.Thirst=0; state.HydrationHours=0; state.Sleepiness=0; Apply(state.Travel(id,definitions,()=>1)); Expect(state.CurrentLocation==id,"could not reach "+id+": "+state.lastReport); Records(); }
            // Nights pass during field work; the day's record and morning radio come first, as in play.
            void Records() { for(int guard=0;guard<6 && state.NextDiary!=null;guard++) ClickGameButton("읽고 계속한다"); foreach(var issue in state.MorningIssues) issue.read=true; Render(); }

            state=SurvivalState.Act3TestStart(definitions,6,"재현"); page="home"; message=""; Save(); Render();
            Expect(state.BrokenPathScenePending && sceneAdvance!=null,"core choice should open as a face-to-face scene");
            sceneAdvance(); sceneAdvance();
            yield return Shot("01-CORE-CHOICE");
            ClickGameButton("동림의 구조를 돕는다");
            Expect(state.BrokenPath.Scene=="path_broken_A" && state.BrokenPath.Helpers==2,"route A call did not open");
            yield return Shot("02-PATH-BROKEN-RADIO");
            ClickGameButton("보행교로 가 본다"); state.PendingEncounter=null; Records();
            Expect(state.BrokenPathFieldWork && state.IsLocationUnlocked("seongnae_bridge",definitions),"field work did not start");

            Go("seongnae_bridge");
            yield return Shot("03-BRIDGE");
            ClickGameButton("끊어진 길이를"); ClickGameButton("남은 교각을"); ClickGameButton("건너편 상황을");
            yield return Shot("04-SURVEY-RADIO");
            ClickGameButton("계획 비교");
            yield return Shot("05-PLAN-UNKNOWN-DETOUR");
            ClickGameButton("이 방법으로",1);
            Expect(state.BrokenPath.Method=="bridge","plan choice failed");
            yield return Shot("06-PLAN-CHOSEN");

            Go("construction_site");
            ClickGameButton("사다리를 든다"); ClickGameButton("사다리를 든다"); ClickGameButton("강철 발판을 든다");
            Expect(state.CarriedLarge==3 && state.TakeYardMaterial("steel_plank",definitions).Contains("한 번에"),"carry limit not enforced");
            ClickGameButton("고정 벨트를");
            yield return Shot("07-YARD-FULL");
            Go("seongnae_bridge"); ClickGameButton("자재 내려놓기");
            Go("construction_site"); ClickGameButton("강철 발판을 든다");
            Go("seongnae_bridge"); ClickGameButton("자재 내려놓기");
            Expect(state.BrokenPath.Site.Count("ladder")==2 && state.BrokenPath.Site.Count("steel_plank")==2,"materials not stacked at the bridge");
            Go("home");
            Apply(state.Craft("make_rope",definitions),"craft"); Apply(state.Craft("make_rope",definitions),"craft"); Apply(state.Craft("make_stretcher",definitions),"craft"); Records();
            foreach(var id in new[]{"rope","cargo_belt","stretcher"}) state.Transfer(id,true,definitions);
            Expect(state.Backpack.Count("rope")==2 && state.Backpack.Count("cargo_belt")==2 && state.Backpack.Count("stretcher")==1,"rope, belts and stretcher not carried");
            Go("seongnae_bridge");
            yield return Shot("08-BRIDGE-READY");
            ClickGameButton("시공한다"); Records();
            Expect(state.BrokenPath.Scene=="evac_A" && sceneAdvance!=null,"evacuation scene did not open");
            sceneAdvance(); sceneAdvance(); sceneAdvance();
            yield return Shot("09-EVAC-STEP");
            SkipScene();
            yield return Shot("10-EVAC-CHOICE");
            ClickGameButton("퇴로를 지킨다");
            yield return Shot("11-RESULT-A2");
            SkipScene(); ClickGameButton("다음");
            ClickGameButton("기록하고 계속");
            Expect(state.BrokenPath.Stage==4 && state.BrokenPath.Outcome=="A2" && state.BrokenPath.DonglimAlive && !state.BrokenPath.SeokhyeonCoop && state.BrokenPath.YeongsukState=="Rescued","route A2 result wrong");
            Expect(Save() && store.TryLoad(out var restored,out _) && restored.BrokenPath.Outcome=="A2","finished act 3 did not save");
            yield return Shot("12-AFTER-A2");

            // Route B with no helpers: two trips for two ladders, the riverbed, and no stretcher.
            state=SurvivalState.Act3TestStart(definitions,0,"재현"); page="home"; message=""; Render();
            ClickGameButton("석현의 중계 철수를");
            Expect(state.BrokenPath.Helpers==0,"route B test should have no helpers, got "+state.BrokenPath.Helpers);
            yield return Shot("13-PATH-BROKEN-B");
            ClickGameButton("보행교로 가 본다"); state.PendingEncounter=null; Records();
            Go("seongnae_bridge"); ClickGameButton("둑 아래 길을"); ClickGameButton("계획 비교"); ClickGameButton("이 방법으로",2);
            for(int trip=0;trip<2;trip++) { Go("construction_site"); ClickGameButton("사다리를 든다"); Expect(state.CarriedLarge==1 && state.TakeYardMaterial("ladder",definitions).Contains("한 번에"),"solo carry limit"); Go("seongnae_bridge"); ClickGameButton("자재 내려놓기"); }
            Go("home"); Apply(state.Craft("make_rope",definitions),"craft"); Records(); state.Transfer("rope",true,definitions);
            Go("seongnae_bridge"); ClickGameButton("시공한다"); Records();
            SkipScene();
            yield return Shot("14-EVAC-B");
            ClickGameButton("다음");
            yield return Shot("15-EMPTY-LADLE");
            ClickGameButton("기록하고 계속");
            Expect(state.BrokenPath.Stage==4 && state.BrokenPath.Outcome.StartsWith("B_") && state.BrokenPath.YeongsukState=="Missing" && state.BrokenPath.SeokhyeonCoop,"route B result wrong");
            Debug.Log("VD_ACT3_UI_OK: core choice, radio call, surveys, plan with unknowns, carry limit, stacking, crafting, build, evacuation, A2 and "+state.BrokenPath.Outcome+" finished and saved.");
            state.day=Math.Max(state.day,state.LateReadyDay); state.hour=8;
            Go("shelter"); ClickGameButton("끊긴 연락처를 직접");
            state.day=Math.Max(state.day,state.LateReadyDay); state.hour=8;
            Go("sp_station"); ClickGameButton("수신 기록을 대조");
            ClickGameButton("대조 결과를 기록"); ClickGameButton("교신 권한을 나눈다");
            ClickGameButton("검증된 분산 안내");
            Expect(state.RelayActive && state.CurrentAct(definitions)==4,"act 4 relay operation unavailable");
            page="home"; Render(); ClickGameButton("아침 거점 운영");
            yield return Shot("16-RELAY-MAP");
            ClickGameButton("거점 점:pharmacy");
            ClickGameButton("확인한 경로·수용 정보 방송");
            ClickGameButton("거점 지도로 돌아가기");
            ClickGameButton(definitions.Location("shelter").mapLabel);
            ClickGameButton("담당자 배치 · 한지우");
            ClickGameButton("거점 지도로 돌아가기");
            yield return Shot("17-RELAY-PLAN");
            ClickGameButton("오늘 방송 송출");
            Expect(state.Relay.broadcasts==1 && state.Relay.plan.Count==0 && state.RelayNodeAt("shelter").staff=="jiwoo","relay plan did not submit");
            Expect(Save() && store.TryLoad(out var relayRestored,out _) && relayRestored.Relay.broadcasts==1,"relay operation did not save");
            Debug.Log("VD_RELAY_UI_OK: rescue outcome into the SP station, map nodes, two queued decisions, broadcast and saved operation.");
            yield return null; Application.Quit();
        }
#endif
    }
}
