# Week 3 — Homework 1: Pong connects and replicates

TaklaTeam

---

## Predictions (written before Step 6) 

| # | Prediction |
|---|---|
| 1 | You press W and nothing moves elsewhere: **my client must tell the server my paddle's new position, then the server must tell every other client** — client → server → other clients, every tick. |
| 2 | Three balls: **the server should own the one real ball**; a client only needs to **receive the ball's position (and whether it is in play)** to draw it. |
| 3 | If the server trusts "my paddle is at z = 3.2" from a client, the first cheat is **teleporting the paddle** — send any z, e.g. always exactly where the ball is, faster than the paddle could move. |

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
