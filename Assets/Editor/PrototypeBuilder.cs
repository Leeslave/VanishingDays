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
        public static void Validate() { SurvivalChecks.Validate(); }

        [MenuItem("Vanishing Days/Build Windows prototype")]
        public static void Build() { Build(false); }

        // A separately compiled player exercises the actual Unity build pipeline.
        // Test automation is never compiled into the normal executable.
        public static void BuildUIVerification() { Build(true); }
        static void Build(bool verification)
        {
            Validate();
            OpeningFlowChecks.Validate();
            var importer=AssetImporter.GetAtPath("Assets/Resources/Art/SeoulErasure.png") as TextureImporter;
            if(importer!=null) { importer.textureType=TextureImporterType.Default; importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
            PlayerSettings.companyName="Vanishing Days";
            PlayerSettings.productName="Vanishing Days";
            PlayerSettings.defaultScreenWidth=540; PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;
            string folder=verification?"Builds/WindowsVerification":"Builds/Windows";
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Prototype.unity"}, locationPathName=folder+"/VanishingDays.exe", target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development, extraScriptingDefines=verification?new[]{"VD_UI_VERIFICATION"}:new string[0] });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("VD_BUILD_OK: "+report.summary.outputPath);
        }
    }
}
