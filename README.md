# DGD0005--TaklaTeam-
DGD0005-Week 3-Homework 1 made by Bora Deniz Nural, Efe Can Eser and Yusuf Arda Göçer

## Pong (FishNet, dedicated server) — branch `dgd0005-YusufArdaGocer`

Team: **Efe Can Eser**, **Bora Deniz Nural**, **Yusuf Arda Göçer**.

Two-player networked Pong: one server, two players (LEFT / RIGHT), extra clients are spectators. Unity **6000.3.2f1**, FishNet 4.7.x (free), transport Tugboat (UDP **7770**).
Project: [`Pong/`](Pong) · scene `Pong/Assets/Scenes/Pong.unity` · my scripts: `Pong/Assets/Scripts/Network/` · weekly answers: [`Week03/README.md`](Week03/README.md).

### (a) The three things

Each player **owns** their paddle: only the owner's keyboard moves it, and its position travels owner → server → everyone else (NetworkTransform, client authoritative). **Nobody owns** the ball and the rules: the server alone moves the ball, decides bounces and goals, and accepts or refuses a serve request. **Everybody shares** the match state — the two scores, whose serve it is, whether the ball is in play and the event feed — written only by the server into SyncVars / a SyncList, so every client, including one that joins mid-match, sees the same numbers.

### (b) How to run

1. **Server:** open `Pong/` in Unity Hub, open `Scenes/Pong`, Play ▸ **Start Server** (or Multiplayer Play Mode Player 2 tagged `Server`, or a build started with `-server`).
2. **Client:** Play ▸ type the server's IP in *Server address* (`127.0.0.1` on the same PC) ▸ **Start Client**.
3. **Play:** W/S or ↑/↓ moves your paddle; Space serves when the HUD says it is your serve.

### What changed each week

- **Week 3:** Pong connects (NetworkManager, HUD, ConnectionLogger, Paddle prefab via PlayerSpawner, NetworkTransform) and replicates (server-moved ball, SyncVar score and serve, ServerRpc / ObserversRpc / TargetRpc, SyncList feed, tick-rate experiment).
