using System;

namespace VanishingDays
{
    public sealed class StreetTradeOffer
    {
        public string give, receive; public int cost, amount;
        public StreetTradeOffer(string give,int cost,string receive,int amount) { this.give=give; this.cost=cost; this.receive=receive; this.amount=amount; }
    }
    public partial class SurvivalState
    {
        public int Sleepiness, SleepHours;
        public static readonly StreetTradeOffer[] StreetOffers={new StreetTradeOffer("tuna",2,"bandage",1),new StreetTradeOffer("wood",3,"ramen",2),new StreetTradeOffer("spam",2,"tools",1)};
        // Assess 08:00–08:00 so a night crossing midnight is evaluated together.
        void TickSleep(bool sleeping)
        {
            Sleepiness=Math.Max(0,Math.Min(100,Sleepiness+(sleeping?-12:4)));
            if(sleeping) { SleepHours=Math.Min(24,SleepHours+1); if(SleepHours>6) Vitals.AdjustMental(2); }
            if(hour!=8) return;
            int missing=Math.Max(0,6-SleepHours);
            if(missing>0) { Vitals.AdjustMental(-missing*3); Note("[수면 평가] 하루 수면 "+SleepHours+" / 6시간. 부족 "+missing+"시간으로 정신력 -"+(missing*3)+"."); }
            SleepHours=0;
        }
        public bool CanStreetTrade(int index)
        {
            if(IsGameOver || PendingEncounter?.kind!="trader" || index<0 || index>=StreetOffers.Length) return false;
            var offer=StreetOffers[index]; return Backpack.Count(offer.give)>=offer.cost;
        }
        public string StreetTrade(int index,SurvivalDefinitions data)
        {
            if(!CanStreetTrade(index)) return "교환할 소지품이 부족하다.";
            var offer=StreetOffers[index]; Backpack.Remove(offer.give,offer.cost); Backpack.Add(offer.receive,offer.amount); PendingEncounter=null;
            return Report("행상인과 "+data.Item(offer.give).name+" "+data.Quantity(offer.give,offer.cost)+"을 주고 "+data.Item(offer.receive).name+" "+data.Quantity(offer.receive,offer.amount)+"을 받았다.");
        }
        public string LeaveStreetTrader()
        {
            if(IsGameOver || PendingEncounter?.kind!="trader") return "현재 거래할 상대가 없다.";
            PendingEncounter=null; return Report("서로의 길을 가기로 했다. 물품은 그대로다.");
        }
        public string RobHungrySurvivor()
        {
            if(IsGameOver || PendingEncounter?.kind!="hungry") return "현재 대응할 일이 없다.";
            PendingEncounter=null; Backpack.Add("tuna",1); Vitals.MoralHarm(10);
            return Report("굶주린 상대를 위협해 마지막 참치캔 1개를 빼앗았다. 정신력 최대치가 10 낮아졌다. 잠을 자도 이 상한은 회복되지 않는다.");
        }
    }
}
