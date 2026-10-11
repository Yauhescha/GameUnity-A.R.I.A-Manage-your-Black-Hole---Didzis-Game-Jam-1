# A.R.I.A. — Manage Your Black Hole

A 2D Unity game about controlling black-hole gravity to steer moving stars into their target portals. Created for Didzis Game Jam #1. https://itch.io/jam/didzis-game-jam-1
You may play it in browser https://yauhescha.itch.io/aria-manage-your-hole

## Gameplay

- Toggle gravity to change a star's trajectory.
- Deliver stars to highlighted portals and avoid obstacles.
- Progress through configured levels with delivery and miss limits.
- Play a comic prologue and use the main menu.
- Experiment with layouts in the included runtime map-creation scene.

## Controls

| Input | Action |
| --- | --- |
| Space or the gravity button | Toggle gravity |
| R | Restart the current level |
| Mouse click | Select/toggle a hole when individual control is enabled |

The active scene's `GameManager` determines whether gravity is controlled globally or per black hole.

## Open and run

Use **Unity 6000.3.20f1**, as recorded in `ProjectSettings/ProjectVersion.txt`.

1. Add the repository in Unity Hub and open it.
2. Allow package and asset import to finish.
3. Open `Assets/Scenes/MainMenu.unity` and press Play.

The build configuration contains the menu, prologue, creation scene and ten level scenes. Use Build Profiles to choose a target platform, preserving the configured scene list.

## Runtime map creation

`Assets/Scenes/Creation.unity` and `MapBuilderController` provide a layout editor with object properties, trajectory preview and JSON save/load. Maps are stored under `Nexus9Maps` within Unity's `Application.persistentDataPath`.

## Code organization

Gameplay scripts are under `Assets/0game/Scripts/`:

- `GameManager`: launch cycle, target selection, score and level results.
- `BlackHoleController`: gravity behavior and toggling.
- `StarProjectile`: moving-star behavior.
- `PortalTarget`, `NebulaObstacle`, `QuasarController`: level objects.
- `MapBuilderController`: runtime editor and preview.
- `menu/`: menu and comic-prologue presentation.

Level balance such as launch speed, required deliveries and permitted misses is configured through serialized Inspector fields. This repository is archived.
