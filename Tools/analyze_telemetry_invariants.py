import glob
import json
import math
import os
import sys
from datetime import datetime


def parse_utc(value):
    if not value:
        return None
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp()
    except (TypeError, ValueError):
        return None


def add_anomaly(anomalies, kind, frame, client, player, detail):
    anomalies.append({
        "type": kind,
        "time": float(frame.get("elapsedSeconds", 0.0)) if frame else 0.0,
        "utc": frame.get("capturedAtUtc", "") if frame else "",
        "client": client,
        "player": player,
        "detail": detail,
    })


def player_maps(frame):
    by_id = {}
    by_slot = {}
    for player in frame.get("players", []):
        player_id = player.get("playerId")
        slot = player.get("slot", -1)
        if player_id:
            by_id[player_id] = player
        if isinstance(slot, int) and slot >= 0:
            by_slot.setdefault(slot, []).append(player_id)
    return by_id, by_slot


def nearest_frame(frames, timestamp):
    candidates = [frame for frame in frames if frame.get("_utc") is not None]
    if not candidates:
        return None
    return min(candidates, key=lambda frame: abs(frame["_utc"] - timestamp))


def interpolate_player_position(frames, timestamp, player_id):
    before = None
    after = None
    for frame in frames:
        frame_time = frame.get("_utc")
        if frame_time is None:
            continue
        if frame_time <= timestamp:
            before = frame
        if frame_time >= timestamp:
            after = frame
            break
    if before is None or after is None or before is after:
        return None, False
    span = after["_utc"] - before["_utc"]
    if span <= 0.0 or span > 1.5 or before.get("stage") != after.get("stage"):
        return None, False
    before_player = player_maps(before)[0].get(player_id)
    after_player = player_maps(after)[0].get(player_id)
    if before_player is None or after_player is None:
        return None, False
    before_position = before_player.get("position", {"x": 0.0, "y": 0.0})
    after_position = after_player.get("position", {"x": 0.0, "y": 0.0})
    transition = (before_player.get("respawning") or after_player.get("respawning")
                  or before_player.get("redrawing") or after_player.get("redrawing")
                  or before_position.get("y", 0.0) < -5.0
                  or after_position.get("y", 0.0) < -5.0
                  or before_player.get("grounded") != after_player.get("grounded"))
    ratio = (timestamp - before["_utc"]) / span
    return {
        "x": before_position.get("x", 0.0)
             + (after_position.get("x", 0.0) - before_position.get("x", 0.0)) * ratio,
        "y": before_position.get("y", 0.0)
             + (after_position.get("y", 0.0) - before_position.get("y", 0.0)) * ratio,
    }, transition


def analyze_telemetry_run(run_dir):
    run_name = os.path.basename(os.path.normpath(run_dir))
    telemetry_files = sorted(glob.glob(os.path.join(run_dir, "telemetry-client-*.jsonl")))
    anomalies = []
    clients_data = {}

    if not telemetry_files:
        add_anomaly(anomalies, "Telemetry Missing", None, "ALL", "ALL",
                    "No telemetry-client-*.jsonl files were found")

    for path in telemetry_files:
        client_name = os.path.basename(path).replace(".jsonl", "")
        frames = []
        malformed = 0
        with open(path, "r", encoding="utf-8-sig", errors="strict") as stream:
            for line_number, line in enumerate(stream, 1):
                if not line.strip():
                    continue
                try:
                    frame = json.loads(line)
                    frame["_utc"] = parse_utc(frame.get("capturedAtUtc"))
                    frames.append(frame)
                except (UnicodeError, json.JSONDecodeError) as error:
                    malformed += 1
                    add_anomaly(anomalies, "Malformed Telemetry", None, client_name, "ALL",
                                f"Line {line_number} could not be parsed: {error}")

        if not frames:
            add_anomaly(anomalies, "Telemetry Empty", None, client_name, "ALL",
                        "The telemetry file contained no valid frames")
            continue

        frames.sort(key=lambda frame: (
            frame.get("_utc") if frame.get("_utc") is not None else float("inf"),
            float(frame.get("elapsedSeconds", 0.0))))
        clients_data[client_name] = frames

        root_ids = {frame.get("playerId") for frame in frames if frame.get("playerId")}
        process_ids = {frame.get("processId") for frame in frames if frame.get("processId")}
        instances = {frame.get("clientInstance") for frame in frames
                     if frame.get("clientInstance") is not None}
        if len(root_ids) > 1 or len(process_ids) > 1 or len(instances) > 1:
            add_anomaly(
                anomalies, "Telemetry Identity Collision", frames[0], client_name, "ALL",
                f"One file contains roots={sorted(root_ids)}, processes={sorted(process_ids)}, "
                f"instances={sorted(instances)}")

        missing_utc = sum(1 for frame in frames if frame.get("_utc") is None)
        if missing_utc:
            add_anomaly(anomalies, "UTC Timestamp Missing", frames[0], client_name, "ALL",
                        f"{missing_utc} frame(s) cannot be safely aligned across clients")

        revision_signatures = {}
        previous_players = {}
        floating_since = {}
        respawning_since = {}
        missing_avatar_since = {}
        slot_history = {}
        active_roster_revision = None
        roster_revision_started_at = 0.0

        for frame in frames:
            elapsed = float(frame.get("elapsedSeconds", 0.0))
            stage = frame.get("stage", "unknown")
            players = frame.get("players", [])
            by_id, by_slot = player_maps(frame)
            root_id = frame.get("playerId")
            root_slot = frame.get("slot", -1)

            ids = [player.get("playerId") for player in players if player.get("playerId")]
            if len(ids) != len(set(ids)):
                add_anomaly(anomalies, "Duplicate PlayerId In Roster", frame, client_name,
                            "ALL", f"Roster contains duplicate IDs: {ids}")
            for assigned_slot, owners in by_slot.items():
                if len(owners) > 1:
                    add_anomaly(anomalies, "Duplicate Slot In Roster", frame, client_name,
                                f"slot {assigned_slot}", f"Owners: {owners}")

            local_player = by_id.get(root_id)
            if local_player is not None and local_player.get("slot") != root_slot:
                add_anomaly(
                    anomalies, "Local Slot Mismatch", frame, client_name, root_id,
                    f"Report says slot {root_slot}, roster says slot {local_player.get('slot')}")

            revision = frame.get("rosterRevision", 0)
            signature = frame.get("rosterSignature", "")
            if revision != active_roster_revision:
                active_roster_revision = revision
                roster_revision_started_at = elapsed
                # Slot assignment is allowed to be recomputed when the visible
                # lobby roster changes (for example, while the fourth client is
                # still joining). Drift is only suspicious inside one revision.
                slot_history.clear()
            frame["_rosterAge"] = max(0.0, elapsed - roster_revision_started_at)
            if not isinstance(revision, int) or revision <= 0 or not signature:
                add_anomaly(anomalies, "Roster Version Missing", frame, client_name, "ALL",
                            "Frame has no usable rosterRevision/rosterSignature")
            elif revision in revision_signatures and revision_signatures[revision] != signature:
                add_anomaly(anomalies, "Roster Revision Reused", frame, client_name, "ALL",
                            f"Revision {revision} represents multiple roster signatures")
            else:
                revision_signatures[revision] = signature

            for player in players:
                player_id = player.get("playerId")
                if not player_id:
                    continue
                slot = player.get("slot", -1)
                position = player.get("position", {"x": 0.0, "y": 0.0})
                velocity = player.get("velocity", {"x": 0.0, "y": 0.0})
                px = position.get("x", 0.0)
                py = position.get("y", 0.0)
                vx = velocity.get("x", 0.0)
                vy = velocity.get("y", 0.0)
                values = [px, py, vx, vy]
                if not all(isinstance(value, (int, float)) and math.isfinite(value)
                           for value in values):
                    add_anomaly(anomalies, "Non-Finite Player State", frame, client_name,
                                player_id, f"position={position}, velocity={velocity}")
                    continue

                if isinstance(slot, int) and slot >= 0:
                    known_slot = slot_history.get(player_id)
                    if known_slot is not None and known_slot != slot:
                        add_anomaly(anomalies, "Player Slot Drift", frame, client_name,
                                    player_id, f"Slot changed from {known_slot} to {slot}")
                    slot_history[player_id] = slot

                speed = math.hypot(vx, vy)
                if speed > 120.0:
                    add_anomaly(anomalies, "Extreme Velocity", frame, client_name,
                                f"slot {slot} ({player_id})", f"Speed reached {speed:.1f} m/s")

                previous = previous_players.get(player_id)
                if previous:
                    previous_position, previous_elapsed, previous_stage, previous_redraw, previous_respawn = previous
                    delta = elapsed - previous_elapsed
                    distance = math.hypot(px - previous_position.get("x", 0.0),
                                          py - previous_position.get("y", 0.0))
                    implied_speed = distance / max(delta, 0.001)
                    transition = (previous_redraw or player.get("redrawing", False)
                                  or previous_respawn or player.get("respawning", False))
                    crossed_death_plane = py < -5.0 or previous_position.get("y", 0.0) < -5.0
                    if (0.0 < delta <= 1.5 and distance > 8.0 and implied_speed > 25.0
                            and not transition and not crossed_death_plane and previous_stage == stage):
                        add_anomaly(anomalies, "Suspicious Teleport / Warp", frame, client_name,
                                    f"slot {slot} ({player_id})",
                                    f"Moved {distance:.2f}m in {delta:.2f}s")
                previous_players[player_id] = (
                    position, elapsed, stage, player.get("redrawing", False),
                    player.get("respawning", False))

                floating = (not player.get("grounded", False)
                            and not player.get("respawning", False)
                            and not player.get("redrawing", False)
                            and player.get("controlsEnabled", True)
                            and speed < 0.1 and stage not in ("title", "unknown"))
                if floating:
                    floating_since.setdefault(player_id, elapsed)
                    duration = elapsed - floating_since[player_id]
                    if duration >= 4.0:
                        add_anomaly(anomalies, "Grounded Lost / Floating Stuck", frame,
                                    client_name, f"slot {slot} ({player_id})",
                                    f"Stationary and ungrounded for {duration:.1f}s")
                else:
                    floating_since.pop(player_id, None)

                if player.get("respawning", False):
                    respawning_since.setdefault(player_id, elapsed)
                    duration = elapsed - respawning_since[player_id]
                    if duration >= 5.0:
                        add_anomaly(anomalies, "Prolonged Respawn Lock", frame, client_name,
                                    f"slot {slot} ({player_id})",
                                    f"Respawning for {duration:.1f}s")
                else:
                    respawning_since.pop(player_id, None)

                active_solid_colliders = player.get("activeSolidColliders", 0)
                if (player.get("eliminated", False)
                        and isinstance(active_solid_colliders, int)
                        and active_solid_colliders > 0):
                    add_anomaly(anomalies, "Eliminated Player Collider Left Active", frame,
                                client_name, f"slot {slot} ({player_id})",
                                f"Eliminated avatar still has {active_solid_colliders} active solid collider(s)")

                if player.get("species") == "Missing":
                    missing_avatar_since.setdefault(player_id, elapsed)
                    duration = elapsed - missing_avatar_since[player_id]
                    if duration >= 3.0:
                        add_anomaly(anomalies, "Player Avatar Missing", frame, client_name,
                                    f"slot {slot} ({player_id})",
                                    f"Roster player has no avatar for {duration:.1f}s")
                else:
                    missing_avatar_since.pop(player_id, None)

    reference_name = next((name for name, frames in clients_data.items()
                           if any(frame.get("clientInstance") == 0 for frame in frames)), None)
    if reference_name is None and clients_data:
        reference_name = sorted(clients_data)[0]

    if reference_name:
        reference_frames = clients_data[reference_name]
        for other_name, other_frames in clients_data.items():
            if other_name == reference_name:
                continue
            position_disagreement_since = {}
            species_disagreement_since = {}
            for reference in reference_frames:
                timestamp = reference.get("_utc")
                if timestamp is None or reference.get("stage") in ("title", "unknown"):
                    continue
                other = nearest_frame(other_frames, timestamp)
                if other is None or abs(other["_utc"] - timestamp) > 1.25:
                    continue
                if other.get("stage") != reference.get("stage"):
                    continue

                reference_by_id, reference_by_slot = player_maps(reference)
                other_by_id, other_by_slot = player_maps(other)
                if (len(reference_by_id) != len(other_by_id)
                        and float(reference.get("elapsedSeconds", 0.0)) > 15.0
                        and float(other.get("elapsedSeconds", 0.0)) > 15.0):
                    add_anomaly(
                        anomalies, "Player Count Mismatch", reference,
                        f"{reference_name} vs {other_name}", "ALL",
                        f"Reference sees {len(reference_by_id)}, other sees {len(other_by_id)}")

                common_player_ids = set(reference_by_id).intersection(other_by_id)
                for tracked_player_id in list(position_disagreement_since):
                    if tracked_player_id not in common_player_ids:
                        position_disagreement_since.pop(tracked_player_id, None)
                for player_id in common_player_ids:
                    reference_player = reference_by_id[player_id]
                    other_player = other_by_id[player_id]
                    if reference_player.get("slot") != other_player.get("slot"):
                        add_anomaly(
                            anomalies, "Cross-Client Slot Assignment", reference,
                            f"{reference_name} vs {other_name}", player_id,
                            f"Slots are {reference_player.get('slot')} and {other_player.get('slot')}")

                    controlled_ready_room_test = (
                        reference.get("result") == "RUNNING_READY_ROOM_SMOKE"
                        or other.get("result") == "RUNNING_READY_ROOM_SMOKE")
                    if controlled_ready_room_test:
                        # This scenario deliberately drops each owner by 24m and
                        # redraws them one at a time. Its dedicated assertions
                        # verify booth recovery and final avatar convergence.
                        position_disagreement_since.pop(player_id, None)
                        continue
                    transient = (reference_player.get("respawning") or other_player.get("respawning")
                                 or reference_player.get("redrawing") or other_player.get("redrawing"))
                    roster_converging = (reference.get("_rosterAge", 0.0) < 2.0
                                         or other.get("_rosterAge", 0.0) < 2.0)
                    if (transient or roster_converging
                            or reference_player.get("species") == "Missing"
                            or other_player.get("species") == "Missing"):
                        position_disagreement_since.pop(player_id, None)
                        species_disagreement_since.pop(player_id, None)
                        continue
                    reference_position = reference_player.get("position", {"x": 0.0, "y": 0.0})
                    other_position = other_player.get("position", {"x": 0.0, "y": 0.0})
                    interpolated_position, interpolation_transition = interpolate_player_position(
                        other_frames, timestamp, player_id)
                    if interpolated_position is not None:
                        other_position = interpolated_position
                    death_transition = (reference_position.get("y", 0.0) < -5.0
                                        or other_position.get("y", 0.0) < -5.0)
                    grounded_transition = (reference_player.get("grounded")
                                           != other_player.get("grounded"))
                    if death_transition or grounded_transition or interpolation_transition:
                        position_disagreement_since.pop(player_id, None)
                        continue
                    difference = math.hypot(
                        reference_position.get("x", 0.0) - other_position.get("x", 0.0),
                        reference_position.get("y", 0.0) - other_position.get("y", 0.0))
                    if difference > 2.5:
                        position_disagreement_since.setdefault(player_id, timestamp)
                        disagreement_duration = timestamp - position_disagreement_since[player_id]
                        if disagreement_duration >= 2.0:
                            add_anomaly(
                                anomalies, "Cross-Client Position Disagreement", reference,
                                f"{reference_name} vs {other_name}", player_id,
                                f"Position difference is {difference:.2f}m for "
                                f"{disagreement_duration:.1f}s")
                    else:
                        position_disagreement_since.pop(player_id, None)
                    if reference_player.get("species") != other_player.get("species"):
                        species_disagreement_since.setdefault(player_id, timestamp)
                        species_disagreement_duration = timestamp - species_disagreement_since[player_id]
                        if species_disagreement_duration >= 2.0:
                            add_anomaly(
                                anomalies, "Species / Avatar Desync", reference,
                                f"{reference_name} vs {other_name}", player_id,
                                f"Species are {reference_player.get('species')} and "
                                f"{other_player.get('species')} for "
                                f"{species_disagreement_duration:.1f}s")
                    else:
                        species_disagreement_since.pop(player_id, None)

                for slot in set(reference_by_slot).intersection(other_by_slot):
                    reference_owners = set(reference_by_slot[slot])
                    other_owners = set(other_by_slot[slot])
                    if reference_owners != other_owners:
                        add_anomaly(
                            anomalies, "Cross-Client Slot Ownership", reference,
                            f"{reference_name} vs {other_name}", f"slot {slot}",
                            f"Owners are {sorted(reference_owners)} and {sorted(other_owners)}")

    unique_anomalies = []
    seen = set()
    for anomaly in anomalies:
        key = (anomaly["type"], anomaly["client"], anomaly["player"], anomaly["detail"])
        if key in seen:
            continue
        seen.add(key)
        unique_anomalies.append(anomaly)

    return {
        "run": run_name,
        "dir": run_dir,
        "total_anomalies": len(anomalies),
        "unique_anomalies": unique_anomalies,
    }


def main():
    json_output = "--json" in sys.argv
    args = [arg for arg in sys.argv[1:] if arg != "--json"]
    if args:
        target_dirs = args
    else:
        base_dir = os.path.join("Temp", "DemoMultiplayerSmoke")
        target_dirs = sorted(glob.glob(os.path.join(base_dir, "*")))[-5:]

    results = [analyze_telemetry_run(path) for path in target_dirs if os.path.isdir(path)]
    if not results:
        results = [{
            "run": "none",
            "dir": "",
            "total_anomalies": 1,
            "unique_anomalies": [{
                "type": "Run Missing", "time": 0.0, "utc": "", "client": "ALL",
                "player": "ALL", "detail": "No telemetry run directories were found",
            }],
        }]

    if json_output:
        print(json.dumps(results, ensure_ascii=False, indent=2))
    else:
        print("NICO DRAW TELEMETRY INVARIANT AUDIT")
        for result in results:
            count = result["total_anomalies"]
            status = "PASS" if count == 0 else "FAIL"
            print(f"[{status}] {result['run']}: {count} invariant violation(s)")
            for anomaly in result["unique_anomalies"][:12]:
                print(f"  [{anomaly['type']}] {anomaly['client']} | "
                      f"{anomaly['player']} | {anomaly['detail']}")

    return 1 if any(result["total_anomalies"] > 0 for result in results) else 0


if __name__ == "__main__":
    sys.exit(main())
