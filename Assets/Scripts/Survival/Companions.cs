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
        // The meal only matters for the expedition itself, so the first promise (meeting Jiwoo) does not ask for one.
        public string CompanionHint => !CompanionAvailable?"동림과의 재회를 이어가면 동행 탐사를 의논할 수 있다.":Companion==CompanionStep.Completed?"첫 동행 탐사를 마쳤다. 각자의 거처에서 생활하며 다음 연락을 기다린다.":
            HasActiveCompanion?"동행 중 · 고등학교\n동림, 지우와 함께하는 일을 마저 정리하자.":
            "동행 약속 · DAY "+CompanionMeetingDay+"부터 낮 · 고등학교\n"+(Companion==CompanionStep.None?"동림이 함께 다니는 사람을 소개해 주기로 했다. 따로 챙길 물건은 없다.":
            (!CompanionHomePrepared?"먼저 자취방에 돌아가 수면과 식량을 정리하자.\n":"")+"집에서 만든 식사를 가방에 챙기면 탐사 전에 함께 나눠 먹는다. 식사가 없어도 짧은 경로로 탐사할 수 있다.");
        // While the companion promise or expedition is open, Dongrim is waiting at the school rather than on patrol.
        public bool CompanionPhase => CompanionPromisePending || HasActiveCompanion;
        // The promise only exists until the first expedition starts; afterwards the companion page just points at the next story stop.
        public bool CompanionPromisePending => CompanionAvailable && (Companion==CompanionStep.None || Companion==CompanionStep.Preparation);
        public bool CompanionPromiseDue => CompanionPromisePending && day>=CompanionMeetingDay && (Companion==CompanionStep.None || CompanionHomePrepared);
        public string CompanionMeetLabel(SurvivalDefinitions data) =>
            CanMeetCompanion(data)?"약속 장소에서 만난다":
            day<CompanionMeetingDay?"DAY "+CompanionMeetingDay+"부터 만날 수 있다":
            Companion==CompanionStep.Preparation && !CompanionHomePrepared?"먼저 자취방에서 준비한다":
            CurrentLocation!="school"?"고등학교에서 만날 수 있다":
            IsNight(data.TEMP_BALANCE)?"날이 밝으면 만날 수 있다":"지금은 만날 수 없다";
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
                case CompanionStep.Briefing:return Script(script.briefing)+"\n\n"+(CarriedMeal==null?"준비한 식사가 없어 짧은 경로를 택하기로 했다. 탐사는 계속할 수 있다.":"소지한 "+data.Item(CarriedMeal).name+" 1개를 셋이 나눠 먹고 출발한다. 내 몫으로 허기 -"+SharedMealRelief(data.Item(CarriedMeal))+".");
                case CompanionStep.Decision:return Script(script.decision);
                case CompanionStep.Debrief:return Script((CompanionWithdrew?script.withdraw:script.inspect)+"\n\n"+script.debrief);
                default:return CompanionHint;
            }
        }
        public static int SharedMealRelief(ItemDefinition meal) => (meal.hungerRelief+1)/2;
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
                    return Report("오늘은 해산한다. 각자 살던 곳에서 수면과 식량을 준비하기로 했다. 집으로 돌아가 준비하자.");
                case CompanionStep.Briefing:
                    CompanionMeal=CarriedMeal??"";
                    string shared="";
                    if(CompanionMeal!="")
                    {
                        // Shared three ways, the player still eats their portion.
                        var meal=data.Item(CompanionMeal); int relief=SharedMealRelief(meal);
                        Backpack.Remove(CompanionMeal,1); CompanionTrust++;
                        Vitals.Hunger=Math.Max(0,Vitals.Hunger-relief); UpdateHungerPenalty(data.TEMP_BALANCE);
                        shared="[나눠 먹은 식사] "+meal.name+" · 허기 -"+relief;
                        RecordFact("expedition",meal.name+" 1개를 나눠 먹고 동림과 학교 뒤편을 살피기 시작했다.");
                    }
                    else RecordFact("expedition","동림과 식사 없이 가능한 짧은 경로로 학교 뒤편을 살피기 시작했다.");
                    AdvanceTime(2,data); if(IsGameOver) return Report("탐사 도중 몸을 일으킬 수 없게 됐다.");
                    Companion=CompanionStep.Decision; return Report((shared.Length>0?shared+"\n\n":"")+CompanionText(data));
                case CompanionStep.Decision:
                    CompanionWithdrew=withdraw;
                    AdvanceTime(withdraw?1:2,data); if(IsGameOver) return Report("탐사를 마칠 힘이 남지 않았다.");
                    if(withdraw) CompanionTrust+=2;
                    Companion=CompanionStep.Debrief;
                    RecordFact("expedition",withdraw?"동림의 걸음이 불편해 먼저 철수했다. 학교 앞에서 지우에게 돌아왔음을 알렸다.":"더 깊이 들어가지 않고 입구의 표식만 확인한 뒤 학교 앞에서 지우에게 결과를 전했다.");
                    return Report(CompanionText(data));
                case CompanionStep.Debrief:
                    // The expedition counts as today's meeting, so the next patrol stop starts from here.
                    Companion=CompanionStep.Completed; LateReadyDay=day+1; LastDongrimMeetingDay=day;
                    RecordFact("expedition","첫 동행을 마치고 해산했다. 각자 거처로 돌아가기로 했다.");
                    return Report("동림과 지우에게 다음에도 귀환 소식을 전하기로 했다. 생활 거점은 송파구 자취방이다. 지도에서 집으로 돌아갈 수 있다.");
                default:return "현재 이어갈 동행 탐사가 없다.";
            }
        }
        public string GreetNeighbor(SurvivalDefinitions data)
        {
            if(NeighborMoved) return data.lateStory.neighborMoved;
            if(!CanAct || CurrentLocation!="home" || NeighborLastDay>=day || NeighborStage==2) return "오늘의 안부는 이미 나눴다. 답이 없었다면 문 앞에서 직접 확인할 수 있다.";
            NeighborLastDay=day; NeighborGreetings++;
            NeighborIntroductionSeen=true;
            if(NeighborStage==0) { NeighborStage=1; RecordFact("neighbor","옆집 노인에게 반찬 그릇을 돌려드렸다. 벽을 두 번 두드려 안부를 나누기로 했다."); return Report(data.lifeStory.neighborFirst); }
            if(NeighborGreetings==3) { NeighborStage=2; RecordFact("neighbor","벽을 두 번 두드렸지만 오늘은 답이 없었다. 아직 이유는 알 수 없다."); return Report(data.lifeStory.neighborQuiet); }
            string text=data.lifeStory.greetings[(NeighborGreetings-2)%data.lifeStory.greetings.Length]; RecordFact("neighbor",text); return Report(text);
        }
        // After the move the neighbor's sign of life is a white towel on the pharmacy window, visible from home in daylight once a day.
        public int TowelLastDay, TowelDays;
        public bool TowelSeenToday => TowelLastDay==day && TowelDays>0;
        public bool TowelMissing(SurvivalDefinitions data) => HasEnded || CurrentBeat(data)?.id is "final_call" or "final_visit" && day>=LateReadyDay;
        public bool CanCheckTowel(SurvivalDefinitions data) => CanAct && NeighborMoved && CurrentLocation=="home" && !IsNight(data.TEMP_BALANCE) && !TowelSeenToday && !TowelMissing(data);
        public string TowelStatus(SurvivalDefinitions data)
        {
            var script=data.lateStory;
            if(TowelMissing(data)) return script.towelMissing;
            if(TowelSeenToday) return script.towelDone;
            if(CurrentLocation!="home") return script.towelAway;
            return IsNight(data.TEMP_BALANCE)?script.towelDark:script.towelWaiting;
        }
        public string CheckTowel(SurvivalDefinitions data)
        {
            if(!CanCheckTowel(data)) return TowelStatus(data);
            TowelLastDay=day; TowelDays++;
            RecordFact("neighbor","약국 위층 창가에 흰 수건이 걸려 있었다.");
            var seen=data.lateStory.towelSeen;
            return Report(Script(seen[(TowelDays-1)%seen.Length]));
        }
        public string CheckNeighbor(SurvivalDefinitions data)
        {
            if(!CanAct || CurrentLocation!="home" || NeighborStage!=2 || NeighborMoved) return "지금 확인할 이웃의 안부는 없다.";
            AdvanceTime(1,data); if(IsGameOver) return Report("문 앞까지 갈 힘이 남지 않았다.");
            NeighborStage=3; NeighborLastDay=day;
            RecordFact("neighbor","옆집 문 앞에서 노인을 직접 확인했다. 잠들어 두드림을 듣지 못했다고 했다.");
            return Report(data.lifeStory.neighborChecked);
        }
    }
}
