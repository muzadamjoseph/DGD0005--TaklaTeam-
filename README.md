# DGD0005--TaklaTeam-
DGD0005 Multiplayer Programming — **Team Takla:** Yusuf Arda Göçer, Efe Can Eser, Bora Deniz Nural

## Pong (FishNet, dedicated server or host) — branch `dgd0005-EfeCanEser, dgd0005-BoraDenizNural, dgd0005-YusufArdaGocer`

Two-player networked Pong: one server, two players (LEFT / RIGHT), extra clients are spectators. The server is either a process nobody plays on or, since Week 5, a host: one player's game is the server and a client at once. Unity **6000.3.2f1**, FishNet 4.7.x (free), transport UnityTransport (FishyUnityTransport, port **7770**, or Unity Relay with a join code since Week 6; Tugboat is still on the NetworkManager, unused).
Project: [`Pong/`](Pong) · scene `Pong/Assets/Scenes/Pong.unity` · our scripts: `Pong/Assets/Scripts/Network/` · weekly answers: [`Week03/README.md`](Week03/README.md) (Homework 1), [`Week04/README.md`](Week04/README.md) (Homework 2, plus a section on a host), [`Week05/README.md`](Week05/README.md) (Pong on a host), [`Week06/README.md`](Week06/README.md) (Homework 3: host cheats, Relay join code).

### (a) The three things

Each player **owns** their paddle, but since Week 4 owning it only means *sending its input*: the owner's keyboard produces up / down / nothing every tick, the server moves the paddle and replicates its position to everyone (NetworkTransform, server authoritative). **Nobody owns** the ball and the rules: the server alone moves the ball, decides bounces and goals, accepts or refuses a serve, and rejects inputs that are malformed or come too fast. **Everybody shares** the match state — the two scores, whose serve it is, whether the ball is in play and the event feed — written only by the server into SyncVars / a SyncList, so every client, including one that joins mid-match, sees the same numbers.

### (b) How to run

0. **Over the internet (Week 6):** host: Play ▸ **Host via Relay**, the HUD shows `JOIN CODE: ......`, send it to your partner. Guest: type the code under the button ▸ **Join via Relay**. No IP, no port forwarding. The guest has to run this repo's project (or a build of it): a code only works inside the cloud project it was made in.
1. **Host (Week 5):** open `Pong/` in Unity Hub, open `Scenes/Pong`, Play ▸ **Start Host**. You play LEFT; Stop Host ends the match for everybody.
2. **Server only:** Play ▸ **Start Server** (or a Multiplayer Play Mode player tagged `Server`, or a build started with `-server`).
3. **Client / guest:** Play ▸ type the host's or server's IP in *Server address* (`127.0.0.1` on the same PC, the IPv4 from `ipconfig` on a LAN) ▸ **Start Client**.
4. **Play:** W/S or ↑/↓ moves your paddle; Space serves when the HUD says it is your serve. Debug cheat keys (Week 4): C spam, X speed hack, T teleport, and G (Week 6) writes LEFT = 9 straight into the score. A guest's server survives all four; on a host, T and G work, which is the point of Week 6.

### What changed each week

- **Week 3:** Pong connects (NetworkManager, HUD, ConnectionLogger, Paddle prefab via PlayerSpawner, NetworkTransform) and replicates (server-moved ball, SyncVar score and serve, ServerRpc / ObserversRpc / TargetRpc, SyncList feed, tick-rate experiment).
- **Week 4:** the server decides — Paddle NetworkTransform client authority off; the owner sends only its input axis every tick (`ServerSetAxis`), the server moves and clamps the paddle; input validation (NaN, clamp, spectator); 30 ms rate limit per client with a rejection counter in the HUD; cheat keys C / X / T; extra: 1 s serve rate limit with a TOO FAST flash.
- **Week 5:** Pong on a host — Start Host / Stop Host in the HUD, a `[Host]` line in the logger, `All.Count` in the log and on the HUD (one paddle on a host is 1, not 2); when a player leaves while the server keeps running, the ball goes back to the centre and the serve moves to a side that still has a player.
- **Week 6:** Homework 3 — cheat key G; the 1 s serve limit moved after the null check and refuses with `TargetRefused(conn, "TOO FAST")`; the host's cheats tested from both windows; Multiplayer Services + FishyUnityTransport, UnityTransport on the NetworkManager; `RelayLink` (allocation, join code, anonymous sign-in with a `host` / `guest` profile); Host via Relay / Join via Relay and a big JOIN CODE on the HUD.
