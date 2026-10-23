using System;
using System.Collections.Generic;

namespace VanishingDays
{
    public partial class SurvivalState
    {
        public List<string> UnlockedLocations=new List<string>();
        public List<string> VisitedLocations=new List<string>();
        public int DongrimMeetings, LastDongrimMeetingDay;
        public bool PendingDongrimMeeting;
        public int TotalDongrimMeetings => Story.HasMetDongrim?Math.Max(1,DongrimMeetings):0;
        public string DongrimMeetingLocation => TotalDongrimMeetings==1 || (TotalDongrimMeetings>=3 && TotalDongrimMeetings%2==1)?"empty_house":"school";
        public int NextDongrimDay => LastDongrimMeetingDay+(TotalDongrimMeetings<3?1:2);
        public void MarkVisited(string id) { if(!VisitedLocations.Contains(id)) VisitedLocations.Add(id); }
        public void DiscoverLocations(SurvivalDefinitions data)
        {
            foreach(var place in data.locations)
            {
                if(IsLocationUnlocked(place.id,data) || place.requiresSupplyRumor) continue;
                var source=Locations.Find(x=>x.id==place.requiresLocation);
                if((string.IsNullOrEmpty(place.requiresLocation) || source!=null && source.searches>=place.requiresSearches) && TotalDongrimMeetings>=place.requiresDongrimMeetings)
                {
                    UnlockedLocations.Add(place.id);
                    string before=lastReport, notice="[장소 발견] "+place.name+"으로 가는 길을 지도에 기록했다.";
                    Note(notice); lastReport=before+"\n\n"+notice;
                }
            }
        }
        public bool CanInstallSecurity(string id,SurvivalDefinitions data)
        {
            var device=data.Security(id);
            return CanAct && CurrentLocation=="home" && device!=null && !InstalledSecurity.Contains(id) && (string.IsNullOrEmpty(device.requiresSecurity) || InstalledSecurity.Contains(device.requiresSecurity)) && HomeStorage.Has(device.ingredients);
        }
        public int DoorLevel => InstalledSecurity.Contains("door3")?3:InstalledSecurity.Contains("door2")?2:InstalledSecurity.Contains("door1")?1:0;
        public bool HasSearchWork(string id,SurvivalDefinitions data)
        {
            var progress=Locations.Find(x=>x.id==id);
            return progress!=null && (progress.remaining.stacks.Exists(x=>x.amount>0) || id=="school" && !Story.HasRadio);
        }
        public int LootPercent(string id,SurvivalDefinitions data)
        {
            var place=data.Location(id); var progress=Locations.Find(x=>x.id==id);
            if(place==null || progress==null || place.storyOnly || id=="home") return 0;
            double total=0, remaining=0;
            foreach(var stack in place.loot)
            {
                double unit=stack.id=="rice"?100:1;
                total+=stack.amount/unit; remaining+=progress.remaining.Count(stack.id)/unit;
            }
            if(id=="school") { total++; if(!Story.HasRadio) remaining++; }
            if(total<=0) return 100;
            int percent=(int)Math.Floor(Math.Max(0,Math.Min(1,1-remaining/total))*100);
            return HasSearchWork(id,data)?Math.Min(99,percent):100;
        }
        public string LootSummary(string id,SurvivalDefinitions data,bool detailed=false)
        {
            var place=data.Location(id); var progress=Locations.Find(x=>x.id==id);
            if(id=="home") return "문 보강 "+DoorLevel+" / 3단계";
            if(place.storyOnly) return Story.CrowdIncidentResolved?"현장 기록 완료":"제보 확인 · 수색 물자 없음";
            bool visited=VisitedLocations.Contains(id) || progress.searches>0;
            if(!visited) return "미수색 · 잔량 미확인";
            string summary="수색 "+LootPercent(id,data)+"% · "+progress.searches+"회";
            if(!HasSearchWork(id,data)) return summary+" · 물자 소진";
            return summary+" · 잔량 "+(100-LootPercent(id,data))+"%";
        }
        public string WorldHint(SurvivalDefinitions data)
        {
            if(Story.HasMetDongrim) return DongrimHint(data);
            if(!IsLocationUnlocked("empty_house",data)) return "편의점을 수색해 주변 골목의 단서를 찾자.";
            if(!IsLocationUnlocked("school",data)) return "빈집을 수색하면 학교로 통하는 길을 확인할 수 있다.";
            if(!Story.HasRadio) return "고등학교를 수색해 라디오를 찾아보자.";
            return Story.SupplyRumorUnlocked?"제보 장소에 낮에 방문해 현장을 살펴보자.":"집에서 라디오를 켜 주변 소식을 확인하자.";
        }
        public string DongrimHint(SurvivalDefinitions data)
        {
            if(!Story.HasMetDongrim) return "";
            return "동림의 약속 · DAY "+NextDongrimDay+"부터 낮에 "+data.Location(DongrimMeetingLocation).name+"에서 재회\n"+(TotalDongrimMeetings==1?"빈집 뒤편 골목의 흔적을 함께 확인하기로 했다.":TotalDongrimMeetings==2?"고등학교 게시판 앞에서 주변 생존자 소식을 듣기로 했다.":"이틀 간격으로 순찰 중인 동림과 소식·식량을 교환할 수 있다.");
        }
        bool TryMeetDongrim(SurvivalDefinitions data)
        {
            if(!CanAct || !Story.HasMetDongrim || day<NextDongrimDay || IsNight(data.TEMP_BALANCE) || CurrentLocation!=DongrimMeetingLocation) return false;
            PendingDongrimMeeting=true;
            Note("[최동림 재회] "+data.Location(CurrentLocation).name+"에서 익숙한 목소리가 들렸다.");
            return true;
        }
        public string DongrimDialogue(SurvivalDefinitions data)
        {
            var text=data.dongrimFollowup;
            return Script(TotalDongrimMeetings==1?text.secondMeeting:TotalDongrimMeetings==2?text.thirdMeeting:text.patrolMeeting);
        }
        string TradeFood => Backpack.Count("tuna")>0?"tuna":Backpack.Count("spam")>0?"spam":Backpack.Count("ramen")>0?"ramen":null;
        public bool CanTradeWithDongrim => PendingDongrimMeeting && TradeFood!=null;
        public string ResolveDongrimMeeting(bool trade,SurvivalDefinitions data)
        {
            if(IsGameOver || !PendingDongrimMeeting || trade && !CanTradeWithDongrim) return "지금은 그 선택을 할 수 없다.";
            string outcome="물자를 건네지 않고 서로 확인한 길과 위험을 지도에 옮겼다.";
            if(trade) { string food=TradeFood; Backpack.Remove(food,1); Backpack.Add("bandage",1); outcome=data.Item(food).name+" 1개를 건네고 붕대 1개를 받았다."; }
            var script=data.dongrimFollowup;
            string body=TotalDongrimMeetings==1?script.secondCompleted:TotalDongrimMeetings==2?script.thirdCompleted:script.patrolCompleted;
            DongrimMeetings=TotalDongrimMeetings+1; LastDongrimMeetingDay=day; PendingDongrimMeeting=false;
            Report(Script(body)+"\n\n"+outcome);
            DiscoverLocations(data);
            return Report(lastReport+"\n\n"+DongrimHint(data));
        }
    }
}
