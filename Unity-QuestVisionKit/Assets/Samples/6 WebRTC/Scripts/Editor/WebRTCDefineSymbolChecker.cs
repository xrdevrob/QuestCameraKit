#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public static class WebRTCDefineSymbolChecker {
    static WebRTCDefineSymbolChecker() {
        EditorApplication.delayCall += UpdateWebRTCDefine;
    }

    [MenuItem("Tools/Update WebRTC Define Symbol")]
    public static void UpdateWebRTCDefine() {
        var hasWebRTC = HasWebRTCPackage();

        var targets = new[]
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
        };

        foreach (var target in targets) {
            try {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target);
                var defineList = defines.Split(';')
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrEmpty(s))
                                        .ToList();

                var symbolExists = defineList.Contains("WEBRTC_ENABLED");

                switch (hasWebRTC) {
                    case true when !symbolExists:
                        defineList.Add("WEBRTC_ENABLED");
                        Debug.Log($"[WebRTCDefineSymbolChecker] Added WEBRTC_ENABLED for {target}");
                        break;
                    case false when symbolExists:
                        defineList.Remove("WEBRTC_ENABLED");
                        Debug.Log($"[WebRTCDefineSymbolChecker] Removed WEBRTC_ENABLED for {target}");
                        break;
                }

                var newDefines = string.Join(";", defineList);
                if (newDefines != defines) PlayerSettings.SetScriptingDefineSymbols(target, newDefines);
            } catch (System.Exception ex) {
                Debug.LogWarning($"[WebRTCDefineSymbolChecker] Could not update define for target {target}: {ex.Message}");
            }
        }
    }

    private static bool HasWebRTCPackage() {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => assembly.GetType("SimpleWebRTC.WebRTCConnection", false) != null);
    }
}
#endif
