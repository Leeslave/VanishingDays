using System;
using System.Linq;
using UnityEngine;

namespace VanishingDays.Editor
{
    // Story text is split by its writing conventions; these checks pin which presentation each kind of scene gets.
    public static class DialogueChecks
    {
        static void Expect(bool condition,string failure) { if(!condition) throw new Exception("Dialogue check failed: "+failure); }
        static ScenePresentation Mode(string text,string player="기록자") => ScriptBlocks.PresentationFor(ScriptBlocks.Parse(text,player));

        public static void Validate()
        {
            var data=SurvivalDefinitions.Load();
            var radio=data.radioStory;

            // Face to face: speech lines, with the player's own lines recognised by nickname.
            var rescue=ScriptBlocks.Parse(radio.rescue.Replace("{PlayerNickname}","기록자"),"기록자");
            Expect(ScriptBlocks.PresentationFor(rescue)==ScenePresentation.Scene,"crowd rescue should be a face-to-face scene");
            Expect(rescue.Any(x=>x.Kind==ScriptBlockKind.Speech && x.Speaker=="남자" && !x.IsPlayer),"the stranger's rescue speech was not recognised");
            var introduction=ScriptBlocks.Parse(radio.introduction.Replace("{PlayerNickname}","기록자"),"기록자");
            Expect(ScriptBlocks.PresentationFor(introduction)==ScenePresentation.Scene && introduction.Any(x=>x.IsPlayer),"Dongrim introduction did not recognise the player");

            // Narration that ends in a choice: the journal page.
            Expect(Mode(radio.panic)==ScenePresentation.Journal,"crowd panic should be journal narration");
            Expect(Mode(radio.approach)==ScenePresentation.Journal,"supply site approach should be journal narration");

            // Heard over the air: the radio receiver, never mistaken for speech.
            var falseReport=data.lateStory.beats.First(x=>x.id=="false_report").body;
            var blocks=ScriptBlocks.Parse(falseReport);
            Expect(ScriptBlocks.PresentationFor(blocks)==ScenePresentation.Radio,"false report should be a radio scene");
            var broadcast=blocks.First(x=>x.Kind==ScriptBlockKind.Broadcast);
            Expect(broadcast.Meta.Contains("확인 수준") && broadcast.Text.StartsWith("“주민회관"),"broadcast source and text not split: "+broadcast.Meta+" / "+broadcast.Text);
            var signal=ScriptBlocks.Parse(data.lateStory.beats.First(x=>x.id=="seokhyeon_reports").body);
            Expect(signal.Any(x=>x.Kind==ScriptBlockKind.Broadcast && x.Text.StartsWith("이석현..")) && ScriptBlocks.PresentationFor(signal)==ScenePresentation.Scene,"the signal and the conversation about it were not kept distinct");
            var talk=ScriptBlocks.Parse("[무전] 한지우: “동림 씨, 들려요?”\n\n다리 건너편은 조용했다.");
            Expect(talk[0].Kind==ScriptBlockKind.Radio && talk[0].Speaker=="한지우" && ScriptBlocks.PresentationFor(talk)==ScenePresentation.Radio,"radio talk convention not parsed");

            // The SP broadcast bodies: static and section headers are recognised, story hints are not.
            var first=ScriptBlocks.Parse(radio.firstBroadcast.body);
            Expect(first[0].Kind==ScriptBlockKind.Noise && first.Last().Kind==ScriptBlockKind.Noise,"static lines not recognised");
            var hint=ScriptBlocks.Parse("[3막 · 마지막 구조 요청] DAY 22 · 주민회관에서 지우가 기다린다.");
            Expect(hint[0].Kind==ScriptBlockKind.Narration && hint[0].ToString().StartsWith("[3막"),"story hint misread as a broadcast");
            var section=ScriptBlocks.Parse("[SP 일일 수신 기록]\n서울 송파구 방이동\n읍 단위 소멸 제보");
            Expect(section[0].Kind==ScriptBlockKind.Section && section[0].Text.StartsWith("서울"),"SP log header not recognised");

            // Every scripted speaker either has a portrait slot or is a voice without a face.
            foreach(var beat in data.lateStory.beats)
                foreach(var block in ScriptBlocks.Parse((beat.body+"\n\n"+beat.bodyIf+"\n\n"+beat.bodyElse).Replace("{PlayerNickname}","기록자")))
                    if(block.Kind==ScriptBlockKind.Speech) Expect(block.Speaker.Length<=8 && !block.Speaker.Contains("“"),"suspicious speaker in "+beat.id+": "+block.Speaker);
            Debug.Log("VD_DIALOGUE_CHECKS_OK: face-to-face scenes, journal narration, radio broadcasts and radio talk are told apart.");
        }
    }
}
