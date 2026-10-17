using FishNet.Connection;
using FishNet.Transporting.Yak.Client;
using System;
using System.Collections.Generic;

namespace FishNet.Transporting.Yak.Server
{
    /// <summary>
    /// Creates a fake socket acting as server.
    /// </summary>
    public class ServerSocket : CommonSocket
    {
        #region Public.
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name = "connectionId">ConnectionId to get ConnectionState for.</param>
        internal RemoteConnectionState GetConnectionState(int connectionId)
        {
            if (connectionId != NetworkConnection.SIMULATED_CLIENTID_VALUE)
                return RemoteConnectionState.Stopped;

            LocalConnectionState state = _client.GetLocalConnectionState();
            return state == LocalConnectionState.Started ? RemoteConnectionState.Started : RemoteConnectionState.Stopped;
        }
        #endregion

        #region Private.
        /// <summary>
        /// Packets received from local client.
        /// </summary>
        private Queue<LocalPacket> _incoming = new();
        /// <summary>
        /// Socket for client.
        /// </summary>
        private ClientSocket _client;
        #endregion

        // PROSTART
        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal override void Initialize(Transport t, CommonSocket socket)
        {
            base.Initialize(t, socket);
            _client = (ClientSocket)socket;
        }
        // PROEND

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal bool StartConnection()
        {
            // PROSTART
            SetLocalConnectionState(LocalConnectionState.Starting, true);
            SetLocalConnectionState(LocalConnectionState.Started, true);
            //PROEND
            // A client that was waiting for the server can now finish starting.
            _client.OnLocalServerConnectionState(LocalConnectionState.Started);
            return true;
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            LocalConnectionState state = GetLocalConnectionState();
            if (state == LocalConnectionState.Stopped || state == LocalConnectionState.Stopping)
                return false;

            SetLocalConnectionState(LocalConnectionState.Stopping, true);
            _client.OnLocalServerConnectionState(LocalConnectionState.Stopping);
            ClearQueue(ref _incoming);
            SetLocalConnectionState(LocalConnectionState.Stopped, true);
            return true;
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name = "connectionId">ConnectionId of the client to disconnect.</param>
        internal bool StopConnection(int connectionId)
        {
            if (connectionId != NetworkConnection.SIMULATED_CLIENTID_VALUE)
                return false;
            return _client.StopConnection();
        }

        /// <summary>
        /// Iterates incoming packets from the local client.
        /// </summary>
        internal void IterateIncoming()
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            while (_incoming.Count > 0)
            {
                LocalPacket packet = _incoming.Dequeue();
                ArraySegment<byte> segment = new(packet.Data, 0, packet.Length);
                Transport.HandleServerReceivedDataArgs(new(segment, (Channel)packet.Channel, NetworkConnection.SIMULATED_CLIENTID_VALUE, Transport.Index));
                packet.Dispose();
            }
        }

        /// <summary>
        /// Sends data to the local client.
        /// </summary>
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;
            if (connectionId != NetworkConnection.SIMULATED_CLIENTID_VALUE)
                return;
            if (_client.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            _client.ReceivedFromLocalServer(new(segment, channelId));
        }

        #region Local client.
        /// <summary>
        /// Called when the local client connection state changes.
        /// </summary>
        internal void OnLocalClientConnectionState(LocalConnectionState state)
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;
            if (state != LocalConnectionState.Started && state != LocalConnectionState.Stopped)
                return;

            RemoteConnectionState remoteState = state == LocalConnectionState.Started ? RemoteConnectionState.Started : RemoteConnectionState.Stopped;
            Transport.HandleRemoteConnectionState(new(remoteState, NetworkConnection.SIMULATED_CLIENTID_VALUE, Transport.Index));
        }

        /// <summary>
        /// Queues data received from the local client.
        /// </summary>
        internal void ReceivedFromLocalClient(LocalPacket packet)
        {
            _incoming.Enqueue(packet);
        }
        #endregion
    }
}