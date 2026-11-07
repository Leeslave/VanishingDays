using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VanishingDays
{
    public enum ScriptBlockKind { Narration, Speech, Broadcast, Radio, Noise, Section }
    // How a scene is presented: B for people in the same room, C for anything heard over a radio,
    // A for everything else that ends in a choice.
    public enum ScenePresentation { Journal, Scene, Radio }

    public sealed class ScriptBlock
    {
        public ScriptBlockKind Kind;
        public string Speaker="", Meta="", Text="", Raw="";
        public bool IsPlayer;
        public bool IsTransmission => Kind==ScriptBlockKind.Broadcast || Kind==ScriptBlockKind.Radio;
        // Plain text for logs and fallbacks.
        public override string ToString() => Kind==ScriptBlockKind.Speech || Kind==ScriptBlockKind.Radio?Speaker+": "+Text:Raw.Length>0?Raw:Text;
    }

    // Splits script text into blocks by its writing conventions:
    // paragraphs separated by a blank line; speech is `이름: “…”`; radio talk is `[무전] 이름: “…”`;
    // a broadcast is a bracketed source line (`[출처 미상 · 확인 수준: 미확인]`) followed by what was heard;
    // static is `[치직…]`; a bracketed header of an SP log (`[SP 일일 수신 기록]`) opens a section.
    public static class ScriptBlocks
    {
        static readonly Regex SpeechLine=new Regex(@"^([^:“”\[\]\n]{1,16}):\s*(“.*)$",RegexOptions.Singleline);
        static readonly Regex BracketLine=new Regex(@"^\[([^\]\n]+)\]\s*(.*)$",RegexOptions.Singleline);

        public static List<ScriptBlock> Parse(string text,string playerName=null)
        {
            var blocks=new List<ScriptBlock>();
            if(string.IsNullOrWhiteSpace(text)) return blocks;
            foreach(var raw in Regex.Split(text.Replace("\r\n","\n").Trim(),@"\n\s*\n"))
            {
                string paragraph=raw.Trim();
                if(paragraph.Length==0) continue;
                var block=ParseParagraph(paragraph,playerName); block.Raw=paragraph;
                blocks.Add(block);
            }
            return blocks;
        }

        static ScriptBlock ParseParagraph(string paragraph,string playerName)
        {
            var bracket=BracketLine.Match(paragraph);
            if(bracket.Success)
            {
                string meta=bracket.Groups[1].Value.Trim(), rest=bracket.Groups[2].Value.Trim();
                if(meta=="무전")
                {
                    var talk=SpeechLine.Match(rest);
                    return talk.Success?new ScriptBlock{Kind=ScriptBlockKind.Radio,Speaker=talk.Groups[1].Value.Trim(),Text=talk.Groups[2].Value.Trim(),IsPlayer=IsPlayer(talk.Groups[1].Value,playerName)}
                        :new ScriptBlock{Kind=ScriptBlockKind.Radio,Text=rest};
                }
                if(IsNoise(meta) && rest.Length==0) return new ScriptBlock{Kind=ScriptBlockKind.Noise,Meta=meta,Text=meta};
                // Only a line that names its source counts as a broadcast; a story hint such as
                // "[3막 · …] DAY 12 …" is narration that happens to start with a bracket.
                if(IsSourceLine(meta)) return new ScriptBlock{Kind=ScriptBlockKind.Broadcast,Meta=meta,Text=rest};
                if(IsSectionHeader(meta)) return new ScriptBlock{Kind=ScriptBlockKind.Section,Meta=meta,Text=rest};
                return new ScriptBlock{Kind=ScriptBlockKind.Narration,Text=paragraph};
            }
            var speech=SpeechLine.Match(paragraph);
            if(speech.Success)
            {
                string speaker=speech.Groups[1].Value.Trim();
                return new ScriptBlock{Kind=ScriptBlockKind.Speech,Speaker=speaker,Text=speech.Groups[2].Value.Trim(),IsPlayer=IsPlayer(speaker,playerName)};
            }
            return new ScriptBlock{Kind=ScriptBlockKind.Narration,Text=paragraph};
        }

        static bool IsPlayer(string speaker,string playerName) => !string.IsNullOrWhiteSpace(playerName) && speaker.Trim()==playerName.Trim();
        public static bool IsNoise(string meta) => meta.Contains("치직") || meta.Contains("잡음") || meta.Contains("지직");
        public static bool IsSourceLine(string meta) => meta.Contains("확인 수준") || meta.Contains("출처") || meta.Contains("제보자");
        public static bool IsSectionHeader(string meta) => meta.StartsWith("SP") || meta.Contains("수신") || meta.Contains("정보 카드") || meta.Contains("기록");

        // Face-to-face talk wins; otherwise anything received over the air makes it a radio scene.
        public static ScenePresentation PresentationFor(List<ScriptBlock> blocks)
        {
            bool transmission=false;
            foreach(var block in blocks)
            {
                if(block.Kind==ScriptBlockKind.Speech) return ScenePresentation.Scene;
                if(block.IsTransmission) transmission=true;
            }
            return transmission?ScenePresentation.Radio:ScenePresentation.Journal;
        }

        // Portrait art id for a speaker; null for the player and for voices with no face on screen.
        public static string PortraitId(string speaker)
        {
            if(string.IsNullOrEmpty(speaker)) return null;
            if(speaker.Contains("동림")) return "donglim";
            if(speaker.Contains("지우")) return "jiwoo";
            if(speaker.Contains("석현")) return "seokhyeon";
            if(speaker.Contains("영숙")) return "yeongsuk";
            if(speaker.Contains("노인")) return "neighbor";
            if(speaker.Contains("SP")) return null;
            return "stranger";
        }
    }
}
