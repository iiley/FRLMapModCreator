#!/usr/bin/env python3
"""Tournament Timing API poller (stdlib only).

Usage: python timing_poll.py http://SERVER_IP:56102 ROOM_KEY API_KEY [interval_seconds]

Keeps a complete local copy of the room's lap history in memory and prints the full
leaderboard to the console every time new laps arrive. Handles epoch changes / resync /
rate limiting exactly as the README recommends.
"""
import http.client
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


class TimingPoller:
    def __init__(self, base, room_key, api_key):
        self.base = base.rstrip("/")
        self.room = urllib.parse.quote(room_key, safe="")
        self.api_key = api_key
        self.epoch = None
        self.cursor = None
        self.room_info = None
        self.players = {}   # id -> header dict + "entries" list

    def _get(self, path):
        req = urllib.request.Request(self.base + path, headers={"X-Api-Key": self.api_key})
        try:
            with urllib.request.urlopen(req, timeout=10) as resp:
                return resp.status, json.loads(resp.read().decode("utf-8")), resp.headers
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", "replace")
            try:
                data = json.loads(body)
            except ValueError:
                data = {"error": "non_json", "raw": body}
            return e.code, data, e.headers
        except (urllib.error.URLError, OSError, http.client.HTTPException) as e:
            # HTTPException (e.g. BadStatusLine) does not inherit from OSError. When polling localhost on
            # the same machine, the source port can occasionally collide with the server port and cause
            # a TCP self-connect (reading back our own request line); treat it as transient and retry
            return 0, {"error": "connection_failed", "detail": str(e)}, {}

    def _apply(self, players):
        # /laps only lists players that have new laps: upsert their header by id and append entries
        for p in players:
            entries = p.pop("entries", [])
            cur = self.players.setdefault(p["id"], {"entries": []})
            cur.update(p)
            cur["entries"].extend(entries)

    def snapshot(self):
        status, data, headers = self._get(f"/v1/rooms/{self.room}/snapshot")
        if status != 200:
            return status, data, headers
        self.players = {}
        self.epoch = data["epoch"]
        self.cursor = data["seq"]
        self.room_info = data.get("room")
        self._apply(data["players"])
        return status, data, headers

    def poll_once(self):
        if self.cursor is None:
            status, data, headers = self.snapshot()
            return status, data, headers, "snapshot", 0
        status, data, headers = self._get(f"/v1/rooms/{self.room}/laps?epoch={self.epoch}&since={self.cursor}")
        new_laps = 0
        if status == 200:
            if data.get("resync"):
                self.cursor = None
                return status, data, headers, "resync", 0
            # Count before _apply pops each player's "entries", otherwise this is always 0
            new_laps = sum(len(p.get("entries", [])) for p in data["players"])
            self._apply(data["players"])
            self.room_info = data.get("room")
            self.cursor = data["seq"]
        elif status == 409:
            self.cursor = None
        return status, data, headers, "laps", new_laps

    def standings(self):
        """Players sorted by best lap; players without a lap yet go last."""
        return sorted(self.players.values(), key=lambda p: p.get("bestMs") or 1 << 62)


def fmt_ms(ms):
    if not ms:
        return "-"
    m, rest = divmod(int(ms), 60_000)
    s, milli = divmod(rest, 1000)
    return f"{m}:{s:02d}.{milli:03d}"


def fmt_sectors(entry):
    return " ".join(fmt_ms(x) for x in entry.get("sectors", [])) or "-"


def print_standings(poller):
    room = poller.room_info or {}
    rows = poller.standings()
    leader = rows[0].get("bestMs") if rows else 0
    print()
    print(f"=== {room.get('name', '?')} [{room.get('key', '?')}] state={room.get('state', '?')} mode={room.get('mode', '?')} "
          f"map={room.get('mapId', '?')} epoch={poller.epoch} seq={poller.cursor} players={len(rows)} ===")
    print(f"{'#':>2} {'name':<16} {'laps':>4} {'best':>10} {'gap':>9} {'last':>10} {'last sectors':<28} {'car':<12} {'hp':>4}")
    for i, p in enumerate(rows, 1):
        best = p.get("bestMs") or 0
        gap = "-" if i == 1 or not best else "+" + fmt_ms(best - leader)
        entries = p.get("entries", [])
        last = entries[-1] if entries else None
        car = p.get("bestCar") or {}
        last_ms = fmt_ms(last["ms"]) if last else "-"
        last_sec = fmt_sectors(last) if last else "-"
        print(f"{i:>2} {p.get('name', '')[:16]:<16} {p.get('laps', 0):>4} {fmt_ms(best):>10} {gap:>9} "
              f"{last_ms:>10} {last_sec:<28} {car.get('name', '')[:12]:<12} {str(car.get('hp', '')):>4}")
    print()


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(2)
    base, room_key, api_key = sys.argv[1:4]
    interval = float(sys.argv[4]) if len(sys.argv) > 4 else 2.0
    poller = TimingPoller(base, room_key, api_key)
    while True:
        status, data, headers, kind, new_laps = poller.poll_once()
        if status == 200 and kind == "resync":
            print("resync requested -> refetching snapshot")
            continue
        elif status == 200:
            if kind == "snapshot" or new_laps > 0:
                what = f"{new_laps} new laps" if kind == "laps" else f"{len(poller.players)} players"
                print(f"{time.strftime('%H:%M:%S')} {kind}: {what} -> full standings:")
                print_standings(poller)
            else:
                print(f"{time.strftime('%H:%M:%S')} laps: seq={poller.cursor} no new laps")
            time.sleep(interval)
        elif status == 409:
            print("epoch changed -> refetching snapshot")
        elif status in (429, 503):
            wait = float(headers.get("Retry-After") or data.get("retryAfter") or 1)
            print(f"{status} {data.get('error')}: {data.get('message', '')} waiting {wait}s")
            time.sleep(wait)
        elif status == 404:
            print("room not found (not created yet, or closed); retrying in 5s")
            time.sleep(5)
        else:
            print(f"error {status}: {data}")
            time.sleep(5)


if __name__ == "__main__":
    main()
