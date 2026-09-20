# Lap Timing Tutorial

This tutorial shows how to add **lap timing** to your FR Legends custom track. When you are done, players can tick **Lap Timing** on the track select screen and the game times every lap they drive on your map.

![Lap Timing toggle on the track select screen](images/lap/1.jpg)

> The **Lap Timing** toggle only appears for tracks that were uploaded with a Lap Manager assigned. Tracks without one work exactly as before — lap timing is optional.

**Contents**

1. [How it works](#1-how-it-works)
2. [Quick start](#2-quick-start)
3. [The Lap Manager component](#3-the-lap-manager-component)
4. [The Lap Check Zone component](#4-the-lap-check-zone-component)
5. [Make your own gate art (Default Visual)](#5-make-your-own-gate-art-default-visual)
6. [Sectors](#6-sectors)
7. [Assign, upload and test](#7-assign-upload-and-test)
8. [Troubleshooting](#8-troubleshooting)
9. [For tournament organizers: Tournament Timing API](#9-for-tournament-organizers-tournament-timing-api)

---

## 1. How it works

Lap timing is built from two components:

| Component | Where | What it does |
|---|---|---|
| **Lap Manager** | One parent node per scene | Owns the lap definition. Its direct children, in Hierarchy order, are the checkpoints of one lap. |
| **Lap Check Zone** | Each child of the Lap Manager | One invisible rectangular **gate** across the track. A car must drive through it in the right direction. |

A lap counts only when the car crosses **every gate, in order, in the driving direction**. Skipping a gate (a shortcut) or crossing one backwards does not count, so the more carefully you place your gates, the harder your track is to cheat.

Gates need **no Collider** and no trigger. The gate is defined purely by the zone's Transform, and the crossing test runs inside the game.

`Assets/Scenes/MapExample049.unity` is a complete reference setup with five zones and three sectors. Open it and click around while you read.

## 2. Quick start

1. **GameObject → FR Legend → Lap Manager.** This creates a `Lap Manager` node with its first zone already inside.
2. Select the Lap Manager and click **Add Zone** once for every additional checkpoint you need.
3. Move, rotate and resize each zone so it spans the track, with its yellow arrow pointing in the driving direction.
4. Select your **RaceManager** and drag the Lap Manager node into its **Lap Manager** field.
5. **Build & Upload**, then test the draft in the game.

The rest of this page explains each step and every parameter.

## 3. The Lap Manager component

![Lap Manager Inspector](images/lap/2.jpg)

### The node itself

- Keep its **Rotation at 0 and Scale at 1**. A rotated or scaled parent distorts the size of the gates below it. Its position does not matter.
- Use **at most one** Lap Manager per scene.
- Its **direct children, in Hierarchy order, are the lap order**. Zone 0 is the first child, zone 1 the second, and so on. To reorder the checkpoints, drag the children in the Hierarchy.
- **Every** direct child must have a `Lap Check Zone` component. Do not park other objects (meshes, empties, lights) under this node.
- **Do not deactivate a zone.** An inactive gate can never be crossed, so it is treated as an error. To take a zone out temporarily, drag it out of the Lap Manager node instead.
- You need **at least 2 zones**.

### Parameters

| Parameter | Default | Meaning |
|---|---|---|
| **Same Start Finish** | on | **On:** zone 0 is both the start line and the finish line. Use this for circuits — the lap runs zone 0 → 1 → … → last → back to zone 0, and the next lap starts immediately. **Off:** zone 0 is the start and the **last zone is the finish**. Use this for point-to-point tracks (touge, hill climb, sprint). |
| **Enable Sectors** | off | Splits the lap into sectors for split timing. Requires **Same Start Finish** and at least one middle zone marked **Sector End**. **Sector times are never shown in the game — they only reach tournament organizers through the Tournament Timing API, so most tracks should leave this off.** See [Sectors](#6-sectors). |

### What the Inspector shows you

- **Status box.** When the setup is valid it prints the lap path, for example `Lap: Check0 → Check1 → Check2 → Check3 → Check4 → Check0 (6 gate crossings per lap)`. When something is wrong it shows a red error or a yellow warning instead — see [Troubleshooting](#8-troubleshooting).
- **Zones (hierarchy order).** One row per child: its index, its name (click it to select the zone), its **Role** (`Start/Finish`, `Start` or `Finish`) and a **Sector End** checkbox. The checkbox is greyed out where a sector cannot end (zone 0, the finish zone, or while Enable Sectors is off).
- **Sectors.** Appears when sectors are active and lists which zones each sector covers.
- **Add Zone.** Creates a new zone at the end of the list, 10 m in front of the last zone, copying its rotation and size, and already snapped to the ground (the very first zone is 8 m wide and 3 m high). This is the fastest way to lay out a track: add, drag into place, repeat.

What you see here is exactly what the game sees: the Inspector and the game use the same validation.

## 4. The Lap Check Zone component

![Lap Check Zone Inspector](images/lap/3.jpg)

### The Transform *is* the gate

A zone is a flat rectangle standing across the track. There is nothing else to configure — its Transform defines it:

| Transform | Meaning |
|---|---|
| **Position** | Center of the gate. |
| **Rotation** | Orientation of the gate. The **blue Z axis** is the **driving direction**; the Scene view draws it as a yellow arrow. **Only crossings in the arrow direction count.** |
| **Scale X** | Gate **width**, in meters. |
| **Scale Y** | Gate **height**, in meters. |
| **Scale Z** | Unused. Keep it at **1**. |

![Zones in the Scene view](images/lap/4.jpg)

In the Scene view each gate is drawn as a translucent green rectangle with a yellow direction arrow. The Lap Manager connects the gates in lap order and labels each one with its name and role (`Start/Finish`, `S1 end · S2 start`, `(inside S2)` …), so you can read the whole lap at a glance. These Gizmos are editor-only and never appear in the game.

**Resizing:** select a zone and drag the green **edge / corner handles** in the Scene view. Dragging an edge moves only that side; hold **Alt** to resize symmetrically. You can of course also type Scale X / Y directly.

### Parameters

| Parameter | Default | Meaning |
|---|---|---|
| **Snap To Ground** | on | Whenever you move, rotate or resize the zone in the editor, it raycasts down and rests the **bottom edge** of the gate on the surface below. It only hits objects on the **`Ground` layer**, so make sure your road uses that layer. Untick it if you need to place a gate manually (bridges, crossovers, tunnels under another road). |
| **Sector End** | off | Crossing this gate ends the current sector and starts the next. Only used when **Enable Sectors** is on in the Lap Manager; ignored on zone 0. You can also tick it from the Lap Manager's zone table. Only relevant for tournaments that use the Tournament Timing API — see [Sectors](#6-sectors). |
| **Default Visual** | on | Shows the built-in in-game look for this gate. **Read the next section before you ship with this on.** |

Below the parameters the Inspector reminds you of the axis rules and tells you where this zone sits in the lap, e.g. `Zone #0 of Lap Manager — Start/Finish, ends S3, starts S1`. **Select Lap Manager** jumps back to the parent. If you see *"Not a child of a Lap Manager: this zone is ignored at runtime"*, the zone is in the wrong place in the Hierarchy.

### Placement tips

- **Make gates wider than the road.** Let the gate reach into the walls or the grass on both sides so a car that runs wide still registers. A gate that is too narrow causes "my lap did not count" complaints.
- **Make gates tall enough** — 2 m or more — so a car that is airborne over a crest or a jump still passes through.
- **Stand the gate square to the road**, arrow pointing the way cars drive. On a bend, align it with the driving line at that point.
- **Put a gate wherever a shortcut is possible.** The gates are the only thing that forces a driver to follow your layout: hairpins that can be cut, infield grass, connecting roads and pit lanes all deserve one. A simple oval may need only 3 zones; a complex circuit may need many more.
- **Do not put two consecutive gates on top of each other** — leave some road between them.
- When two pieces of road run close together (or cross over each other), size the gate so it covers only the road it belongs to.

## 5. Make your own gate art (Default Visual)

With **Default Visual** ticked, the game draws a built-in look for the gate: a translucent quad over the gate rectangle (checkered for the finish line) and a small sign with the gate number.

> ### ⚠️ Important — the default look is only a placeholder
>
> The built-in visual is **deliberately minimal**. It exists so that a track is drivable and testable right away, not to look good. A track that ships with it looks unfinished.
>
> **We strongly recommend that you model your own gates and untick Default Visual — above all for the start / finish line.** The start/finish line is the most looked-at spot on any track: give it a proper gantry, an arch, a banner, start lights, a painted checkered line on the asphalt — whatever fits the style of your map. For the checkpoints in between, sponsor arches, flag poles, cones, marshal posts, boards or painted lines all work well.
>
> How to do it:
> 1. Build the gate model like any other scenery in your scene (keep it **outside** the Lap Manager node — that node may only contain zones).
> 2. Place it where the zone is, so what the player sees matches where the timing really happens.
> 3. Untick **Default Visual** on that zone.
>
> You can decide per zone: for example, custom art on the start/finish line and the sector gates, and the default visual (or nothing at all) on the rest. Remember the polygon and 10 MB bundle limits when you model them.

Unticking Default Visual only hides the in-game look. The gate still times laps, and the editor Gizmos are unaffected.

## 6. Sectors

Sectors split the lap into consecutive parts (S1, S2, S3 …) and record a split time for each.

> ### ⚠️ Read this first — most tracks do not need sectors
>
> **The game client never shows sector times.** Players only ever see their lap times. Sector times are recorded with each lap and delivered **only to tournament organizers through the [Tournament Timing API](TournamentTimingApi/README.md)** (the `sectors` array of each lap), where organizers use them for live timing pages with split times and purple / green sector highlighting.
>
> That means sectors are useful **only** when a tournament is run on your track by an organizer who reads the Tournament Timing API. **If that is not your case, there is normally no reason to set up sectors** — leave **Enable Sectors** off and skip this section. Lap timing works fully without them, and regular players will not notice any difference.

If your track is meant to host such tournaments, set sectors up as follows (three sectors of roughly similar length is the motorsport convention):

1. Make sure **Same Start Finish** is on — sectors are only available on circuits.
2. Tick **Enable Sectors** on the Lap Manager.
3. Tick **Sector End** on the middle zones where a sector should end (in the zone's own Inspector or in the Lap Manager's zone table).

The rules:

- S1 always starts at zone 0. Each **Sector End** zone closes the current sector and opens the next. The last sector always ends back at zone 0. So *N* ticked zones give *N + 1* sectors.
- Zones that are not ticked simply sit **inside** a sector; they still have to be crossed for the lap to count.
- A lap can have **at most 16 sectors**.

In the example scene, `Check1` and `Check3` are ticked, which gives three sectors:

```
S1  Check0 → Check1
S2  Check1 → Check2 → Check3
S3  Check3 → Check4 → Check0
```

The Lap Manager Inspector lists the sectors exactly like this, and the Scene view alternates the line color per sector and labels each segment, so you can verify the split visually.

If the sector setup is not valid (see [Troubleshooting](#8-troubleshooting)) the Inspector shows a yellow warning and **sectors are switched off** — lap timing itself keeps working and the upload is not blocked.

## 7. Assign, upload and test

1. Select the **RaceManager** in your scene and drag the Lap Manager node into its **Lap Manager** field. This is the switch that turns lap timing on for the map.
2. Open **Tools → UGC Map Manager** and click **Build & Upload** as usual. The scene check refuses to build when:
   - the scene contains more than one Lap Manager,
   - a Lap Manager exists but is **not assigned** to the RaceManager, or
   - the assigned Lap Manager shows a red **error** in its Inspector.

   Yellow sector warnings do not block the upload.
3. In the game, open **Custom Tracks → My Drafts** and choose your track to open its details page.

   ![Lap Timing Supported badge on the draft's details page](images/lap/5.jpg)

   If lap timing is set up correctly, the details page shows the **Lap Timing Supported** badge (stopwatch icon), as in the screenshot above. If the badge is missing, the map was uploaded without a Lap Manager assigned to the RaceManager — go back to step 1 and **Build & Upload** again.

4. With the badge showing, tap **Preview** to enter the map and test that lap timing really works. Check that:
   - a lap is recorded when you drive the full layout through every gate;
   - every shortcut you can think of does **not** produce a lap;
   - driving through a gate backwards does not count;
   - running wide at each gate still registers;
   - your gate art lines up with where timing actually happens.

If something is off, fix the scene and **Build & Upload** again to update the draft.

> If you add lap timing to a map that is already published, click **Create Version** first, as with any other change.

## 8. Troubleshooting

| Message / symptom | Cause and fix |
|---|---|
| `At least 2 zones are required (found N)` | Click **Add Zone** until you have at least two. |
| `Child 'X' (#i) has no LapCheckZone component` | Something that is not a zone sits under the Lap Manager. Move it out, or add a Lap Check Zone component to it. |
| `Child 'X' (#i) is inactive; remove it from the LapManager instead of deactivating it` | Re-activate the zone, or drag it out of the Lap Manager node. |
| `Sectors require Same Start/Finish; sectors disabled` | Turn **Same Start Finish** on, or turn **Enable Sectors** off. |
| `Mark at least one middle zone as Sector End; sectors disabled` | Tick **Sector End** on at least one zone other than zone 0. |
| `More than 16 sectors; sectors disabled` | Untick some **Sector End** boxes. |
| `Sector End on zone 0 is ignored (it is the start/finish line)` | Harmless; untick it on zone 0 to clear the warning. |
| `Not a child of a Lap Manager: this zone is ignored at runtime` | Drag the zone under the Lap Manager node. |
| Build says `Scene contains a LapManager but RaceManager.lapManager is not assigned` | Drag the Lap Manager into **RaceManager → Lap Manager**. |
| Build says `Scene must contain at most one LapManager` | Delete the extra Lap Manager. |
| No **Lap Timing Supported** badge on the details page / no **Lap Timing** toggle in the game | The map was uploaded without a Lap Manager assigned. Assign it and **Build & Upload** again. |
| Laps are not counted | A gate points the wrong way (check the yellow arrows), a gate is too narrow or too low, or the zones are in the wrong order in the Hierarchy. |
| A gate floats or sinks into the road | Your road is not on the **`Ground`** layer, or **Snap To Ground** is off. |
| Gates look the wrong size | The Lap Manager node (or one of its parents) is rotated or scaled. Reset it to rotation 0, scale 1. |

## 9. For tournament organizers: Tournament Timing API

Tracks with lap timing can be used for timed competitions. The **Tournament Timing API** is a read-only HTTP API that gives an organizer's backend the complete lap history of a live room — every lap of every driver, with lap time, **sector times**, car and timestamps — and lets it poll for new laps in near real time. It is what you would use to build a live timing screen or a results website for your event.

- Documentation: [Tournament Timing API](TournamentTimingApi/README.md)
- Sample program: [timing_poll.py](TournamentTimingApi/timing_poll.py) — Python 3, standard library only. It keeps a local copy of a room's lap history and prints a live leaderboard with last-lap sector times to the console.

Access requires a personal API key. To apply for one, contact the FR Legends team at **info.frlegends [at] gmail [dot] com**.

Map authors do not need the API for anything — it matters to you only in that the sectors you define on your track are what organizers will see in their timing data.
