using System;
using System.Collections.Generic;
using System.Linq;

namespace VanishingDays
{
    // TEMP_BALANCE: relay capacity, daily decay and ending thresholds are provisional.
    [Serializable] public class RelayNode
    {
        public string id, staff="";
        public int supplies=2, signal=2, occupancy=1;
        public bool filter, antenna, shelf;
    }

    [Serializable] public class RelayDecision
    {
        public string node, action, option, source;
    }

    [Serializable] public class RelayProgress
    {
        public List<RelayNode> nodes=new List<RelayNode>();
        public List<RelayDecision> plan=new List<RelayDecision>();
        public Inventory shelterStorage=new Inventory();
        public int startDay, cycleDay, submittedDay, broadcasts, relayDays, supplyDays, checks, badDawns, handovers;
        public bool hiddenRequest, pressureResolved, routeLost, collapseDue;
    }

    public partial class SurvivalState
    {
        public RelayProgress Relay=new RelayProgress();
        public string BroadcastPolicy => LateFlags.Contains("policy_distributed")?"distributed":LateFlags.Contains("policy_gather")?"gather":LateFlags.Contains("policy_silent")?"silent":"";
        public bool RelayActive => Relay.startDay>0 && !HasEnded;
        public bool RelayReviewReady => RelayActive && (Relay.broadcasts>=2 || Relay.cycleDay>=Relay.startDay+3);
        public int RelayDecisionLimit => BroadcastPolicy=="distributed"?2:BroadcastPolicy=="gather"?1:0;
        public int RelayNetwork => Relay.relayDays;
        public int SupplyNetwork => Relay.supplyDays;
        public int NeighborCheck => (LateFlags.Contains("neighbor_visited") || LateFlags.Contains("neighbor_delegated")?1:0)+Relay.checks;
        public string JiuControlState => BrokenPath.DonglimAlive?(Relay.handovers>0?"Autonomous":"Dependent"):Relay.handovers>=2?"Recovering":Relay.hiddenRequest?"Concealing":"Grieving";
        public bool CanReachTrueEnding => BroadcastPolicy=="distributed" && LateFlags.Contains("jiwoo_shared");
        public static readonly string[] RelayFacilityItems={"relay_filter","relay_antenna","relay_shelf"};
        public static readonly string[] RelayGoods={"food","water","medicine"};
        static readonly string[][] DispatchItems={new[]{"spam","tuna","ramen","chocolate","energy_bar","rice_meal","spam_meal","tuna_meal","ramen_meal"},new[]{"water"},new[]{"bandage","pills","antiseptic"}};
        public static readonly string[] RelayLocations={"home","shelter","pharmacy","relay_room","rescue_post"};
        public RelayNode RelayNodeAt(string id) => Relay.nodes.Find(x=>x.id==id);
        public string RelayStaffName(string id) => id switch
        {
            "jiwoo"=>"한지우", "donglim"=>"최동림", "seokhyeon"=>"이석현",
            "yeongsuk"=>"영숙", "survivor"=>"구조 연락조", "helper"=>"지원 인력", _=>"없음"
        };
        public IEnumerable<string> RelayStaff()
        {
            yield return "jiwoo";
            if(BrokenPath.DonglimAlive) yield return "donglim";
            if(BrokenPath.SeokhyeonCoop) yield return "seokhyeon";
            if(BrokenPath.YeongsukState=="Rescued") yield return "yeongsuk";
            yield return "survivor";
            if(BrokenPath.Helpers>0) yield return "helper";
        }
        void BeginRelayNetwork(SurvivalDefinitions data)
        {
            if(Relay.startDay>0) return;
            UnlockLateStory("rescue_post",data);
            Relay.startDay=day;
            Relay.cycleDay=day;
            Relay.handovers=LateFlags.Contains("jiwoo_shared")?1:0;
            Relay.nodes.Add(new RelayNode{id="home",signal=DoorLevel>0?3:2});
            Relay.nodes.Add(new RelayNode{id="shelter",staff=BrokenPath.YeongsukState=="Rescued"?"yeongsuk":"",supplies=BrokenPath.YeongsukState=="Rescued"?3:2});
            Relay.nodes.Add(new RelayNode{id="pharmacy"});
            Relay.nodes.Add(new RelayNode{id="relay_room",staff=BrokenPath.SeokhyeonCoop?"seokhyeon":"jiwoo"});
            Relay.nodes.Add(new RelayNode{id="rescue_post",staff=BrokenPath.DonglimAlive?"donglim":"survivor",occupancy=BrokenPath.RescuedCount>=5?2:1});
            Relay.shelterStorage.Add("water",3);
            Relay.shelterStorage.Add("tuna",3);
        }
        public bool CanOperateRelay => CanAct && RelayActive && hour>=8 && Relay.cycleDay==day && Relay.submittedDay!=day &&
            (CanUseRadio || CurrentLocation=="sp_station" || CurrentLocation=="relay_room" || CurrentLocation=="shelter");
        public bool CanSeeRelayPressure => RelayActive && Relay.cycleDay>=Relay.startDay+2 && !Relay.pressureResolved;
        public bool RelayHiddenAt(string id) => Relay.hiddenRequest && RelayNodeAt(id)?.staff=="jiwoo";
        public string RelayRoute(string a,string b)
        {
            if(a==b) return "현장";
            if(IsLocationErased(a) || IsLocationErased(b)) return "끊김";
            if((a=="rescue_post" && b=="home") || (b=="rescue_post" && a=="home"))
                return Relay.routeLost?"끊김":"위험";
            return "열림";
        }
        public string RelayNodeStatus(RelayNode node)
        {
            if(IsLocationErased(node.id)) return "소멸 · 연락 두절";
            return "수용 "+(node.occupancy>=3?"가득":node.occupancy>=2?"빠듯":"여유")+
                " · 물자 "+(node.supplies>=2?"넉넉":node.supplies==1?"부족":"바닥")+
                " · 교신 "+(RelayHiddenAt(node.id) || node.signal>=2?"정상":node.signal==1?"불안정":"두절")+
                " · "+RelayStaffName(node.staff);
        }
        static string GoodsName(string goods) => goods=="food"?"식량":goods=="water"?"물":"약품";
        public string RelayDecisionText(RelayDecision action)
        {
            return action.action switch
            {
                "announce"=>"안내 방송",
                "staff"=>"담당 · "+RelayStaffName(action.option),
                "send"=>(action.source=="home"?"집":"주민회관")+"에서 "+GoodsName(action.option)+" 배분",
                "check"=>"안부 확인 · "+RelayStaffName(action.option),
                _=>""
            };
        }
        bool CanFieldCheck(string staff) => RelayStaff().Contains(staff) && !(staff=="donglim" && BrokenPath.Outcome=="B_Heavy");
        static Inventory RelayInventoryCopy(Inventory original)
        {
            var copy=new Inventory();
            foreach(var item in original.stacks) copy.Add(item.id,item.amount);
            return copy;
        }
        static bool SpendRelayGoods(Inventory inventory,string goods)
        {
            int index=Array.IndexOf(RelayGoods,goods);
            if(index<0) return false;
            var ids=DispatchItems[index];
            int left=goods=="medicine"?2:3;
            if(ids.Sum(inventory.Count)<left) return false;
            foreach(string id in ids)
            {
                int take=Math.Min(left,inventory.Count(id));
                if(take>0) inventory.Remove(id,take);
                left-=take;
                if(left==0) break;
            }
            return true;
        }
        bool RelayPlanValid(IEnumerable<RelayDecision> plan)
        {
            var home=RelayInventoryCopy(HomeStorage);
            var shelter=RelayInventoryCopy(Relay.shelterStorage);
            foreach(var action in plan)
            {
                if(action==null || RelayNodeAt(action.node)==null || IsLocationErased(action.node)) return false;
                switch(action.action)
                {
                    case "announce": break;
                    case "staff": if(!RelayStaff().Contains(action.option)) return false; break;
                    case "check": if(!CanFieldCheck(action.option) || RelayHiddenAt(action.node)) return false; break;
                    case "send":
                        if((action.source!="home" && action.source!="shelter") || action.source==action.node ||
                            RelayRoute(action.source,action.node)=="끊김" || !SpendRelayGoods(action.source=="home"?home:shelter,action.option)) return false;
                        break;
                    default: return false;
                }
            }
            return true;
        }
        public bool CanQueueRelay(RelayDecision action) => action!=null && CanOperateRelay && Relay.plan.Count<RelayDecisionLimit &&
            !Relay.plan.Exists(x=>x.node==action.node && x.action==action.action || x.action=="staff" && action.action=="staff" && x.option==action.option) &&
            RelayPlanValid(Relay.plan.Append(action));
        public string QueueRelay(RelayDecision action)
        {
            if(!CanQueueRelay(action)) return "오늘의 결정, 담당자, 물자와 경로를 확인하자.";
            Relay.plan.Add(action);
            return "송출 전에 결정을 검토하거나 취소할 수 있다.";
        }
        public string CancelRelay(int index)
        {
            if(!CanOperateRelay || index<0 || index>=Relay.plan.Count) return "취소할 결정이 없다.";
            Relay.plan.RemoveAt(index);
            return "결정 하나를 취소했다.";
        }
        public bool CanSubmitRelay => CanOperateRelay && Relay.plan.Count==RelayDecisionLimit && RelayPlanValid(Relay.plan);
        public string SubmitRelay(SurvivalDefinitions data)
        {
            if(!CanSubmitRelay) return "오늘의 결정과 보낼 물자를 확인하자.";
            var results=new List<string>();
            foreach(var action in Relay.plan)
            {
                var node=RelayNodeAt(action.node);
                switch(action.action)
                {
                    case "announce":
                        node.signal=Math.Min(3,node.signal+1);
                        if(node.occupancy>1 && BroadcastPolicy=="distributed") node.occupancy--;
                        break;
                    case "staff":
                        foreach(var previous in Relay.nodes.Where(x=>x.staff==action.option)) previous.staff="";
                        node.staff=action.option;
                        if(action.option=="jiwoo" || !BrokenPath.DonglimAlive && action.option!="jiwoo") Relay.handovers++;
                        if(Relay.handovers>=2) Relay.hiddenRequest=false;
                        else if(!BrokenPath.DonglimAlive && action.option=="jiwoo" && !LateFlags.Contains("jiwoo_request_found")) Relay.hiddenRequest=true;
                        break;
                    case "send":
                        SpendRelayGoods(action.source=="home"?HomeStorage:Relay.shelterStorage,action.option);
                        bool lost=RelayRoute(action.source,node.id)=="위험" && day%3==0;
                        if(!lost) RaiseRelaySupplies(node);
                        else results.Add(data.Location(node.id).name+"으로 가는 둑길에서 물자 일부를 잃어 새 배분 몫을 채우지 못했다.");
                        break;
                    case "check":
                        node.signal=Math.Max(2,node.signal);
                        Relay.checks++;
                        break;
                }
                results.Add(data.Location(node.id).name+" · "+RelayDecisionText(action));
            }
            Relay.plan.Clear();
            Relay.submittedDay=day;
            Relay.broadcasts++;
            if(BroadcastPolicy=="gather")
            {
                Lure++;
                var shelter=RelayNodeAt("shelter");
                shelter.occupancy=Math.Min(3,shelter.occupancy+1);
            }
            CountRelayDay();
            string report=BroadcastPolicy=="silent"?"공개 방송 없이 내부 교대표를 닫았다.":"오늘 확인한 내용을 송출했다.";
            if(results.Count>0) report+="\n"+string.Join("\n",results);
            RecordFact("story",report);
            return Report(report);
        }
        void CountRelayDay()
        {
            if(Relay.nodes.Count(x=>!IsLocationErased(x.id) && x.signal>=2)>=3) Relay.relayDays++;
            if(Relay.nodes.Count(x=>!IsLocationErased(x.id) && x.supplies>=1)>=3) Relay.supplyDays++;
        }
        static void RaiseRelaySupplies(RelayNode node) => node.supplies=Math.Min(node.shelf?4:3,node.supplies+1);
        public bool CanVisitRelay(string id,SurvivalDefinitions data) => CanAct && RelayActive && CurrentLocation==id &&
            RelayNodeAt(id)!=null && !IsLocationErased(id) && !IsNight(data.TEMP_BALANCE);
        public string VisitRelay(string id,SurvivalDefinitions data)
        {
            if(!CanVisitRelay(id,data) || LateFlags.Contains("relay_visit:"+day+":"+id)) return "낮에 거점마다 하루 한 번 확인할 수 있다.";
            var node=RelayNodeAt(id);
            LateFlags.Add("relay_visit:"+day+":"+id);
            string text=data.Location(id).name+"의 교신과 경로를 직접 확인했다.";
            if(RelayHiddenAt(id))
            {
                Relay.hiddenRequest=false;
                LateFlags.Add("jiwoo_request_found");
                text="장부 밑에서 구조 요청을 찾았다. 지우는 응답하지 않으면 위험한 길로 갈 사람이 없을 거라고 말했다. 요청을 교대표에 되돌렸다.";
            }
            else if(node.signal==0 && node.staff=="jiwoo" && !BrokenPath.DonglimAlive && Relay.handovers<2)
                text="지우가 위험한 우회로를 확인해 달라고 했다. 장부에는 더 가까운 길이 남아 있었다.";
            Relay.checks++;
            node.signal=Math.Max(2,node.signal);
            RecordFact("story",text);
            return Report(text);
        }
        public bool CanAddressJiuRequest(string id,SurvivalDefinitions data) => CanVisitRelay(id,data) &&
            RelayNodeAt(id).staff=="jiwoo" && LateFlags.Contains("jiwoo_request_found") && !LateFlags.Contains("jiwoo_request_addressed");
        public string AddressJiuRequest(string id,bool share,SurvivalDefinitions data)
        {
            if(!CanAddressJiuRequest(id,data)) return "지금 이 요청을 정리할 수 없다.";
            LateFlags.Add("jiwoo_request_addressed");
            if(share) Relay.handovers++;
            else Lure+=data.lateStory.lureEnding;
            string text=share
                ?"지우와 다른 연락조가 구조 요청의 시각과 퇴로를 함께 확인했다. 장부의 다음 칸에 두 사람의 이름이 적혔다."
                :"지우가 확인하지 않은 둑길을 외부 구조조에게 넘겼다. 안전하다는 안내까지 함께 송출됐다.";
            RecordFact("story",text);
            return Report(text);
        }
        public string DeliverRelay(string id,string goods,SurvivalDefinitions data)
        {
            if(!CanVisitRelay(id,data)) return "낮에 물자를 직접 건네자.";
            var node=RelayNodeAt(id);
            if(node.supplies>=(node.shelf?4:3)) return "이 거점에는 물자가 충분하다.";
            if(!SpendRelayGoods(id=="home"?HomeStorage:Backpack,goods)) return GoodsName(goods)+"이 부족하다.";
            RaiseRelaySupplies(node);
            string text=data.Location(id).name+"에 "+GoodsName(goods)+"을 전달했다.";
            RecordFact("story",text);
            return Report(text);
        }
        public static bool IsRelayDispatchItem(string item) => DispatchItems.Any(group=>group.Contains(item));
        public string DepositShelterGoods(string item,SurvivalDefinitions data)
        {
            if(!CanVisitRelay("shelter",data) || !IsRelayDispatchItem(item) ||
                !Backpack.Transfer(Relay.shelterStorage,item,1)) return "배분 창고에 맡길 물품이 없다.";
            return Report("주민회관 배분 창고에 "+data.Item(item).name+" 1개를 맡겼다.");
        }
        public string InstallRelayFacility(string id,string item,SurvivalDefinitions data)
        {
            if(!CanVisitRelay(id,data) || !RelayFacilityItems.Contains(item)) return "낮에 거점에 설비를 가져가자.";
            var node=RelayNodeAt(id);
            var stock=id=="home"?HomeStorage:Backpack;
            bool installed=item=="relay_filter"?node.filter:item=="relay_antenna"?node.antenna:node.shelf;
            if(installed || !stock.Remove(item,1)) return "이미 설치했거나 소지한 설비가 없다.";
            if(item=="relay_filter") node.filter=true;
            else if(item=="relay_antenna") node.antenna=true;
            else node.shelf=true;
            string text=data.Location(id).name+"에 "+data.Item(item).name+"를 설치했다.";
            RecordFact("story",text);
            return Report(text);
        }
        public string ResolveRelayPressure(bool gather)
        {
            if(!CanAct || !CanSeeRelayPressure || !CanOperateRelay) return "오늘의 제보를 확인할 수 없다.";
            Relay.pressureResolved=true;
            if(gather)
            {
                Lure+=3;
                RelayNodeAt("shelter").occupancy=3;
            }
            string text=gather?"출처를 확인하지 못한 집결 제보를 방송했다.":"확인 시각과 다른 대피 경로를 전하고 출처 미상 제보를 보류했다.";
            RecordFact("story",text);
            return Report(text);
        }
        void TickRelayDawn(SurvivalDefinitions data)
        {
            if(!RelayActive || Relay.cycleDay>=day) return;
            int age=day-Relay.startDay;
            Relay.cycleDay=day;
            Relay.plan.Clear();
            if(age==1) RecordFact("story","주파수가 잠시 들뜨고 다시 가라앉았다. 확인해 보니 거점들은 그대로였다.");
            if(age>=2 && !Relay.routeLost)
            {
                Relay.routeLost=true;
                var node=RelayNodeAt("rescue_post");
                if(node.occupancy>=2) node.supplies=Math.Max(0,node.supplies-1);
                RecordFact("story","성내천 둑길 한 구간이 사라졌다. 집에서 구조 거점으로 가는 직통 배분을 멈췄다.");
            }
            foreach(var node in Relay.nodes)
            {
                if(IsLocationErased(node.id)) continue;
                if(BroadcastPolicy!="silent" && node.id=="home" && DoorLevel>=2) node.signal=Math.Max(2,node.signal);
                if(BroadcastPolicy!="silent" && node.staff!="" && (node.staff!="jiwoo" || BrokenPath.DonglimAlive && Relay.handovers>0))
                    node.signal=Math.Min(3,node.signal+1);
                if(node.staff=="" || BroadcastPolicy=="silent")
                {
                    if(age%(node.antenna?4:2)==0) node.signal=Math.Max(0,node.signal-1);
                    if(age%(node.filter?4:2)==0) node.supplies=Math.Max(0,node.supplies-1);
                }
            }
            if(!BrokenPath.DonglimAlive && age>=2 && Relay.handovers<2 && !LateFlags.Contains("jiwoo_request_found") && Relay.nodes.Exists(x=>x.staff=="jiwoo"))
                Relay.hiddenRequest=true;
            bool failed=Relay.nodes.Count(x=>x.signal>=1 && !IsLocationErased(x.id))<2 &&
                Relay.nodes.Count(x=>x.supplies>=1 && !IsLocationErased(x.id))<2;
            Relay.badDawns=failed?Relay.badDawns+1:0;
            Relay.collapseDue=Relay.badDawns>=2;
        }
    }
}
