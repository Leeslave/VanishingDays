using System;

namespace VanishingDays
{
    public partial class SurvivalState
    {
        public int CollectorCycleHours, CollectorElapsedHours;
        public bool CollectorNoticePending;
        public bool CollectorReady => CollectorCycleHours>0 && CollectorElapsedHours==CollectorCycleHours;
        public int CollectorCapacity => CollectorCycleHours*2;
        public int WaterFilterLevel => HasFacility("water_filter3")?3:HasFacility("water_filter2")?2:HasFacility("water_filter")?1:0;
        public int PurifiedWaterAmount => WaterFilterLevel==3?4:WaterFilterLevel==2?3:2;
        public bool CanStartCollector => CanAct && CurrentLocation=="home" && HasFacility("water_collector") && CollectorCycleHours==0;
        public bool CanCollectWater => CanAct && CurrentLocation=="home" && CollectorReady;
        public string CollectorStatus => !HasFacility("water_collector")?"집수통 미설치 · 제작에서 설치 가능":CollectorCycleHours==0?"집수통 대기 · 2 / 5 / 10시간 자동 집수 선택":CollectorReady?"집수통 가득 참 · 혼탁한 물 "+CollectorCapacity+"병 · 회수 0시간":"자동 집수 중 · "+(CollectorCycleHours-CollectorElapsedHours)+"시간 후 혼탁한 물 "+CollectorCapacity+"병";
        public string StartWaterCollection(int hours)
        {
            if(!CanStartCollector || hours!=2 && hours!=5 && hours!=10) return "집에서 빈 집수통에 2·5·10시간 집수를 설정할 수 있다.";
            CollectorCycleHours=hours; CollectorElapsedHours=0; CollectorNoticePending=false;
            return Report(hours+"시간 자동 집수를 시작했다. 외출·수면·다른 작업 중에도 모이며, 가득 차면 멈추고 알려준다. 설정에 시간은 들지 않는다.");
        }
        void TickWaterCollector()
        {
            if(!HasFacility("water_collector") || CollectorCycleHours==0 || CollectorReady) return;
            CollectorElapsedHours++;
            if(!CollectorReady) return;
            CollectorNoticePending=true;
            Note("[집수 완료] 집수통에 혼탁한 물 "+CollectorCapacity+"병이 가득 찼다. 집에서 시간 소모 없이 회수할 수 있다.");
        }
        public string CollectWater()
        {
            if(!CanCollectWater) return "집수통이 가득 차면 집에서 회수할 수 있다.";
            int amount=CollectorCapacity;
            HomeStorage.Add("raw_water",amount);
            CollectorCycleHours=0; CollectorElapsedHours=0; CollectorNoticePending=false;
            RecordFact("story","자동 집수한 혼탁한 물 "+amount+"병을 회수했다.");
            return Report("혼탁한 물 "+amount+"병을 집에 회수했다. 시간 소모 없음. 다시 집수를 설정할 수 있다.");
        }
        public void UpgradeSurvivalBalance(SurvivalDefinitions data)
        {
            CollectorCycleHours=0; CollectorElapsedHours=0; CollectorNoticePending=false;
            foreach(var place in data.locations)
            {
                var pool=Locations.Find(x=>x.id==place.id)?.remaining;
                if(pool==null) continue;
                foreach(var loot in place.loot)
                    if((loot.id=="chocolate" || loot.id=="soda" || loot.id=="energy_bar") && !pool.stacks.Exists(x=>x.id==loot.id)) pool.Add(loot.id,loot.amount);
            }
        }
        public DateTime HomeArrivalAfterSearch(SurvivalDefinitions data) => CalendarDate.AddHours(hour+data.Location(CurrentLocation).searchHours+data.Location(CurrentLocation).travelHours);
        public string SearchNightWarning(SurvivalDefinitions data)
        {
            if(CurrentLocation=="home" || data.Location(CurrentLocation).storyOnly || !HasSearchWork(CurrentLocation,data)) return "";
            var location=data.Location(CurrentLocation); var b=data.TEMP_BALANCE;
            var now=CalendarDate.AddHours(hour); var finish=now.AddHours(location.searchHours); var arrival=finish.AddHours(location.travelHours);
            bool crossesNight=false;
            for(int i=0;i<=location.searchHours+location.travelHours;i++) { int at=now.AddHours(i).Hour; if(at<b.dayStarts || at>=b.nightStarts) crossesNight=true; }
            if(!crossesNight) return "";
            return (IsNight(b)?"이미 야간이다. ":"수색을 더 하면 귀갓길이 야간에 걸린다. ")+"\n현재 "+now.ToString("MM.dd HH:mm")+" → 수색 완료 "+finish.ToString("MM.dd HH:mm")+" → 예상 귀가 "+arrival.ToString("MM.dd HH:mm")+".\n\n"+b.nightStarts+"시부터 다음날 "+b.dayStarts+"시까지 야간이다. 약탈꾼에게 발견되면 큰 피해를 입을 수 있다.";
        }
    }
}
