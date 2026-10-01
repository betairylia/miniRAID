# AlphaWolf small-scene playtest

Use **miniRAID → AlphaWolf → Open playable lesson** in Unity 6000.3.15f1, then Play. This opens **CombatBase + AlphaWolfPlaytest additively**. The encounter no longer contains duplicate combat services. A fresh backend/tracker is created before Base UI binds, and combat waits for encounter loading and unit initialization.

## Current design clarification (2026-10-01)

Three characters; choose two distinct characters per phase. A character can move and use several commands inside their one segment. Pass or general EndTurn ends that segment; it does not grant another segment to the same character. Recovery restores eligibility for the following phase.

**T means one player character action segment**, not a command and not a full round:

- Sweep windup → character A → fixed-cell sweep → character B → phase-end bite → recovery.
- Charge windup → character A → charge → character B → phase-end bite unless stunned → recovery.
- Roar windup → character A → character B → roar → phase-end bite unless stunned → recovery.

The phase counter is currently displayed by the existing round counter. Each phase has exactly two player segments in the scheduler, including the current segment. The first phase has a one-time initial wake-up; subsequent recovery is at phase end.

Charge locks **character identity**, not a destination. Moving that character updates the visible path. Resolution uses its current position, with the first pillar or other ally intercepting normally. The wolf stops before the occupied cell; pillars break and allies take damage. A dead/unavailable marked target cancels the charge without choosing a new target. Sweep remains fixed to its original cells.

Blocked charge and damage-interrupted roar stun the wolf: skip remaining current-phase bite and every wolf action in the next phase, while the party still receives its two segments and recovery. The wolf resumes in the following phase. No additional movement was added to the phase-end bite; its current range is two cells to the nearest living party member.

**Open design detail:** the existing separate party Auto Attack stage is currently retained pending confirmation. It can allow an otherwise unchosen character to auto attack and is not counted as a player command segment. Do not consider final action economy/balance verified until this is resolved.

## Party and arena

Guardian uses existing movement/Slash; Fire Mage uses cloned Fire Bolt/Fire Blast; Healer uses Basic heal. Original Astrologer/Archer and other bosses are unchanged.

The 13×13 flat arena (x/z 10–22, floor y=0) is a separate serialized MapChunk and Addressables entry. It avoids unrelated default-map tall obstacles and solid starting cells. Two breakable pillar units retain normal targeting/collision. Terrain geometry and actor sprites remain prototype art. Long tutorial hints can be expanded; the current telegraph is always visible.

Current staff behavior is inherited from Staff: special attack is available initially; casting Fire Blast locks **both** staff attacks until two Recovery events. This is a post-cast lock, not a two-stack preparation resource. Fire Blast still has the inherited 2.5 power multiplier. These are descriptive implementation facts, not a claim that the draft's intended resonance loop is settled. No generic damage nerf has been applied.

## Completion and restart

Wolf dead = victory; all party members dead = defeat. Restart is enabled only after combat work stops. It reloads CombatBase, then the same encounter, with a new backend/tracker/harness session. Lesson rewind is disabled because the legacy serializer cannot restore its state machine.

## Scope and validation

The original three-person teaching draft supplies fixed sweep, target-following charge, destructible-pillar/ally interception, damage-interruptible roar and spark staff. User clarification supersedes the earlier full-round timing assumption. The other draft's 2×2 body, triple attack, slam, P2 buffs/AP loss/thorns were not merged.

Prototype numbers remain wolf HP480, base bite/sweep/charge/roar 12/26/32/38 and interrupt threshold70, through the ordinary damage pipeline. Final balance toward roughly two–three roar cycles is pending action-economy/resonance clarification and final normal-play acceptance.

Current checks (2026-10-01):

- 110 geometry/asset/queue assertions; separate default scheduler timestamp check.
- 39 real-player-path phase assertions after the final arena fix: multiple commands per segment, general EndTurn consuming actor eligibility, two distinct segments, recovery, target-following preview, pillar interception and stun duration.
- 36 real-player-path roar assertions: unblocked charge damage, healing, two-segment damage interruption and staff lock/recovery. This run used the final collision map, before removal of an obsolete visual preview mesh.
- 20 real-player-path ally interception assertions after the visual fix: relocated mage remains at 68 HP; blocking Guardian loses 31 HP; no pillar breaks; wolf stuns.
- 70 completed victory/restart assertions: ordinary movement/skills kill the wolf, the lethal command returns `finished` after teardown, boss HUD retains 0/480 HP, and the real Restart button creates a new session with fresh combat and working damage logs.
- Natural defeat through ordinary Pass/EndTurn also reached all-party death and a stopped scheduler; its real Restart button was verified. The combined exploratory script later failed on a test assumption that a dead boss remains in the harness unit list; its partial 97 checks are not counted as a completed suite. The corrected victory suite above completed.
- 27 negative checks: two explicitly diagnostic removal/death injections verify cancel-without-retarget/damage; an isolated requested/preloaded encounter conflict is rejected. These injections are not presented as player-path victory evidence.

Independent code review covered additive initialization, phase locks, death-event ordering and teardown. Earlier 2026-09-30 prototype results are not acceptance evidence for this revision. Current normal-play victory is faster than the intended two–three roar-cycle balance target; this remains a tuning/semantics question, not a completed balance claim.

Scene and asset edits use Editor APIs. `AlphaWolfPlaytestBuilder.Build()` remains a guarded one-time authoring recipe. These are Editor Play checks, not a standalone build certification.
