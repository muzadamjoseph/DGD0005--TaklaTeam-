// Pong — Steps 7–9. The match state everybody shares: scores, whose serve, the event feed.
// A scene NetworkObject. Only the server writes; SyncVars/SyncList carry it to every client,
// including one that joins late (Notes 4.7, 6).
using System.Text;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance;

    public readonly SyncVar<int> LeftScore   = new SyncVar<int>(0);
    public readonly SyncVar<int> RightScore  = new SyncVar<int>(0);
    public readonly SyncVar<int> ServingSide = new SyncVar<int>(0);

    /// The last six things that happened. Server adds, everybody reads.
    public readonly SyncList<string> Feed = new SyncList<string>();

    private const int FeedLength = 6;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ConnectionHud.Panels.Add(Panel);
    }

    private string Panel()
    {
        if (!IsServerInitialized && !IsClientInitialized) return null;   // not connected yet

        StringBuilder sb = new StringBuilder();
        sb.Append($"<b>LEFT {LeftScore.Value} : {RightScore.Value} RIGHT</b>\n");
        bool inPlay = NetworkBall.Instance != null && NetworkBall.Instance.InPlay.Value;
        sb.Append($"Serve: {PaddleController.SideName(ServingSide.Value)}{(inPlay ? "  (in play)" : "  (press Space)")}");
        if (PaddleController.Local != null)
            sb.Append($"\nYou: {PaddleController.SideName(PaddleController.Local.Side.Value)}");
        if (Feed.Count > 0)
        {
            sb.Append("\n<i>— feed —</i>");
            foreach (string line in Feed) sb.Append("\n").Append(line);
        }
        return sb.ToString();
    }

    /// Server only. Called by NetworkBall when it crosses a goal line.
    public void Goal(int scorerSide)
    {
        if (!IsServerInitialized) return;

        // 1. state first (SyncVars) ...
        if (scorerSide == PaddleController.Left) LeftScore.Value++;
        else RightScore.Value++;
        ServingSide.Value = 1 - scorerSide;
        Announce($"{PaddleController.SideName(scorerSide)} scored. {LeftScore.Value} : {RightScore.Value}");

        // 2. ... then the events (RPCs)
        PaddleController scorer = null, conceder = null;
        foreach (PaddleController p in PaddleController.All)
        {
            if (p.Side.Value == scorerSide) scorer = p;
            else if (p.Side.Value == 1 - scorerSide) conceder = p;
        }
        if (scorer != null) scorer.AnnounceGoal(LeftScore.Value, RightScore.Value);
        if (conceder != null) conceder.YouServe(conceder.Owner);
    }

    /// Server only. Adds a line to the feed; keeps the last six.
    public static void Announce(string line)
    {
        if (Instance == null || !Instance.IsServerInitialized) return;
        Instance.Feed.Add(line);
        while (Instance.Feed.Count > FeedLength) Instance.Feed.RemoveAt(0);
    }
}
