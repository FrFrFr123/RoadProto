# Skill：路基模板

## 1. Skill 基本信息

| 项 | 内容 |
| --- | --- |
| Skill ID | `subgrade_template` |
| 显示名称 | 路基模板 |
| 所属主控 Agent | `roadproto_engineering_agent` |
| 建议业务 Agent | `cross_section_agent` 或 `subgrade_template_agent` |
| 所属 RoadProto 模块 | `CROSS_SECTION` |
| RoadProto 本地承接模块 | `AGENT` |
| 业务对象 | `DnSubgradeTemplateEntity` |
| 领域模型 | `SubgradeTemplateModel` / `SubgradeTemplateDefaults` |
| Skill 文档状态 | MVP 草案 |

本 Skill 负责路基模板对象的自然语言创建、修改、删除和查询。它是 Agent 体系中的业务能力边界，不等同于单个创建意图，也不等同于某个 Tool。

## 2. 支持的 Intent

| Intent ID | 动作 | 风险等级 | 是否写 CAD | 是否需要确认 | 文档 |
| --- | --- | --- | --- | --- | --- |
| `subgrade_template.create` | 创建路基模板 | 中 | 是 | 是 | `docs/agent/intents/subgrade_template_create.md` |
| `subgrade_template.modify` | 修改路基模板 | 中 | 是 | 是 | `docs/agent/intents/subgrade_template_modify.md` |
| `subgrade_template.delete` | 删除路基模板 | 高 | 是 | 是 | `docs/agent/intents/subgrade_template_delete.md` |
| `subgrade_template.query` | 查询路基模板 | 低 | 否 | 否 | `docs/agent/intents/subgrade_template_query.md` |

本 Skill 不负责：

- 路面结构层模板创建、修改、删除、查询。
- 边坡模板创建、修改、删除、查询。
- 横断面戴帽。
- 土石方计算。
- 横断面出图。
- 自动生成道路模型。
- 创建、修改、删除、查询或解释道路模型工作流。道路模型是独立工程对象，不应被路由到 `subgrade_template` Skill。

## 3. 标准处理流程

路基模板 Skill 必须嵌入全局十二层流程：

```text
用户 / 前端
-> 01 Agent Console 接收输入
-> 02 配置中心确认 roadproto_engineering_agent 启用 subgrade_template Skill
-> 03 Model Gateway 准备模型通道
-> 04 Orchestrator 将候选 Skill 限定为 subgrade_template 或同类候选
-> 05 Context Manager 裁剪路基模板相关上下文
-> 06 LLM 在候选 Skill 内识别 create / modify / delete / query Intent
-> 07 Schema 校验 Agent / Skill / Intent / 参数 / 目标对象
-> 08 Rule Engine 执行默认值、校验、追问和风险判断
-> 09 Tool Registry 校验 Tool 白名单
-> 10 EICAD Adapter 映射到 RoadProto 本地 Adapter
-> 11 DryRun / 审批 / 执行 / 保存 / 回滚
-> 12 Trace / 评测 / 治理
```

模型只能抽取和解释用户表达。道路等级默认模板、部件默认值、坡度、颜色和 CAD 对象合法性必须由规则层或 RoadProto 领域能力处理。

## 4. 共享上下文

本 Skill 需要的上下文包括：

- 当前 DWG 名称和保存状态。
- RoadProto 版本。
- 当前选择集摘要。
- 当前图中可见或可查询的 `DnSubgradeTemplateEntity` 摘要。
- 当前可用本地 Tool 清单。
- 当前启用模型 Provider 和模型名。

选择集摘要只允许包含脱敏结构信息，例如：

```text
对象类型、handle、名称、是否唯一、是否可写、是否存在引用
```

不得把完整 DWG 数据、大段几何点列或明文敏感路径直接传给模型。

## 5. 共享参数

| 参数 | 类型 | 用途 | 默认值来源 |
| --- | --- | --- | --- |
| `targetHandle` | string | 修改、删除、查询指定模板 | 当前选择集、用户输入、候选列表 |
| `targetName` | string | 按名称定位模板 | 用户输入、实体摘要 |
| `roadGrade` | enum | 创建或修改道路等级 | 用户输入，缺失时按 Intent 规则追问 |
| `templateName` | string | 模板名称 | Agent 规则文件或用户输入 |
| `displayScale` | number | 模板显示比例 | Agent 规则文件或用户输入 |
| `components` | array | 创建时完整部件列表 | `defaultComponentsByRoadGrade` + 用户覆盖 |
| `laneWidth` | number | 行车道宽度覆盖项 | 用户明确输入 |
| `laneWidthDelta` | number | 行车道宽度增量 | 用户明确输入 |
| `hardShoulderWidth` | number | 硬路肩宽度覆盖项 | 用户明确输入 |
| `earthShoulderWidth` | number | 土路肩宽度覆盖项 | 用户明确输入 |
| `medianWidth` | number | 中央分隔带总宽覆盖项 | 用户明确输入 |
| `unit` | string | 参数单位 | Agent 规则文件，当前本地工具只接受 `m` |
| `sideScope` | enum | 左侧、右侧或双侧 | 用户明确输入，歧义时追问 |

创建类 Tool 必须接收完整路基模板部件、路缘石、变宽表、坡度变化表和路面结构层绑定字段。RoadProto 本地 Adapter 只映射和校验 `components`，不得用标量字段重新推导默认模板。

## 6. 共享默认值规则

- 默认值来源必须是后端机器规则文件或明确上下文，不是 LLM。
- 当前 MVP 创建默认值写在 `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml`。
- 创建时由规则引擎读取 `defaultSource: agent-rule` / `defaultValue` 补全模板名称、显示比例和单位。
- 创建时由规则引擎读取 `defaultComponentsByRoadGrade` 补全道路等级对应的完整组件列表。
- RoadProto 本地 Tool Adapter 只负责执行和校验，不补行车道、硬路肩、土路肩、中央分隔带、坡度、颜色、路缘石或单位默认值。
- 修改时以实体原值为基础，只覆盖用户明确指定的字段。
- 删除时不补参数，只定位目标对象并做风险校验。
- 查询时允许目标为空，表示查询当前图中所有路基模板摘要。
- 用户只说总宽时，不自动拆分各部件，必须追问或进入专门总宽分配规则。

## 7. 共享校验规则

- `roadGrade` 必须属于 RoadProto 支持枚举。
- 宽度类参数必须大于 `0`。
- `displayScale` 必须属于 RoadProto 支持值。
- 修改、删除指定当前模板时，当前选择集必须存在唯一 `DnSubgradeTemplateEntity`。
- 按名称定位时，如果匹配多个模板，必须返回候选列表，不能直接执行。
- 删除前必须检查引用风险；MVP 阶段发现引用时默认阻断。
- 写 CAD 前必须完成确认。
- 后端不得直接写 DWG。

## 8. 追问策略

本 Skill 统一追问原则：

- 创建缺道路等级时追问。
- 修改或删除缺目标对象时追问或要求 CAD 点选。
- 目标对象不唯一时展示候选并追问。
- 用户表达跨 Skill 时追问，例如“路基模板戴帽”应澄清是查询模板还是执行道路模型。
- 用户表达为“道路模型”或“创建道路模型”时不得进入本 Skill，应路由为独立 `road_model` 候选；当前 MVP 尚未接入该 Skill 时应明确说明不调用路基模板工具。
- 默认值能安全补全时不追问。
- 默认值会改变工程含义或写入风险时必须追问。

## 9. Tool 白名单

本 Skill 只允许调用以下 Tool：

| Tool | 说明 | 是否写 CAD |
| --- | --- | --- |
| `SubgradeTemplate.Create` | 创建新路基模板 | 是 |
| `SubgradeTemplate.Modify` | 修改既有路基模板 | 是 |
| `SubgradeTemplate.Delete` | 删除既有路基模板 | 是 |
| `SubgradeTemplate.Query` | 查询模板摘要或详情 | 否 |

任何其他 Tool，即使模型输出了，也必须被规则层拒绝。

## 10. WPF 展示要求

WPF Agent Console 至少展示：

- 当前 Agent。
- 当前 Skill。
- 当前 Intent。
- 目标模板。
- 参数摘要。
- 默认值来源。
- 用户覆盖项。
- 风险等级。
- Tool 名称。
- 是否需要 DryRun。
- 是否需要审批。
- TraceId / TaskId。

查询类结果展示模板列表或模板详情。写入类结果在用户确认前不得执行。

## 11. Trace 阶段

本 Skill 的 Trace 至少记录：

- 用户输入。
- Agent 路由。
- Skill 识别。
- Intent 识别。
- 模型调用。
- Schema 校验。
- 目标对象解析。
- 参数抽取。
- 默认值补全。
- 参数校验。
- 追问或确认。
- DryRun。
- 用户审批。
- Tool 调用。
- RoadProto 本地 Adapter 执行。
- ObjectARX 执行结果。
- Tool Result 回传。
- 错误、取消和重试。

Trace 中必须包含 `AgentId`、`SkillId`、`IntentId` 和 `ToolName`。不得记录明文 API Key。

## 12. 机器规则映射

后端机器规则建议对应：

```text
rules/skills/subgrade_template.skill.yaml
rules/intents/subgrade_template.create.yaml
rules/intents/subgrade_template.modify.yaml
rules/intents/subgrade_template.delete.yaml
rules/intents/subgrade_template.query.yaml
```

四个 Intent 必须声明：

```yaml
skillId: subgrade_template
agentId: roadproto_engineering_agent
```

后端加载时必须校验 Skill 和 Intent 双向一致。
