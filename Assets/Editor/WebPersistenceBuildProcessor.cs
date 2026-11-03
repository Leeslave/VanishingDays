using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace VanishingDays.Editor
{
    // Apply to both the menu command and ordinary Unity Web builds.
    public sealed class WebPersistenceBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.WebGL) return;
            string path=Path.Combine(report.summary.outputPath,"index.html");
            string html=EnablePersistence(File.ReadAllText(path));
            html=InstallRecoveryCopy(report.summary.outputPath,html);
            File.WriteAllText(path,html);
        }

        static string InstallRecoveryCopy(string folder,string html)
        {
            var match=Regex.Match(html,"frameworkUrl:\\s*buildUrl \\+ \"/([^\"]+)\"");
            if(!match.Success) throw new BuildFailedException("Cannot find the Web framework to install browser save recovery.");
            string name=match.Groups[1].Value;
            if(name!=Path.GetFileName(name)) throw new BuildFailedException("Invalid Web framework path.");
            byte[] bytes=File.ReadAllBytes(Path.Combine(folder,"Build",name));
            bool gzip=bytes.Length>2 && bytes[0]==0x1f && bytes[1]==0x8b;
            if(gzip)
            {
                using(var input=new MemoryStream(bytes))
                using(var decoder=new GZipStream(input,CompressionMode.Decompress))
                using(var decoded=new MemoryStream()) { decoder.CopyTo(decoded); bytes=decoded.ToArray(); }
            }
            else if(!name.EndsWith(".js",StringComparison.Ordinal))
                throw new BuildFailedException("Browser save recovery requires Gzip with Decompression Fallback, or an uncompressed Web build.");
            string framework=Encoding.UTF8.GetString(bytes);
            const string marker="Module.removeRunDependency(\"JS_FileSystem_Mount\")";
            if(framework.Split(new[]{marker},StringSplitOptions.None).Length!=2)
                throw new BuildFailedException("Unity filesystem startup changed; browser save recovery must be updated.");
            if(!framework.Contains("installVanishingDaysSaveBackup"))
                framework=framework.Replace(marker,File.ReadAllText("Assets/Plugins/WebGL/BrowserSaveBackup.js.txt")+";"+marker);
            bytes=Encoding.UTF8.GetBytes(framework);
            if(gzip)
            {
                using(var output=new MemoryStream())
                {
                    using(var encoder=new GZipStream(output,CompressionMode.Compress,true)) encoder.Write(bytes,0,bytes.Length);
                    bytes=output.ToArray();
                }
                // Unity's decompression fallback detects this gzip comment.
                byte[] comment=Encoding.ASCII.GetBytes("UnityWeb Compressed Content (gzip)\0");
                byte[] marked=new byte[bytes.Length+comment.Length];
                Array.Copy(bytes,0,marked,0,10); marked[3]|=16;
                Array.Copy(comment,0,marked,10,comment.Length);
                Array.Copy(bytes,10,marked,10+comment.Length,bytes.Length-10);
                bytes=marked;
            }
            string hash;
            using(var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant().Substring(0,12);
            string updated="WebGL."+hash+".framework.js"+(gzip?".unityweb":"");
            File.WriteAllBytes(Path.Combine(folder,"Build",updated),bytes);
            return html.Replace("\"/"+name+"\"","\"/"+updated+"\"");
        }

        public static string EnablePersistence(string html)
        {
            const string setting="config.autoSyncPersistentDataPath = true;";
            const string marker="createUnityInstance(canvas, config,";
            int start=html.IndexOf(marker,StringComparison.Ordinal);
            if(start<0) throw new BuildFailedException("Web template must pass config to createUnityInstance so browser saves can be enabled.");
            // Set immediately before startup, even if a custom template disabled it.
            return html.Insert(start,setting+"\n        ");
        }

        public static void Validate()
        {
            foreach(string previous in new[]{"// config.autoSyncPersistentDataPath = true;","config.autoSyncPersistentDataPath = false;"})
            {
                string html=EnablePersistence(previous+"\ncreateUnityInstance(canvas, config, () => {});");
                if(!html.Contains("\nconfig.autoSyncPersistentDataPath = true;\n        createUnityInstance"))
                    throw new Exception("Web persistence was not enabled before Unity startup.");
            }
            bool rejected=false;
            try { EnablePersistence("<html>unsupported custom template</html>"); }
            catch(BuildFailedException) { rejected=true; }
            if(!rejected) throw new Exception("Unsupported Web template silently disabled browser saves.");
            UnityEngine.Debug.Log("VD_WEB_PERSISTENCE_CHECKS_OK: default/custom template persistence and unsupported template detection.");
        }
    }
}
