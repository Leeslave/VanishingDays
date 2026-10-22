using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace VanishingDays.Editor
{
    public static class OpeningFlowChecks
    {
#if UNITY_EDITOR
        [MenuItem("Vanishing Days/Validate opening flow")]
        public static void Validate()
        {
            int count=ValidateData(OpeningScriptData.Load());
            Debug.Log("VD_OPENING_CHECKS_OK: "+count+" assertions.");
        }
#endif
        public static int ValidateData(OpeningScriptData data)
        {
            int checks=0;
            Action<bool,string> require=(condition,message)=> { checks++; if(!condition) throw new Exception(message); };
            require(data.screens.Length==13,"Expected thirteen opening screens.");
            for(int i=0;i<13;i++) require(data.screens[i].id=="OPEN_"+i.ToString("00"),"Screen order changed.");
            require(data.screens[6].title=="2030년 5월 12일" && data.screens[7].title=="2030년 5월 13일" && data.screens[8].title=="2030년 6월 5일" && data.screens[9].title=="2030년 12월 26일","Archive dates changed.");
            require(data.screens[9].body.EndsWith("나 드디어 100만 유튜버가 될 수 있을지도 모른다."),"Last archive sentence changed.");
            require(data.screens[10].body=="— 이석현의 기록 중 마지막.\n\n이 이후로 이석현도 흔적도 없이 사라졌다.","Disappearance notice changed.");
            require(data.screens[11].kind=="Nickname" && data.screens[12].date=="2031년 2월 26일","Nickname/date transition changed.");
            require(data.screens[12].body=="대한민국을 떠들썩하게 했던 사건이 지나고 2개월이 지났다.\n\n그 이후, 국회의사당도, 청와대도, 방송국들도, 전부 사라졌다.\n\n나는 오늘도 살아남아야 한다.","Player record changed.");
            require((new DateTime(2030,12,26)).AddMonths(2)==new DateTime(2031,2,26),"Date interval is not two months.");
            string allText=string.Join("\n",Array.ConvertAll(data.screens,s=>s.title+"\n"+s.body));
            foreach(string forbidden in new[]{"LRU","시뮬레이션","AI","실험 목적","롯데월드타워","인구 밀도"}) require(!allText.Contains(forbidden),"Unapproved lore or landmark name: "+forbidden);
            foreach(bool crowd in new[]{false,true}) foreach(bool panic in new[]{false,true})
            {
                var flow=new OpeningFlowController(data);
                int calls=0; string received=null;
                flow.OnOpeningCompleted+=name=> { calls++; received=name; flow.Next(); };
                require(flow.CurrentScreenId=="OPEN_00","Opening must start at OPEN_00.");
                flow.Next(); require(flow.CurrentScreenId=="OPEN_01","Start transition failed.");
                flow.Next(); require(flow.CurrentScreenId=="OPEN_01","Question bypass was allowed.");
                flow.Answer(crowd); require(flow.CurrentScreenId=="OPEN_02","Crowd answer did not merge.");
                require(flow.Body==(crowd?data.screens[2].yesLead:data.screens[2].noLead)+"\n\n"+data.screens[2].body,"Crowd definition differs.");
                flow.Next(); flow.Answer(panic);
                require(flow.CurrentScreenId=="OPEN_04","Panic answer did not merge.");
                require(flow.Body==(panic?data.screens[4].yesLead:data.screens[4].noLead)+"\n\n"+data.screens[4].body,"Panic definition differs.");
                for(int screen=5;screen<=11;screen++) { flow.Next(); require(flow.CurrentScreenId=="OPEN_"+screen.ToString("00"),"Archive transition failed."); }
                flow.Next(); require(flow.CurrentScreenId=="OPEN_11","Nickname bypass was allowed.");
                foreach(string invalid in new[]{"", "  \t\n", "한글 이름", "<b>이름</b>", "닉네임!", "ABCDEFGHIJKLM", "🙂"}) require(!flow.SubmitNickname(invalid),"Invalid nickname accepted.");
                require(flow.SubmitNickname("  기록자Ab12  "),"Valid Korean/English/digit nickname rejected.");
                require(flow.PlayerNickname=="기록자Ab12" && flow.Author=="작성자 : 기록자Ab12","Nickname was not trimmed/bound.");
                require(data.screens[6].author=="작성자 : 이석현","Original author was modified.");
                require(calls==0 && !flow.IsCompleted,"Main game started before final click.");
                flow.Next(); flow.Next(); flow.Answer(true); flow.SubmitNickname("다른이름");
                require(calls==1 && flow.IsCompleted && received=="기록자Ab12","Completion callback must fire exactly once.");
            }
            require(OpeningFlowController.IsNicknameValid("가"),"One-character nickname rejected.");
            require(OpeningFlowController.IsNicknameValid("가나다라마바사아자차카타"),"Twelve-character nickname rejected.");
            require(!OpeningFlowController.IsNicknameValid("가나다라마바사아자차카타파"),"Thirteen-character nickname accepted.");
            return checks;
        }
    }
}
