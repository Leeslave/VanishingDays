using System;

namespace VanishingDays
{
    public enum CompanionStep { None, Jiwoo, Preparation, Briefing, Decision, Debrief, Completed }
    public partial class SurvivalState
    {
        public int NeighborStage,NeighborGreetings,NeighborLastDay,CompanionReadyDay,CompanionTrust;
        public CompanionStep Companion;
        public string CompanionMeal="";
        public bool CompanionWithdrew;
        public bool CompanionHomePrepared;
        public bool HasActiveCompanion => Companion==CompanionStep.Jiwoo || Companion==CompanionStep.Briefing || Companion==CompanionStep.Decision || Companion==CompanionStep.Debrief;
        public bool CompanionAvailable => TotalDongrimMeetings>=3;
        public int CompanionMeetingDay => Companion==CompanionStep.None?LastDongrimMeetingDay+1:CompanionReadyDay;
        public string CompanionHint => !CompanionAvailable?"동림과의 재회를 이어가면 동행 탐사를 의논할 수 있다.":Companion==CompanionStep.Completed?"첫 동행 탐사를 마쳤다. 각자의 거처에서 생활하며 다음 연락을 기다린다.":"동행 약속 · DAY "+CompanionMeetingDay+"부터 낮 · 고등학교\n"+(Companion==CompanionStep.Preparation && !CompanionHomePrepared?"먼저 자취방에 돌아가 수면과 식량을 정리하자.\n":"")+"집에서 준비한 식사를 챙길 수 있다. 식사가 없어도 짧은 경로로 탐사할 수 있다.";
        public bool CanMeetCompanion(SurvivalDefinitions data) => CanAct && CompanionAvailable && (Companion==CompanionStep.None || Companion==CompanionStep.Preparation && CompanionHomePrepared) && day>=CompanionMeetingDay && CurrentLocation=="school" && !IsNight(data.TEMP_BALANCE);
        public string MeetCompanion(SurvivalDefinitions data)
        {
            if(!CanMeetCompanion(data)) return "약속 날짜 이후 낮에 고등학교에서 만나자. 늦어도 다음 날 다시 만날 수 있다.";
            if(Companion==CompanionStep.None) { Companion=CompanionStep.Jiwoo; Story.HasMetJiwoo=true; RecordFact("story","고등학교에서 한지우를 만나 동행 탐사의 위험을 들었다."); }
            else Companion=CompanionStep.Briefing;
            return Report(CompanionText(data));
        }
        public string CompanionText(SurvivalDefinitions data)
        {
            var script=data.lifeStory;
            switch(Companion)
            {
                case CompanionStep.Jiwoo:return Script(script.jiwooOpposition);
                case CompanionStep.Briefing:return Script(script.briefing)+"\n\n"+(CarriedMeal==null?"준비한 식사가 없어 짧은 경로를 택하기로 했다. 탐사는 계속할 수 있다.":"소지한 탐사용 식사 1개를 나누고 출발할 수 있다.");
                case CompanionStep.Decision:return Script(script.decision);
                case CompanionStep.Debrief:return Script((CompanionWithdrew?script.withdraw:script.inspect)+"\n\n"+script.debrief);
                default:return CompanionHint;
            }
        }
        string CarriedMeal
        {
            get { foreach(string id in new[]{"rice_meal","spam_meal","tuna_meal","ramen_meal"}) if(Backpack.Count(id)>0) return id; return null; }
        }
        public string ContinueCompanion(SurvivalDefinitions data,bool withdraw=true)
        {
            if(IsGameOver || !HasActiveCompanion || PendingEncounter!=null || HasActiveCrowdIncident || PendingDongrimMeeting) return "현재 이어갈 동행 탐사가 없다.";
            switch(Companion)
            {
                case CompanionStep.Jiwoo:
                    Companion=CompanionStep.Preparation; CompanionReadyDay=day+1;
                    RecordFact("expedition","동림과 내일 이후 낮에 학교에서 만나기로 했다. 준비를 위해 각자 집으로 돌아가기로 했다.");
                    return Report("오늘은 해산한다. 각자 살던 곳에서 수면과 식량을 준비하기로 했다. 집으로 돌아가 준비하자.\n\n"+CompanionHint);
                case CompanionStep.Briefing:
                    CompanionMeal=CarriedMeal??"";
                    if(CompanionMeal!="") { Backpack.Remove(CompanionMeal,1); CompanionTrust++; RecordFact("expedition",data.Item(CompanionMeal).name+" 1개를 나누고 동림과 학교 뒤편을 살피기 시작했다."); }
                    else RecordFact("expedition","동림과 식사 없이 가능한 짧은 경로로 학교 뒤편을 살피기 시작했다.");
                    AdvanceTime(2,data); if(IsGameOver) return Report("탐사 도중 몸을 일으킬 수 없게 됐다.");
                    Companion=CompanionStep.Decision; return Report(CompanionText(data));
                case CompanionStep.Decision:
                    CompanionWithdrew=withdraw;
                    AdvanceTime(withdraw?1:2,data); if(IsGameOver) return Report("탐사를 마칠 힘이 남지 않았다.");
                    if(withdraw) CompanionTrust+=2;
                    Companion=CompanionStep.Debrief;
                    RecordFact("expedition",withdraw?"동림의 걸음이 불편해 먼저 철수했다. 학교 앞에서 지우에게 돌아왔음을 알렸다.":"더 깊이 들어가지 않고 입구의 표식만 확인한 뒤 학교 앞에서 지우에게 결과를 전했다.");
                    return Report(CompanionText(data));
                case CompanionStep.Debrief:
                    Companion=CompanionStep.Completed;
                    RecordFact("expedition","첫 동행을 마치고 해산했다. 각자 거처로 돌아가기로 했다.");
                    return Report("동림과 지우에게 다음에도 귀환 소식을 전하기로 했다. 생활 거점은 송파구 자취방이다. 지도에서 집으로 돌아갈 수 있다.");
                default:return "현재 이어갈 동행 탐사가 없다.";
            }
        }
        public string GreetNeighbor(SurvivalDefinitions data)
        {
            if(!CanAct || CurrentLocation!="home" || NeighborLastDay>=day || NeighborStage==2) return "오늘의 안부는 이미 나눴다. 답이 없었다면 문 앞에서 직접 확인할 수 있다.";
            NeighborLastDay=day; NeighborGreetings++;
            if(NeighborStage==0) { NeighborStage=1; RecordFact("neighbor","옆집 노인에게 반찬 그릇을 돌려드렸다. 벽을 두 번 두드려 안부를 나누기로 했다."); return Report(data.lifeStory.neighborFirst); }
            if(NeighborGreetings==3) { NeighborStage=2; RecordFact("neighbor","벽을 두 번 두드렸지만 오늘은 답이 없었다. 아직 이유는 알 수 없다."); return Report(data.lifeStory.neighborQuiet); }
            string text=data.lifeStory.greetings[(NeighborGreetings-2)%data.lifeStory.greetings.Length]; RecordFact("neighbor",text); return Report(text);
        }
        public string CheckNeighbor(SurvivalDefinitions data)
        {
            if(!CanAct || CurrentLocation!="home" || NeighborStage!=2) return "지금 확인할 이웃의 안부는 없다.";
            AdvanceTime(1,data); if(IsGameOver) return Report("문 앞까지 갈 힘이 남지 않았다.");
            NeighborStage=3; NeighborLastDay=day;
            RecordFact("neighbor","옆집 문 앞에서 노인을 직접 확인했다. 잠들어 두드림을 듣지 못했다고 했다.");
            return Report(data.lifeStory.neighborChecked);
        }
    }
}
