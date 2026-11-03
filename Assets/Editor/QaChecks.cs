using System;
using UnityEditor;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class QaChecks
    {
        [MenuItem("Vanishing Days/Validate QA fixes")]
        public static void Validate()
        {
            PrototypeBuilder.Validate(); OpeningFlowChecks.Validate();
            var data=SurvivalDefinitions.Load();int checks=0;
            foreach(var difficulty in new[]{GameDifficulty.EASY,GameDifficulty.NORMAL,GameDifficulty.HARD})
            foreach(int start in new[]{0,5,8,16,20,23})
            foreach(int hours in new[]{2,6,8})
            {
                var state=SurvivalState.NewGame(difficulty,data,123);state.hour=start;
                int before=state.day*24+state.hour;
                state.Rest(data,false,hours);
                if(state.day*24+state.hour-before!=hours) throw new Exception("Sleep elapsed time differs from selected hours.");
                checks++;
            }
            var explicitSleep=SurvivalState.NewGame(GameDifficulty.EASY,data,123);
            explicitSleep.Rest(data,true,8);
            if(explicitSleep.hour!=16) throw new Exception("Explicit sleep duration must override period skipping.");
            Debug.Log("VD_QA_CHECKS_OK: "+checks+" sleep time cases plus explicit duration and existing game rules.");
        }
    }
}
