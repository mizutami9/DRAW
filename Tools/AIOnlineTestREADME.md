# NICO DRAW AI online test

This harness launches four separate game processes. Each process owns one local
player and sends decisions through `PlayerController2D.SetScriptedInput` and
`PlayerCarryController.ApplyActionForScript`; it never moves gameplay transforms
or rigidbodies directly.

## Build

Use `PICO > Build Windows AI Online Test` in Unity. The output is
`Builds/NICO DRAW AI Test/NICO DRAW.exe`. The runtime bootstrap is compiled
only when the `NICO_DRAW_AI_TEST` build define is present, so a normal product
build cannot activate the harness even if test command-line arguments are passed.

## Run

Fast deterministic transport:

```powershell
.\Tools\RunAiOnlineTest.ps1 -Transport Direct -Stage "1-1"
```

EOS lobby and P2P transport:

```powershell
.\Tools\RunAiOnlineTest.ps1 -Transport EOS -Stage "1-1" -TimeoutMinutes 20
```

EOS runs isolate `TEMP`, `LOCALAPPDATA`, `APPDATA`, and `USERPROFILE` for every
process so Device ID accounts and PlayEveryWare's `Application.temporaryCachePath`
are independent. EOS can still map all processes on one Windows device to the
same Device ID ProductUserId; a trustworthy four-player EOS run therefore needs
four Steam/Epic authentication contexts or separate VMs/devices. Duplicate
ProductUserIds are detected as `identityCollisions` and make the run fail. The
host publishes only the temporary private room code; clients join through the
normal EOS lobby API.

Reports, telemetry, Unity logs, incidents, and screenshots are written under
`Temp/AiOnlineTests/<run-id>/`.

## Current stage coverage

The first semantic definition targets stage 1-1. It observes linked-object state
and assigns the two key deliveries to different player slots. Navigation reacts
to target position, vertical difference, ground, obstacles, gaps, and stalls;
the definition contains objectives rather than timed input playback.

The 1-1 coordinator follows the intended solution as semantic phases:

1. One player throws a teammate across the first gap to reveal the bridge.
2. All four players wait for the bridge animation, then cross it.
3. The thrower carries and throws a teammate over the tall wall to reveal the
   jump pad; all players move to the upper route.
4. Two players are assigned to the separate upper-left key platforms. They
   discard obstructing boxes through the normal carry/throw action and collect
   one key each.
5. All players gather in the right basin. The thrower sends three teammates out.
6. The last player uses the DRAW flow to create long legs and jumps out.
7. Both locks are opened and every player moves to the goal.

Positions in the definition are staging areas and object goals, not recorded
input timings. Each client continuously observes current networked state before
choosing movement, jump, grab, throw, redraw, or retry actions.
