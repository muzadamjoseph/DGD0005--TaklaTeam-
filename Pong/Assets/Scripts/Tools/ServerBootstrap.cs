// Pong — starts the server automatically when this process is a server (given; you do not write this one).
// Put it on the NetworkManager object. It decides it IS a server if any of these is true:
//   1. the build was made with the Dedicated Server target      (UNITY_SERVER define)
//   2. the game was launched with -batchmode                    (headless normal build)
//   3. the game was launched with the -server argument
//   4. (editor only) this Multiplayer Play Mode instance has the tag "Server"
// Optional argument: -port 7777   (default is whatever Tugboat has in the Inspector, 7770)
//
// Untick ServerManager > "Start On Headless" so this script is the only thing starting the server.
//
// The Multiplayer Play Mode check is done through reflection so that this file compiles whether
// or not the com.unity.multiplayer.playmode package is installed. With the package, a Play Mode
// instance tagged "Server" starts the server; without it, the check simply returns false.
using System;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class ServerBootstrap : MonoBehaviour
{
    [SerializeField] private int headlessTargetFrameRate = 60;   // don't let a headless server spin at 1000 fps

    private void Start()
    {
        NetworkManager nm = GetComponent<NetworkManager>();
        string reason = WhyAmIAServer();
        if (reason == null) return;                 // a normal client instance: do nothing, the HUD takes over

        ushort port = ReadPortArg(nm.TransportManager.Transport.GetPort());
        nm.TransportManager.Transport.SetPort(port);

        if (Application.isBatchMode) Application.targetFrameRate = headlessTargetFrameRate;

        if (!nm.ServerManager.Started) nm.ServerManager.StartConnection();
        Debug.Log($"[Server] Auto-started ({reason}) on UDP port {port}.");
    }

    private static string WhyAmIAServer()
    {
#if UNITY_SERVER
        return "Dedicated Server build";
#else
        string[] args = Environment.GetCommandLineArgs();
        if (Application.isBatchMode)                          return "-batchmode";
        if (args.Contains("-server"))                          return "-server argument";
#if UNITY_EDITOR
        if (HasPlayModeTag("Server"))                          return "Multiplayer Play Mode tag 'Server'";
#endif
        return null;
#endif
    }

    /// Editor only. True if this Multiplayer Play Mode instance carries the tag. Uses reflection so the
    /// script compiles without the package: Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags().
    private static bool HasPlayModeTag(string tag)
    {
        try
        {
            Type currentPlayer = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer", throwOnError: false))
                .FirstOrDefault(t => t != null);
            if (currentPlayer == null) return false;                          // package not installed

            MethodInfo readOnlyTags = currentPlayer.GetMethod("ReadOnlyTags", BindingFlags.Public | BindingFlags.Static);
            if (readOnlyTags == null) return false;

            object tags = readOnlyTags.Invoke(null, null);
            if (tags is System.Collections.IEnumerable list)
                foreach (object t in list) if (t as string == tag) return true;
        }
        catch (Exception) { /* any API change: behave as "no tag" */ }
        return false;
    }

    private static ushort ReadPortArg(ushort fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-port" && ushort.TryParse(args[i + 1], out ushort p)) return p;
        return fallback;
    }
}
