namespace VanishingDays
{
    public partial class SurvivalState
    {
        public int OnboardingVersion, TutorialStep, TutorialViewed;
        public bool TutorialActive, NeighborIntroductionSeen;
        public bool NeedsNeighborIntroduction => NeighborStage==0 && !NeighborIntroductionSeen;
        public void UpgradeOnboarding()
        {
            if(OnboardingVersion!=0) return;
            OnboardingVersion=1; TutorialStep=0; TutorialViewed=0; TutorialActive=false;
            NeighborIntroductionSeen=NeighborStage>0;
        }
        public void BeginTutorial()
        {
            if(!CanAct || HasLifeNotice) return;
            TutorialActive=true; TutorialStep=0; TutorialViewed=0;
        }
        public void SkipTutorial() { TutorialActive=false; }
        public bool ViewTutorialTopic(int step)
        {
            if(!TutorialActive || !CanAct || HasLifeNotice || step<2 || step>5 || (TutorialViewed&(1<<step))!=0) return false;
            TutorialViewed|=1<<step; return true;
        }
        public bool CanAdvanceTutorial => TutorialActive && CanAct && !HasLifeNotice && (TutorialStep==0 || TutorialStep==1 && NeighborStage>0 || TutorialStep>=2 && TutorialStep<=5 && (TutorialViewed&(1<<TutorialStep))!=0);
        public bool AdvanceTutorial()
        {
            if(!CanAdvanceTutorial) return false;
            TutorialStep++; if(TutorialStep==6) TutorialActive=false;
            return true;
        }
    }
}
