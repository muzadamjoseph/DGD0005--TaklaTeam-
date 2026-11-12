# Week 5 — Pong on a host (listen server)

**Efe Can Eser** (Team Takla) — Unity 6000.3.2f1 · FishNet 4.7.4 · Tugboat UDP 7770 · tick rate 30 · Multiplayer Play Mode 2.0.2

Our Pong, unchanged as a game. What changed is who runs the server: one click on **Start Host** makes this process the server and a client at the same time. Our Pong already has Homework 2 in it (the paddle is server-authoritative, the owner only sends its input), so some of what the sheet expects on the guest may look different here.

---

## 1. Setup check

- [x] Homework 1 Pong opens and works: one ball, score, serve, feed (and Homework 2 on top).
- [x] `ProjectSettings/ProjectVersion.txt`: `6000.3.2f1`. FishNet in the project is **4.7.4** (the sheet says 4.7.3R; nothing we use is different). No Mirror, no Netcode.
- [x] Multiplayer Play Mode 2.0.2 installed. Player 2 had the `Server` tag from Week 3; removed.
- [x] ServerManager ▸ Start On Headless **off**. NetworkManager ▸ Run In Background **on**. TimeManager ▸ Tick Rate 30.
- [x] Repo `DGD0005--TaklaTeam-`, branch `dgd0005-EfeCanEser`, answers in `Week05/README.md`.

