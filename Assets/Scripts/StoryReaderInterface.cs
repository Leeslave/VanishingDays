using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        const string StoryScenePath="Assets/Scenes/StoryReader.unity";
        [SerializeField] bool storyReaderOnly;
        bool readingStory, leavingStory;
        int storyChapter;
        string storyReaderName="안재현";
        Action returnFromStory;
        List<(string title,string body)>[] storyChapters;

        // The reader spoils every branch, so players earn it by reaching any ending once; the flag outlives "new record" restarts.
        const string StoryReaderUnlockKey="vd_story_reader_unlocked";
        bool StoryReaderUnlocked
        {
            get
            {
#if UNITY_EDITOR || VD_UI_VERIFICATION
                return true;
#else
                return state?.HasEnded==true || PlayerPrefs.GetInt(StoryReaderUnlockKey,0)==1;
#endif
            }
        }

        void UnlockStoryReader()
        {
            if(PlayerPrefs.GetInt(StoryReaderUnlockKey,0)==1) return;
            PlayerPrefs.SetInt(StoryReaderUnlockKey,1);
            PlayerPrefs.Save();
        }

        void OpenStoryReader()
        {
            if(!readingStory && StoryReaderUnlocked) StartCoroutine(LoadStoryReader());
        }

        IEnumerator LoadStoryReader()
        {
            readingStory=true;
            yield return SceneManager.LoadSceneAsync(StoryScenePath,LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByPath(StoryScenePath);
            var reader=scene.GetRootGameObjects().Select(x=>x.GetComponent<VanishingDaysGame>()).First(x=>x!=null);
            if(!string.IsNullOrEmpty(state.PlayerNickname)) reader.storyReaderName=state.PlayerNickname;
            reader.storyChapters=null;
            reader.returnFromStory=()=>StartCoroutine(CloseStoryReader(scene));
            root.parent.gameObject.SetActive(false);
            if(reader.root!=null) reader.StoryReaderPage();
        }

        IEnumerator CloseStoryReader(Scene scene)
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            root.parent.gameObject.SetActive(true);
            readingStory=false;
        }

        void LeaveStoryReader()
        {
            if(leavingStory) return;
            leavingStory=true;
            if(returnFromStory!=null) returnFromStory();
            else SceneManager.LoadSceneAsync("Prototype");
        }

        void StoryReaderPage()
        {
            storyChapters??=BuildStoryChapters();
            Clear();
            Label("소멸일지 · 스토리 열람",48,74,804,62,42,Paper).fontStyle=FontStyle.Bold;
            Label("전체 대본과 선택 결과를 공개합니다. 생존 기록에는 반영되지 않습니다.",48,152,804,52,23,Muted);
            string[] labels={"프롤로그","1막","2막","3막","4막","5막","결말"};
            for(int i=0;i<labels.Length;i++)
            {
                int chapter=i;
                Button(labels[i],48+i*116,226,108,58,()=>{storyChapter=chapter;StoryReaderPage();},true,i==storyChapter);
            }
            ScrollArea("스토리 본문",320,1080,Panel,out var content);
            float y=24;
            foreach(var entry in storyChapters[storyChapter])
            {
                var title=Label(entry.title,24,y,756,54,30,Amber,content);
                title.fontStyle=FontStyle.Bold;
                y+=Mathf.Max(48,title.preferredHeight+12);
                var body=Label(StoryReaderText(entry.body),24,y,756,160,27,Paper,content);
                body.font=storyFont??font;
                body.lineSpacing=1.25f;
                float height=body.preferredHeight+12;
                body.rectTransform.sizeDelta=new Vector2(756,height);
                y+=height+34;
                Box("Story divider",24,y-16,756,2,Edge,content);
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(1080,y));
            Button("돌아가기",48,1450,804,76,LeaveStoryReader,true,true);
        }

        List<(string title,string body)>[] BuildStoryChapters()
        {
            var chapters=Enumerable.Range(0,7).Select(_=>new List<(string title,string body)>()).ToArray();
            foreach(var screen in OpeningScriptData.Load().screens.Where(x=>!string.IsNullOrEmpty(x.body) && x.kind!="Nickname"))
                chapters[0].Add((screen.title,screen.body));

            var radio=definitions.radioStory;
            var life=definitions.lifeStory;
            var dongrim=definitions.dongrimFollowup;
            chapters[1].AddRange(new[]{
                ("옆집의 첫 인사",life.neighborFirst),("대답 없는 벽",life.neighborQuiet+"\n\n"+life.neighborChecked),
                (radio.firstBroadcast.title,radio.firstBroadcast.body+"\n\n"+radio.clueCard),
                ("가락시장",string.Join("\n\n",radio.approach,radio.observe,radio.panic,radio.rescue,radio.disappearance,radio.introduction,radio.completed))
            });
            chapters[2].AddRange(new[]{
                ("빈집과 고등학교의 재회",string.Join("\n\n",dongrim.secondMeeting,dongrim.secondCompleted,dongrim.thirdMeeting,dongrim.thirdCompleted)),
                ("첫 동행",string.Join("\n\n",life.jiwooOpposition,life.briefing,life.decision,"[철수] "+life.withdraw,"[입구 확인] "+life.inspect,life.debrief))
            });
            foreach(var beat in definitions.lateStory.beats)
            {
                if(beat.id=="core_choice")
                {
                    foreach(var scene in definitions.brokenPath.scenes)
                        chapters[3].Add((scene.title,scene.body+"\n\n"+string.Join("\n",scene.choices.Select(choice=>"· "+choice.label))));
                    continue;
                }
                var lines=new List<string>{beat.body};
                if(!string.IsNullOrEmpty(beat.bodyIf))
                {
                    string condition=beat.condition switch
                    {
                        "seokhyeon_coop"=>"석현과 협력",
                        "donglim_lost"=>"동림 사망",
                        "warned"=>"사전 경고",
                        "neighbor_met"=>"이웃과 인사를 나눔",
                        "meal_check_log"=>"배식 장부에 안부 확인 칸을 만듦",
                        "jiwoo_shared"=>"교신 권한을 나눔",
                        _=>"조건 충족"
                    };
                    lines.Add("["+condition+"]\n"+beat.bodyIf);
                    if(!string.IsNullOrEmpty(beat.bodyElse)) lines.Add("[다른 경우]\n"+beat.bodyElse);
                }
                foreach(var choice in beat.choices)
                    lines.Add("선택 · "+choice.label+"\n"+choice.result);
                if(!string.IsNullOrEmpty(beat.aftermath)) lines.Add(beat.aftermath);
                chapters[beat.act].Add((beat.title,string.Join("\n\n",lines)));
            }
            chapters[5].Add((definitions.lateStory.lureTitle,definitions.lateStory.lureGatherBody));
            foreach(var ending in definitions.lateStory.endings)
                chapters[6].Add((ending.title,ending.body));
            chapters[6].Add(("진엔딩 이후",state.ArchiveText(definitions)));
            return chapters;
        }

#if UNITY_EDITOR || VD_UI_VERIFICATION
        IEnumerator SmokePlaytest()
        {
            state.Story.OpeningCompleted=true;
            state.PlayerNickname="21일차 검증";
            state.NeighborIntroductionSeen=true;
            state.Story.HasRadio=true;
            state.HomeStorage.Add("radio",1);
            page="home";
            Render();
            var actions=root.Find("생활 행동 목록").GetComponent<UnityEngine.UI.ScrollRect>();
            var names=actions.content.GetComponentsInChildren<UnityEngine.UI.Button>().Select(button=>button.name).ToArray();
            if(names.Length!=5 || !names[2].StartsWith("03  제작") || !names[3].StartsWith("04  라디오") ||
                !names[4].Contains("잠을 잔다") || actions.content.rect.height<=actions.viewport.rect.height)
                throw new InvalidOperationException("Home action order or scrolling regressed.");

            var recipe=definitions.crafting.Find("water_collector");
            foreach(var cost in recipe.ingredients) state.HomeStorage.Add(cost.id,cost.amount);
            Apply(state.Craft(recipe.id,definitions),"craft");
            if(state.CollectorGuideStep!=1 || root.Find("Collector guide")==null) throw new InvalidOperationException("Installing the collector did not launch the guide.");
            ClickGameButton("식수 관리");
            if(state.CollectorGuideStep!=2 || page!="water") throw new InvalidOperationException("Water button did not advance the collector guide.");
            ClickGameButton("물 확보");
            if(state.CollectorGuideStep!=3 || waterTab!=1) throw new InvalidOperationException("Water tab did not advance the collector guide.");
            ClickGameButton("2시간 자동 집수 시작");
            if(state.CollectorGuideStep!=0 || state.CollectorCycleHours!=2) throw new InvalidOperationException("Collection button did not finish the guide.");
            state.AdvanceTime(2,definitions);
            Render();
            var scrim=root.Find("Collection notice scrim").GetComponent<UnityEngine.UI.Image>();
            if(scrim.color.a>=.5f) throw new InvalidOperationException("Collector popup still covers the underlying screen.");
            ClickGameButton("물을 회수한다 · 0시간");

            page="menu";
            Render();
            int clock=state.Clock;
            uint rng=state.RandomState;
            string save=System.IO.File.ReadAllText(SavePath);
            ClickGameButton("스토리 열람");
            VanishingDaysGame reader=null;
            for(int frame=0;frame<360 && (reader?.root==null || reader.returnFromStory==null);frame++)
            {
                yield return null;
                reader=FindObjectsByType<VanishingDaysGame>(FindObjectsSortMode.None).FirstOrDefault(game=>game.storyReaderOnly);
            }
            if(reader?.root==null || reader.returnFromStory==null || root.parent.gameObject.activeSelf) throw new InvalidOperationException("The separate story scene did not take focus.");
            reader.ClickGameButton("5막");
            reader.ClickGameButton("결말");
            if(!reader.root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(label=>label.text.Contains("EXP-01")))
                throw new InvalidOperationException("Story archive is missing ending content.");
            reader.ClickGameButton("돌아가기");
            for(int frame=0;frame<360 && readingStory;frame++) yield return null;
            if(readingStory || !root.parent.gameObject.activeSelf || state.Clock!=clock || state.RandomState!=rng || System.IO.File.ReadAllText(SavePath)!=save)
                throw new InvalidOperationException("Opening and leaving the reader mutated survival state or the save.");
            Debug.Log("VD_DAY21_UI_OK: scrollable home actions, collector click-through and translucent notice, separate story scene and intact save.");
            Application.Quit();
        }
#endif

        string StoryReaderText(string body)
        {
            return SurvivalState.WithScriptName(body,storyReaderName)
                .Replace("{BLine}",definitions.brokenPath.lightLine+" / "+definitions.brokenPath.heavyLine)
                .Replace("{MethodBuilt}","[선택한 시공 방법]").Replace("{FarPeople}","[건너편 인원]")
                .Replace("{FirstGroup}","[먼저 건넌 인원]").Replace("{Remaining}","[남은 인원]")
                .Replace("{Extra}","[추가 구조 인원]").Replace("{Lost}","[실종 인원]")
                .Replace("{Rescued}","[구조 인원]").Replace("{Survivors}","[생환 인원]");
        }
    }
}
