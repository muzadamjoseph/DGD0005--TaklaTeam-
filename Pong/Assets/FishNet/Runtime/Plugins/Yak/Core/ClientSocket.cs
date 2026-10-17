using FishNet.Transporting.Yak.Server;
using System;
using System.Collections.Generic;

namespace FishNet.Transporting.Yak.Client
{
    /// <summary>
    /// Creates a fake client connection to interact with the ServerSocket.
    /// </summary>
    public class ClientSocket : CommonSocket
    {
        #region Private.
        /// <summary>
        /// Socket for the server.
        /// </summary>
        private ServerSocket _server;
        /// <summary>
        /// Incomimg data.
        /// </summary>
        private Queue<LocalPacket> _incoming = new();
        #endregion

        // PROSTART
        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal override void Initialize(Transport t, CommonSocket socket)
        {
            base.Initialize(t, socket);
            _server = (ServerSocket)socket;
        }
        // PROEND

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        internal bool StartConnection()
        {
            // PROSTART
            // Already starting/started, or stopping.
            if (GetLocalConnectionState() != LocalConnectionState.Stopped)
                return false;

            SetLocalConnectionState(LocalConnectionState.Starting, false);
            /* Certain conditions need the client state to change as well.
             * Such as, if the server state is stopping then the client should
             * also be stopping, rather than starting. Or if the server state
             * is already started then client should immediately be set started
             * rather than waiting for server started callback. */
            LocalConnectionState serverState = _server.GetLocalConnectionState();
            if (serverState == LocalConnectionState.Stopping || serverState == LocalConnectionState.Started)
                OnLocalServerConnectionState(_server.GetLocalConnectionState());
            //PROEND
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

            SetLocalConnectionState(LocalConnectionState.Stopping, false);
            ClearQueue(ref _incoming);
            SetLocalConnectionState(LocalConnectionState.Stopped, false);
            _server.OnLocalClientConnectionState(LocalConnectionState.Stopped);
            return true;
        }

        /// <summary>
        /// Iterates incoming packets from the local server.
        /// </summary>
        internal void IterateIncoming()
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            while (_incoming.Count > 0)
            {
                LocalPacket packet = _incoming.Dequeue();
                ArraySegment<byte> segment = new(packet.Data, 0, packet.Length);
                Transport.HandleClientReceivedDataArgs(new(segment, (Channel)packet.Channel, Transport.Index));
                packet.Dispose();
            }
        }

        /// <summary>
        /// Sends data to the local server.
        /// </summary>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;
            if (_server.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            _server.ReceivedFromLocalClient(new(segment, channelId));
        }

        #region Local server.
        /// <summary>
        /// Called when the local server connection state changes.
        /// </summary>
        internal void OnLocalServerConnectionState(LocalConnectionState state)
        {
            if (state == LocalConnectionState.Started)
            {
                if (GetLocalConnectionState() != LocalConnectionState.Starting)
                    return;
                SetLocalConnectionState(LocalConnectionState.Started, false);
                _server.OnLocalClientConnectionState(LocalConnectionState.Started);
            }
            else if (state == LocalConnectionState.Stopping || state == LocalConnectionState.Stopped)
            {
                StopConnection();
            }
        }

        /// <summary>
        /// Queues data received from the local server.
        /// </summary>
        internal void ReceivedFromLocalServer(LocalPacket packet)
        {
            _incoming.Enqueue(packet);
        }
        #endregion
    }
}