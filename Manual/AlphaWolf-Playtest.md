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

Blocked charge and damage-interrupted roar stun the wolf: skip remaining current-phase bite and every wolf action in the next phase, while the party still receives its two segments and recovery. The ability sequence pauses during that stunned phase: after sweep → charge → stun, the following phase resumes roar rather than skipping it. After an interrupted roar → stun, it resumes sweep. No additional movement was added to the phase-end bite; its current range is two cells to the nearest living party member.

**Confirmed:** all three party members retain the existing separate Auto Attack stage, subject to their normal weapon rules. Only active character segments are three-choose-two; an unchosen character can still auto attack.

## Party and arena

Guardian uses existing movement/Slash; Fire Mage uses cloned Fire Bolt/Fire Blast; Healer uses Basic heal. Original Astrologer/Archer and other bosses are unchanged.

The 13×13 flat arena (x/z 10–22, floor y=0) is a separate serialized MapChunk and Addressables entry. It avoids unrelated default-map tall obstacles and solid starting cells. Two breakable pillar units retain normal targeting/collision. Terrain geometry and actor sprites remain prototype art. The short English/Chinese encounter panel shows the current telegraph and segment count. Internal preparation/resolution steps are hidden from the queue. Skill menus reuse the equipment skill block and existing staff mechanism note, with a bounded, wrapping tooltip.

Current staff behavior is inherited from Staff: special attack is available initially; casting Fire Blast locks **both** staff attacks until two Recovery events. This is a post-cast lock, not a two-stack preparation resource. Fire Blast still has the inherited 2.5 power multiplier. The user confirmed this exact post-cast cooldown interpretation. A real test verified 2 immediately after casting and after ending only the first segment, then 1 after the first phase-end Recovery, then ready after the second. No generic damage nerf has been applied.

## Completion and restart

Wolf dead = victory; all party members dead = defeat. Restart is enabled only after combat work stops. It reloads CombatBase, then the same encounter, with a new backend/tracker/harness session. Lesson rewind is disabled because the legacy serializer cannot restore its state machine.

## Scope and validation

The original three-person teaching draft supplies fixed sweep, target-following charge, destructible-pillar/ally interception, damage-interruptible roar and spark staff. User clarification supersedes the earlier full-round timing assumption. The other draft's 2×2 body, triple attack, slam, P2 buffs/AP loss/thorns were not merged.

Current isolated playtest numbers: wolf HP900 (raised from the initial 480), base bite/sweep/charge/roar 12/26/32/38 and interrupt threshold70, through the ordinary damage pipeline. Only this cloned wolf's HP changed for balance. A full normal-player-path run won in phase7, after two roar windows: one interrupted and one released. All three survived (Guardian11/76, Fire Mage17/68, Healer73/92). Healing mattered; this is a playable first tuning pass, not statistical balance certification.

Historical checks through d9b5065 (2026-10-01):

- 110 geometry/asset/queue assertions; separate default scheduler timestamp check.
- 39 real-player-path phase assertions after the final arena fix: multiple commands per segment, general EndTurn consuming actor eligibility, two distinct segments, recovery, target-following preview, pillar interception and stun duration.
- 36 real-player-path roar assertions: unblocked charge damage, healing, two-segment damage interruption and staff lock/recovery. This run used the final collision map, before removal of an obsolete visual preview mesh.
- 20 real-player-path ally interception assertions after the visual fix: relocated mage remains at 68 HP; blocking Guardian loses 31 HP; no pillar breaks; wolf stuns.
- 70 completed victory/restart assertions: ordinary movement/skills kill the wolf, the lethal command returns `finished` after teardown, boss HUD retains 0/480 HP, and the real Restart button creates a new session with fresh combat and working damage logs.
- Natural defeat through ordinary Pass/EndTurn also reached all-party death and a stopped scheduler; its real Restart button was verified. The combined exploratory script later failed on a test assumption that a dead boss remains in the harness unit list; its partial 97 checks are not counted as a completed suite. The corrected victory suite above completed.
- 27 negative checks: two explicitly diagnostic removal/death injections verify cancel-without-retarget/damage; an isolated requested/preloaded encounter conflict is rejected. These injections are not presented as player-path victory evidence.

Independent code review covered additive initialization, phase locks, death-event ordering and teardown. Earlier 2026-09-30 prototype results are not acceptance evidence for this revision. The 480-HP runs above validate behavior before tuning. A further 98-check complete run at final HP900 verifies the confirmed cooldown timing, two roar windows, legal healing/attacks and victory after scheduler teardown. Crit variability remains; one tuning win does not establish all-skill balance.

A separate isolated HUD regression test verifies data-object replacement (the rollback path) and the cached zero-HP fallback after renderer destruction; it does not claim an end-to-end rollback of this rewind-disabled lesson.

Scene and asset edits use Editor APIs. `AlphaWolfPlaytestBuilder.Build()` remains a guarded one-time authoring recipe. These are Editor Play checks, not a standalone build certification.

## Focused review revision (2026-10-01)

No combat numbers, bite movement/range, party skills, or staff cooldown rules changed in this revision. Stun now pauses the ability sequence. Runtime resolution checks after each player segment choose the correct 1T/2T boundary even for phases queued before a stun occurs, with an exactly-once guard.

- 110 geometry/asset/queue checks and 4 Boss HUD checks passed after compilation.
- 47 normal-player checks: blocked charge → stunned phase → resumed roar; no release after its first segment, exactly one after its second.
- 42 normal-player checks: clear-lane charge, healing, Fire Blast interruption → stun → resumed sweep, and both Recovery ticks of the staff lock.
- 86 UI checks: English/Chinese labels, rendered damage values, one shared resonance note, 300-unit wrapping tooltip, locale changes without advancing combat, menu/equipment transitions and view re-enable cleanup. Real screenshots were inspected in both locales.
- 24 normal menu close/reopen cycles retained one 11-node skill tooltip tree. Median complete cycle 0.837s, maximum 0.939s, **including local CLI calls and inspection**; these are not frame times or Windows measurements.
- 106 completion checks: ordinary movement/healing/attacks won at phase7 with Guardian58/76, Fire Mage36/68 and Healer92/92. Actual boss death/0HP, scheduler teardown, the real Restart button, new session/phase1 and post-restart damage were verified. Raw combat logs were saved before restart.

All 30 lesson keys have nonempty en-US/zh-CN translations (60 locale entries). The wider 332-entry scan found four pre-existing unrelated gaps: `fighter.combo.tooltip` in both Actions locales and `null` in both Nouns locales. They were not filled with invented fallback text. Existing generic labels such as Equipments/Info/Pass are outside this translation pass.

A first UI test attempt misparsed an existing CRLF inside the Chinese staff description; the parser was corrected and the complete 86-check run above passed. This was a test-record separator error, not a missing translation.

Mac menu testing did not reproduce the reported Windows hang. This revision removes eager full-tooltip formatting, reuses its visual tree and stops rebuilding lesson prose every frame, but no causal evidence establishes a Windows hang fix. The separate Mac launch interruption was a scene-recovery modal; preserved backups and a main-thread sample identified it, and the user resolved it. The available previous-session log does not establish why that Editor session ended.

After the Play checks, the Addressables content rebuild reached a native dialog and the Pipeline request timed out. That build result and a standalone player remain unverified; the successful Editor Play/localization checks above are separate evidence.
