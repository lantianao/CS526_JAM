# CS526 JAM — Paired Prototype

A 2D top-down shooter where you steal an enemy's body instead of taking cover
(Top-Down Shooter + Possession).

Immersive-sim powers — possession, teleportation, time-freeze — dropped into a
top-down shooter, with the genre's usual stealth incentive inverted: the powers
reward pushing a fight, not avoiding one.

## Requirements

- **Unity 6000.3.22f1** (the exact version is pinned in `ProjectSettings/ProjectVersion.txt`)
- Created from the **Unity Essentials Pathway** template (1.2.3)
- Active Input Handling is set to **Input System Package (New)**. The legacy
  `UnityEngine.Input` API throws at runtime in this project — use
  `UnityEngine.InputSystem` (see `JAMInput.cs`).

## Running it

Clone, open in Unity, and load `Assets/JAM/Scenes/JAM.unity`.

> The scene is not in the build list yet, so a player build will not contain it.
> Add it under `File → Build Profiles` before producing a build.

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move |
| Left mouse | Fire toward the cursor (1 damage) |
| `1` | Teleport to the cursor after a short wind-up. Rooted and unable to shoot while charging. |
| `2` | Freeze time for 5 seconds. Everything but you holds still; your bullets keep flying. |
| `3` | Possess the enemy nearest your crosshair for 5 seconds. Press again to leave early. |

Possession ends when the timer runs out, when you press `3`, or when the host
dies. The player reappears beside the host, and a surviving host is stunned for
2 seconds. While you are wearing an enemy, the other enemies hunt *that* body.

## Project layout

Everything the team wrote lives under `Assets/JAM/`. Everything else is the
Unity Essentials template, untouched except where noted below.

```
Assets/JAM/
├── Scripts/
│   ├── JAMPlayerController.cs   Movement, shooting, and the three abilities
│   ├── JAMEnemy.cs              Enemy AI, possession, and stun — one script, three modes
│   ├── JAMWeapon.cs             Fire rate + bullet spawning, shared by player and enemies
│   ├── JAMInput.cs              Keyboard/mouse polling, shared so a possessed
│   │                            enemy handles identically to the player
│   ├── JAMBullet.cs             Projectile flight and damage
│   ├── JAMHealth.cs             Hit points
│   └── JAMTimeFreeze.cs         Global freeze state
├── Scenes/JAM.unity
├── Prefabs/Bullet.prefab
└── Art/                         Dog animation clip + controller
```

### Two ideas worth knowing before editing

**`JAMEnemy` is one script, not two.** An enemy's own behaviour and a player
driving it are not different things — moving, aiming, shooting and taking damage
are identical either way. Only the *source of intent* differs, so the script
splits into a decision half (`DecideFromAI` / `DecideFromInput` / stunned) and a
shared execution half, with `moveIntent`, `aimDirection` and `wantsFire` as the
entire interface between them.

**Time freeze does not touch `Time.timeScale`.** Setting it to zero would stop
`FixedUpdate` and freeze the player's own physics too. Instead `JAMTimeFreeze`
switches every other `Rigidbody2D` to Kinematic (not `simulated = false`, which
would drop them out of the physics world and let your bullets pass straight
through), zeroes every `Animator.speed`, and pauses every `ParticleSystem`.

A script that moves something by hand cannot be caught from outside, so it has
to opt in:

```csharp
void Update()
{
    if (JAMTimeFreeze.IsFrozen) return;
    // ...
}
```

`Collectible2D.Update` does exactly this for the spinning cheese.

### Modified template files

Two files inside `Assets/_Unity Essentials/` were changed and are **not** stock:

- `Scripts/Provided Scripts/Collectible2D.cs` — now recognises `JAMPlayerController`
  as well as `PlayerController2D`, so pickups still work in the JAM scene.
  Also guards against an unassigned `onCollectEffect`.
- `Sprites/Animated/Sprite Sheet_Dog.png.meta` — sliced into 8 frames.

## Team contributions

<!-- The rubric grades this section. Each of us fills in our own row. -->

| Member | Contribution |
| --- | --- |
| _(name)_ | _(what you built — code, design, art, level)_ |
| _(name)_ | _(what you built)_ |

## Design document

`docs/CSCI526 Paired Prototype Descriptive Document.docx`
