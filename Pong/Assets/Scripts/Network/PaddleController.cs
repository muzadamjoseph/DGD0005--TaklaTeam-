// Pong — Step 4 (+ Step 8 RPCs). The networked paddle, spawned once per client by PlayerSpawner.
// The OWNER reads the keyboard and moves it; NetworkTransform (client authoritative) copies the
// position to the server and from there to everybody else (Notes 4.3–4.7).
// The SERVER chooses the side and writes it into a SyncVar; every client reads it from there.
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

public class PaddleController : NetworkBehaviour
{
    public const int Left = 0, Right = 1, Spectator = 2;

    /// Every paddle this process knows about (server copies and client copies; one entry per object).
    public static readonly List<PaddleController> All = new List<PaddleController>();
    /// The paddle this client owns, or null.
    public static PaddleController Local;

    /// 0 = LEFT, 1 = RIGHT, 2 = spectator. Written by the server only.
    public readonly SyncVar<int> Side = new SyncVar<int>(Spectator);

    [SerializeField] private float speed = 8f;

    public static string SideName(int side) => side == Left ? "LEFT" : side == Right ? "RIGHT" : "SPECTATOR";

    private void Awake()
    {
        Side.OnChange += OnSideChanged;
    }

    private void OnDestroy()
    {
        Side.OnChange -= OnSideChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!All.Contains(this)) All.Add(this);

        // first free side; both taken -> spectator
        bool leftTaken = false, rightTaken = false;
        foreach (PaddleController p in All)
        {
            if (p == this) continue;
            if (p.Side.Value == Left) leftTaken = true;
            if (p.Side.Value == Right) rightTaken = true;
        }
        Side.Value = !leftTaken ? Left : !rightTaken ? Right : Spectator;

        Debug.Log($"[Server] client {Owner.ClientId} gets side {SideName(Side.Value)}");
        GameState.Announce($"Client {Owner.ClientId} joined as {SideName(Side.Value)}");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        All.Remove(this);
        GameState.Announce($"Client {Owner.ClientId} ({SideName(Side.Value)}) left");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!All.Contains(this)) All.Add(this);

        if (IsOwner)
        {
            Local = this;
            Debug.Log($"[Client] I am client {Owner.ClientId}, side {SideName(Side.Value)}");
        }
        Place();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        All.Remove(this);
        if (Local == this) Local = null;
    }

    private void OnSideChanged(int prev, int next, bool asServer)
    {
        Place();
    }

    /// Puts the paddle on its side of the court and colours it. Spectators are invisible.
    private void Place()
    {
        int side = Side.Value;
        float x = side == Right ? Court.PaddleX : -Court.PaddleX;
        Vector3 p = transform.position;
        transform.position = new Vector3(x, 0.5f, p.z);

        Court.Paint(gameObject, side == Right ? new Color(1f, 0.60f, 0.25f) : new Color(0.35f, 0.65f, 1f));
        Renderer r = GetComponent<Renderer>();
        if (r != null) r.enabled = side != Spectator;
    }

    private void Update()
    {
        if (!IsOwner) return;               // only the owner's machine reads its keyboard for this paddle

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame) RequestServe();

        if (Side.Value == Spectator) return;

        float dir = 0f;                     // one keyboard per machine: W/S or the arrows, both work
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   dir += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir -= 1f;

        transform.position += Vector3.forward * dir * speed * Time.deltaTime;
        Court.ClampPaddle(transform);
    }

    // ---------- Step 8: three RPCs ----------

    /// Client -> server: "let me serve". The server decides whether that is allowed.
    [ServerRpc]
    private void RequestServe()
    {
        GameState gs = GameState.Instance;
        NetworkBall ball = NetworkBall.Instance;
        if (gs == null || ball == null) return;
        if (Side.Value != gs.ServingSide.Value || ball.InPlay.Value) return;

        ball.Serve(1 - Side.Value);
        GameState.Announce($"{SideName(Side.Value)} serves");
    }

    /// Server -> every client: who scored and the new score.
    [ObserversRpc]
    public void AnnounceGoal(int left, int right)
    {
        ConnectionHud.Flash($"{SideName(Side.Value)} scored! {left} : {right}");
    }

    /// Server -> one client (the conceder): it is your serve now.
    [TargetRpc]
    public void YouServe(NetworkConnection target)
    {
        ConnectionHud.Flash("YOU SERVE");
    }
}
