using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VanishingDays.Editor
{
    public static class PrototypeBuilder
    {
        [MenuItem("Vanishing Days/Validate game rules")]
        public static void Validate()
        {
            var s=SurvivalState.NewGame();
            Require(s.NextErasure()==6,"Oldest unvisited place must disappear first.");
            s.Visit(6,false);
            Require(s.NextErasure()==4,"A visit must change the next erasure.");
            int before=s.districts[4].silence;
            s.Observe(4);
            Require(s.districts[4].silence==before,"Remote observation must not reset silence.");
            s.scrap=10; s.Craft(2); s.Protect(4);
            Require(s.NextErasure()!=4,"Protected places cannot disappear.");
            int target=s.NextErasure(); s.Midnight();
            Require(s.districts[target].erased,"Midnight must erase the predicted place.");
            Require(s.day==2 && s.hour==8 && s.protectedDistrict==-1,"Dawn must reset the daily state.");
            s.hour=23; int food=s.food; s.Visit(0,true);
            Require(s.hour==23 && s.food==food,"Actions must not overrun midnight.");
            s=SurvivalState.NewGame(); s.hour=8; s.Visit(3,true); int supplies=s.food; s.Visit(3,true);
            Require(s.food==supplies,"Loot cannot be collected twice.");
            s=SurvivalState.NewGame(); s.food=0; s.water=0; s.health=40; s.Midnight();
            Require(s.finished&&!s.won,"Resource deprivation must allow defeat.");
            s=SurvivalState.NewGame();
            s.Visit(4,true); s.Visit(0,true); s.Craft(0); s.Craft(1);
            for(int day=1;day<=7;day++) s.Midnight();
            Require(s.finished && s.won && s.health>0,"A reachable crafting strategy must survive seven nights.");
            var restored=JsonUtility.FromJson<SurvivalState>(JsonUtility.ToJson(s));
            Require(restored.won && restored.districts.Count==8 && restored.journal.Count==s.journal.Count,"Save round trip must preserve progress.");
            Debug.Log("VD_RULES_OK: erasure, visits, observation, preservation, dawn, time limits, finite loot, defeat, reachable victory, save round trip.");
        }
        static void Require(bool condition,string reason) { if(!condition) throw new Exception(reason); }

        [MenuItem("Vanishing Days/Build Windows prototype")]
        public static void Build()
        {
            Validate();
            var importer=AssetImporter.GetAtPath("Assets/Resources/Art/SeoulErasure.png") as TextureImporter;
            if(importer!=null) { importer.textureType=TextureImporterType.Default; importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
            PlayerSettings.companyName="Vanishing Days";
            PlayerSettings.productName="Vanishing Days";
            PlayerSettings.defaultScreenWidth=540; PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Prototype.unity"}, locationPathName="Builds/Windows/VanishingDays.exe", target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("VD_BUILD_OK: "+report.summary.outputPath);
        }
    }
}
