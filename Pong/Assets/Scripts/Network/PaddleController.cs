// Pong — Step 4 (+ Step 8 RPCs), made server-authoritative in Homework 2.
// The networked paddle, spawned once per client by PlayerSpawner.
// The OWNER only reads the keyboard and sends its input (up / down / nothing) every tick.
// The SERVER moves the paddle; NetworkTransform (client authoritative OFF) sends the server's
// position to everybody, the owner included.
// The SERVER also chooses the side and writes it into a SyncVar; every client reads it from there.
using System.Collections.Generic;
using FishNet.Component.Transforming;
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

    private float _serverAxis;              // server-only memory: the last input the server accepted
    private NetworkTransform _networkTransform;

    // Rate limit, server-only. One paddle belongs to one client, so one timestamp per paddle
    // is one timestamp per client (an instance field, never static).
    private const double MinInputGap = 0.030;   // seconds
    private double _lastInputTime = -1;
    public int RejectedInputs;
    private const double MinServeGap = 1.0;     // seconds, serve requests (extra)
    private double _lastServeTime = -1;

    public static string SideName(int side) => side == Left ? "LEFT" : side == Right ? "RIGHT" : "SPECTATOR";

    private void Awake()
    {
        _networkTransform = GetComponent<NetworkTransform>();
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

        // server window: how many inputs this client sent too fast (null = paddle gone, draw nothing)
        ConnectionHud.Panels.Add(() => this == null ? null : $"Rejected inputs (paddle {OwnerId}): {RejectedInputs}");
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

        if (kb.spaceKey.wasPressedThisFrame) ServerRequestServe();

        // ---- Step 5: the cheating client. These stay in the project; the server must survive them. ----
        if (kb.cKey.wasPressedThisFrame)                        // C: spam, 20 inputs in one frame
            for (int i = 0; i < 20; i++) ServerSetAxis(1f);
        if (kb.xKey.wasPressedThisFrame) ServerSetAxis(50f);    // X: speed hack, an impossible input
        if (kb.tKey.wasPressedThisFrame && NetworkBall.Instance != null)
            transform.position = NetworkBall.Instance.transform.position;   // T: teleport, on this client only, no RPC
    }

    // ---------- Homework 2: input up, position down ----------

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        if (TimeManager != null) TimeManager.OnTick -= OnTick;
    }

    private void OnTick()
    {
        // 1. Owner sends: only the input, every tick, also when it is 0.
        if (IsOwner && Side.Value != Spectator)
        {
            Keyboard kb = Keyboard.current;
            float axis = 0f;                // one keyboard per machine: W/S or the arrows, both work
            if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed))   axis += 1f;
            if (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed)) axis -= 1f;
            ServerSetAxis(axis);
        }

        // 2. Server moves: the only place that writes the paddle's position.
        if (IsServerInitialized)
        {
            transform.position += Vector3.forward * _serverAxis * speed * (float)TimeManager.TickDelta;
            Court.ClampPaddle(transform);
            // NetworkTransform only sends changes. Resend the server's position every tick, so a client
            // that wrote its own position (cheat key T) is corrected within one tick, even when the
            // paddle stands still. Costs at most what a moving paddle costs anyway.
            _networkTransform.ForceSend();
        }
    }

    /// Client -> server: "this is my input now". The server decides what it does with it.
    [ServerRpc]
    private void ServerSetAxis(float axis)
    {
        // well-formed? a client can send any number
        if (float.IsNaN(axis)) return;
        axis = Mathf.Clamp(axis, -1f, 1f);     // a client that sends 50 gets 1
        if (Side.Value == Spectator) return;   // a spectator has no paddle to move

        // too fast? one keyboard sends one input per tick (33 ms); anything inside 30 ms is a program
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastInputTime >= 0 && now - _lastInputTime < MinInputGap)
        {
            RejectedInputs++;
            Debug.Log($"[Server] Rejected input from client {OwnerId}: too fast.");
            return;
        }
        _lastInputTime = now;

        _serverAxis = axis;
    }

    // ---------- Step 8: three RPCs ----------

    /// Client -> server: "let me serve". The server decides whether that is allowed.
    [ServerRpc]
    private void ServerRequestServe()
    {
        // too fast? (extra, not graded) one serve request per second per client
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastServeTime >= 0 && now - _lastServeTime < MinServeGap)
        {
            TargetTooFast(Owner);
            return;
        }
        _lastServeTime = now;

        // allowed by the game rule?
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

    /// Server -> one client: your serve request came less than a second after the last one.
    [TargetRpc]
    private void TargetTooFast(NetworkConnection target)
    {
        ConnectionHud.Flash("TOO FAST");
    }

    /// Server -> one client (the conceder): it is your serve now.
    [TargetRpc]
    public void YouServe(NetworkConnection target)
    {
        ConnectionHud.Flash("YOU SERVE");
    }
}
