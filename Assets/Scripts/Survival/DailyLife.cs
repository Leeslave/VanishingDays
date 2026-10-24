using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    [Serializable] public class DailyFact { public int day,hour,count; public string kind,text; }
    [Serializable] public class DiaryEntry { public int day; public string body; public bool read; }
    [Serializable] public class MorningIssue { public int day; public string kind,body; public bool read; }
    [Serializable] public class LifeStoryDefinition
    {
        public string neighborFirst,neighborQuiet,neighborChecked,jiwooOpposition,briefing,decision,withdraw,inspect,debrief;
        public string[] greetings,news;
        public static LifeStoryDefinition Load()
        {
            var asset=Resources.Load<TextAsset>("Survival/LifeStory");
            if(asset==null) throw new InvalidOperationException("Missing daily life story.");
            var data=JsonUtility.FromJson<LifeStoryDefinition>(asset.text);
            if(data.greetings==null || data.greetings.Length<3 || data.news==null || data.news.Length<4 || new[]{data.neighborFirst,data.neighborQuiet,data.neighborChecked,data.jiwooOpposition,data.briefing,data.decision,data.withdraw,data.inspect,data.debrief}.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Incomplete daily life story.");
            return data;
        }
    }
    public partial class SurvivalState
    {
        public List<DailyFact> DailyFacts=new List<DailyFact>();
        public List<DiaryEntry> Diaries=new List<DiaryEntry>();
        public List<MorningIssue> MorningIssues=new List<MorningIssue>();
        public int DiaryStartDay=1,LastDiaryDay,MorningLastDay,MorningDeferredDay,PartialDiaryDay;
        public DiaryEntry NextDiary => Diaries.Find(x=>!x.read);
        public bool HasPendingMorning => CanUseRadio && MorningDeferredDay<MorningLastDay && MorningIssues.Exists(x=>!x.read);
        public bool HasLifeNotice => NextDiary!=null || HasPendingMorning;
        public bool CanViewErasureReport(ErasureReport report) => CanUseRadio || report.day<=ErasureReadDay || MorningIssues.Exists(x=>x.day==report.day && x.read);
        public void RecordFact(string kind,string text,int count=1)
        {
            if(day<DiaryStartDay) return;
            if(kind=="sleep")
            {
                var same=DailyFacts.Find(x=>x.day==day && x.kind==kind);
                if(same!=null) { same.count+=count; return; }
            }
            DailyFacts.Add(new DailyFact{day=day,hour=hour,kind=kind,text=text,count=count});
        }
        public void FinalizeDiary(int date,SurvivalDefinitions data)
        {
            if(date<DiaryStartDay || date<=LastDiaryDay) return;
            var facts=DailyFacts.Where(x=>x.day==date).ToArray();
            var lines=new List<string>();
            lines.AddRange(facts.Where(x=>x.kind=="expedition" || x.kind=="story").Select(x=>x.text).Distinct().TakeLast(2));
            if(lines.Count<2) lines.AddRange(facts.Where(x=>x.kind=="loot").Select(x=>x.text).Distinct().TakeLast(2-lines.Count));
            if(lines.Count<2) lines.AddRange(facts.Where(x=>x.kind=="travel").Select(x=>x.text).Distinct().TakeLast(2-lines.Count));
            lines.AddRange(facts.Where(x=>x.kind=="loss" || x.kind=="injury" || x.kind=="shortage").Select(x=>x.text).Distinct().TakeLast(2));
            var sleep=facts.FirstOrDefault(x=>x.kind=="sleep"); if(sleep!=null) lines.Add(sleep.text.Replace("{hours}",sleep.count.ToString()));
            lines.AddRange(facts.Where(x=>x.kind=="neighbor" || x.kind=="cook").Select(x=>x.text).Distinct().TakeLast(2));
            if(lines.Count==0) lines.Add("새로 적어 둘 탐사나 만남은 없었다. 오늘 확인한 일만 기록으로 남긴다.");
            if(Story.HasMetDongrim) lines.Add("알고 있는 다음 약속: "+DongrimHint(data));
            string author=string.IsNullOrWhiteSpace(PlayerNickname)?ProtagonistScriptName:PlayerNickname;
            string body="기록지 #"+date.ToString("000")+"\n"+new DateTime(2031,2,26).AddDays(date-1).ToString("yyyy.MM.dd")+" · 작성자 : "+author+"\n\n"+(date==PartialDiaryDay?"기록을 다시 이어 쓰기 시작한 뒤 확인한 일들이다.\n\n":"")+string.Join("\n\n",lines);
            Diaries.Add(new DiaryEntry{day=date,body=body}); LastDiaryDay=date;
        }
        public void ReadDiary(int date) { var entry=Diaries.Find(x=>x.day==date); if(entry!=null) entry.read=true; }
        public void PrepareMorning(SurvivalDefinitions data)
        {
            if(!Story.HasRadio || IsGameOver || day<=MorningLastDay) return;
            bool first=!Story.HasHeardSPBroadcast && !MorningIssues.Exists(x=>x.kind=="first");
            string body;
            if(first) body=data.radioStory.firstBroadcast.title+"\n\n"+data.radioStory.firstBroadcast.body+"\n\n[정보 카드]\n"+data.radioStory.clueCard;
            else
            {
                GenerateDailyErasureNews(data);
                var report=ErasureHistory.Find(x=>x.day==day);
                body="[SP 아침 수신 · 08:00]\n출처: SP 기지국 중계 / 제보의 현장 확인은 진행 중\n\n"+data.lifeStory.news[(day-1)%data.lifeStory.news.Length]+"\n\n"+(report==null?"오늘 새로 접수된 지역 소식은 없다.":ErasureBody(report,data));
            }
            MorningIssues.Add(new MorningIssue{day=day,kind=first?"first":"regular",body=body}); MorningLastDay=day;
        }
        public bool ReadMorning(int date,SurvivalDefinitions data)
        {
            if(!CanAct || !CanUseRadio) return false;
            var issue=MorningIssues.Find(x=>x.day==date); if(issue==null) return false;
            if(issue.kind=="first" && !Story.HasHeardSPBroadcast) ListenRadio(data);
            issue.read=true;
            foreach(var report in ErasureHistory)
            {
                if(report.day<=ErasureReadDay) continue;
                var matching=MorningIssues.Find(x=>x.day==report.day);
                if(matching==null || !matching.read) break;
                ErasureReadDay=report.day;
            }
            return true;
        }
        public void DismissMorning() { MorningDeferredDay=MorningLastDay; }
        public void UpgradeDailyLife(SurvivalDefinitions data)
        {
            DailyFacts=new List<DailyFact>(); Diaries=new List<DiaryEntry>(); MorningIssues=new List<MorningIssue>();
            DiaryStartDay=day; PartialDiaryDay=day; LastDiaryDay=day-1; MorningLastDay=day; MorningDeferredDay=0;
            foreach(var report in ErasureHistory) if(report.day>ErasureReadDay) MorningIssues.Add(new MorningIssue{day=report.day,kind="legacy",body="[기존 수신 기록]\n\n"+ErasureBody(report,data)});
        }
    }
}
