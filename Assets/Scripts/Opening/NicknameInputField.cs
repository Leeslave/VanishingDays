using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace VanishingDays
{
    // uGUI's default field reads legacy Event.PopEvent; IME commits from the new
    // Input System must instead come from onTextInput. Use one text source only.
    public sealed class NicknameInputField : InputField
    {
        Keyboard keyboard;
        int committedFrame=-1;
        KeyControl repeatingKey;
        float nextRepeat;
        protected override void OnEnable()
        {
            base.OnEnable();BindKeyboard();InputSystem.onDeviceChange+=DeviceChanged;
        }
        protected override void OnDisable()
        {
            InputSystem.onDeviceChange-=DeviceChanged;
            if(keyboard!=null) keyboard.onTextInput-=TextCommitted;
            keyboard=null;repeatingKey=null;base.OnDisable();
        }
        void DeviceChanged(InputDevice device,InputDeviceChange change) { if(device is Keyboard) BindKeyboard(); }
        void BindKeyboard()
        {
            if(keyboard==Keyboard.current)return;
            if(keyboard!=null)keyboard.onTextInput-=TextCommitted;
            keyboard=Keyboard.current;
            if(keyboard!=null)keyboard.onTextInput+=TextCommitted;
        }
        void TextCommitted(char character)
        {
            if(!isFocused || !isActiveAndEnabled || readOnly || char.IsControl(character))return;
            var bridge=EventSystem.current?.currentInputModule?.input as OpeningInputBridge;
            committedFrame=Time.frameCount;
            if(bridge!=null) bridge.InsertCommitted(character,()=>Append(character));
            else Append(character);
            ForceLabelUpdate();
        }
        bool Pressed(KeyControl key,bool repeat)
        {
            if(key.wasPressedThisFrame) { repeatingKey=key;nextRepeat=Time.unscaledTime+.4f;return true; }
            if(repeat && key==repeatingKey && key.isPressed && Time.unscaledTime>=nextRepeat)
            {nextRepeat=Time.unscaledTime+.05f;return true;}
            return false;
        }
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if(!isFocused || keyboard==null)return;
            var bridge=EventSystem.current?.currentInputModule?.input as OpeningInputBridge;
            if(!string.IsNullOrEmpty(bridge?.compositionString) || committedFrame==Time.frameCount || bridge?.CompositionFrame==Time.frameCount)
            {ForceLabelUpdate();eventData.Use();return;}
            var modifiers=EventModifiers.None;
            if(keyboard.shiftKey.isPressed)modifiers|=EventModifiers.Shift;
            if(keyboard.ctrlKey.isPressed)modifiers|=EventModifiers.Control;
            if(keyboard.altKey.isPressed)modifiers|=EventModifiers.Alt;
            if(keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed)modifiers|=EventModifiers.Command;
            Handle(keyboard.backspaceKey,KeyCode.Backspace,modifiers,true);
            Handle(keyboard.deleteKey,KeyCode.Delete,modifiers,true);
            Handle(keyboard.leftArrowKey,KeyCode.LeftArrow,modifiers,true);
            Handle(keyboard.rightArrowKey,KeyCode.RightArrow,modifiers,true);
            Handle(keyboard.homeKey,KeyCode.Home,modifiers,false);
            Handle(keyboard.endKey,KeyCode.End,modifiers,false);
            if((modifiers&(EventModifiers.Control|EventModifiers.Command))!=0)
            {
                Handle(keyboard.aKey,KeyCode.A,modifiers,false);Handle(keyboard.cKey,KeyCode.C,modifiers,false);
                Handle(keyboard.vKey,KeyCode.V,modifiers,false);Handle(keyboard.xKey,KeyCode.X,modifiers,false);
            }
            Handle(keyboard.enterKey,KeyCode.Return,modifiers,false);
            Handle(keyboard.numpadEnterKey,KeyCode.KeypadEnter,modifiers,false);
            Handle(keyboard.escapeKey,KeyCode.Escape,modifiers,false);
            ForceLabelUpdate();eventData.Use();
        }
        void Handle(KeyControl key,KeyCode code,EventModifiers modifiers,bool repeat)
        {
            if(!Pressed(key,repeat))return;
            if(KeyPressed(new Event{type=EventType.KeyDown,keyCode=code,modifiers=modifiers})==EditState.Finish)
            {
                if(code!=KeyCode.Escape)SendOnSubmit();
                DeactivateInputField();
            }
        }
    }
}
