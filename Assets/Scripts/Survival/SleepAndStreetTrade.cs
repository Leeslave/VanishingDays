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
        void TickSleep(bool sleeping,SurvivalDefinitions data)
        {
            Sleepiness=Math.Max(0,Math.Min(100,Sleepiness+(sleeping?-SleepinessRecovery:data.Rates(Difficulty).sleepinessPerHour)));
            if(sleeping) { SleepHours=Math.Min(24,SleepHours+1); if(SleepHours>6) Vitals.AdjustMental(SleepMentalRecovery); }
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
            var offer=StreetOffers[index]; Backpack.Remove(offer.give,offer.cost); Backpack.Add(offer.receive,offer.amount); string before=PendingEncounter.report; PendingEncounter=null;
            RecordFact("story","길에서 행상인에게 "+data.Item(offer.give).name+" "+data.Quantity(offer.give,offer.cost)+"을 주고 "+data.Item(offer.receive).name+" "+data.Quantity(offer.receive,offer.amount)+"을 받았다.");
            return EncounterOutcome(before,Report("행상인과 "+data.Item(offer.give).name+" "+data.Quantity(offer.give,offer.cost)+"을 주고 "+data.Item(offer.receive).name+" "+data.Quantity(offer.receive,offer.amount)+"을 받았다."),data);
        }
        public string LeaveStreetTrader(SurvivalDefinitions data)
        {
            if(IsGameOver || PendingEncounter?.kind!="trader") return "현재 거래할 상대가 없다.";
            string before=PendingEncounter.report; PendingEncounter=null; return EncounterOutcome(before,Report("서로의 길을 가기로 했다. 물품은 그대로다."),data);
        }
    }
}
