// Week 6, Step 5 (Notes 6.1): Unity Relay for our Pong.
// The host asks Relay for a room (an allocation) and a join code for it. The guest turns the code
// back into the same room. Both connect OUT to the Relay server, which passes the packets on, so
// nobody needs the other one's IP address and nobody opens a port on a router.
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public static class RelayLink
{
    // Guests the room takes; the host itself is not counted. One opponent plus two spectators.
    private const int MaxGuests = 3;

    /// Host: book a room on Relay, get its join code, then start the server and our own client.
    public static async Task<string> StartHostAsync(NetworkManager nm)
    {
        await SignInAsync("host");

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxGuests);
        string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.Log($"[Relay] Join code: {code}");

        // The transport has to know the Relay room BEFORE it starts, or it listens on a normal port.
        UnityTransport transport = nm.TransportManager.GetTransport<UnityTransport>();
        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));

        nm.ServerManager.StartConnection();
        nm.ClientManager.StartConnection();   // the host's own client: stays inside this process
        return code;
    }

    /// Guest: turn the code into the host's room and connect to it through the Relay.
    public static async Task StartGuestAsync(NetworkManager nm, string code)
    {
        await SignInAsync("guest");

        JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());

        UnityTransport transport = nm.TransportManager.GetTransport<UnityTransport>();
        transport.SetRelayServerData(join.ToRelayServerData("dtls"));

        nm.ClientManager.StartConnection();
        Debug.Log($"[Relay] Joined room {code}, connecting through the Relay");
    }

    // Relay only talks to a signed-in player; an anonymous one is enough. Two windows on one PC
    // would be the same player, so the host signs in under the profile "host", the guest under "guest".
    private static async Task SignInAsync(string profile)
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));

        IAuthenticationService auth = AuthenticationService.Instance;
        if (auth.IsSignedIn && auth.Profile == profile) return;
        if (auth.IsSignedIn) auth.SignOut();
        if (auth.Profile != profile) auth.SwitchProfile(profile);
        await auth.SignInAnonymouslyAsync();
        Debug.Log($"[Relay] Signed in as {profile}, player {auth.PlayerId}");
    }
}
