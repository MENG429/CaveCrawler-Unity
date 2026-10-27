using UnityEngine;

namespace CB
{
    /// <summary>
    /// Runtime assembly of the whole game. No scene setup is required - if you can press
    /// Play, you get a level, a player, a camera and a HUD.
    ///
    /// AfterSceneLoad guarantees the scene's own objects (including the template Main Camera)
    /// exist before we build on top of them.
    /// </summary>
    public static class CBBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (CBGame.Instance != null) return;   // already running (domain reload off, etc.)

            var clockObject = new GameObject("CB_Clock");
            clockObject.AddComponent<CBGameClock>();

            var hostObject = new GameObject("CB_Game");
            var cbGame = hostObject.AddComponent<CBGame>();

            // AddComponent runs Awake before we can assign anything, so the config is passed
            // into Boot() and read there - never rely on Awake seeing assigned fields.
            var config = ScriptableObject.CreateInstance<CBConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;

            cbGame.Boot(config);
        }
    }
}
