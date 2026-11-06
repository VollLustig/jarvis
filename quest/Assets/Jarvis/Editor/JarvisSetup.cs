// Editor helpers: sets up Jarvis's sprite sheets when they're imported, and adds a menu item that puts her in the scene.
using UnityEditor;
using UnityEngine;

namespace JarvisRoom.EditorTools
{
    public class JarvisTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Jarvis/Resources/JarvisSprites/")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;   // keep exact pixels so frames line up
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;                         // less shimmer when she's across the room
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }

    // Makes sure the Quest build can open the system keyboard (for her settings) and reach the internet (for her brain and voice).
    [InitializeOnLoad]
    public static class JarvisProjectSettings
    {
        static JarvisProjectSettings() { EditorApplication.delayCall += Apply; }

        [MenuItem("Jarvis/Fix Quest settings for talking")]
        public static void Apply()
        {
            PlayerSettings.Android.forceInternetPermission = true;
            // Meta's "Requires System Keyboard" setting, set by name so this still compiles if Meta moves it.
            try
            {
                System.Type type = System.Type.GetType("OVRProjectConfig, Oculus.VR.Editor") ?? FindType("OVRProjectConfig");
                if (type == null) return;
                object config = type.GetMethod("GetProjectConfig", new System.Type[0]) != null
                    ? type.GetMethod("GetProjectConfig", new System.Type[0]).Invoke(null, null)
                    : null;
                if (config == null) return;
                System.Reflection.FieldInfo keyboard = type.GetField("requiresSystemKeyboard");
                if (keyboard == null || (bool)keyboard.GetValue(config)) return;
                keyboard.SetValue(config, true);
                System.Reflection.MethodInfo commit = type.GetMethod("CommitProjectConfig");
                if (commit != null) commit.Invoke(null, new[] { config });
                Debug.Log("[Jarvis] Turned on Requires System Keyboard so her settings can open the Quest keyboard.");
            }
            catch (System.Exception problem) { Debug.LogWarning("[Jarvis] Couldn't turn on the Quest keyboard setting: " + problem.Message); }
        }

        private static System.Type FindType(string name)
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type found = assembly.GetType(name);
                if (found != null) return found;
            }
            return null;
        }
    }

    public static class JarvisMenu
    {
        [MenuItem("Jarvis/Add Jarvis to Scene")]
        public static void AddToScene()
        {
            JarvisPet existing = Object.FindFirstObjectByType<JarvisPet>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorUtility.DisplayDialog("Jarvis", "Jarvis is already in this scene.", "OK");
                return;
            }
            GameObject jarvis = new GameObject("Jarvis");
            jarvis.AddComponent<RoomSense>();
            jarvis.AddComponent<JarvisHands>();
            jarvis.AddComponent<JarvisPet>();
            Undo.RegisterCreatedObjectUndo(jarvis, "Add Jarvis");
            Selection.activeObject = jarvis;
            if (Object.FindFirstObjectByType<OVRCameraRig>() == null)
                EditorUtility.DisplayDialog("Jarvis", "Jarvis is in. This scene has no Camera Rig yet, so add the Camera Rig, Passthrough and MR Utility Kit building blocks too (Meta > Tools > Building Blocks).", "OK");
        }
    }
}
