# Tournament Timing API

Read-only HTTP API that gives a tournament organizer's **backend** the complete lap history of a **keyed live room** on FRServer, and lets it poll for new laps incrementally. Designed for building live timing / results websites.

Call it **server-to-server** from your backend. Do not call it from a browser: your API key would be exposed, and the API is plain HTTP.

> **Getting an API key:** access requires your own dedicated API key. Tournament organizers can apply for one by contacting the FR Legends team at **info.frlegends [at] gmail [dot] com**. Keep the key private and do not share it between organizers.

Sector times (`sectors`) are only available on tracks whose author set up sectors; see the [Lap Timing Tutorial](../LapTiming.md#6-sectors). They are not displayed in the game client; this API is the only place they are delivered.

## 1. Basics

- Base URL: `http://<server-ip>:56102`, where `<server-ip>` is the IP of the game server (region) hosting your room. Look it up with your API key — see [Finding the server IP](#finding-the-server-ip) below.
- Auth: header `X-Api-Key: <your key>`. Keys are issued per organizer (see above for how to apply) and can be revoked.
- Only rooms created **with a Room Key** and still alive can be queried — see [What is the Room Key?](#what-is-the-room-key) below. When the room is closed (last player leaves) its data is gone: keep your own copy by polling.
- All times are server UTC milliseconds. All lap/sector times are milliseconds (integers). Player `id` is a **string** (64-bit).
- Lap times are reported by the game client and only format-checked by the server. There is no anti-cheat, no invalid-lap flag, no race position / interval (the server does not know track position).

### Finding the server IP

Each game server region has its own IP, and a room lives on the server of the region the host was connected to when creating it. Server IPs can change, so do not hard-code them: look the current IP up with the same API key.

```
GET https://twinturbogames.com/api/servers.php?region=<Region>
X-Api-Key: <your key>
```

```bash
curl -H "X-Api-Key: <your key>" "https://twinturbogames.com/api/servers.php?region=EastAsia"
```

Response (`200`):

```json
{ "ip": "203.0.113.10" }
```

Then use `http://<ip>:56102` as the base URL for the endpoints in section 2.

`region` is case-sensitive and must be one of:

| `region` | Server |
|---|---|
| `EastAsia` | East Asia |
| `SoutheastAsia` | Southeast Asia |
| `JapanWest` | Japan West |
| `AustraliaSoutheast` | Australia Southeast |
| `NorthEurope` | North Europe |
| `WestEurope` | West Europe |
| `EastUs` | East US |
| `WestUs` | West US |
| `BrazilSouth` | Brazil South |

Errors of this lookup (shape `{ "error": "<message>" }`; these are separate from the Timing API errors in section 3):

| HTTP | `error` | Meaning |
|---|---|---|
| 400 | `missing region` | No `region` parameter |
| 401 | `unauthorized` | Missing or unknown `X-Api-Key` |
| 404 | `unknown region` | `region` is not in the list above (check spelling and case) |

Unlike the Timing API itself, this lookup is served over **HTTPS**. Still call it from your backend only, so your API key stays private. Resolve the IP once when your backend starts (and again if the Timing API stops answering) rather than before every request.

### What is the Room Key?

The **Room Key** is what the host types on the game's **Create Room** screen: tick the **Room Key** checkbox and enter a key (`8054` in the screenshot). That same value is the `{roomKey}` in the endpoint URLs below — it is how your backend tells the server *which room* it wants.

![Room Key field on the Create Room screen](../images/lap/roomkey.jpg)

- A room created **without** the Room Key checkbox ticked cannot be queried at all.
- The host of your event must create the room with the key you agreed on, and your backend must use exactly the same key.

Do not confuse the two keys:

| | Room Key | API key |
|---|---|---|
| What it is | Identifies **one room** | Identifies **you**, the organizer |
| Who sets it | The room host, in the game's Create Room screen | Issued to you by the FR Legends team |
| Where it goes | In the URL: `/v1/rooms/{roomKey}/...` | In the `X-Api-Key` request header |
| Who knows it | You and the room host | **Only you** — keep it secret, on your backend |

## 2. Endpoints

### `GET /v1/rooms/{roomKey}/snapshot`

Everything the server currently holds for the room. URL-encode `roomKey`.

```json
{
  "epoch": 1757849000123,
  "seq": 1532,
  "serverTime": 1757849123456,
  "room": { "key": "cup2026-q1", "name": "Cup 2026 Q1", "hostName": "Tony", "state": "Running", "mode": "Contest",
            "mapId": 3, "customMapId": "", "customMapTitle": "", "playerCount": 8, "audienceCount": 2 },
  "players": [
    {
      "id": "76561198000000001", "name": "Tony",
      "laps": 42, "bestMs": 61234, "bestCar": { "model": 5, "name": "GT-R", "hp": 560 },
      "lastActiveAt": 1757849120000,
      "entries": [
        { "seq": 1, "lap": 1, "ms": 62001, "sectors": [20500, 21001, 20500], "completedAt": 1757849001000 }
      ]
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `epoch` | Identifies the current history. Changes whenever the server resets the room's history (room closed and key reused, or the host cleared the records). |
| `seq` | Highest lap sequence number in the room. Use it as `since` in your next `/laps` call. `0` when there are no laps. |
| `room.state` | `Wait`, `Prepare`, `Starting`, `Running`. Laps are only recorded while `Running`. |
| `room.mode` | `Free`, `Battle`, `Contest`. |
| `room.mapId` | `0` means a custom map; see `customMapId` / `customMapTitle`. |
| `players[]` | Every player the server still remembers for this room, sorted by `id`, including players who left (kept for up to 1 hour after leaving unless they are on the top-16 board). |
| `laps` | Total completed laps in this room. Can exceed `entries.length`: the server keeps at most 2000 laps per player and drops the oldest. |
| `bestMs` | Best lap in this room. `bestCar` is the car used on that lap; `null` if no best yet. |
| `entries[]` | Per-lap records, ascending by `seq`. `lap` is the lap number; `sectors` is `[]` on tracks without sectors. |

A player the server has forgotten (left more than 1 hour ago and not on the top-16 board) disappears from `players[]`; keep or drop your local copy as you see fit.

Snapshot is limited to **1 per minute per API key and room** (burst 3). It is meant for the first fetch and for recovery; use `/laps` for updates.

### `GET /v1/rooms/{roomKey}/laps?epoch=E&since=S`

Laps with `seq > S`, grouped by player. Only players that have at least one new lap appear.

```json
{
  "epoch": 1757849000123, "seq": 1540, "serverTime": 1757849130000, "resync": false,
  "room": { "...": "same as snapshot" },
  "players": [
    { "id": "76561198000000001", "name": "Tony", "laps": 43, "bestMs": 61234,
      "bestCar": { "model": 5, "name": "GT-R", "hp": 560 }, "lastActiveAt": 1757849129000,
      "entries": [ { "seq": 1538, "lap": 43, "ms": 61900, "sectors": [20300, 21100, 20500], "completedAt": 1757849129000 } ] }
  ]
}
```

- `players[]` contains **only the players with new laps**, each with a refreshed header (`name`, `laps`, `bestMs`, `bestCar`, `lastActiveAt`) and the new `entries`. Upsert by `id` into your local copy. When nothing happened since `S`, `players` is `[]`.
- `409 { "error": "epoch_changed", "epoch": <current> }`: the history was reset. Fetch a new snapshot. Whether you keep the previous epoch's data on your site is your decision; store laps keyed by `(epoch, seq)` so they never collide.
- `"resync": true` (with `"players": []`): laps you have not seen were already discarded by the server (you were away too long). Fetch a new snapshot.
- Rate limit: 2 requests per second per key (burst 5), at most 2 concurrent requests per key. Poll every 1–2 seconds.

## 3. Errors

All errors have the shape `{ "error": "<code>", ... }`.

| HTTP | `error` | Meaning |
|---|---|---|
| 400 | `bad_request` | `epoch` / `since` missing or not a non-negative integer |
| 401 | `invalid_api_key` | Missing or unknown `X-Api-Key` |
| 404 | `room_not_found` | Unknown path, or no live room with that key |
| 405 | `method_not_allowed` | Only `GET` is supported |
| 409 | `epoch_changed` | See above; body includes the current `epoch` |
| 429 | `rate_limited` | Too many requests; honor the `Retry-After` header (seconds). The body also carries `retryAfter` (seconds), same value, for clients that cannot read headers |
| 429 | `snapshot_abuse` | Too many snapshots (per API key and room); poll `/laps` instead. Body has `message` and `retryAfter`, and the response also sets the `Retry-After` header |
| 503 | `busy` | Server queue full or game loop did not answer in time; retry after a second |

## 4. Recommended polling loop

```
loop:
  if no cursor:
      r = GET /snapshot                       # once
      store everything; epoch, cursor = r.epoch, r.seq
  r = GET /laps?epoch=<epoch>&since=<cursor>
  if 409 or r.resync:  forget cursor; continue
  if 429 or 503:       sleep Retry-After (or 1s); continue
  for each player in r.players:
      upsert player header; append player.entries
  cursor = r.seq
  sleep 1-2 s
```

See `timing_poll.py` (Python 3, standard library only) for a complete implementation that prints the full standings to the console whenever new laps arrive.

## 5. What you can build from this data

Best-lap leaderboard (sort players by `bestMs`), gap to leader (`bestMs - leader.bestMs`), last lap (highest `seq` per player), lap counts, per-sector personal/overall bests (purple/green highlighting), lap-by-lap analysis per driver, consistency stats, and a "new lap / new best" ticker from each incremental response.

## Credits

- Idea originally requested by **FGCS – FRL GT Championship Series** — Instagram: [https://www.instagram.com/fgcs.league/](https://www.instagram.com/fgcs.league/)
- **kinkpedil12 (Fadly Alfarizy)** — project **FRLcast**: [https://www.frlcast.my.id](https://www.frlcast.my.id)
