# Gameplay harness（Editor / v1）

通用于当前战斗系统的语义操作接口。使用正在运行的 Unity Editor 和 Pipeline；不需要新服务器，不依赖屏幕坐标。AlphaWolf 是验收用例，不是接口内置场景。

## 开始

使用项目记录的 Unity 版本打开项目，等待编译完成，正常打开包含战斗服务的入口场景并进入 Play。当前 AlphaWolf 验收从 `Assets/Scenes/CombatBase.unity` 开始。测试期间由 agent 单独操作。

```sh
python3 Tools/gameplay_harness.py '{"op":"observe"}'
```

客户端从自身文件位置定位项目。需要现有 `unity` CLI 在 PATH、Pipeline 可连接。内部只调用：

```csharp
miniRAID.EditorTools.GameplayHarness.Call(jsonRequest)
```

默认输出为 compact；请求加 `"detail":"full"` 或 CLI 加 `--full` 可取完整调试快照。`result` 查询可以切换显示粒度，同一动作不会重新执行；detail 不计入幂等身份。

`observe` 返回 `session`、`version` 和紧凑 `state`。状态包括单位 GUID、位置/格子、HP/AP/移动余量、当前菜单、回合锁、未来最多8个可见调度条目及目标选项。菜单条目 `id` 仅在当前版本有效；单位 GUID 仅在本次 Play 会话有效。不要使用昵称、菜单顺序或上一次会话的 GUID 作为长期身份。

## 玩家操作

每个修改请求必须包含最新观察中的 `session`、`version`，以及新的字符串 `id`。以下 ID 和坐标只是格式示例，必须替换为实际观察结果：

```json
{"op":"select","id":"select-1","session":"<session>","version":1,"unit":"<unit-id>"}
{"op":"menu","id":"move-1","session":"<session>","version":3,"entry":0}
{"op":"target","id":"destination-1","session":"<session>","version":5,"grid":[14,1,15]}
{"op":"cancel","id":"cancel-1","session":"<session>","version":7}
{"op":"general","id":"general-1","session":"<session>","version":9}
```

- `select`：从 FreeView 走正常玩家单位选择流程。
- `menu`：执行当前菜单中的启用条目；技能、移动、Pass、EndTurn 都通过同一个入口。`general` 打开正常的空格通用菜单；从中执行 EndTurn。Cheat 和回合回退明确禁用。
- `target`：向当前 requester 提交合法格子。移动保留 BFS/路径/消耗规则，技能保留 requester/视线/冷却/费用/行动收尾。方向技能使用返回的四个邻格。
- `cancel`：执行当前 UI 状态的取消逻辑；多阶段目标可能退回上一步。
- 状态为 `await_target` 时可提交目标或取消；`resolving` 时等待。

默认 compact 在目标总数不超过8时直接附上 choices；较大集合只返回 kind/count，避免重复展开。full 在 `observe` 中最多显示64项。更多选项通过只读分页获取，按 x/y/z 排序：

```sh
python3 Tools/gameplay_harness.py '{"op":"options","offset":64,"limit":128}'
```

返回总数、会话、版本和该页格子；上限128。跨页版本改变时重新观察。菜单可用表示 UI 允许开始该操作；完整目标/费用校验仍可能在执行阶段拒绝。

## 等待结果，不重复动作

```sh
python3 Tools/gameplay_harness.py --wait '<完整修改请求 JSON>'
python3 Tools/gameplay_harness.py '{"op":"result","id":"destination-1"}'
```

客户端在本机等待，只输出最终 JSON，不需要模型逐帧轮询。菜单操作成功可能表示已经到达 `await_target`，并不意味着技能已经施放；目标提交成功后会等待动作与 UI 收尾以及下一个玩家决策点。`succeeded` 不表示命中或胜利，实际结果以返回状态为准。

- `accepted:false, status:rejected`：未提交动作，例如非法目标、过期版本、忙、禁用菜单。已知字段类型在动作提交前校验；`invalid_request` 不会触发游戏回调或令会话故障。
- `accepted:true, pending:true`：已接受，仍在执行。
- `accepted:true, status:succeeded`：到达下一个决策点，`result` 是当时快照。
- `accepted:true, status:rejected`：执行阶段检查拒绝，`reason` 说明原因。
- `status:timed_out, pending:true`：只是等待超时；**动作可能仍在运行，没有撤销，不要换 ID 重发**。继续查同一个 `id`。
- `faulted`：执行时出现异常（同步回调异常可能返回 `accepted:null`，表示无法保证尚未改变状态），停止接受新动作；不声称状态已回滚，正常停止/重新进入 Play 后再测试。

同 ID、同动作请求（忽略 detail）返回原结果；同 ID 不同动作内容拒绝。最近256个已接受请求保留到退出 Play/域重载；之后旧结果不可查询。会话更换使旧请求失效。连接失败也不能推断动作未执行；恢复连接后先查询该 ID。客户端只对只读请求做短暂重试，不重发修改请求。

## 减少输出和模型往返

compact 初始观察包含全部单位；动作结果的 `state.unitsChanged` 只包含相对此动作提交前变化的单位及字段，`unitsRemoved` 为消失的 ID。未出现的单位/字段表示未变化。队列仅在变化时返回；菜单返回当前可操作条目，空值和0冷却省略；phase/ui 告诉调用方当前是菜单还是选目标。不要把 unitsChanged 当成全量单位列表。显式 `observe` 可以重新同步完整的精简观察。

已知连续步骤可以在一个本地 CLI 调用内串联，省去每步都让模型再次调用工具；下面的单位与格子必须来自当前实际观察/合法选项：

```sh
python3 Tools/gameplay_harness.py --sequence '[{"op":"select","unit":"<unit-id>"},{"op":"menu","label":"Move"},{"op":"target","grid":[15,1,15]}]'
```

这是简单客户端顺序执行，不是规划器。初始只观察一次，每步使用上一结果的最新 session/version；内部仍逐步走原 UI 验证。menu label 必须唯一，也可以直接给 entry。拒绝/fault/timeout 立即停止后续步骤。传输异常返回 unknown、完整 request 和 ID，不能推断是否执行；查询该 ID 后再决定。steps 保留每步状态和变化，所以末步取消不会吞掉前面移动/伤害信息。没有自动补操作或重放。

wait 只在本机轮询，间隔从0.15秒逐步退避至0.6秒。网络/域重载重试会延长客户端实际耗时，等待参数不是硬性进程截止时间；服务端 timed_out 仍表示等待超时、动作未撤销。

实测同一次动作的 full/compact 完成响应：Move 1998/875 B，Slash 1999/933 B；初始 observe 920/635 B。没有安装 tokenizer，以 UTF-8 bytes/3 作中心估算、bytes/4..bytes/2 给范围；不是模型实际计费。7步 sequence 在本机2.336秒、16次内部CLI调用完成，模型侧只需1次工具调用。动画耗时未改变，不能把减少输出字节等同于同等比例加速。

复现小型基准（会进入/停止 Play，要求 CombatBase 默认 AlphaWolf）：

```sh
python3 Tools/benchmark_gameplay_harness.py --run --output /tmp/miniraid-benchmark.json
```

## 验收

以下脚本会正常重启 Play、执行测试，最后停止 Play。先把 Editor 停在 CombatBase；脚本对 AlphaWolf 默认 Warrior 菜单有明确期望：

```sh
python3 Tools/test_gameplay_harness.py --run --output /tmp/miniraid-harness-tests.json
```

覆盖选择、合法目标分页、取消、非法目标无状态变化、过期/重复/冲突、忙、超时后结果、BFS移动、Slash费用、费用不足拒绝、调试入口禁用、EndTurn、重启隔离、Pass。实际伤害有随机性，测试检查真实费用与状态推进，不把一次伤害数字固定为规则。

复核回归另覆盖敌方检查菜单权限、错误参数无副作用、连续只读查询、真实 WindBuff 费用回调的隔离测试，以及推进至敌方实际施法后返回玩家决策点。脚本在重启前逐字备份 `review-miniRAID.combat.log` 与 `review-miniRAID.log` 到结果文件所在目录。编号回合内可能有多个玩家行动段，EndTurn 不保证回合数字立即加一。

在现有 Astrologer 场景还实测了真实四方向技能：连续施放两次、第三次进入后取消、非法方向无快照变化。没有修改技能、单位或场景资产。

本次观察到移动 `[16,1,15] → [14,1,15]`，随后 Slash 消耗2 AP，AlphaWolf HP `680 → 638`。该数字是一次运行证据。

## 范围

保留正常动画和游戏规则；不做严格确定性、无渲染模式、多人控制切换或任意状态修改。没有通用胜负判定，现有 `IsCombatFinished()` 仍未实现。已有时间戳 TODO、随机源和存档恢复问题不在此次改动范围。

协议报错与编译/运行 Console 是互补的：已有 `Timestamp is not correct` 日志不会自动被当作 harness 异常。正常玩家 UI 仍可使用共用入口。版本以观察状态变化及命令提交为界，不是全游戏事务版本。

当前完成判定对多阶段 requester 比较类型与合法格子集合；未来若引入连续两阶段集合完全相同的 requester，需要补充阶段标识。现有实测方向、移动和单体技能没有触发此限制。费用查询只隔离 Cost/dNumber；未来自定义查询回调仍须遵守只读约定。

2026-09-30 后续在项目外隔离 venv 安装 tiktoken 0.14.0，用 `o200k_base` 对上述同份紧凑 JSON 文本补算：初始 observe full/compact 为383/234 tokens，Move 完成为759/284，Slash 完成为760/310，64项目标页为422，7步 sequence 输出为399。此前 bytes 范围只是粗估（例如目标页粗估偏低）；这些是指定 encoding 的精确文本计数，不代表未知当前模型的计费，也未包含工具包装/隐藏提示。venv 与 encoding 缓存均在项目外，不是项目依赖。

## Slime King 实战后修复：按需战术信息与失败归属

`observe` 的单位增加 mana（无对应资源时 null）。完成响应仍只返回变化单位，mana / buffs 变化也参与版本判断并返回增量；显式 tactical 才展开所有 Buff 与可见覆盖格。

```sh
python3 Tools/gameplay_harness.py '{"op":"tactical","unit":"<可选单位ID>","offset":0,"limit":32}'
python3 Tools/gameplay_harness.py '{"op":"options_detail","offset":0,"limit":16}'
```

`tactical`：读取与 UnitBar 相同的 mana/current/max 和 Buff detailedName，以及现有可见队列、Boss 面板已显示的 incomingText、正在绘制的 GridOverlay。覆盖格分页上限128；不是所有覆盖格都是危险区。没有通用权威危险区分类，也不查询隐藏 AI 计划，因此 hazards 和超出可见队列的预测明确 unknown。unit 只过滤单位详情，不过滤战场覆盖格。未实现隐藏资源推断。

`options_detail` 默认16格，共同费用说明只返回一次；沿用当前 requester 的合法格子页，每项包含 grid、该格碰撞到的 unit（空格 null；方向选择时不代表最终受击者）以及 movement 预览（非移动 null）。movementAP、moveRemaining 来自同一缓存 BFS 路径、逐步距离与实际 AP 向下取整规则；页级 movementRules.actionCostBounds 是 UI 已有基础费用界限，单独列出，不把它混作移动距离费用。动态触发效果不能提前保证，返回 excludes 明示这一点。旧 options 格子数组格式保持不变。

每个 UI 提交持有自己的动作收据，目标提交沿用同一次 UI 动作。只有匹配该玩家与 RuntimeAction 且处在 action 主体执行期间的失败属于它；onFinished/AutoPass/后续敌方行动不污染收据。完成仍等待最终玩家决策点。

- 执行前费用/冷却等失败：accepted=true、rejected、effectsMayHaveOccurred=false。
- 已应用费用或开始效果后失败：partial_failure、effectsMayHaveOccurred=true；不承诺回滚，不自动重试。
- 敌方后续失败：不把已经完成的玩家动作改成 rejected；原游戏日志继续保留诊断。
- pending/timeout/fault 语义不变。已完成结果的效果标记冻结，后续目标操作不会改变此前菜单请求的历史结果。

定向验收脚本 `Tools/test_gameplay_tactical.py --run --output /tmp/tactical-tests.json` 会重启 Play，验证 AlphaWolf 移动费用、只读查询、实体映射、真实运行时 AP 不足，以及 SlimeKing 资源与实际 EndTurn。包含明确标注的临时失败注入夹具：归属/部分失败检查不是自然发生的战斗证据。测试退出 Play 清理，不保存场景或修改游戏资产。
