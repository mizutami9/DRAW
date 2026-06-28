# DrawBody Prototype

Unity 2D prototype for the roadmap Phase 0.

## First Run

1. Open this folder with Unity Hub.
2. In Unity, run `PICO > Build Phase 0 Scene`.
3. Open `Assets/Scenes/GameScene.unity`.
4. Press Play.

## Controls

- `A` / `D` or left / right arrow: move
- `Space`: jump
- Left click: swing both arms
- `Tab`: open redraw screen
- Select a body part button at the top of the redraw screen: `Head`, `Torso`, `Left Arm`, `Right Arm`, `Left Leg`, or `Right Leg`
- Mouse drag in redraw screen: draw one line for the selected part
- `C` or Clear: erase the selected part
- `Enter` or Decide: rebuild body and resume from the same position

## Language

- Use the top-right `日本語` / `EN` buttons to switch language
- UI text, body part names, ability text, and stage labels support Japanese and English

## Drawing Rules

- Total ink limit: 1000
- Torso can be drawn freely
- Head, arms, and legs must start near the torso
- Decide is blocked until every drawn non-torso part is connected
- After Decide, drawn lines become the player body
- Each line segment gets a `CapsuleCollider2D`

## Abilities

- Left Leg + Right Leg ink 0-49: normal jump
- Left Leg + Right Leg ink 50-79: jump x2
- Left Leg + Right Leg ink 80+: jump x3
- Left Arm + Right Arm ink 0-49: normal reach
- Left Arm + Right Arm ink 50-79: long reach
- Left Arm + Right Arm ink 80+: fast swing
- Torso ink 0-49: normal
- Torso ink 50-79: can press heavy switches later
- Torso ink 80+: heavy body
- Arm swing uses arm ability: 50+ gives longer reach, 80+ gives faster swing

## Phase 0 Gimmicks

- High Platform: use more leg ink for higher jumps
- Heavy Switch: use 50+ torso ink to open the purple gate
- Far Lever: use arm swing or long reach to open the blue gate
- Narrow Hole: redraw into a thinner shape to pass through
- Ball Hit: swing arms to push the yellow ball

The first milestone intentionally starts with a normal square character. After the first valid redraw, that square is replaced by generated line renderers and capsule colliders.

## Online Lobby Test

This prototype now uses the EOS backend by default. Login is Device ID based, so players do not need to sign in with an Epic account. Direct TCP is still available from the `OnlineManager` inspector as a fallback.

1. In Unity, run `PICO > Build Phase 0 Scene`.
2. Configure EOS with `EOS Plugin > EOS Configuration`.
3. Press Play and choose `MULTI`.
4. Host player: choose `Room` -> `Create Room`.
5. Share the EOS lobby `ID` shown on the room screen.
6. Friend player: choose `Room` -> `Join Room`, enter that ID, then press `Join`.
7. Host presses `1-1 Start` to load stage `1-1` for everyone.

EOS setup needs values from the Epic Developer Portal:

- Product Name
- Product Version
- Product ID
- Sandbox ID
- Deployment ID
- Client ID
- Client Secret
- Encryption Key

For the Steam version, the login path should become Steam account authentication -> EOS internal Product User ID. Until Steamworks is integrated, the prototype uses Device ID -> EOS internal Product User ID.

Target auth flow:

```text
Launch game
-> Steam build: Steam account auth ticket
-> Non-Steam/dev build: EOS Device ID
-> EOS Connect Product User ID
-> EOS Lobby / P2P
```

If you switch `OnlineManager` back to `DirectTcp`, use `IP:7777` as the room ID. Direct TCP is useful for same-LAN debugging only.

## Windows Build

Use `PICO > Build Windows EXE` to create:

`Builds/DrawBodyOnline/DrawBody.exe`

Send the whole `Builds/DrawBodyOnline` folder to a friend, not only the exe.
