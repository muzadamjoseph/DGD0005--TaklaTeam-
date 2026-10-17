// Pong — a tiny debug HUD to start/stop the server and client (given; you do not write this one).
// Uses Unity's old IMGUI (OnGUI) so there is no Canvas to set up. Replace it with
// real UI later; the calls into FishNet stay the same.
//
//   NetworkManager.ServerManager.StartConnection()              -> become a server
//   NetworkManager.ClientManager.StartConnection(address, port) -> connect as a client
//
// This file knows nothing about paddles, balls or scores. Your own scripts talk to it through
// two static hooks, so the HUD never dictates how you name or structure your gameplay code:
//
//   ConnectionHud.Panels.Add(() => "LEFT 1 : 0 RIGHT");   // a function that returns text to draw every frame
//                                                          // (return null or "" to draw nothing). Rich text is allowed.
//   ConnectionHud.Flash("YOU SERVE");                       // one line, shown for two seconds
//
// Bytes per second, FishNet 4.7.3: StatisticsManager > Network Traffic > Enable Mode = Development,
// Update Client and Update Server ticked. FishNet then raises OnNetworkTraffic once per TICK with
// the bytes of that tick; this HUD adds them up into one-second windows. The per-tick counters are
// internal to FishNet (they feed its Network Profiler window), so they are read by reflection here.
using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet;
using FishNet.Editing.NetworkProfiler;
using FishNet.Managing;
using FishNet.Managing.Statistic;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionHud : MonoBehaviour
{
    [SerializeField] private string address = "127.0.0.1";   // "localhost". Type the server's IP here.
    [SerializeField] private ushort port    = 7770;          // Tugboat's default port (UDP)

    /// Text panels drawn under the connection status, in the order they were added.
    /// Each function is called once per OnGUI; return null or "" to draw nothing this frame.
    /// A function that throws (for example because its object was destroyed) is removed.
    public static readonly List<Func<string>> Panels = new List<Func<string>>();

    private NetworkManager _nm;
    private LocalConnectionState _serverState = LocalConnectionState.Stopped;
    private LocalConnectionState _clientState = LocalConnectionState.Stopped;

    private NetworkTrafficStatistics _traffic;               // null when statistics are disabled in the Inspector
    private ulong _inBytes, _outBytes;                       // last full second, this process as a client
    private ulong _srvInBytes, _srvOutBytes;                 // last full second, this process as a server
    private ulong _winIn, _winOut, _winSrvIn, _winSrvOut;    // the second being counted
    private float _winStart;

    private static string _flash = "";
    private static float  _flashUntil;

    /// Show a one-line message for two seconds. Call it from anywhere (an RPC body, an OnChange handler).
    public static void Flash(string text) { _flash = text; _flashUntil = Time.time + 2f; }

    private void Start()
    {
        _nm = InstanceFinder.NetworkManager;       // finds the NetworkManager in the scene
        if (_nm == null)
        {
            Debug.LogError("[HUD] No NetworkManager in the scene. Add one first.");
            enabled = false;
            return;
        }
        _nm.ServerManager.OnServerConnectionState += OnServerState;
        _nm.ClientManager.OnClientConnectionState += OnClientState;

        // Bytes per second. Needs the StatisticsManager component on the NetworkManager
        // object with Network Traffic > Enable Mode = Development and Update Client / Update Server ticked.
        if (_nm.StatisticsManager != null && _nm.StatisticsManager.TryGetNetworkTrafficStatistics(out _traffic))
        {
            _traffic.OnNetworkTraffic += OnTraffic;
            _winStart = Time.time;
        }
    }

    private void OnDestroy()
    {
        if (_nm == null) return;
        _nm.ServerManager.OnServerConnectionState -= OnServerState;
        _nm.ClientManager.OnClientConnectionState -= OnClientState;
        if (_traffic != null) _traffic.OnNetworkTraffic -= OnTraffic;
    }

    private void OnServerState(ServerConnectionStateArgs args) => _serverState = args.ConnectionState;
    private void OnClientState(ClientConnectionStateArgs args) => _clientState = args.ConnectionState;

    // Once per tick: the bytes of THAT tick, in and out, for the server side and the client side of this process.
    private void OnTraffic(uint tick, BidirectionalNetworkTraffic serverTraffic, BidirectionalNetworkTraffic clientTraffic)
    {
        _winSrvIn  += BytesOf(serverTraffic, inbound: true);
        _winSrvOut += BytesOf(serverTraffic, inbound: false);
        _winIn     += BytesOf(clientTraffic, inbound: true);
        _winOut    += BytesOf(clientTraffic, inbound: false);

        if (Time.time - _winStart >= 1f)             // roll the window: what the HUD shows is the last full second
        {
            _srvInBytes = _winSrvIn;  _srvOutBytes = _winSrvOut;  _inBytes = _winIn;  _outBytes = _winOut;
            _winSrvIn = _winSrvOut = _winIn = _winOut = 0;
            _winStart = Time.time;
        }
    }

    // FishNet keeps BidirectionalNetworkTraffic.InboundTraffic / OutboundTraffic (and their .Bytes) internal.
    private static FieldInfo _inField, _outField, _bytesField;
    private static ulong BytesOf(BidirectionalNetworkTraffic traffic, bool inbound)
    {
        if (traffic == null) return 0;
        if (_inField == null)
        {
            _inField  = typeof(BidirectionalNetworkTraffic).GetField("InboundTraffic",  BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            _outField = typeof(BidirectionalNetworkTraffic).GetField("OutboundTraffic", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        object side = (inbound ? _inField : _outField)?.GetValue(traffic);
        if (side == null) return 0;
        if (_bytesField == null) _bytesField = side.GetType().GetField("Bytes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return _bytesField == null ? 0 : (ulong)_bytesField.GetValue(side);
    }

    private void OnGUI()
    {
        if (_nm == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 420), GUI.skin.box);
        GUILayout.Label("<b>PONG — Week 3: Connect and Replicate</b>", Rich());

        // --- server ---
        if (_serverState == LocalConnectionState.Stopped)
        {
            if (GUILayout.Button("Start Server")) _nm.ServerManager.StartConnection();
        }
        else if (GUILayout.Button($"Stop Server  [{_serverState}]"))
        {
            _nm.ServerManager.StopConnection(true);
        }

        // --- client ---
        GUILayout.BeginHorizontal();
        GUILayout.Label("Server address", GUILayout.Width(100));
        address = GUILayout.TextField(address);
        GUILayout.EndHorizontal();

        if (_clientState == LocalConnectionState.Stopped)
        {
            if (GUILayout.Button("Start Client")) _nm.ClientManager.StartConnection(address, port);
        }
        else if (GUILayout.Button($"Stop Client  [{_clientState}]"))
        {
            _nm.ClientManager.StopConnection();
        }

        // --- status ---
        GUILayout.Space(6);
        string stats = _traffic == null ? "  (statistics disabled: StatisticsManager ▸ Network Traffic ▸ Enable Mode)" : "";
        if (_serverState == LocalConnectionState.Started)
        {
            GUILayout.Label($"Server: {_nm.ServerManager.Clients.Count} client(s) on UDP {port}");
            GUILayout.Label($"Server traffic: in {_srvInBytes} B/s, out {_srvOutBytes} B/s{stats}");
        }
        if (_clientState == LocalConnectionState.Started)
        {
            GUILayout.Label($"Client: my id = {_nm.ClientManager.Connection.ClientId}");
            GUILayout.Label($"RTT (ping): {_nm.TimeManager.RoundTripTime} ms");
            GUILayout.Label($"Client traffic: in {_inBytes} B/s, out {_outBytes} B/s{stats}");
        }
        GUILayout.Label($"Tick {_nm.TimeManager.Tick}  @ {_nm.TimeManager.TickRate} ticks/s");

        // --- your panels: score, serve, feed, whatever your scripts registered ---
        for (int i = 0; i < Panels.Count; i++)
        {
            string text;
            try { text = Panels[i]?.Invoke(); }
            catch (Exception) { Panels.RemoveAt(i); i--; continue; }     // the object behind it is gone: drop the panel
            if (string.IsNullOrEmpty(text)) continue;
            GUILayout.Space(6);
            GUILayout.Label(text, Rich());
        }

        if (Time.time < _flashUntil)
        {
            GUILayout.Space(6);
            GUILayout.Label($"<b><color=#ffcc55>{_flash}</color></b>", Rich());
        }
        GUILayout.EndArea();
    }

    private static GUIStyle Rich()
    {
        GUIStyle s = new GUIStyle(GUI.skin.label) { richText = true };
        return s;
    }
}
