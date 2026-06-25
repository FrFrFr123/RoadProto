# 入口路由：闲聊、咨询与工作流判断

## 1. 定位

入口路由层负责判断用户一句话应该如何进入 Agent。它位于自然语言输入之后、正式 Agent / Skill / Intent 识别之前。

用户希望 Agent 自主判断，因此 MVP 不应强制用户手动切换“聊天 / 执行”模式。但自主判断不等于自由执行，低置信度和高风险必须追问。

## 2. 路由类型

| 类型 | 含义 | 是否进入工作流 |
| --- | --- | --- |
| `ChatOnly` | 闲聊、寒暄、非工程任务 | 否 |
| `HelpOnly` | 概念解释、怎么设置、参数含义、规范咨询 | 否 |
| `WorkflowCandidate` | 像是工程动作，但动作、对象或参数不完整 | 暂不执行，先追问 |
| `WorkflowContinuation` | 当前已有等待补充的任务，用户在补充参数 | 是，继续原任务 |
| `WorkflowCommand` | 明确工程动作和业务对象 | 是，进入 Agent / Skill / Intent |
| `UnsupportedWorkflow` | 明确工程对象但当前未接入对应 Skill | 否，终止性提示，不保持等待 |
| `BlockedOrUnsafe` | 越权、危险、无法确认目标或不支持 | 否，解释阻断原因 |

入口路由只决定“进入哪条通道”，不直接等同于最终回答。尤其是 `ChatOnly` 和 `HelpOnly`：它们不应进入工程意图识别，也不应调用 Tool，但可以进入受控大模型对话通道，生成像正常助手一样自然的回复。

受控对话通道还应先经过运行时事实层。日期、时间、当前 Provider、当前模型名、Agent 身份、宿主软件连接状态等事实不应让模型自由生成。命中这类问题时，后端直接返回事实；未命中时，再把事实注入 prompt 交给模型自然回答。

建议把三个策略拆开记录：

| 字段 | 示例 | 作用 |
| --- | --- | --- |
| `routeType` | `ChatOnly` / `HelpOnly` / `WorkflowCommand` | 判断用户输入类型 |
| `responseMode` | `LLMChat` / `LLMConsult` / `Clarify` / `Workflow` | 判断最终如何回答 |
| `toolPolicy` | `NoTool` / `ToolAfterConfirm` | 判断是否允许工具调用 |
| `factPolicy` | `RuntimeFactsFirst` / `ModelWithFacts` | 判断是否先由后端事实层回答 |

## 3. 优先级

入口路由优先级建议：

```text
当前等待补充的任务
> 明确取消 / 停止 / 不执行
> 明确工程动作
> 咨询解释
> 闲聊
> 不确定追问
```

### 3.1 当前工作流补充优先

如果系统刚问：

```text
请问道路等级是什么？
```

用户回答：

```text
高速公路
```

应识别为 `WorkflowContinuation`，不是闲聊。

但并非所有等待补充都应无条件继续原任务。若上一轮只是“动作候选、对象未定”，例如：

```text
用户：我想创建
Agent：请问你要操作哪个工程对象？
用户：道路模型
```

第二轮必须重新执行入口路由，识别出 `road_model` 是独立 Skill 候选；不能把“道路模型”硬塞进上一轮默认 Skill，更不能误进入路基模板创建。

同样，若当前任务已经进入某个 Intent 的缺参追问，也不能把后续所有输入都当成参数补充。系统应先判断用户新输入本身是否是明确闲聊、咨询、运行时事实问题、未接入对象或新的完整工程指令；如果是，应记录类似 `ContinuationRerouted` 的阶段并重新入口路由。例如“请先明确要操作的路基模板目标”之后用户说“你好”，应进入 `ChatOnly`；用户说“今天是几号”，应由运行时事实层回答；用户说“创建路基模板”，应按新的工程指令重新识别。只有“高速公路”“宽度 3.75 米”“当前选中的模板”这类可作为追问答案的短补充，才继续原任务。

### 3.2 明确工程动作进入候选工作流

例如：

```text
创建路基模板
修改当前模板行车道宽度
删除这个路基模板
查询图里的路基模板
```

这类进入 `WorkflowCommand` 或 `WorkflowCandidate`，再由 Agent / Skill / Intent 精细识别。

### 3.3 解释和咨询不执行

例如：

```text
路基模板怎么设置？
高速公路路基一般多宽？
这个参数是什么意思？
为什么需要硬路肩？
```

这类进入 `HelpOnly`，可以回答，也可以推荐可执行动作，但不能直接调用 Tool。

`HelpOnly` 推荐走 `LLMConsult` 响应模式：系统提示限定为工程咨询，不执行、不写入、不声称已操作宿主软件。这样用户能得到顺畅解释，而不会误触发工作流。

### 3.4 不完整动作先追问

例如：

```text
弄个模板
帮我处理一下
路基模板
```

应进入 `WorkflowCandidate`，追问：

```text
你是想创建、修改、删除还是查询路基模板？
```

对象词必须精确管理，不能只靠“模板”这类泛化词路由。比如 `道路模型`、`路面结构模板`、`边坡模板` 都不是 `路基模板`；如果对应 Skill 尚未接入，应返回 `UnsupportedWorkflow` 这类终止性说明，而不是落到现有 Skill，也不要保持等待补充状态。

## 4. 路由输出结构

建议输出：

```json
{
  "routeType": "WorkflowCandidate",
  "confidence": 0.72,
  "reason": "用户提到路基模板，但未说明创建、修改、删除或查询",
  "candidateAgentId": "engineering_agent",
  "candidateSkillId": "subgrade_template",
  "candidateIntentIds": [
    "subgrade_template.create",
    "subgrade_template.modify",
    "subgrade_template.query"
  ],
  "followUpMessage": "你是想创建、修改、删除还是查询路基模板？",
  "shouldCallModelForIntent": false,
  "shouldCallTool": false
}
```

## 5. 前端展示规则

前端不应只展示最终回答。至少应在日志或 thinking 区展示：

```text
入口路由：WorkflowCandidate
候选 Skill：subgrade_template
原因：动作不明确
下一步：追问用户
```

正式聊天区可只显示自然话术：

```text
你是想创建、修改、删除还是查询路基模板？
```

## 6. Trace 阶段

入口路由必须记录：

- `InputReceived`
- `EntryRouted`
- `RouteType`
- `RouteConfidence`
- `RouteReason`
- `CandidateAgent`
- `CandidateSkill`
- `CandidateIntent`
- `FollowUpMessage`

## 7. RoadProto 实践修正

RoadProto MVP 中出现过“缺道路等级时只显示请补充必要信息”的问题。修正结论是：

- 具体追问必须作为 run 级字段返回前端，例如 `followUpMessage`。
- 前端应优先展示 run 级追问，再兼容 plan 级追问，最后才使用兜底文案。
- Trace 里记录追问不够，用户可见响应也必须有同一条追问。

当前 RoadProto MVP 已接入规则版入口路由：

- `ChatOnly` 和 `HelpOnly` 在模型意图识别前拦截，不进入工程 Skill / Intent，不调用 Tool；当前通过受控对话 Agent 调用已配置模型生成自然回答。
- `ChatOnly` 和 `HelpOnly` 会先检查运行时事实问题；“今天几号”“你是什么模型”等由后端事实层直接回答，不调用模型。
- `WorkflowCandidate` 返回 `AwaitingUserInput` 和具体追问。
- `WorkflowCommand` 才继续调用现有模型版 Skill / Intent 识别链路。
- WPF DTO 暴露 `entryRoute`，面板日志记录 `EntryRouted`。
- `道路模型` 被显式作为独立工程对象处理，候选 Skill 为 `road_model`；当前 MVP 未接入该 Skill 时返回 `UnsupportedWorkflow` 终止性提示，不调用 `subgrade_template` Tool，也不让后续输入继续粘在旧 Run 上。
- 对象未定的 `WorkflowCandidate` 在用户补充对象时会重新执行入口路由，避免“我想创建 -> 道路模型”沿用上一轮上下文进入错误 Skill。
- 已识别 Intent 但处于缺参追问时，也会先检查补充内容是否是明确闲聊、咨询、运行时事实问题、未接入对象或新的工程指令；命中时记录 `ContinuationRerouted` 并重新路由，避免“修改模板缺目标 -> 你好”继续卡在模板目标追问。

本次修正的关键经验是：不要把“禁止 Tool”误实现成“禁止模型回答”。闲聊和咨询应该禁止工程执行，但仍可调用大模型做自然对话；真正需要禁止的是工具调用、CAD 写入、DWG 保存和未经审批的状态改变。

进一步修正的经验是：不要把“自然对话”误实现成“所有回答都交给模型”。当前日期、当前时间、模型 Provider、模型名和 Agent 身份等运行时事实，必须由系统事实层提供。模型可以组织语言，但不能自己猜这些事实。

## 8. MVP 验收标准

- 闲聊不会触发 Tool。
- 闲聊能以受控大模型方式自然回复。
- 闲聊中的运行时事实问题能由后端事实层直接回答。
- 咨询不会触发 Tool。
- 咨询能以受控大模型方式解释概念和规则。
- 当前等待补充时，短回答能进入原任务。
- 对象未定的补充输入能重新路由，不会把 `道路模型` 误识别成 `路基模板`。
- 缺参追问期间的明确闲聊或新指令能跳出旧任务，不会无限重复同一追问。
- 未接入对象返回后，下一句用户输入会作为新任务重新路由，不会进入循环追问。
- 明确工程动作能进入正确 Skill / Intent。
- 动作不完整时能追问动作。
- 参数不完整时能追问参数。
- 低置信度不会自动执行。
- 前端和 Trace 都能看到路由结果。
