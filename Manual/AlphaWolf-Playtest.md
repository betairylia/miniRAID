# AlphaWolf small-scene playtest

Open `Assets/Scenes/OpenTest/AlphaWolfPlaytest.unity` (or **miniRAID → AlphaWolf → Open playable lesson**) in Unity 6000.3.15f1 and press Play. The scene includes its own CombatBase services. Do not load another combat scene alongside it.

## Play

- Guardian: move, Slash and normal automatic attacks. Fire Mage: Fire Bolt or Fire Blast. Healer: Basic heal.
- Purple cells are the fixed sweep/charge preview. Move before committing actions. Pass each actor to advance; when nobody is active, click empty ground and choose EndTurn.
- Sweep: leave the purple cells. Afterwards the wolf can Bite the nearest living ally within two cells, in any direction; this separate follow-up is announced in the lesson panel.
- Charge: locks a low-defense/low-HP party member and the path shown during preparation. A pillar or another party member blocks it; the wolf stops before the occupied cell and loses its next round. The interceptor takes normal damage; a struck pillar breaks. Terrain also stops the charge.
- Roar: two full party rounds to deal 70 actual damage to the wolf. Healing and positioning during its stun make the next burst window safer. Fire Blast puts both staff attacks into resonance until two Recovery stages have passed.
- Wolf dead = victory; all three party members dead = defeat. The Restart lesson button appears after outstanding combat work finishes. It creates a fresh scene, combat tracker and harness session.

Example tested opening: Guardian moves from (16,1,15) to (16,1,17), avoiding sweep. On the charge round it can move to (14,1,15) to intercept before the pillar. Heal during the following stun; save Slash + Fire Blast for Roar. Alternatively leave the marked path alone to demonstrate the pillar breaking.

## Design mapping and scope

Source: miniRAID's **头狼** design note in the active Old-vaults miniRAID content-design hierarchy, read in full on 2026-09-30. This is a playable interpretation of that draft, not a claim that its unspecified numbers were finalized.

| Draft requirement | Implementation |
| --- | --- |
| Three-person tutorial | Guardian, a neutral Fire Mage, a basic Healer |
| Spark staff, Fire Bolt / Fire Blast, resonance 2 | Separate cloned staff/actions; Fire damage; ordinary targeting and resource rules |
| Breakable pillars and charge collision/stun | Two 40-HP targetable pillar units; charge destroys its first pillar collision and stops |
| Fixed delayed frontal sweep | Nine grid cells locked for one party round; preview and hit test share the same set |
| Low-defense/low-HP charge target; allies can block | Defense, then absolute HP, then name tie-break; locked grid path; one-round boss stun |
| Two-turn roar, interrupted by sufficient damage | Two party rounds; 70 actual damage threshold; otherwise damages all living party members |
| Basic bite | Announced nearby follow-up after sweep |

Prototype choices: wolf 480 HP; base Bite/Sweep/Charge/Roar amounts 12/26/32/38, then the existing armor/hit/death/log pipeline. Charge uses a deterministic orthogonal staircase line through grid cells. One draft “T” is one full three-opportunity party round. The original note did not specify the two other classes, damage values, exact AoE shape, threshold, AI priority ties or death UI. These are explicit test assumptions, not changes to the original AlphaWolf or other bosses. The tentative victim stun and loot/progression are deferred. Mage copies basic attributes and projectile machinery but does not inherit Astrologer race/job or mechanics; Archer/Astrologer originals are unchanged.

## Technical boundary

The original AlphaWolf scene and AI remain available. Only this scene installs `ICombatCompletion`, fresh backend/tracker initialization and the lesson schedule. Generic scenes retain their existing completion behavior. Harness commands still use player UI permissions; the lesson disables its incompatible legacy turn rewind. Restart runs only after `CombatStopped`. No save/load persistence for the lesson's state machine is promised.

Scene/assets were generated and updated with Unity Editor APIs. `AlphaWolfPlaytestBuilder.Build()` is a one-time authoring recipe that refuses to overwrite an existing lesson. Geometry and asset checks: `AlphaWolfLessonChecks.Run()` via Pipeline eval.

Visuals are a functional prototype using existing party sprites, a wolf sprite and simple pillar meshes, not finished encounter art. The normal game retains existing warnings and debug UI. English lesson instructions are also used as a fallback in the Chinese table.

## Verified on 2026-09-30

- Unity compilation succeeded; 74 geometry/asset assertions passed.
- 23 final normal-player integration assertions passed: legal dodge, ally intercept/damage/stun, healing, preview cleanup, two-round roar interruption, safe resolution, victory, finished-after-scheduler-stop, UI-pointer restart, fresh session/units/resources, first post-restart logged damage.
- Separate passive run demonstrated pillar collision → break → one-round stun, an unobstructed charge hit, uninterrupted roar damage and natural party defeat in round 20.
- Two normal wins were recorded: first in round 6 via automatic Fire Bolt (27 HP → 0), final regression in round 7 via Fire Bolt (80 HP → 0). No forced HP, outcome or position injection was used. Random crits differ between runs.
- Independent static review found and verified fixes for turn-rewind incompatibility, stale danger cells, unannounced Bite, and premature completion.
- Restart was exercised with UI Toolkit PointerDown/PointerUp on the real button. Keyboard submit is blocked by existing global navigation policy; the native window-coordinate click failed. Neither was reported as successful.

These are Editor Play validations, not a standalone player build or platform certification.
