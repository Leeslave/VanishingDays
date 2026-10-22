using System.IO;
using System.Linq;
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VanishingDays.Editor
{
    // Configure the user's existing scene through Unity so the open scene and disk stay in sync.
    [InitializeOnLoad]
    public static class PrototypeSceneSetup
    {
        const string ScenePath="Assets/Scenes/Prototype.unity";
        static PrototypeSceneSetup() { EditorApplication.delayCall+=PrepareLoadedScene; }

        [MenuItem("Vanishing Days/Prepare prototype scene")]
        public static void PrepareLoadedScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(EditorApplication.isCompiling) { EditorApplication.delayCall+=PrepareLoadedScene; return; }
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid() || !scene.isLoaded) return;
            ConfigurePortraitGameView();
            bool wasDirty=scene.isDirty, changed=false;
            var game=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<VanishingDaysGame>(true)).FirstOrDefault();
            if(game==null)
            {
                var root=new GameObject("Vanishing Days - Prototype");
                SceneManager.MoveGameObjectToScene(root,scene);
                game=root.AddComponent<VanishingDaysGame>();
                changed=true;
            }
            var serialized=new SerializedObject(game);
            var direct=serialized.FindProperty("skipOpeningInEditor");
            if(changed && direct.boolValue) { direct.boolValue=false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            if(changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                // Do not save unrelated edits that the user has not saved yet.
                if(!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            var scenes=EditorBuildSettings.scenes;
            if(scenes.Length==0 || scenes[0].path!=ScenePath || !scenes[0].enabled)
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(scenes.Where(s=>s.path!=ScenePath)).ToArray();
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/prototype-scene-setup.txt", "VD_PROTOTYPE_READY\nScene: "+scene.path+"\nController: "+game.name+"\nStart in shelter: "+direct.boolValue+"\nSaved: "+!scene.isDirty);
            Debug.Log("VD_PROTOTYPE_READY: Prototype scene starts with the opening flow.");
        }

        static void ConfigurePortraitGameView()
        {
            // Unity exposes Game-view presets only through internal editor APIs.
            // The runtime layout remains 9:16 even if a future editor changes these APIs.
            try
            {
                var assembly=typeof(EditorWindow).Assembly;
                var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
                var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
                var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
                var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
                var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
                int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null), index=-1;
                var sizeType=assembly.GetType("UnityEditor.GameViewSize");
                for(int i=0;i<count;i++)
                {
                    var preset=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
                    int w=(int)sizeType.GetProperty("width",flags).GetValue(preset), h=(int)sizeType.GetProperty("height",flags).GetValue(preset);
                    if(w==9 && h==16) { index=i; break; }
                }
                if(index<0)
                {
                    var kind=assembly.GetType("UnityEditor.GameViewSizeType");
                    var constructor=sizeType.GetConstructor(flags,null,new[]{kind,typeof(int),typeof(int),typeof(string)},null);
                    var preset=constructor.Invoke(new[]{Enum.Parse(kind,"AspectRatio"),(object)9,16,"Vanishing Days 9:16"});
                    group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{preset});
                    index=count;
                }
                var gameViewType=assembly.GetType("UnityEditor.GameView");
                var window=EditorWindow.GetWindow(gameViewType,false,"Game",false);
                gameViewType.GetProperty("selectedSizeIndex",flags).SetValue(window,index);
                window.Repaint();
            }
            catch(Exception e) { Debug.LogWarning("Game 뷰의 비율 목록에서 9:16을 선택해 주세요. "+e.Message); }
        }
    }
}
