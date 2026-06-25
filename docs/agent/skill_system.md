# Agent Skill 体系与十二层受控流程

本文档定义 `Agent / Skill / Intent` 在 RoadProto 可控工程 Agent 十二层架构中的位置。后续新增任意业务能力时，必须先明确它属于哪个 Agent、哪个 Skill、哪些 Intent，并把 Schema、规则、工具、执行控制和 Trace 都挂到这条十二层链路上。

## 1. 核心目标

Agent 体系的目标不是让大模型直接执行命令，而是让自然语言进入一条可解释、可校验、可审批、可追踪的工程流程。

必须同时满足：

- 意图可控：用户表达必须先进入明确的 Agent 和 Skill 边界，再匹配 Intent。
- 参数可控：模型只能抽取用户表达和解释歧义，不能自行补默认值或臆造 CAD 对象。
- 执行可控：任何 Tool 调用必须来自已注册 Skill / Intent 的白名单绑定。
- 风险可控：写入、修改、删除类动作必须经过 DryRun、确认和 Trace。
- 扩展可控：新增业务能力时，先新增 Skill，再在 Skill 下拆分 Intent。

## 2. 十二层总流程

RoadProto Agent 的总关系固定为：

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

`Agent / Skill / Intent` 不替代这十二层，而是嵌入其中：

- `Agent` 在第 02、04、06、07、08、12 层均有体现。
- `Skill` 在第 02、04、05、06、07、08、09、11、12 层均有体现。
- `Intent` 在第 06、07、08、09、11、12 层均有体现。
- `Tool` 只能从第 09 层进入，不允许由第 06 层模型直接调用。

## 3. Agent / Skill / Intent 定义

### Agent

`Agent` 是一次用户请求的编排入口，负责识别当前请求应由哪个业务能力体系承接。

MVP 默认主控 Agent：

```text
roadproto_engineering_agent
```

主控 Agent 负责：

- 接收 WPF 输入。
- 组织上下文包。
- 选择候选 Skill。
- 调用模型做受控识别和结构化抽取。
- 推进状态机。
- 记录 Trace。

后续可以增加更细的业务 Agent，例如 `cross_section_agent`、`alignment_agent`、`profile_agent`。但业务 Agent 仍然只能管理 Skill，不能直接绕过 Skill 调 Tool。

### Skill

`Skill` 是 Agent 下的业务能力包，是“能做什么”的边界。

一个 Skill 必须声明：

- Skill ID。
- 所属 Agent。
- 所属 RoadProto 业务模块。
- 支持的 Intent 清单。
- 共享上下文需求。
- 共享参数和默认值来源。
- 共享校验规则。
- 追问策略。
- Tool 白名单。
- 审批和风险策略。
- Trace 阶段。
- 关联业务文档和复用文档。

Skill 不是 Prompt，也不是单个工具。Skill 是一组可控意图、规则、Schema 和工具绑定的集合。

### Intent

`Intent` 是 Skill 下的具体用户动作，例如创建、修改、删除、查询。

一个 Intent 只能归属于一个明确 Skill。不得把同一个 Intent 同时挂到多个 Skill 下，也不得让 Intent 脱离 Skill 独立存在。

Intent 文档负责说明：

- 用户可能如何表达。
- 不应匹配的表达。
- 必要参数和可选参数。
- 默认值、校验、追问和确认展示。
- Tool 绑定和结果结构。
- Intent 级 Trace 阶段。

### Schema

`Schema` 是模型输出、规则输入、Tool 参数和结果回传的结构化约束。

Schema 控制层必须覆盖：

- Agent 路由输出。
- Skill 识别结果。
- Intent 识别结果。
- 目标对象定位。
- 参数抽取结果。
- 追问请求。
- Tool 调用计划。
- Tool 执行结果。

模型输出必须先通过 Schema 校验，再进入规则层。Schema 不通过时，不能调用 Tool。

### Rule

`Rule` 是默认值补全、领域校验、风险阻断和追问判断的执行层。

规则层负责：

- 根据 Skill 和 Intent 读取可用参数。
- 补默认值。
- 校验枚举、数值范围和目标对象。
- 判断是否需要追问。
- 判断是否需要用户确认。
- 生成确认前展示项。

LLM 不能替代 Rule。

### Tool

`Tool` 是受控执行能力。Tool 只能通过 Skill / Intent 的工具绑定产生。

RoadProto 规则：

- 后端不直接写 DWG。
- WPF 不直接操作 ObjectARX 类型。
- 写入类 Tool 必须由 RoadProto 本地 `AGENT` 模块转发到 ObjectARX Adapter。
- 未出现在 Skill 白名单中的 Tool 一律拒绝执行。

### Trace

`Trace` 是每次请求的可回放执行记录。所有阶段都必须带 `TraceId` / `SessionId` / `TaskId`。

Trace 不只是日志，它也是后续评测、调试和规则优化的依据。

## 4. Agent / Skill / Intent 在十二层中的作用

| 层级 | 层名 | Agent / Skill / Intent 的作用 |
| --- | --- | --- |
| 01 | 前端交互层 Agent Console | 展示当前 Agent、候选 Skill、确认页、追问、Trace；用户不直接选择 Tool。 |
| 02 | Agent 配置中心 / 后端后台配置服务 | 配置启用的 Agent、Skill、模型 Provider、Tool 权限和风险策略。 |
| 03 | 模型网关 Model Gateway | 按 Agent 配置选择模型通道，只负责受控调用，不判断业务。 |
| 04 | 工作流编排层 Orchestrator | 创建 Run，启动 Agent 路由，选择候选 Skill，推进 Intent 状态机。 |
| 05 | 上下文管理层 Context Manager | 按候选 Skill 裁剪上下文，例如当前选择集、DWG 摘要、可用模板候选。 |
| 06 | LLM 能力层 | 在候选 Agent / Skill 范围内做 Intent 识别、参数提取、追问建议和解释，不生成最终执行决定。 |
| 07 | Schema 结构化控制层 | 固化 Agent 路由、Skill 识别、Intent 抽取、目标对象、参数和追问输出结构。 |
| 08 | 规则引擎层 Rule Engine | 对 Agent、Skill、Intent、参数、目标和风险做最终裁决；补默认值、校验、追问或阻断。 |
| 09 | 工具与原子函数层 Tool Registry | 校验 Intent 绑定的 Tool 是否在 Skill 白名单中，并生成受控 Tool 调用计划。 |
| 10 | 软件 Adapter 层 EICAD Adapter | 把 Tool 调用映射到 RoadProto / EICAD 本地 Adapter，不暴露 CAD 内部对象给模型。 |
| 11 | 执行控制层 | 按 Skill / Intent 风险策略执行 DryRun、审批、执行、保存和回滚。 |
| 12 | Trace / 评测 / 治理层 | 记录 AgentId、SkillId、IntentId、Schema 版本、规则版本、Tool、执行结果和失败样例。 |

## 5. 标准流程

每次用户输入必须按以下顺序流转：

```text
1. 用户表达
2. 01 Agent Console 接收输入并携带当前 Agent 配置
3. 02 配置中心加载可用 Agent / Skill / Tool 权限
4. 03 Model Gateway 准备当前模型通道
5. 04 Orchestrator 创建 Run，确定候选 Agent 和候选 Skill
6. 05 Context Manager 为候选 Skill 裁剪上下文包
7. 06 LLM 能力层在候选范围内输出 Agent / Skill / Intent / 参数 / 追问建议
8. 07 Schema 层校验结构化输出
9. 08 Rule Engine 对 Agent、Skill、Intent、参数和目标对象做最终裁决
10. 08 Rule Engine 补默认值、校验、追问或阻断
11. 09 Tool Registry 校验 Tool 是否在 Skill 白名单中
12. 10 EICAD Adapter 映射为 RoadProto 本地受控 Adapter 请求
13. 11 执行控制层执行 DryRun
14. 11 WPF 展示确认页并等待用户审批
15. 11 审批通过后执行、保存或回滚
16. 12 Trace / 日志 / 评测样例沉淀
```

查询类只读请求可以跳过 DryRun 和审批，但不能跳过 Agent、Skill、Intent、Schema、Rule 和 Trace。

关键点：

- Agent 识别不是模型自由发挥，而是在第 04 层启动、第 06 层输出、第 07 层结构化、第 08 层裁决。
- Skill 识别同理，必须先有第 02 层注册和第 04 层候选范围，模型不能凭空创造 Skill。
- Intent 识别必须发生在已确认的 Skill 内部。
- Tool 调用必须经过第 09 层 Tool Registry，不允许由模型直接触发。

## 6. 机器规则建议结构

后端机器可读规则推荐分为 Skill 和 Intent 两层：

```text
rules/
  skills/
    subgrade_template.skill.yaml
  intents/
    subgrade_template.create.yaml
    subgrade_template.modify.yaml
    subgrade_template.delete.yaml
    subgrade_template.query.yaml
```

Skill 规则负责共享边界：

```yaml
id: subgrade_template
agentId: roadproto_engineering_agent
businessModule: CROSS_SECTION
displayName: 路基模板
intents:
  - subgrade_template.create
  - subgrade_template.modify
  - subgrade_template.delete
  - subgrade_template.query
toolWhitelist:
  - SubgradeTemplate.Create
  - SubgradeTemplate.Modify
  - SubgradeTemplate.Delete
  - SubgradeTemplate.Query
```

Intent 规则负责具体动作：

```yaml
skillId: subgrade_template
id: subgrade_template.create
toolBinding:
  toolName: SubgradeTemplate.Create
  requiresDryRun: true
  requiresApproval: true
```

后端加载规则时必须校验：

- Skill 的 `agentId` 必须存在。
- Intent 的 `skillId` 必须存在。
- Intent 必须出现在 Skill 的 `intents` 清单中。
- Intent 绑定的 Tool 必须出现在 Skill 的 `toolWhitelist` 中。
- 写入类 Intent 必须声明审批策略。
- 删除类 Intent 必须声明高风险确认策略。
- Trace 必须记录 `AgentId`、`SkillId`、`IntentId`、`SchemaVersion`、`RuleVersion` 和 `ToolName`。

## 7. 文档规范

人读文档采用同样两层：

```text
docs/agent/skills/
  subgrade_template_skill.md

docs/agent/intents/
  subgrade_template_create.md
  subgrade_template_modify.md
  subgrade_template_delete.md
  subgrade_template_query.md
```

Skill 文档用于说明业务能力包。

Intent 文档用于说明单个动作。

新增业务时，不允许只写 Intent 文档。必须先写 Skill 文档，再写 Intent 文档。

## 8. 控制原则

- Agent 负责路由，不负责业务默认值。
- Skill 负责能力边界，不负责具体 CAD 写入。
- Intent 负责动作边界，不负责绕过审批。
- Schema 负责结构化，不负责业务判断。
- Rule 负责默认值、校验、追问和风险判断。
- Tool 负责执行接口，不负责解释用户自然语言。
- Adapter 负责 CAD 读写，不负责模型判断。
- Trace 负责全链路记录，不保存明文密钥。

任何实现如果试图让模型直接输出 AutoCAD 命令、直接调用 Tool、直接写 DWG、直接删除对象，均违反本规范。

## 9. 路基模板 MVP 落点

本轮先实现路基模板 Skill 文档和四个 Intent 文档：

```text
Agent: roadproto_engineering_agent
Skill: subgrade_template
Intent:
  subgrade_template.create
  subgrade_template.modify
  subgrade_template.delete
  subgrade_template.query
Tool:
  SubgradeTemplate.Create
  SubgradeTemplate.Modify
  SubgradeTemplate.Delete
  SubgradeTemplate.Query
```

旧文档中出现的 `subgrade_template_create_agent` 是早期动作级命名。后续应收敛为：

```text
Agent: roadproto_engineering_agent
Skill: subgrade_template
Intent: subgrade_template.create
```

创建只是路基模板 Skill 的一个 Intent，不应继续作为独立业务 Agent 边界。
