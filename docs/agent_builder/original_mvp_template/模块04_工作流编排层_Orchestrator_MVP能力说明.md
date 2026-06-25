# 模块 04：工作流编排层 / Orchestrator MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 04：工作流编排层 / Orchestrator
```

工作流编排层是可控工程 Agent 的主控层。

它负责把用户输入、Agent 配置、模型调用、Schema 校验、规则执行、工具计划、DryRun、用户审批、正式执行、结果解释、Trace 记录串成一个受控流程。

MVP 中，Orchestrator 必须采用状态机方式实现，而不是临时串行脚本。

---

## 2. 本模块在 MVP 总体流程中的位置

整体链路如下：

```text
用户输入自然语言
↓
前端提交任务
↓
Agent 配置中心加载配置
↓
模型网关准备模型调用能力
↓
Orchestrator 创建 run_id 和工作流实例
↓
加载上下文
↓
识别意图
↓
路由业务 Agent
↓
提取参数
↓
Schema 校验
↓
规则补全、推导、校验
↓
风险判断
↓
生成工具调用计划
↓
DryRun 预演
↓
等待用户审批
↓
正式执行
↓
结果校验
↓
结果解释
↓
Trace 记录
↓
完成 / 失败 / 取消 / 回滚
```

本模块负责控制：

```text
谁先执行；
谁后执行；
失败怎么处理；
什么时候追问；
什么时候阻断；
什么时候 DryRun；
什么时候需要审批；
什么时候可以正式执行；
什么时候记录 Trace；
什么时候结束任务。
```

---

## 3. 模块定位

Orchestrator 的定位是：

```text
让 Agent 的执行过程由状态机控制，而不是由大模型自由控制。
```

它解决的问题是：

```text
1. 防止模型直接执行工程动作；
2. 保证每一步都有明确输入输出；
3. 保证工具调用前经过 Schema、规则、权限、DryRun、审批；
4. 保证失败后有明确去向；
5. 保证人工确认后可以恢复执行；
6. 保证每次运行可追踪、可回放、可测试；
7. 保证后续多个业务 Agent 可以复用同一套主控流程。
```

Orchestrator 不是：

```text
1. 模型；
2. Prompt；
3. 规则引擎；
4. 工具实现；
5. 软件 Adapter；
6. 前端页面；
7. 数据库。
```

一句话：

```text
模型负责理解；
规则负责判断；
工具负责动作；
Orchestrator 负责控制顺序和状态。
```

---

## 4. 为什么 MVP 必须使用状态机

MVP 不能写成简单的临时串行脚本。

临时串行脚本类似：

```text
用户输入
→ 调模型
→ 提参数
→ 调工具
→ 执行
→ 返回结果
```

这种方式看起来简单，但问题是：

```text
参数缺失时不好停；
规则不通过时不好阻断；
DryRun 失败时不好回退；
用户取消时不好恢复；
执行失败时不好定位；
后续加审批、回滚、Trace、多 Agent 路由会很乱。
```

状态机方式则是：

```text
每一步都是明确状态；
每个状态有明确输入；
每个状态有明确输出；
每个状态只能进入允许的下一状态；
失败、取消、追问、审批、回滚都有明确去向。
```

简单理解：

```text
串行脚本 = 一条路跑到底，出问题再补洞。
状态机 = 每一步都有闸门，没满足条件不能往下走。
```

对工程 Agent 来说，必须采用状态机方式。

---

## 5. MVP 必须实现的状态机节点

MVP 状态机建议包含以下节点：

```text
InputReceived
ConfigLoaded
ContextLoaded
IntentRecognized
AgentRouted
ParamsExtracted
SchemaValidated
ParamsNormalized
RulesApplied
BusinessValidated
RiskClassified
PlanGenerated
DryRunExecuted
PreviewReady
WaitingApproval
Executing
ResultValidated
Explained
Traced
Completed
Failed
Cancelled
```

如果 MVP 支持回滚，则增加：

```text
RollingBack
RolledBack
RollbackFailed
```

如果 MVP 支持参数追问，则增加：

```text
WaitingUserInput
UserInputCompleted
```

---

## 6. 推荐 MVP 主流程

标准成功流程：

```text
InputReceived
↓
ConfigLoaded
↓
ContextLoaded
↓
IntentRecognized
↓
AgentRouted
↓
ParamsExtracted
↓
SchemaValidated
↓
ParamsNormalized
↓
RulesApplied
↓
BusinessValidated
↓
RiskClassified
↓
PlanGenerated
↓
DryRunExecuted
↓
PreviewReady
↓
WaitingApproval
↓
Executing
↓
ResultValidated
↓
Explained
↓
Traced
↓
Completed
```

异常分支：

```text
参数缺失 → WaitingUserInput
用户补充 → ParamsExtracted / SchemaValidated

Schema 不通过 → WaitingUserEdit / Failed

规则阻断 → Failed

风险较高 → WaitingApproval

DryRun 失败 → Failed

用户取消 → Cancelled

执行失败 → Failed / RollingBack

回滚成功 → RolledBack

回滚失败 → RollbackFailed
```

---

## 7. 路基模板创建 Agent 的状态机示例

用户输入：

```text
帮我创建一个高速公路路基模板，设计速度 80，路基宽度 24.5。
```

状态机运行示例：

```text
InputReceived：收到用户输入
↓
ConfigLoaded：加载总体 Agent 和业务 Agent 配置
↓
ContextLoaded：读取当前项目、当前路线、当前断面
↓
IntentRecognized：识别意图为创建路基模板
↓
AgentRouted：调用路基模板创建 Agent
↓
ParamsExtracted：提取道路等级、设计速度、路基宽度
↓
SchemaValidated：检查字段类型、单位、必填项
↓
RulesApplied：补默认边坡、结构层、模板参数
↓
BusinessValidated：检查业务参数是否满足创建模板条件
↓
RiskClassified：判断是否修改工程对象、是否需要审批
↓
PlanGenerated：生成工具调用计划
↓
DryRunExecuted：预演会生成什么模板、影响哪些断面
↓
PreviewReady：前端展示预览结果
↓
WaitingApproval：等待用户确认
↓
Executing：用户确认后正式创建模板
↓
ResultValidated：校验模板是否创建成功
↓
Explained：解释执行结果
↓
Traced：记录全过程
↓
Completed：完成任务
```

如果参数缺失：

```text
ParamsExtracted
↓
发现缺少关键参数
↓
WaitingUserInput
↓
用户补充
↓
重新进入 ParamsExtracted / SchemaValidated
```

如果 DryRun 失败：

```text
DryRunExecuted
↓
Failed
↓
返回失败原因和处理建议
```

如果用户取消：

```text
WaitingApproval
↓
Cancelled
```

---

## 8. 每个节点的最小输入输出

### 8.1 InputReceived

输入：

```text
用户自然语言；
当前项目；
当前模块；
当前选中对象；
前端选择的模型 / API；
session_id。
```

输出：

```text
run_id；
初始 workflow_state。
```

---

### 8.2 ConfigLoaded

输入：

```text
main_agent_id；
用户选择的模型 / API；
当前项目；
当前模块。
```

输出：

```text
总体 Agent 配置；
业务 Agent 注册表；
模型策略；
权限策略；
配置版本。
```

---

### 8.3 ContextLoaded

输入：

```text
当前项目；
当前模块；
当前选中对象；
当前会话。
```

输出：

```text
Context Package。
```

---

### 8.4 IntentRecognized

输入：

```text
用户输入；
Context Package；
模型策略；
Prompt。
```

输出：

```text
intent；
confidence；
候选业务 Agent；
是否需要澄清。
```

---

### 8.5 AgentRouted

输入：

```text
intent；
confidence；
当前模块；
AgentRouter 配置。
```

输出：

```text
routed_agent；
route_reason；
是否需要用户确认。
```

---

### 8.6 ParamsExtracted

输入：

```text
用户输入；
Context Package；
业务 Agent Skill；
参数提取 Prompt；
ParamSchema。
```

输出：

```text
结构化参数；
缺失字段；
字段来源；
置信度。
```

---

### 8.7 SchemaValidated

输入：

```text
结构化参数；
ParamSchema。
```

输出：

```text
Schema 校验结果；
字段错误；
是否允许继续。
```

---

### 8.8 ParamsNormalized

输入：

```text
Schema 校验后的参数；
单位规则；
枚举映射规则；
桩号格式规则。
```

输出：

```text
标准化后的参数；
标准化记录；
字段变更说明。
```

---

### 8.9 RulesApplied

输入：

```text
已校验参数；
项目配置；
业务规则；
默认值规则；
推导规则。
```

输出：

```text
补全后的参数；
规则命中结果；
自动修复结果；
警告；
阻断错误。
```

---

### 8.10 BusinessValidated

输入：

```text
规则处理后的参数；
业务校验规则；
当前工程上下文。
```

输出：

```text
业务校验结果；
是否满足执行条件；
阻断原因；
警告信息。
```

---

### 8.11 RiskClassified

输入：

```text
任务类型；
工具计划候选；
影响对象；
保存策略；
回滚能力。
```

输出：

```text
风险等级；
是否需要审批；
是否需要强确认；
是否允许继续。
```

---

### 8.12 PlanGenerated

输入：

```text
最终参数；
业务 Agent 工具范围；
风险策略。
```

输出：

```text
工具调用计划；
动作列表；
预计影响对象。
```

---

### 8.13 DryRunExecuted

输入：

```text
工具调用计划；
工具入参；
当前工程上下文。
```

输出：

```text
DryRun 结果；
影响范围；
预览结果；
风险提示；
是否支持回滚。
```

---

### 8.14 PreviewReady

输入：

```text
DryRun 结果；
预览数据；
规则命中信息；
风险信息。
```

输出：

```text
前端可展示的预览包；
审批说明；
用户可操作项。
```

---

### 8.15 WaitingApproval

输入：

```text
DryRun 结果；
风险等级；
审批策略；
预览包。
```

输出：

```text
用户确认；
用户取消；
用户修改参数；
用户要求解释。
```

---

### 8.16 Executing

输入：

```text
用户确认记录；
工具调用计划；
最终参数。
```

输出：

```text
工具执行结果；
成功 / 失败；
变更对象；
错误码。
```

---

### 8.17 ResultValidated

输入：

```text
工具执行结果；
期望结果；
验收规则。
```

输出：

```text
执行是否有效；
是否部分成功；
是否需要回滚；
结果摘要。
```

---

### 8.18 Explained

输入：

```text
执行结果；
规则命中结果；
参数来源；
DryRun 结果；
错误信息。
```

输出：

```text
给用户看的结果解释；
默认值来源说明；
警告说明；
下一步建议。
```

---

### 8.19 Traced

输入：

```text
本次运行所有节点输入输出；
模型调用记录；
规则命中记录；
工具调用记录；
用户审批记录。
```

输出：

```text
Trace ID；
运行日志；
失败样例候选。
```

---

### 8.20 Completed / Failed / Cancelled

输出：

```text
最终状态；
结果解释；
Trace ID；
下一步建议。
```

---

## 9. 状态流转规则

MVP 中需要明确哪些状态可以进入哪些状态。

示例：

```text
InputReceived 只能进入 ConfigLoaded 或 Failed；
ConfigLoaded 只能进入 ContextLoaded 或 Failed；
ContextLoaded 只能进入 IntentRecognized 或 Failed；
IntentRecognized 可以进入 AgentRouted、WaitingUserInput 或 Failed；
AgentRouted 可以进入 ParamsExtracted、WaitingUserInput 或 Failed；
ParamsExtracted 可以进入 SchemaValidated、WaitingUserInput 或 Failed；
SchemaValidated 可以进入 ParamsNormalized、WaitingUserEdit 或 Failed；
ParamsNormalized 可以进入 RulesApplied 或 Failed；
RulesApplied 可以进入 BusinessValidated、WaitingUserInput 或 Failed；
BusinessValidated 可以进入 RiskClassified 或 Failed；
RiskClassified 可以进入 PlanGenerated、WaitingApproval 或 Failed；
PlanGenerated 可以进入 DryRunExecuted 或 Failed；
DryRunExecuted 可以进入 PreviewReady 或 Failed；
PreviewReady 可以进入 WaitingApproval；
WaitingApproval 可以进入 Executing、Cancelled、WaitingUserInput 或 Explained；
Executing 可以进入 ResultValidated、RollingBack 或 Failed；
ResultValidated 可以进入 Explained、RollingBack 或 Failed；
Explained 可以进入 Traced；
Traced 可以进入 Completed；
```

核心原则：

```text
不能从 ParamsExtracted 直接跳到 Executing；
不能从 RulesApplied 直接跳到正式执行；
不能绕过 DryRun；
不能绕过用户审批；
不能由模型自由决定状态跳转；
状态跳转必须由 Orchestrator 根据规则和结果判断。
```

---

## 10. Orchestrator 与其他模块的关系

Orchestrator 调用：

```text
Agent 配置中心；
上下文管理层；
模型网关；
IntentRouter；
AgentRouter；
Schema Validator；
Normalizer；
Rule Engine；
Risk Classifier；
Policy Guard；
Tool Registry；
DryRun Executor；
Approval Gate；
Execution Engine；
Rollback Manager；
Trace Logger。
```

Orchestrator 不直接调用：

```text
具体大模型 API；
CAD / BIM 内部函数；
未注册工具；
未经校验的工程动作。
```

---

## 11. MVP 中不做范围

Orchestrator MVP 暂不做：

```text
复杂多 Agent 自治；
模型自己规划任意节点；
并行工具调用；
复杂图可视化编排；
长期任务调度；
跨项目工作流；
分布式任务队列；
复杂人工协同审批流。
```

但需要预留：

```text
Agent Handoff；
Workflow Version；
节点重试；
人工中断；
恢复执行；
回滚状态；
Trace 回放；
失败样例沉淀。
```

---

## 12. MVP 验收标准

本模块完成后，应满足：

```text
1. 每次运行都有 run_id；
2. 每个任务都有明确 workflow_state；
3. 工作流采用状态机实现；
4. 每个状态有明确输入和输出；
5. 每个状态只能进入允许的下一状态；
6. 模型不能跳过状态机直接执行工具；
7. 工具调用前必须经过参数提取、Schema 校验、规则校验；
8. 执行前必须经过 DryRun；
9. 需要审批时必须停在 WaitingApproval；
10. 用户确认后才能进入 Executing；
11. 用户取消后进入 Cancelled；
12. 任一节点失败后能进入 Failed；
13. 执行失败时能触发回滚或给出不可回滚说明；
14. 每个节点的输入输出能进入 Trace；
15. 后续新增业务 Agent 时，可以复用主状态机。
```

---

## 13. 本模块结论

工作流编排层是 MVP 的“中枢控制系统”。

它的核心不是让模型更聪明，而是让流程更可控。

正确设计是：

```text
模型只能在指定节点输出；
规则只能在指定节点执行；
工具只能在指定节点调用；
用户确认只能在指定节点发生；
工程修改只能在执行节点发生；
Trace 必须覆盖所有节点。
```

MVP 中最重要的原则是：

```text
任何工程动作都不能绕过 Orchestrator。
```

推荐命名：

```text
Orchestrator / 工作流编排层 / 状态机主控层
```

一句话：

```text
状态机不是为了复杂，而是为了让 Agent 每一步都可控、可停、可查、可回退。
```
