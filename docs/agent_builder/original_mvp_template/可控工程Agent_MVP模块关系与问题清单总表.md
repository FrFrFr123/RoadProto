# 可控工程 Agent MVP 模块关系与问题清单总表

> 文档定位：本文件是可控工程 Agent MVP 的模块总表，用于说明 12 个核心模块之间的关系、各自要解决的问题、输入输出边界和 MVP 必做内容。  
> 核心原则：MVP 不是砍掉架构，而是保留完整链路，每个模块先做最小可运行能力。后续成熟版应以增补为主，不推倒重来。

---

## 1. MVP 总体模块关系

```text
用户 / 前端
↓
01 前端交互层 Agent Console
↓
02 Agent 配置中心 / 后端后台配置服务
↓
03 模型网关 Model Gateway
↓
04 工作流编排层 Orchestrator
↓
05 上下文管理层 Context Manager
↓
06 LLM 能力层：意图识别 / 参数提取 / 追问 / 解释
↓
07 Schema 结构化控制层
↓
08 规则引擎层 Rule Engine
↓
09 工具与原子函数层 Tool Registry
↓
10 软件 Adapter 层 EICAD Adapter
↓
11 执行控制层 DryRun / 审批 / 执行 / 保存 / 回滚
↓
12 Trace / 评测 / 治理层
```

这 12 个模块不是彼此孤立的功能点，而是一条完整可控链路：

```text
前端提出任务；
配置中心决定可用 Agent、模型、工具和策略；
模型网关统一调用大模型；
Orchestrator 用状态机控制流程；
上下文管理层给模型和规则提供必要信息；
LLM 负责理解和提取；
Schema 负责结构约束；
规则引擎负责默认值、推导、校验、风险和审批判断；
Tool Registry 控制可调用工具；
Adapter 安全对接 EICAD / CAD；
执行控制层负责 DryRun、审批、执行和回滚；
Trace / 评测 / 治理层记录、复盘和持续迭代。
```

---

## 2. 12 个模块总表

| 序号 | 模块 | 主要解决的问题 | 上游输入 | 下游输出 | MVP 必做内容 | 与其他模块关系 |
|---|---|---|---|---|---|---|
| 01 | 前端交互层 / Agent Console | 用户如何输入任务、查看系统理解、确认 DryRun、选择模型/API、查看结果和 Trace | 用户输入、当前项目、当前对象、用户选择模型/API | 任务请求、审批动作、参数修改、Trace 查看请求 | 统一入口、总体 Agent 展示、本次调用业务 Agent 展示、模型/API 选择入口、参数面板、规则面板、DryRun 面板、审批按钮、执行结果、Trace 入口 | 面向用户；调用后端后台和 Orchestrator；不直接调用模型 API、不直接调用工具、不直接改工程数据 |
| 02 | Agent 配置中心 / 后端后台配置服务 | 系统有哪些 Agent、每个 Agent 能做什么、用什么模型、能调什么工具、什么动作要审批 | 前端任务、main_agent_id、项目、模块、用户模型/API选择 | Runtime Agent Config、Agent 路由配置、模型策略、工具范围、审批策略 | 总体 Agent 配置、业务 Agent 注册表、AgentRouter 配置、model_policy、tool_scope、approval_policy、risk_policy、版本记录、配置校验 | 给 Orchestrator、模型网关、Tool Registry、规则引擎提供配置；负责“选模型、配模型”，不负责真实调用模型 |
| 03 | 模型网关 / Model Gateway | 不同大模型和 API 如何统一调用、切换、重试、降级、统计 | Runtime Agent Config、Prompt、Context Package、目标 Schema、模型/API配置 | 统一模型返回、raw_output、parsed_output、token、耗时、错误 | 模型清单、API 通道清单、Credential 引用、统一请求、统一返回、结构化输出、超时、重试、降级、Trace | 配置中心决定用哪个模型；模型网关负责怎么调用；前端只选择和展示；不判断业务成功 |
| 04 | 工作流编排层 / Orchestrator | 整个 Agent 流程如何受控执行，不能变成模型自由跑 | 前端任务、Runtime Agent Config、Context Package、模型结果、规则结果、工具计划 | 工作流状态、下一节点、失败状态、审批等待、执行结果 | run_id、状态机、节点输入输出、异常分支、WaitingApproval、Failed、Cancelled、Completed、节点 Trace | 系统主控层；调用其他模块；任何工程动作不能绕过 Orchestrator |
| 05 | 上下文管理层 / Context Manager | 每次模型调用前应该给模型什么信息、不给什么信息 | 用户输入、项目、软件模块、选中对象、Agent配置、会话状态 | Context Package、上下文摘要、字段来源 | 用户输入、总体/业务 Agent、项目、模块、对象、草稿、可用工具、规则版本、对话摘要、权限上下文 | 给 LLM、规则、工具计划提供必要上下文；原则是给摘要不给全量；需要详细数据通过工具读取 |
| 06 | LLM 能力层 | 大模型到底负责什么，如何限制在理解层 | Context Package、Prompt、Skill、Schema、模型策略 | 意图、候选 Agent、结构化参数、缺失字段、追问、结果解释 | 意图识别、参数提取、缺失参数追问、结果解释；结构化 JSON 输出；置信度和来源标记 | 只负责理解和表达；不直接补默认值；不直接推导强规则；不直接调工具；不直接执行工程动作 |
| 07 | Schema 结构化控制层 | 模型输出、规则结果、工具输入、DryRun、审批、执行结果如何统一成可校验数据 | LLM 输出、前端输入、规则结果、工具结果 | 校验结果、标准化参数、Draft Object、ToolPlan、ExecutionSchema | InputSchema、IntentSchema、ParamSchema、DraftObjectSchema、RuleHitSchema、ToolPlanSchema、DryRunSchema、ExecutionSchema、TraceSchema | Schema 是数据契约；LLM 输出必须落 Schema；规则处理基于 Schema；工具入参来自 Schema；前端展示读取 Schema |
| 08 | 规则引擎层 / Rule Engine | 默认值、推导、业务校验、风险、审批依据不能靠模型猜 | 标准化参数、Draft Object、项目配置、客户配置、规则文件 | 补全参数、推导字段、RuleHitSchema、ValidationResult、RiskResult、ApprovalDecision | 默认值规则、推导规则、业务校验规则、自动修复规则、风险规则、审批规则、工具选择规则 | LLM 提取用户明确表达；规则引擎补默认值和推导；规则结果进入前端、DryRun、审批、Trace |
| 09 | 工具与原子函数层 / Tool Registry | Agent 如何安全调用软件能力，防止越权或误调用 | ToolPlan、Schema参数、Agent工具范围、用户权限、规则结果 | 已校验工具调用、工具错误、工具风险、工具版本 | 工具注册表、工具契约、input_schema、output_schema、风险等级、是否DryRun、是否审批、Tool Scope、工具六道门 | Tool Registry 控制工具是否能调；不执行软件动作；真正执行交给 Adapter；LLM 不能绕过它 |
| 10 | 软件 Adapter 层 / EICAD Adapter | 如何把受控工具调用安全转换为 EICAD / CAD 真实动作 | 已授权工具调用、标准入参、operation_mode、工程上下文 | Adapter 标准结果、错误码、affected_objects、rollback_token | adapter_base、EICAD Adapter、read/dry_run/execute模式、错误码转换、数据转换、基础快照、基础回滚能力 | 隔离 Agent 与具体软件；Tool Registry 管工具，Adapter 执行软件动作；不做业务规则、不做审批判断 |
| 11 | 执行控制层 / Execution Control | 正式改工程数据前如何预演、确认、执行、保存和回滚 | 工具计划、规则结果、风险等级、Adapter能力、用户审批 | DryRun结果、审批记录、执行结果、保存结果、回滚结果 | DryRun、Preview、Approval、Execution、SaveControl、Rollback、ResultValidation | 工程数据安全最后一道闸门；没有 DryRun 不执行；没有审批不写入；默认不自动保存工程文件 |
| 12 | Trace / 评测 / 治理层 | 运行过程如何追踪、问题如何复盘、失败如何沉淀、版本如何定位 | 全链路节点输入输出、模型结果、规则结果、工具结果、审批和执行记录 | run_id、Trace、失败样例、评测样例、版本记录、审计记录 | run_id、workflow_trace、llm_trace、rule_trace、tool_trace、dryrun_trace、approval_trace、execution_trace、失败样例、核心测试集、版本记录 | 横向贯穿所有模块；不参与业务执行，但保证可追踪、可回放、可测试、可治理 |

---

## 3. 模块之间的关键关系

### 3.1 前端、后端后台、模型网关的关系

```text
前端：让用户选择模型 / API，并展示当前使用情况；
后端后台 / 配置中心：管理可用模型清单、API 通道、权限和默认策略；
模型网关：真正调用模型 API。
```

关键原则：

```text
前端可以选择；
后端负责校验；
模型网关负责调用；
API Key 不暴露给前端。
```

---

### 3.2 总体 Agent 与业务 Agent 的关系

```text
总体 Agent：用户面对的统一入口，例如 EICAD 工程智能助手；
业务 Agent：系统自动路由后的具体能力，例如路基模板创建 Agent。
```

关键原则：

```text
用户不需要先选择具体 Agent；
系统根据意图、上下文、配置自动判断业务 Agent；
前端展示本次调用了哪个业务 Agent，以及为什么调用。
```

---

### 3.3 LLM、Schema、规则引擎的关系

```text
LLM：理解用户输入，提取用户明确表达的参数；
Schema：约束模型输出结构，保证字段、类型、单位、来源一致；
Rule Engine：补默认值、做推导、做校验、判断风险和审批。
```

关键原则：

```text
LLM 不直接补默认值；
LLM 不直接推导强业务规则；
默认值和推导由规则引擎执行；
规则执行必须基于 Schema。
```

---

### 3.4 Tool Registry 与 Adapter 的关系

```text
Tool Registry：判断工具是否存在、是否授权、入参是否合法、风险等级是什么；
Adapter：把已授权、已校验的工具调用转换为 EICAD / CAD 真实动作。
```

关键原则：

```text
Tool Registry 不直接操作软件；
Adapter 不决定业务规则；
LLM 和前端都不能直接调用 Adapter；
所有软件动作必须走 Tool Registry → Adapter。
```

---

### 3.5 Orchestrator 与其他模块的关系

```text
Orchestrator 是主控状态机；
其他模块都是被它按节点调用的能力模块。
```

关键原则：

```text
任何工程动作不能绕过 Orchestrator；
任何工具调用不能跳过 Schema、规则、Tool Registry、DryRun、审批；
任何失败都必须进入明确状态。
```

---

### 3.6 Execution Control 与工程数据安全的关系

```text
DryRun：正式执行前预演；
Approval：用户确认影响范围；
Execution：正式执行；
SaveControl：控制是否保存；
Rollback：失败或撤销时恢复。
```

关键原则：

```text
没有 DryRun，不执行；
没有审批，不写入；
默认不自动保存工程文件；
不可回滚动作必须提前提示；
执行结果必须结构化校验。
```

---

## 4. MVP 中每个模块最核心的交付物

| 模块 | 核心交付物 |
|---|---|
| 前端交互层 | Agent Console、模型/API选择入口、参数面板、DryRun面板、审批区、Trace入口 |
| Agent 配置中心 | main_agent_manifest、business_agent_manifest、agent_registry、router_config、model_policy、tool_scope、approval_policy |
| 模型网关 | model_registry、api_channel_config、credential_ref、统一请求/返回、结构化输出、重试/降级、模型 Trace |
| Orchestrator | 状态机、run_id、workflow_state、节点输入输出、异常分支、审批等待、失败处理 |
| Context Manager | Context Package、项目摘要、对象摘要、工具摘要、对话摘要、来源标记、脱敏裁剪 |
| LLM 能力层 | 意图识别 Prompt、参数提取 Prompt、追问 Prompt、结果解释 Prompt、结构化输出 |
| Schema 层 | InputSchema、IntentSchema、ParamSchema、DraftObjectSchema、RuleHitSchema、ToolPlanSchema、ExecutionSchema |
| 规则引擎 | 默认值规则、推导规则、校验规则、风险规则、审批规则、RuleHitSchema、规则测试样例 |
| Tool Registry | 工具契约、工具注册表、Tool Scope、工具风险等级、工具入参/出参 Schema、工具六道门 |
| EICAD Adapter | adapter_base、EICAD Adapter、read/dry_run/execute、错误码映射、rollback_token |
| Execution Control | DryRunExecutor、ApprovalGate、ExecutionEngine、SaveController、RollbackManager、ResultValidator |
| Trace / 评测 / 治理 | run_id、Trace日志、失败样例、核心测试集、版本记录、审计记录 |

---

## 5. MVP 最小闭环

MVP 最小闭环建议如下：

```text
1. 用户在前端输入任务；
2. 用户可选择模型 / API；
3. 后端校验模型 / API 是否可用；
4. 配置中心加载总体 Agent 和业务 Agent 配置；
5. Orchestrator 创建 run_id 和状态机实例；
6. Context Manager 构造 Context Package；
7. LLM 识别意图并提取参数；
8. Schema 校验结构；
9. Rule Engine 补默认值、推导、校验、判断风险；
10. Tool Registry 生成并校验工具计划；
11. Adapter 执行 DryRun；
12. 前端展示预览和影响范围；
13. 用户确认；
14. Adapter 正式执行；
15. Execution Control 校验结果并控制保存 / 回滚；
16. Trace 记录全过程；
17. 失败样例进入测试集。
```

这个闭环完成，才算真正具备“可控工程 Agent MVP”。

---

## 6. MVP 的关键边界

```text
LLM 不能直接执行工程动作；
LLM 不能直接补默认值；
LLM 不能直接推导强业务规则；
前端不能直接调用模型 API；
前端不能直接调用 Adapter；
工具不能绕过 Tool Registry；
执行不能绕过 DryRun；
写入不能绕过审批；
默认不自动保存工程文件；
所有关键节点必须进入 Trace。
```

---

## 7. 总结

这 12 个模块共同构成一个完整的 MVP 架构。

它不是“大平台”，但已经具备完整骨架：

```text
有前端入口；
有后端配置；
有模型网关；
有状态机；
有上下文；
有 LLM 理解；
有 Schema 约束；
有规则判断；
有工具注册；
有 Adapter 执行；
有 DryRun 和审批；
有 Trace 和评测。
```

因此，MVP 的核心不是功能多，而是链路完整、边界清楚、后续可扩展。

一句话：

```text
MVP 要做的是“可控工程 Agent 的最小完整底座”，不是一个临时 Prompt Demo。
```
