// Pong — Step 3. Logs every connection event of this process, as a server and as a client.
// Plain MonoBehaviour on the NetworkManager object. Subscribes in Start (not Awake): FishNet's
// managers are only guaranteed to be ready once every Awake has run (Notes 4.1, 5).
// Week 5: on a host this process is both, so both halves log, plus one [Host] line.
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
        _nm.ClientManager.OnAuthenticated += OnAuthenticated;
    }

    private void OnDestroy()
    {
        if (_nm == null) return;
        _nm.ServerManager.OnServerConnectionState -= OnServerState;
        _nm.ServerManager.OnRemoteConnectionState -= OnRemoteState;
        _nm.ClientManager.OnClientConnectionState -= OnClientState;
        _nm.ClientManager.OnAuthenticated -= OnAuthenticated;
    }

    // This process as a server: Starting -> Started -> Stopping -> Stopped.
    private void OnServerState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[Server] {args.ConnectionState}");
    }

    // A remote client connected to / disconnected from this server.
    private void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        bool connected = args.ConnectionState == RemoteConnectionState.Started;
        // on a disconnect FishNet calls us before it removes the client from the list
        int clients = _nm.ServerManager.Clients.Count - (!connected && _nm.ServerManager.Clients.ContainsKey(conn.ClientId) ? 1 : 0);
        Debug.Log($"[Server] Client {conn.ClientId} {(connected ? "connected" : "disconnected")}. Clients: {clients}");
    }

    // This process as a client: Starting -> Started -> Stopping -> Stopped.
    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[Client] {args.ConnectionState}");

        // Checked here, in the client handler: by the time the client has started, a host's server has too.
        if (args.ConnectionState == LocalConnectionState.Started && _nm.ServerManager.Started)
            Debug.Log("[Host] This process is the server AND a client. RTT is 0 ms.");
    }

    // The server has accepted this client and sent it its ClientId.
    private void OnAuthenticated()
    {
        Debug.Log($"[Client] Authenticated, the server calls me client {_nm.ClientManager.Connection.ClientId}");
    }
}
