using System;

namespace VanishingDays
{
    public enum CrowdStoryStep { None, Observing, Panic, Rescue, Disappearance, Introduction, Completed }
    [Serializable] public sealed class CrowdStoryProgress
    {
        public CrowdStoryStep Step;
        public string BagChoice="", Consequence="";
    }
    public partial class SurvivalState
    {
        public CrowdStoryProgress Crowd=new CrowdStoryProgress();
        public string LastRadioBroadcastId="";
        public bool HasActiveCrowdIncident => Crowd!=null && Crowd.Step>=CrowdStoryStep.Panic && Crowd.Step<CrowdStoryStep.Completed;
        // Story dialogue names the protagonist with the player's nickname. "(이)" after the
        // placeholder follows the final consonant: 재현 → 재현이라고, 민수 → 민수라고.
        public string Script(string text)
        {
            string name=string.IsNullOrEmpty(PlayerNickname)?ProtagonistScriptName:PlayerNickname;
            char last=name.Length>0?name[name.Length-1]:' ';
            bool batchim=last>='가' && last<='힣' && (last-'가')%28!=0;
            return (text??"").Replace("{PlayerNickname}(이)",name+(batchim?"이":"")).Replace("{PlayerNickname}",name);
        }
        public bool IsLocationUnlocked(string id,SurvivalDefinitions data)
        {
            var location=data.Location(id);
            return location!=null && (!location.requiresSupplyRumor || Story.SupplyRumorUnlocked);
        }
        public bool CanApproachCrowd(SurvivalDefinitions data) => CanAct && CurrentLocation=="supply_site" && Story.SupplySiteVisited && Story.SupplyRumorUnlocked && Story.HasHeardSPBroadcast && !IsNight(data.TEMP_BALANCE) && !Story.CrowdIncidentResolved && Story.CanTriggerDongrimFirstEncounter(true);
        public string ObserveSupplySite(SurvivalDefinitions data)
        {
            if(!CanApproachCrowd(data)) return "지금은 배분 구역에 접근할 수 없다.";
            Crowd.Step=CrowdStoryStep.Observing;
            return Report(Script(data.radioStory.observe));
        }
        public string ApproachCrowd(SurvivalDefinitions data)
        {
            if(!CanApproachCrowd(data)) return "지금은 배분 구역에 접근할 수 없다.";
            // Dialogue consumes no hours: travel decides day/night before entry.
            // The guaranteed rescue cannot kill a starving player through forced time ticks.
            Story.CrowdIncidentStarted=true; Crowd.Step=CrowdStoryStep.Panic;
            return Report(Script(data.radioStory.panic));
        }
        public string ChooseCrowdResponse(bool dropBag,SurvivalDefinitions data)
        {
            if(IsGameOver || PendingEncounter!=null || Crowd.Step!=CrowdStoryStep.Panic) return "지금은 그 선택을 할 수 없다.";
            var balance=data.radioStory.TEMP_BALANCE;
            int loss=dropBag?balance.dropBagLoss:balance.keepBagLoss;
            int damage=Math.Max(0,(dropBag?balance.dropBagDamage:balance.keepBagDamage)-Defense(data));
            damage=Math.Min(damage,Math.Max(0,Vitals.CurrentHP-1));
            if((dropBag?balance.dropBagDamage:balance.keepBagDamage)>0)
            {
                InjuryRestProgress=0; Vitals.Damage(damage);
                Vitals.Injuries.Add(new Injury { kind="군중 속에서 입은 부상",recoveryPenalty=data.TEMP_BALANCE.injuryCapPenalty });
            }
            Crowd.BagChoice=dropBag?"drop":"hold";
            Crowd.Consequence=(dropBag?"가방을 놓고 몸을 틀었다. ":"가방을 붙잡고 버티다 부상을 입었다. ")+"체력 손실 "+damage+". "+LoseSupplies(Backpack,loss,data);
            Crowd.Step=CrowdStoryStep.Rescue;
            return Report(Crowd.Consequence);
        }
        public string ContinueCrowdStory(SurvivalDefinitions data)
        {
            if(IsGameOver || PendingEncounter!=null || !HasActiveCrowdIncident) return "현재 이어갈 사건이 없다.";
            switch(Crowd.Step)
            {
                case CrowdStoryStep.Rescue: Crowd.Step=CrowdStoryStep.Disappearance; return Report(Script(data.radioStory.disappearance));
                case CrowdStoryStep.Disappearance: Crowd.Step=CrowdStoryStep.Introduction; return Report(Script(data.radioStory.introduction));
                case CrowdStoryStep.Introduction:
                    bool condition=CurrentLocation=="supply_site" && Story.SupplyRumorUnlocked && Story.HasHeardSPBroadcast && Story.CrowdIncidentStarted;
                    if(!Story.CompleteDongrimRescue(condition)) return "첫 만남의 진행 조건을 확인할 수 없다.";
                    Story.CrowdIncidentResolved=true; Story.DongrimContactHintReceived=true;
                    Crowd.Step=CrowdStoryStep.Completed;
                    return Report(Script(data.radioStory.completed));
                default:return "먼저 어떻게 버틸지 선택해야 한다.";
            }
        }
    }
}
