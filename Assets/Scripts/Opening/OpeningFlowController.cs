using System;
using System.Text.RegularExpressions;

namespace VanishingDays
{
    public enum OpeningAnswer { Unanswered, Yes, No }

    // Pure flow state: answers affect only the definition's lead-in, never game stats.
    public sealed class OpeningFlowController
    {
        readonly OpeningScriptData script;
        int index;
        bool completed;
        public OpeningAnswer CrowdPsychologyAnswer { get; private set; }
        public OpeningAnswer PanicAnswer { get; private set; }
        public string PlayerNickname { get; private set; }="";
        public string CurrentScreenId => Current.id;
        public OpeningScreenData Current => script.screens[index];
        public bool IsCompleted => completed;
        public event Action ScreenChanged;
        public event Action<string> OnOpeningCompleted;

        public OpeningFlowController(OpeningScriptData data) { script=data ?? throw new ArgumentNullException(nameof(data)); }
        public string Author => (Current.author??"").Replace("{PlayerNickname}",PlayerNickname);
        public string Body
        {
            get
            {
                var answer=index==2?CrowdPsychologyAnswer:index==4?PanicAnswer:OpeningAnswer.Unanswered;
                if(answer==OpeningAnswer.Unanswered) return Current.body??"";
                return (answer==OpeningAnswer.Yes?Current.yesLead:Current.noLead)+"\n\n"+Current.body;
            }
        }
        public void Answer(bool yes)
        {
            if(completed || Current.kind!="Question") return;
            if(index==1) CrowdPsychologyAnswer=yes?OpeningAnswer.Yes:OpeningAnswer.No;
            else if(index==3) PanicAnswer=yes?OpeningAnswer.Yes:OpeningAnswer.No;
            else return;
            MoveNext();
        }
        public void Next()
        {
            if(completed || Current.kind=="Question" || Current.kind=="Nickname") return;
            if(index==script.screens.Length-1)
            {
                completed=true; // Set before invoking subscribers, including re-entrant callbacks.
                OnOpeningCompleted?.Invoke(PlayerNickname);
            }
            else MoveNext();
        }
        public static bool IsNicknameValid(string input)
        {
            string name=(input??"").Trim();
            return name.Length>=1 && name.Length<=12 && Regex.IsMatch(name,@"\A[가-힣ᄀ-ᇿㄱ-ㆎA-Za-z0-9]+\z");
        }
        public bool SubmitNickname(string input)
        {
            if(completed || Current.kind!="Nickname" || !IsNicknameValid(input)) return false;
            PlayerNickname=input.Trim();
            MoveNext();
            return true;
        }
        void MoveNext() { index++; ScreenChanged?.Invoke(); }
    }
}
