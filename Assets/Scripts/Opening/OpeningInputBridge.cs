using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace VanishingDays
{
    // uGUI InputField requests IME through BaseInput. Adapt it to the existing Input System
    // while NicknameInputField receives committed characters from onTextInput.
    public sealed class OpeningInputBridge : BaseInput
    {
        Keyboard keyboard;
        string composition="";
        IMECompositionMode mode;
        Vector2 cursor;
        bool inserting;
        public int CompositionFrame { get; private set; }=-1;
        public void InsertCommitted(char character,System.Action insert)
        {
            if(composition==character.ToString())composition="";
            inserting=true;
            try { insert(); } finally { inserting=false; }
        }
        protected override void OnEnable() { base.OnEnable(); BindKeyboard(); InputSystem.onDeviceChange+=DeviceChanged; }
        protected override void OnDisable()
        {
            InputSystem.onDeviceChange-=DeviceChanged;
            if(keyboard!=null) { keyboard.onIMECompositionChange-=CompositionChanged; keyboard.SetIMEEnabled(false); }
            keyboard=null; composition=""; base.OnDisable();
        }
        void DeviceChanged(InputDevice device,InputDeviceChange change) { if(device is Keyboard) BindKeyboard(); }
        void BindKeyboard()
        {
            if(keyboard==Keyboard.current) return;
            if(keyboard!=null) keyboard.onIMECompositionChange-=CompositionChanged;
            keyboard=Keyboard.current;
            if(keyboard!=null) { keyboard.onIMECompositionChange+=CompositionChanged; keyboard.SetIMEEnabled(mode==IMECompositionMode.On); }
        }
        void CompositionChanged(IMECompositionString value)
        {
            CompositionFrame=Time.frameCount;
            composition=value.ToString();
            var events=EventSystem.current;
            var selected=events!=null?events.currentSelectedGameObject:null;
            if(selected!=null)
            {
                var field=selected.GetComponent<InputField>();
                if(field!=null)field.ForceLabelUpdate();
            }
        }
        public override string compositionString => inserting?"":composition;
        public override IMECompositionMode imeCompositionMode
        {
            get => mode;
            set { mode=value; keyboard?.SetIMEEnabled(value==IMECompositionMode.On); if(value!=IMECompositionMode.On) composition=""; }
        }
        public override Vector2 compositionCursorPos { get => cursor; set { cursor=value; keyboard?.SetIMECursorPosition(value); } }
        public override bool touchSupported => Touchscreen.current!=null;
    }
}
