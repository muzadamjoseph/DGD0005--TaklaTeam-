# Week 3 — Homework 1: Pong connects and replicates

**TaklaTeam:** Efe Can Eser · Bora Deniz Nural · Yusuf Arda Göçer — Unity 6000.3.2f1 · FishNet 4.7.x (free) · Tugboat UDP 7770 · tick rate 30

Scripts I wrote (all in `Pong/Assets/Scripts/Network/`):

| Script | Kind | Lives on | Job |
|---|---|---|---|
| `ConnectionLogger.cs` | MonoBehaviour | NetworkManager | logs server / remote-client / client connection events |
| `PaddleController.cs` | NetworkBehaviour | Paddle prefab (player prefab) | owner input, `Side` SyncVar, `RequestServe` / `AnnounceGoal` / `YouServe` RPCs |
| `NetworkBall.cs` | NetworkBehaviour | Ball prefab | server-only ball movement, `InPlay` SyncVar, `Serve()` |
| `BallSpawner.cs` | NetworkBehaviour | Court (scene NetworkObject) | `OnStartServer`: Instantiate + `Spawn` the one ball |
| `GameState.cs` | NetworkBehaviour | GameState (scene NetworkObject) | scores, serving side, event feed, `Goal()`, `Announce()` |

Project notes: `ServerManager ▸ Start On Headless` off, `ServerBootstrap` + `ConnectionLogger` + `PlayerSpawner` on the NetworkManager, Statistics on (Development, Update Client + Update Server), Run In Background on. *URP/Unlit* is in **Graphics ▸ Always Included Shaders**: without it a player build strips the shader `Court.Paint` finds by name, and `Court.Awake` / `NetworkBall.Awake` throw (found while measuring Step 10 with builds).

---

## Checkpoint D (Step 4)

1. **Which component gives the `ObjectId`?** The **`NetworkObject`** component. When the server spawns the paddle it assigns the ObjectId and sends it with the spawn message, so every machine refers to the same paddle by the same number.
2. **Which machine chose LEFT, how did the client learn it?** The **server**, in `PaddleController.OnStartServer` (first free side → `Side.Value = 0`). The client learned it because `Side` is a **`SyncVar<int>`**: the current value travels with the spawn message, and `Side.OnChange` → `Place()` moves and colours the paddle on the client.
3. **Which line stops other clients moving your paddle?** The first line of `Update`: `if (!IsOwner) return;` — only the machine that owns the paddle reads its keyboard for it.

---

## Predictions (written before Step 6) + grades (after Step 10)

| # | Prediction | Grade |
|---|---|---|
| 1 | You press W and nothing moves elsewhere: **my client must tell the server my paddle's new position, then the server must tell every other client** — client → server → other clients, every tick. | **Right** — NetworkTransform (client authoritative) does exactly that. |
| 2 | Three balls: **the server should own the one real ball**; a client only needs to **receive the ball's position (and whether it is in play)** to draw it. | **Right** — Step 7: server moves it, NetworkTransform sends position, `InPlay` SyncVar sends the state. |
| 3 | If the server trusts "my paddle is at z = 3.2" from a client, the first cheat is **teleporting the paddle** — send any z, e.g. always exactly where the ball is, faster than the paddle could move. | **Right** — client-authoritative NetworkTransform relays whatever the owner sends; nothing on the server checks speed. (Week 4 fixes this.) |

---

## Connection card (Step 11)

| Direction | Partner | Network | Server IP | My ClientId + side | RTT | Worked first try? |
|---|---|---|---|---|---|---|
| Editor: server + client in one process | — | loopback | 127.0.0.1 | client 0, LEFT; restarted → client 1, LEFT, score 1 : 0 kept | 33 ms | yes |
| Builds: 1 server + 2 client processes, one laptop | — | loopback | 127.0.0.1 | client 0 LEFT, client 1 RIGHT | 7–33 ms (tick 30), 40–100 ms (tick 10), ≈17 ms (tick 60) | yes |
| **Three laptops — Efe is server** (+ his own spectator client) | Yusuf Arda Göçer, Bora Deniz Nural | Tailscale virtual network over the Internet | 100.89.114.56 (Efe's Tailscale IP), UDP 7770 | Efe: client 3, SPECTATOR (local client on 127.0.0.1) | 33 ms | yes |
| ↳ guest Yusuf | Efe (server) | Tailscale | 100.89.114.56 | client 1, LEFT (blue) | 33 ms | yes |
| ↳ guest Bora | Efe (server) | Tailscale | 100.89.114.56 | RIGHT (orange) | — | yes |

Result: both paddles moved on every screen, serve / rally / goals worked, score and feed identical everywhere (played up to LEFT 3 : 4 RIGHT); server HUD: 3 clients on UDP 7770, server traffic in 284 B/s, out 1,506 B/s at tick 30. Screenshots: [server](screenshots/server_efe_spectator.png), [Yusuf, LEFT](screenshots/client_yusuf_left_1.jpg), [Yusuf, rally](screenshots/client_yusuf_left_2.jpg).

**Swapped roles (Yusuf or Bora as server): tried, did not work** — the Pong build crashed on their laptops when they started the server, so only Efe's laptop could host. Not solved yet; next step is to read their `Player.log` (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Pong\Player.log`) to see why.

> We live in different cities, so instead of one phone hotspot we joined one **Tailscale** network (free virtual LAN; every PC gets a 100.x.y.z address) and ran the Pong build on each laptop. Firewall: Unity/Pong allowed on Private + Public networks.

---

## Design sheet (before Step 7)

| THING | STATE OR EVENT? | FISHNET PIECE | ON WHICH OBJECT? | WHO WRITES / CALLS? |
|---|---|---|---|---|
| Ball position | State | NetworkTransform (client auth off) | Ball | Server |
| Ball in play? | State | `SyncVar<bool> InPlay` | Ball | Server |
| Two scores | State | `SyncVar<int>` ×2 | GameState | Server |
| Whose serve | State | `SyncVar<int> ServingSide` | GameState | Server |
| "Let me serve" | Event | `[ServerRpc] RequestServe` | Paddle | Owner client |
| "LEFT scored!" to all | Event | `[ObserversRpc] AnnounceGoal` | Paddle (scorer) | Server |
| "YOU SERVE" to one | Event | `[TargetRpc] YouServe` | Paddle (conceder) | Server |
| Event feed | State | `SyncList<string> Feed` | GameState | Server |

The one ball is created by **`BallSpawner`** on the **Court** scene object, in **`OnStartServer`**: it `Instantiate`s the Ball prefab at `(0, Court.BallY, 0)` and calls `Spawn(...)`, so it exists once and every client — also a late one — gets a copy. The first line of the ball's `Update` is **`if (!IsServerInitialized) return;`**, so only the server moves it.

---

## Step 8 answers

1. **Hit sound on all clients:** `[ObserversRpc]` (server → every observer). **"Not your serve" to one player:** `[TargetRpc]` (server → one connection).
2. **Where is the score, who may change it?** In `GameState`, two `SyncVar<int>` (`LeftScore`, `RightScore`). **Only the server** (`Goal()` returns unless `IsServerInitialized`); clients only read them. Because it is state, a client that joins late sees the current score at once — tested: stop a client at 1 : 0, restart it, it shows 1 : 0.
3. **Ball bounced on the server but passed through on my screen — which paddle copy did the server use, why different?** The server used **its own copy of that paddle — the position the owner last sent**, a tick or two old. My screen draws the ball **interpolated two ticks in the past** plus network delay, while my own paddle is drawn *now*. The two pictures are of different moments; the server's is the truth and my screen is early.

---

## Step 10 — bytes and tick rate

**How measured:** one laptop, three player processes of a development build — 1 server (`-server`) + 2 clients (LEFT, RIGHT) on 127.0.0.1, both paddles moving continuously, ball always in play (served again 0.3 s after each goal). Bytes from FishNet's StatisticsManager (the same counters the HUD shows), one-second windows, read after ~10 s. "Still frames" = frames in which the ball / the other paddle did not move at all on a client screen (~500 fps) while it was in motion.

| TICK RATE | SERVER OUT B/S (RALLY) | BALL IN A CLIENT WINDOW | OTHER PADDLE |
|---|---|---|---|
| 10 | **≈ 510** (414–631); server in ≈ 290; each client in ≈ 250 | **steps**: stands still in up to 30 % of frames (e.g. 130 of 430), visibly jerky; RTT 40–100 ms | glides most of the time, freezes now and then (98 still frames in one second); late |
| 30 | **≈ 1,370** (1,344–1,415); server in ≈ 860; each client in ≈ 685 | smooth: 0 still frames during flight; RTT 7–33 ms | smooth |
| 60 | **≈ 2,800** (2,743–2,830); server in ≈ 1,695; each client in ≈ 1,400 | smooth: 0 still frames during flight; RTT ≈ 17 ms | smooth (a few still frames out of ~500) |

- **What did 60 cost?** ≈ **2× the bytes of 30** and ≈ 5.5× of 10 (2,800 vs 1,370 vs 510 B/s server out) — every tick the server sends the ball and the paddles to every client, so bandwidth grows with tick rate × objects × clients, plus twice the server work per second.
- **What did it buy?** Fresher state: a snapshot every 16.7 ms instead of 33 ms, a shorter interpolation delay and a lower RTT; but at 30 the ball was already smooth on screen, so the visible gain over 30 is small.
- **Why did the ball step even for the player moving a paddle?** The ball is moved **only on the server**; every client — including the one whose paddle is moving smoothly, because that paddle is local — only ever gets the ball as 10 snapshots per second. When a snapshot arrives late, interpolation runs out and the ball waits. No client moves the ball itself.

**Grades of my predictions:** 1 right · 2 right · 3 right (see the table above).

Final setting: **tick rate 30**.

---

## Answers to notes Section 14

**1. Why can two machines never share a "now"?** Information cannot travel faster than light in glass fibre (~200,000 km/s) and it queues in routers, so every machine only knows the others' past. Istanbul → Frankfurt (~1,900 km) is at least 9.5 ms one way / 19 ms round trip, in practice **35–50 ms RTT ≈ 2–3 frames at 60 fps** (one frame = 16.7 ms).

**2. Topology grid.**

| | Dedicated server | Listen server | Peer-to-peer |
|---|---|---|---|
| **Trust** | a headless server nobody plays on | the host player's machine | nobody; peers agree by rule |
| **Cost** | the studio pays per server, every month | free; the host's upload pays | free |
| **Latency** | everyone has some ping to a neutral point | host 0 ms, guests ping the host | direct, when NAT allows |

Pong today is a **dedicated server**. In Step 5 the server was **Multiplayer Play Mode Player 2, tagged `Server`** (started by `ServerBootstrap`); in Step 11 it was **the Pong build on Efe's laptop** (Start Server, reached at 100.89.114.56 over Tailscale), and Yusuf's and Bora's laptops were the clients.

**3. Why UDP?** TCP guarantees order, so one lost packet blocks everything behind it until it is resent — a ball position from 100 ms ago is worthless and not worth waiting for. Games use UDP and rebuild reliability only for the messages that need it. In Pong **reliable**: a goal / the score (`AnnounceGoal`, the score SyncVars, `RequestServe`); **unreliable**: the ball's position every tick (NetworkTransform).

**4.** A **NetworkObject** gives an object a network identity (the same ObjectId everywhere), an owner and the ability to be spawned/despawned on every machine. Inheriting **NetworkBehaviour** gives a script the network questions (`IsOwner`, `IsServerInitialized`…), the callbacks (`OnStartServer`, `OnStartClient`…) and the right to hold SyncVars and RPCs. The **Hud has neither**: it is a local MonoBehaviour that draws this machine's own buttons and numbers — nothing about it has to exist on, or be synchronised with, other machines.

**5.** Same Paddle prefab, owned by client 0:

| | Server | Client 0 (owner) | Client 1 |
|---|---|---|---|
| `IsOwner` | false | **true** | false |
| `IsServerInitialized` | **true** | false | false |
| callback | `OnStartServer` | `OnStartClient` | `OnStartClient` |
| keyboard | never | **reads it** | must not |

**6.** The **NetworkTransform** on the Paddle prefab. Path: **owner's client → server → every other client**. The setting is **Client Authoritative** (on for the paddle: its owner is trusted this week). On the **Ball** it is off, because the ball has no owner to trust — the server moves it and everybody receives the server's position.

**7.** In Step 5 every process ran `Court.Awake` with *Local Ball* ticked, so each built its own private, non-networked ball: three windows → three balls → three matches. `BallSpawner.OnStartServer` runs **only on the server, once**; it creates one ball and `Spawn` tells every client to create a copy of the same NetworkObject. If a client called `Spawn`, FishNet refuses it (only the server may spawn; it logs a warning) — at best the client gets a local, unsynchronised object nobody else sees.

**8.**

| Item | State / event | Where |
|---|---|---|
| the score | state | SyncVar |
| "LEFT scored" | event | RPC (ObserversRpc) |
| whose serve it is | state | SyncVar |
| "you serve" | event | RPC (TargetRpc) |
| ball-hits-paddle sound | event | RPC (ObserversRpc) |
| whether the ball is in play | state | SyncVar |
| the ball's position | state | NetworkTransform (a continuously-sent state) |

**9.** The **server** decides, in `NetworkBall.Update`, using **its own copies of the paddles** (the positions their owners last sent, a tick or two old); only one paddle can be "towards" the ball, and the server's answer is final. The client whose paddle "clearly touched it" sees the ball pass through (or bounce late) — its screen was ahead of the server. If a client wrote `InPlay.Value` itself, only its own copy would change, the server would never know (no serve, no movement), and the value would snap back at the next server write — and if clients *were* allowed, anyone could serve whenever they liked: a cheat.

**10.** One client receives **8 × 30 × 40 = 9,600 B/s ≈ 9.6 kB/s**; with 4 clients the server sends **4 × 9,600 = 38,400 B/s ≈ 38 kB/s**. In my Step 10 table tick rate 10 cut the server's output to about a third (≈ 510 vs 1,370 B/s) and made the ball step (still in up to 30 % of frames); my own paddle did not change because it is moved **locally by my keyboard every frame** (client authoritative) — the tick rate only changes how often *others* hear about it.

---

## Vocabulary (notes Section 13, for the demo)

| Word | Meaning, in my words |
|---|---|
| topology | Who connects to whom and which machine is trusted. |
| dedicated server | A server process nobody plays on; every player is a client of it. |
| listen server | One player's game is also the server (the host); trusted, zero ping for them. |
| peer-to-peer | Players connect to each other directly; nobody is the referee. |
| client | A player's game: sends what the player wants, shows what the server says. |
| host | One process that is server and client at the same time. |
| connection | One client's link to the server. |
| ClientId | The small number FishNet gives each connection, starting at 0. |
| tick | One fixed simulation step of the server. |
| tick rate | Ticks per second (30 here); how often state is sent. |
| packet | One chunk of bytes on the network, a few hundred bytes, below the ~1,200 B MTU. |
| UDP vs. TCP | UDP throws packets (may be lost/late); TCP guarantees order and waits for lost bytes. Games use UDP. |
| reliable / unreliable | Reliable = guaranteed and ordered, costs latency (goals); unreliable = may be lost, always fresh (positions). |
| RTT / ping | Time for a message to go to the server and back; half is about the one-way delay. |
| bandwidth | Bytes per second sent/received. |
| NetworkManager | The one switchboard component that starts server/client and holds the sub-managers. |
| transport | The layer that moves the bytes (Tugboat = plain UDP); swappable without changing game code. |
| NetworkObject | Gives an object a network identity, an owner and spawning. |
| ObjectId | The id of a NetworkObject, the same on every machine. |
| NetworkBehaviour | Base class for scripts on networked objects: IsOwner, callbacks, SyncVars, RPCs. |
| scene object | A NetworkObject already in the scene; the server spawns it for every client, no owner. |
| prefab | A saved object template; networked prefabs are registered in DefaultPrefabObjects. |
| spawn | The server creating a networked object and telling every client to create its copy. |
| PlayerSpawner | Spawns the player prefab once per connection, owned by that connection. |
| owner | The client connection allowed to control an object (or nobody → server). |
| ownership | Who the owner is: decides IsOwner, who may call ServerRpcs, whose NetworkTransform sends. |
| observer | A client allowed to see an object and receive its updates. |
| replication | Copying state from the machine that knows it to the machines that need it. |
| state vs. event | State has a current value (score) → SyncVar; an event happens once (goal) → RPC. |
| SyncVar | A field only the server writes; every machine mirrors it, late joiners get it. |
| SyncList | A SyncVar for a whole list; sends operations, not the list. |
| OnChange | Callback that fires on every machine when a SyncVar changes. |
| NetworkTransform | Component that sends position (rotation, scale) every tick and interpolates it. |
| client-authoritative | The owner decides the value and the server relays it — convenient, cheatable. |
| interpolation | Drawing an object between the last received positions, a couple of ticks behind, so it glides. |
| ServerRpc | Method called on a client, runs on the server: a request. |
| ObserversRpc | Called on the server, runs on every observing client: an event for all. |
| TargetRpc | Called on the server, runs on one client: a private message. |
| RequireOwnership | ServerRpc option; `false` lets non-owners call it (FishNet passes the caller's connection). |
| serialization | Turning values into bytes to send, and back. |
| interest management | Sending each client only what is relevant to it (near, same scene/team). |
