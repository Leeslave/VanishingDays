using System;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        string selectedRelayNode="shelter";

        void OpenStoryPage(string target)
        {
            page=target;
            message="";
            Render();
        }
        void CommunityAndRelayShortcut()
        {
            if(!state.CanAct || (page!="home" && page!="map" && page!="radio" && page!="journal")) return;
            if(state.RelayActive)
                Button(state.CanOperateRelay?"아침 거점 운영":"거점 연락망",592,422,260,38,()=>OpenStoryPage("relay-map"),true,state.CanOperateRelay);
            else if(state.CanReceiveCommunityRequests(definitions) && (state.CommunityRequests.Count>0 || state.CurrentLocation=="shelter"))
            {
                // Empty request boards stay out of the way (the shelter still needs it for the daily trade);
                // a request that ends today says so on the badge itself.
                int due=state.CommunityRequestsDueToday;
                string text="동네 요청 · "+state.CommunityRequests.Count+(due>0?" · 오늘 마감 "+due:"");
                float w=due>0?380:260;
                Button(text,852-w,422,w,38,()=>OpenStoryPage("requests"),true,state.CommunityRequests.Count>0);
            }
        }
        void CommunityPage()
        {
            Label("동네 요청",48,462,804,56,38,Paper);
            string text=string.IsNullOrEmpty(message)?"낮에 직접 다녀온 뒤 소식이나 물품을 전한다. 지나간 요청은 다음 교대에 넘긴다.":message;
            if(string.IsNullOrEmpty(message) && !string.IsNullOrEmpty(state.CommunityFollowup)) text+="\n\n"+state.CommunityFollowup;
            bool result=!string.IsNullOrEmpty(message);
            float textH=result?300:134, listTop=532+textH+8, listH=1288-listTop;
            Paragraph(text,532,textH,23);
            ScrollArea("Community requests",listTop,listH,out var content);
            float y=8;
            foreach(var request in state.CommunityRequests)
            {
                var place=definitions.Location(request.location);
                Label(request.text,18,y,766,66,24,Paper,content);
                bool dueToday=request.day+1==state.day;
                string details=place.name+" · "+(dueToday?"오늘 마감":"DAY "+(request.day+1)+"까지")+(request.amount>0?" · "+definitions.Item(request.item).name+" "+request.amount+"개 (소지 "+state.Backpack.Count(request.item)+" · 집 "+state.HomeStorage.Count(request.item)+")":"");
                Label(details,18,y+68,766,34,22,dueToday?Amber:Muted,content);
                var reward=state.CommunityReward(request);
                Label("보상 · "+definitions.Item(reward.item).name+" "+definitions.Quantity(reward.item,reward.amount)+" · 신뢰 +1",18,y+105,766,32,22,Amber,content);
                if(state.CurrentLocation==request.location)
                    Button("요청을 마친다",18,y+145,766,60,()=>Apply(state.CompleteCommunityRequest(request,definitions),"requests"),state.CanCompleteCommunityRequest(request,definitions),true,content);
                else
                    Button("장소와 경로 확인",18,y+145,766,60,()=>SelectLocation(request.location),state.IsLocationUnlocked(request.location,definitions),false,content);
                y+=222;
            }
            if(state.CurrentLocation=="shelter")
            {
                bool traded=state.LateFlags.Contains("shelter_trade:"+state.day);
                Button("참치캔 2개로 붕대 1개 교환"+(traded?" · 오늘 교환함":" · 소지 참치캔 "+state.Backpack.Count("tuna")),18,y,766,68,()=>Apply(state.TradeAtShelter(definitions),"requests"),state.CanTradeAtShelter(definitions),false,content);
                y+=82;
            }
            if(y==8) Label("지금 맡을 수 있는 요청이 없다.",18,y,766,64,26,Muted,content);
            content.sizeDelta=new Vector2(804,Mathf.Max(listH,y));
            Button("생활 화면으로",48,1304,804,72,()=>OpenStoryPage("home"));
        }
        void RelayMapPage()
        {
            if(!state.RelayActive) { Home(); return; }
            if(!string.IsNullOrEmpty(message) && message.Length>80)
            {
                Label("오늘의 송출 결과",48,462,804,56,38,Paper);
                Paragraph(message,534,740,24,true);
                PrimaryButton("거점 지도 보기",48,1300,804,76,()=>{message="";Render();});
                return;
            }
            string policy=state.BroadcastPolicy=="distributed"?"분산 안내":state.BroadcastPolicy=="gather"?"단일 집결":"방송 중단";
            Label("SP 거점 연락망",48,462,804,56,38,Paper);
            Label(policy+" · DAY "+state.Relay.cycleDay+" · 오늘의 결정 "+state.Relay.plan.Count+" / "+state.RelayDecisionLimit,48,524,804,40,25,Amber);
            string status=state.CanOperateRelay?"오늘 방송을 아직 송출하지 않았다. 거점 점을 눌러 결정을 고른다.":
                state.Relay.cycleDay!=state.day && state.Relay.plan.Count>0?"정해 둔 결정 "+state.Relay.plan.Count+"개는 아침 08:00 운영 때 다시 확인해 그대로 쓴다.":state.Relay.submittedDay==state.day?"오늘의 운영을 마쳤다. 낮에는 직접 확인하거나 전달할 수 있다.":"라디오나 교신 거점에서 08:00 이후 운영할 수 있다.";
            Paragraph(string.IsNullOrEmpty(message)?status:message,574,76,22);
            var world=Rect("Relay neighborhood",root,48,682,804,390);
            var map=world.gameObject.AddComponent<NeighborhoodMapGraphic>();
            map.raycastTarget=false;
            map.VisibleLocations=state.Relay.nodes.Where(x=>!state.IsLocationErased(x.id)).Select(x=>x.id).Concat(new[]{"sp_station"}).ToArray();
            map.ErasedLocations=state.Locations.Where(x=>x.erasedDay>0).Select(x=>x.id).ToArray();
            var edges=new[]{("home","shelter"),("home","rescue_post"),("shelter","pharmacy"),("shelter","relay_room"),("shelter","rescue_post"),("relay_room","sp_station")};
            map.RelayRoutes=edges.Select(edge=>(edge.Item1,edge.Item2,state.RelayRoute(edge.Item1,edge.Item2))).ToArray();
            map.CurrentLocation=state.CurrentLocation;
            Vector2 station=NeighborhoodMapGraphic.Position("sp_station");
            Label("SP 기지국",Mathf.Clamp(station.x*804-68,0,668),station.y*390+28,136,40,20,Muted,world).alignment=TextAnchor.MiddleCenter;
            Label("선  청록 열림 · 주황 위험 · 끊긴 빨강 두절      이름표  거점 · 상세에서 담당자",48,652,804,26,18,Muted);
            foreach(var node in state.Relay.nodes)
            {
                Vector2 point=NeighborhoodMapGraphic.Position(node.id);
                Action select=()=>{selectedRelayNode=node.id; OpenStoryPage("relay-node");};
                Pressable("거점 점:"+node.id,point.x*804-36,point.y*390-30,72,58,select,true,Color.clear,Color.clear,world);
                Button(RelayMapLabel(node.id),Mathf.Clamp(point.x*804-68,0,668),point.y*390+28,136,40,select,true,node.id==state.CurrentLocation,world);
            }
            if(state.CanSeeRelayPressure && state.CanOperateRelay)
            {
                string source=state.BrokenPath.SeokhyeonCoop?"석현 대조 · 어제 집결 제보와 문장 구조가 같다.":"출처 미상 집결 제보 · 확인되지 않았다.";
                Label(source,48,1080,804,32,21,Amber);
                Button("검증까지 보류",48,1116,390,50,()=>Apply(state.ResolveRelayPressure(false),"relay-map"));
                Button("집결을 방송",462,1116,390,50,()=>Apply(state.ResolveRelayPressure(true),"relay-map"));
            }
            for(int i=0;i<state.Relay.plan.Count;i++)
            {
                int index=i;
                var action=state.Relay.plan[i];
                Button(RelayMapLabel(action.node)+" · "+state.RelayDecisionText(action)+" · 취소",48,1176+i*50,804,44,()=>Apply(state.CancelRelay(index),"relay-map"),state.CanOperateRelay);
            }
            Button("동네 지도",48,1302,390,72,()=>OpenStoryPage("map"));
            Button(state.BroadcastPolicy=="silent"?"교대표 마감":"오늘 방송 송출",462,1302,390,72,()=>Apply(state.SubmitRelay(definitions),"relay-map"),state.CanSubmitRelay,true);
        }
        void RelayNodePage()
        {
            var node=state.RelayNodeAt(selectedRelayNode);
            if(node==null || !state.RelayActive) { RelayMapPage(); return; }
            Label(definitions.Location(node.id).name,48,462,804,56,37,Paper);
            Label(state.RelayNodeStatus(node),48,524,804,40,24,Amber);
            Paragraph(string.IsNullOrEmpty(message)?"경로 · 집 "+state.RelayRoute("home",node.id)+" / 주민회관 "+state.RelayRoute("shelter",node.id):message,574,98,22);
            ScrollArea("Relay node actions",674,612,out var content);
            float y=8;
            void Row(string label,Action action,bool enabled=true)
            {
                Button(label,12,y,780,58,action,enabled,false,content);
                y+=68;
            }
            void Queue(string title,string action,string option="",string source="")
            {
                var pick=new RelayDecision{node=node.id,action=action,option=option,source=source};
                bool booked=state.Relay.plan.Exists(x=>x.node==pick.node && x.action==pick.action && x.option==pick.option && x.source==pick.source);
                bool open=state.CanQueueRelay(pick);
                string why=booked?" · 예약됨":open?"":state.Relay.plan.Count>=state.RelayDecisionLimit?" · 오늘 결정을 모두 골랐다":action=="send"?" · 보낼 물자나 경로가 없다":" · 담당자나 경로 조건이 맞지 않는다";
                Row(title+why,()=>Apply(state.QueueRelay(pick),"relay-node"),open);
            }
            if(state.CurrentLocation!=node.id && state.IsLocationUnlocked(node.id,definitions))
                Row("직접 방문 · 이동 경로",()=>SelectLocation(node.id));
            if(state.CanVisitRelay(node.id,definitions))
            {
                Row("직접 안부 확인",()=>Apply(state.VisitRelay(node.id,definitions),"relay-node"),!state.LateFlags.Contains("relay_visit:"+state.day+":"+node.id));
                if(state.CanAddressJiuRequest(node.id,definitions))
                {
                    Row("숨긴 요청 · 다른 연락조와 확인한다",()=>Apply(state.AddressJiuRequest(node.id,true,definitions),"relay-node"));
                    Row("숨긴 요청 · 위험한 우회로로 안내한다",()=>Apply(state.AddressJiuRequest(node.id,false,definitions),"relay-node"));
                }
                foreach(string goods in SurvivalState.RelayGoods)
                    Row("직접 전달 · "+RelayGoodsLabel(goods),()=>Apply(state.DeliverRelay(node.id,goods,definitions),"relay-node"));
                foreach(string item in SurvivalState.RelayFacilityItems)
                {
                    var stock=node.id=="home"?state.HomeStorage:state.Backpack;
                    bool installed=item=="relay_filter"?node.filter:item=="relay_antenna"?node.antenna:node.shelf;
                    Row(definitions.Item(item).name+(installed?" · 설치 완료":" 설치"),()=>Apply(state.InstallRelayFacility(node.id,item,definitions),"relay-node"),!installed && stock.Count(item)>0);
                }
                if(node.id=="shelter")
                    foreach(var item in state.Backpack.stacks.Where(x=>x.amount>0 && SurvivalState.IsRelayDispatchItem(x.id)))
                        Row(definitions.Item(item.id).name+" 1개를 배분 창고에 맡긴다",()=>Apply(state.DepositShelterGoods(item.id,definitions),"relay-node"));
            }
            if(state.CanOperateRelay && state.RelayDecisionLimit>0)
            {
                Queue("확인한 경로·수용 정보 방송","announce");
                foreach(string staff in state.RelayStaff())
                {
                    Queue("담당자 배치 · "+state.RelayStaffName(staff),"staff",staff);
                    if(node.signal<2 && !state.RelayHiddenAt(node.id))
                        Queue("안부 확인 요청 · "+state.RelayStaffName(staff),"check",staff);
                }
                foreach(string source in new[]{"home","shelter"}.Where(x=>x!=node.id))
                    foreach(string goods in SurvivalState.RelayGoods)
                        Queue((source=="home"?"집":"주민회관")+"에서 "+RelayGoodsLabel(goods)+" 배분","send",goods,source);
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(612,y));
            Button("거점 지도로 돌아가기",48,1304,804,72,()=>OpenStoryPage("relay-map"));
        }
        string RelayMapLabel(string id)
        {
            var place=definitions.Location(id);
            return string.IsNullOrEmpty(place.mapLabel)?place.name:place.mapLabel;
        }
        static string RelayGoodsLabel(string goods) => goods=="food"?"식량 3개":goods=="water"?"물 3병":"약품 2개";
        void RelayArchivePage()
        {
            Hero("EXP-01 최종 관측 기록","진엔딩 · 교대 이후");
            Transcript(Blocks(state.ArchiveText(definitions)),770,414,true,-1,false);
            Button("마지막 기록으로",48,1240,804,76,()=>OpenStoryPage("home"));
        }
    }
}
