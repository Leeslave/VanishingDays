#if UNITY_EDITOR || VD_UI_VERIFICATION
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        IEnumerator SmokeNicknameInput()
        {
            yield return null;
            var flow=openingView.Flow;
            while(flow.Current.kind!="Nickname")
            {if(flow.Current.kind=="Question")flow.Answer(true);else flow.Next();}
            yield return null;yield return null;
            var field=openingView.GetComponentInChildren<NicknameInputField>();
            if(field==null || !field.isFocused)throw new Exception("Native nickname field was not focused.");
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            var composition=IMECompositionEvent.Create(keyboard.deviceId,"ㅎ",InputState.currentTime);InputSystem.QueueEvent(ref composition);
            yield return null;yield return null;
            if(field.text!="" || !field.textComponent.text.Contains("ㅎ"))throw new Exception("IME preview was not displayed separately from committed text.");
            composition=IMECompositionEvent.Create(keyboard.deviceId,"한",InputState.currentTime);InputSystem.QueueEvent(ref composition);
            InputSystem.QueueTextEvent(keyboard,'한');
            composition=IMECompositionEvent.Create(keyboard.deviceId,"",InputState.currentTime);InputSystem.QueueEvent(ref composition);
            yield return null;yield return null;
            // A new syllable can begin in the same update as the previous syllable commits.
            InputSystem.QueueTextEvent(keyboard,'글');
            composition=IMECompositionEvent.Create(keyboard.deviceId,"ㄱ",InputState.currentTime);InputSystem.QueueEvent(ref composition);
            yield return null;yield return null;
            if(field.text!="한글" || !field.textComponent.text.Contains("ㄱ"))throw new Exception("Commit/new composition ordering lost Korean text.");
            composition=IMECompositionEvent.Create(keyboard.deviceId,"",InputState.currentTime);InputSystem.QueueEvent(ref composition);
            foreach(char c in "A1!_")InputSystem.QueueTextEvent(keyboard,c);
            yield return null;yield return null;
            if(field.text!="한글A1!_")throw new Exception("Input System text commits were missing or duplicated: "+field.text);
            field.selectionAnchorPosition=field.selectionFocusPosition=1;InputSystem.QueueTextEvent(keyboard,'★');
            yield return null;yield return null;
            if(field.text!="한★글A1!_")throw new Exception("Caret insertion failed.");
            field.MoveTextEnd(false);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Backspace));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            if(field.text!="한★글A1!")throw new Exception("Backspace failed or ran twice: "+field.text);
            OpeningButton("기록 시작").onClick.Invoke();
            if(flow.PlayerNickname!="한★글A1!" || !flow.Author.Contains("한★글A1!"))throw new Exception("Committed Korean nickname was not bound to the record.");
            flow.Next();yield return null;
            if(state.PlayerNickname!="한★글A1!" || !store.TryLoad(out var saved,out _) || saved.PlayerNickname!=state.PlayerNickname)throw new Exception("Korean nickname was not saved.");
            Debug.Log("VD_NATIVE_NICKNAME_OK: real InputSystem text/IME events, preview, commit ordering, symbols, caret, backspace and profile save in "+(Application.isEditor?"Editor Play":"Windows Player"));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }
}
#endif
