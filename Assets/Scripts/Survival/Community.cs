using System;
using System.Collections.Generic;

namespace VanishingDays
{
    [Serializable] public class CommunityRequest
    {
        public string id, location, item, text, reward;
        public int day, amount, rewardAmount;
    }

    public partial class SurvivalState
    {
        static readonly (string id,string location,string item,int amount,string text,string reward,int rewardAmount)[] Deliveries=
        {
            ("medicine","shelter","antiseptic",1,"영숙 · 배식 준비 중 베인 사람이 있어요. 소독약 한 병을 부탁해요.","water",3),
            ("cord","shelter","cord",3,"지우 · 귀환 표식을 다시 매야 해요. 끈 세 개를 가져와 주세요.","tuna",2),
            ("cloth","school","cloth",2,"동림 · 학교 뒤편의 안전한 통로를 천으로 표시하려고 한다.","bandage",1),
            ("repair","hardware","scrap",2,"철물점 교대 · 빗장 수리에 고철 두 개가 필요합니다.","ramen",2),
            ("warm_meal","shelter","rice_meal",1,"영숙 · 늦게 돌아온 사람 몫으로 따뜻한 밥 한 그릇을 부탁해요.","water",2),
            ("first_aid","pharmacy","bandage",1,"약국 연락조 · 오늘 저녁 교대에 쓸 붕대 한 장을 구해 주세요.","tuna",2),
        };
        static readonly (string id,string location,string text,string result)[] Surveys=
        {
            ("report","hardware","동림 · 철물점 뒷길이 막혔는지 한 번 확인해 주세요.","철물점 뒤편 통로가 열려 있었다. 교대표에 표시했다."),
            ("report_school","school","지우 · 학교 게시판의 귀환 표식이 남았는지 봐 주세요.","학교 게시판의 귀환 표식을 확인했다. 아직 읽을 수 있었다."),
            ("report_pharmacy","pharmacy","영숙 · 약국 옆 계단을 들것으로 지나갈 수 있는지 확인해 주세요.","약국 옆 계단의 폭을 재고 연락조에 우회로를 남겼다."),
        };
        public List<CommunityRequest> CommunityRequests=new List<CommunityRequest>();
        public int CommunityRequestDay;
        public string CommunityFollowup="";
        public void UpgradeCommunityProgress()
        {
            if(CommunityRequests==null) CommunityRequests=new List<CommunityRequest>();
            foreach(var request in CommunityRequests)
            {
                if((request?.id=="report" || request?.id=="soup_check") && !LateFlags.Contains("survey_issued:"+request.id)) LateFlags.Add("survey_issued:"+request.id);
            }
            if(Relay==null) Relay=new RelayProgress();
            if(Relay.nodes==null) Relay.nodes=new List<RelayNode>();
            if(Relay.plan==null) Relay.plan=new List<RelayDecision>();
            if(Relay.shelterStorage==null) Relay.shelterStorage=new Inventory();
        }
        public bool CanReceiveCommunityRequests(SurvivalDefinitions data) => CurrentAct(data)==2 && LateStoryStarted && !HasEnded;
        public void PrepareCommunityRequests(SurvivalDefinitions data)
        {
            if(!CanReceiveCommunityRequests(data) || CommunityRequestDay==day) return;
            var expired=CommunityRequests.Find(x=>x.day<day-1);
            CommunityFollowup=expired==null?"":expired.id=="soup_check"?"영숙이 말했다. “계단 쪽 자리는 비워 뒀어요. 다른 연락조에도 알아봐 달라고 했고요.”":"지우가 말했다. “어제 부탁은 다음 교대에 넘겼어요. 오늘 들어온 것부터 봐요.”";
            CommunityRequests.RemoveAll(x=>x.day<day-1);
            CommunityRequestDay=day;
            if(IsLocationUnlocked("soup_house",data) && !LateFlags.Contains("soup_check") && !LateFlags.Contains("survey_issued:soup_check") &&
                AddRequest("soup_check","soup_house","영숙 · 배식에 오지 않은 사람의 거처를 확인해 주세요.",reward:"water",rewardAmount:2))
                LateFlags.Add("survey_issued:soup_check");
            for(int i=0;i<Deliveries.Length;i++)
            {
                var offer=Deliveries[(day+i)%Deliveries.Length];
                if(IsLocationUnlocked(offer.location,data) && AddRequest(offer.id,offer.location,offer.text,offer.item,offer.amount,offer.reward,offer.rewardAmount)) break;
            }
            if(CommunityRequests.FindAll(x=>x.day==day).Count<2 && !CommunityRequests.Exists(x=>x.id.StartsWith("report",StringComparison.Ordinal)))
            {
                foreach(var survey in Surveys)
                {
                    if(!IsLocationUnlocked(survey.location,data) || LateFlags.Contains("survey_issued:"+survey.id) ||
                        LateFlags.Exists(flag=>flag.StartsWith("request:",StringComparison.Ordinal) && flag.EndsWith(":"+survey.id,StringComparison.Ordinal))) continue;
                    if(AddRequest(survey.id,survey.location,survey.text,reward:"water",rewardAmount:2))
                    {
                        LateFlags.Add("survey_issued:"+survey.id);
                        break;
                    }
                }
            }
        }
        bool AddRequest(string id,string location,string text,string item="",int amount=0,string reward="",int rewardAmount=0)
        {
            if(CommunityRequests.Exists(x=>x.id==id) || LateFlags.Contains("request:"+day+":"+id)) return false;
            CommunityRequests.Add(new CommunityRequest{id=id,location=location,item=item,text=text,amount=amount,reward=reward,rewardAmount=rewardAmount,day=day});
            return true;
        }
        public (string item,int amount) CommunityReward(CommunityRequest request)
        {
            if(!string.IsNullOrEmpty(request.reward) && request.rewardAmount>0) return (request.reward,request.rewardAmount);
            var delivery=Array.Find(Deliveries,offer=>offer.id==request.id);
            return delivery.reward!=null?(delivery.reward,delivery.rewardAmount):("water",2);
        }
        public bool CanCompleteCommunityRequest(CommunityRequest request,SurvivalDefinitions data)
        {
            return CanAct && CanReceiveCommunityRequests(data) && request!=null && CommunityRequests.Contains(request) &&
                request.day>=day-1 && CurrentLocation==request.location && !IsNight(data.TEMP_BALANCE) &&
                (request.amount==0 || Backpack.Count(request.item)>=request.amount);
        }
        public string CompleteCommunityRequest(CommunityRequest request,SurvivalDefinitions data)
        {
            if(!CanCompleteCommunityRequest(request,data)) return "지금은 요청을 마칠 수 없다.";
            if(request.amount>0) Backpack.Remove(request.item,request.amount);
            LateFlags.Add("request:"+day+":"+request.id);
            CommunityRequests.Remove(request);
            LateTrust++;
            var reward=CommunityReward(request);
            Backpack.Add(reward.item,reward.amount);
            var survey=Array.Find(Surveys,x=>x.id==request.id);
            string outcome=request.id=="soup_check"?"적혀 있던 번지에 집이 없다. 영숙에게 빈 터를 확인한 시각을 남겼다.":
                survey.result??(data.Location(request.location).name+" 교대에 부탁받은 물품을 건넸다.");
            outcome+="\n보상 · "+data.Item(reward.item).name+" "+data.Quantity(reward.item,reward.amount)+" · 신뢰 +1";
            if(request.id=="soup_check")
            {
                LateFlags.Add("soup_check");
                EraseLocation(request.location,data);
            }
            RecordFact("story",outcome);
            return Report(outcome);
        }
        public bool CanGuideToShelter(SurvivalDefinitions data) => !IsGameOver && PendingEncounter?.kind=="hungry" &&
            PendingEncounter.context=="길거리" && CanReceiveCommunityRequests(data) &&
            (CurrentLocation=="shelter" || CurrentLocation=="hardware");
        public string GuideToShelter(SurvivalDefinitions data)
        {
            if(!CanGuideToShelter(data)) return "지금 안내할 수 있는 배식소가 없다.";
            string before=PendingEncounter.report;
            PendingEncounter=null;
            string text="주민회관의 배식 시간과 붐비는 길을 피하는 방법을 전했다. 상대는 표식을 확인한 뒤 길을 비켜 주었다.";
            RecordFact("story",text);
            return EncounterOutcome(before,Report(text),data);
        }

        public string TradeAtShelter(SurvivalDefinitions data)
        {
            if(!CanAct || CurrentLocation!="shelter" || !CanReceiveCommunityRequests(data) || IsNight(data.TEMP_BALANCE) || LateFlags.Contains("shelter_trade:"+day) || !Backpack.Remove("tuna",2)) return "낮에 참치캔 두 개를 가져오면 하루 한 번 교환할 수 있다.";
            Backpack.Add("bandage",1);
            LateFlags.Add("shelter_trade:"+day);
            RecordFact("story","주민회관에서 참치캔 두 개를 붕대 한 장으로 바꿨다.");
            return Report("참치캔 두 개를 붕대 한 장으로 바꿨다.");
        }

        public bool IsLocationErased(string id) => Locations?.Find(x=>x.id==id)?.erasedDay>0;
        void EraseLocation(string id,SurvivalDefinitions data)
        {
            var place=Locations.Find(x=>x.id==id);
            if(place==null || place.erasedDay>0 || id=="home") return;
            if(id==CurrentLocation)
            {
                string deferred="erase_after_leaving:"+id;
                if(!LateFlags.Contains(deferred)) LateFlags.Add(deferred);
                return;
            }
            place.erasedDay=day;
            place.remaining=new Inventory();
            RecordFact("story",data.Location(id).name+"이 사라졌다. 지도에 빈 자리와 확인 날짜를 남겼다.");
        }
        void FinishDeferredErasure(string previous,SurvivalDefinitions data)
        {
            if(LateFlags.Remove("erase_after_leaving:"+previous)) EraseLocation(previous,data);
        }
    }
}
