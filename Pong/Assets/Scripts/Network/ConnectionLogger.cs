// Pong — Step 3. Logs every connection event of this process, as a server and as a client.
// Plain MonoBehaviour on the NetworkManager object. Subscribes in Start (not Awake): FishNet's
// managers are only guaranteed to be ready once every Awake has run (Notes 4.1, 5).
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionLogger : MonoBehaviour
{
    private NetworkManager _nm;

    private void Start()
    {
        _nm = InstanceFinder.NetworkManager;
        if (_nm == null) { Debug.LogError("[Client] ConnectionLogger: no NetworkManager in the scene."); return; }

        _nm.ServerManager.OnServerConnectionState += OnServerState;
        _nm.ServerManager.OnRemoteConnectionState += OnRemoteState;
        _nm.ClientManager.OnClientConnectionState += OnClientState;
    }

    private void OnDestroy()
    {
        if (_nm == null) return;
        _nm.ServerManager.OnServerConnectionState -= OnServerState;
        _nm.ServerManager.OnRemoteConnectionState -= OnRemoteState;
        _nm.ClientManager.OnClientConnectionState -= OnClientState;
    }

    // This process as a server: Starting -> Started -> Stopping -> Stopped.
    private void OnServerState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[Server] {args.ConnectionState}");
    }

    // A remote client connected to / disconnected from this server.
    private void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        string what = args.ConnectionState == RemoteConnectionState.Started ? "connected" : "disconnected";
        Debug.Log($"[Server] client {conn.ClientId} {what}. Clients: {_nm.ServerManager.Clients.Count}");
    }

    // This process as a client: Starting -> Started -> Stopping -> Stopped.
    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[Client] {args.ConnectionState}");
    }
}
