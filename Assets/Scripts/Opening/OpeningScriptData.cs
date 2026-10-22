using System;
using UnityEngine;

namespace VanishingDays
{
    [Serializable]
    public class OpeningScreenData
    {
        public string id, title, date, author, body, button, kind, yesLead, noLead;
    }

    [Serializable]
    public class OpeningScriptData
    {
        public OpeningScreenData[] screens;
        public static OpeningScriptData Load()
        {
            var asset=Resources.Load<TextAsset>("Opening/OpeningScriptData");
            if(asset==null) throw new InvalidOperationException("Opening/OpeningScriptData.json is missing.");
            var data=JsonUtility.FromJson<OpeningScriptData>(asset.text);
            if(data?.screens==null || data.screens.Length!=13) throw new InvalidOperationException("Opening requires OPEN_00 through OPEN_12.");
            for(int i=0;i<data.screens.Length;i++)
                if(data.screens[i].id!="OPEN_"+i.ToString("00")) throw new InvalidOperationException("Invalid opening screen order.");
            return data;
        }
    }
}
