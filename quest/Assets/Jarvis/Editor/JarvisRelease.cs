// Release setup for Jarvis Room: app name, app ID, icon and a signing key, then a sideloadable APK in Builds/.
// The signing key lives in Keystore/ next to Assets (kept out of GitHub by .gitignore). Keep a backup of that folder:
// future updates must be signed with the same key to install over an older version.
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace JarvisRoom.EditorTools
{
    [InitializeOnLoad]
    public static class JarvisRelease
    {
        public const string AppName = "Jarvis Room";
        public const string AppId = "com.jarvisroom.quest";
        public const string Version = "1.0.1";
        public const int VersionCode = 2;
        private const string Alias = "jarvis";

        private static string Folder { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Keystore")); } }
        private static string KeystorePath { get { return Path.Combine(Folder, "jarvis-room.keystore"); } }
        private static string PasswordPath { get { return Path.Combine(Folder, "keystore-password.txt"); } }

        // Unity forgets keystore passwords between sessions, so they're read back in every time the editor loads.
        static JarvisRelease() { EditorApplication.delayCall += UseKeystore; }

        private static void UseKeystore()
        {
            if (!File.Exists(KeystorePath) || !File.Exists(PasswordPath)) return;
            string password = File.ReadAllText(PasswordPath).Trim();
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasName = Alias;
            PlayerSettings.Android.keyaliasPass = password;
        }

        [MenuItem("Jarvis/Release/1. Set app name, ID, icon and signing key")]
        public static void Prepare()
        {
            PlayerSettings.productName = AppName;
            PlayerSettings.companyName = "Jarvis fans";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Jarvis/Icon/jarvis-icon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            if (!File.Exists(KeystorePath)) MakeKeystore();
            UseKeystore();
            AssetDatabase.SaveAssets();
            Debug.Log("[Jarvis] Release setup done: " + AppName + " (" + AppId + ") v" + Version + ", signed with " + KeystorePath);
        }

        [MenuItem("Jarvis/Release/2. Build release APK")]
        public static void Build()
        {
            Prepare();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "JarvisRoom-v" + Version + ".apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Jarvis] Release build " + report.summary.result + ": " + output);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder(output);
        }

        private static void MakeKeystore()
        {
            Directory.CreateDirectory(Folder);
            string password = RandomPassword();
            string keytool = FindKeytool();
            if (keytool == null) { Debug.LogError("[Jarvis] Couldn't find keytool in Unity's OpenJDK. Is Android Build Support installed?"); return; }
            string args = "-genkeypair -keystore \"" + KeystorePath + "\" -storetype pkcs12 -alias " + Alias +
                " -keyalg RSA -keysize 2048 -validity 10000 -storepass " + password + " -keypass " + password +
                " -dname \"CN=Jarvis Room, O=Jarvis fans\"";
            var start = new System.Diagnostics.ProcessStartInfo(keytool, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using (var run = System.Diagnostics.Process.Start(start))
            {
                string errors = run.StandardError.ReadToEnd();
                run.WaitForExit();
                if (run.ExitCode != 0 || !File.Exists(KeystorePath)) { Debug.LogError("[Jarvis] keytool failed: " + errors); return; }
            }
            File.WriteAllText(PasswordPath, password);
            File.WriteAllText(Path.Combine(Folder, "README.txt"),
                "Jarvis Room's signing key. Back this folder up somewhere safe and never upload it.\n" +
                "Updates must be signed with this same key, or they won't install over an older Jarvis Room.\n");
            Debug.Log("[Jarvis] Made a signing key in " + Folder + ". Back that folder up!");
        }

        private static string FindKeytool()
        {
            string[] tries =
            {
                Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool.exe"),
                Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool"),
            };
            foreach (string path in tries) if (File.Exists(path)) return path;
            return null;
        }

        private static string RandomPassword()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            byte[] bytes = new byte[24];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            char[] chars = new char[24];
            for (int i = 0; i < 24; i++) chars[i] = letters[bytes[i] % letters.Length];
            return new string(chars);
        }
    }
}
