// Pong — Step 7. On the Court (a scene NetworkObject). The server creates the one ball when it
// starts; Spawn() tells every client — present and future — to create its copy.
using FishNet.Object;
using UnityEngine;

public class BallSpawner : NetworkBehaviour
{
    public NetworkObject ballPrefab;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (ballPrefab == null) { Debug.LogError("[Server] BallSpawner: no ball prefab."); return; }

        NetworkObject ball = Instantiate(ballPrefab, new Vector3(0, Court.BallY, 0), Quaternion.identity);
        Spawn(ball);
        Debug.Log("[Server] Ball spawned.");
    }
}
