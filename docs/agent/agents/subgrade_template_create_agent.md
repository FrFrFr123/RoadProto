# 路基模板创建意图 MVP 验证说明

## 定位

本文档是早期“路基模板创建 Agent”验证说明。按当前全局规范，该能力已收敛为：

```text
Agent: roadproto_engineering_agent
Skill: subgrade_template
Intent: subgrade_template.create
```

因此，“创建”不再作为独立业务 Agent 边界，而是 `subgrade_template` Skill 下的一个 Intent。本文件保留为 MVP 创建链路的历史验证说明；新增规则和后续实现应优先参考 `docs/agent/skill_system.md`、`docs/agent/skills/subgrade_template_skill.md` 和 `docs/agent/intents/subgrade_template_create.md`。

路基模板创建意图是可控工程 Agent MVP 的首个验证动作。它的目标不是把横断面模块智能化做完，而是验证完整 Agent 底座能否安全调用一个现有 RoadProto 业务能力。

该意图只验证“自然语言创建独立路基模板”的最小闭环。所有写入动作必须先生成结构化执行计划，由 WPF 可停靠面板展示给用户确认，再通过 RoadProto 本地 Adapter 调用现有路基模板能力执行。

## Agent 标识

```text
agent_id: roadproto_engineering_agent
skill_id: subgrade_template
intent_id: subgrade_template.create
module: AGENT
verified_business_module: CROSS_SECTION
entry_command: RD_AGENT_CONSOLE
```

## 支持意图

本验证文档只覆盖：

```text
创建独立路基模板
```

详细 Skill 和意图规则见：

```text
docs/agent/skills/subgrade_template_skill.md
docs/agent/intents/subgrade_template_create.md
```

示例输入：

```text
创建一个高速公路路基模板，名称叫主线路基模板，显示比例 1:10。
```

MVP 不支持：

- 直接生成横断面戴帽道路模型。
- 自动绑定道路中线。
- 自动绑定路面结构层模板。
- 根据总宽自由拆分每个路基部件宽度。
- 批量修改既有路基模板。
- 覆盖保存 DWG 文件。
- 无用户确认直接创建 CAD 对象。

## ParamSchema

MVP 参数字段：

| 字段 | 类型 | 单位 | 必填 | 来源 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `template_name` | string | 无 | 否 | 用户输入或默认值 | 路基模板名称 |
| `road_grade` | enum | 无 | 否 | 用户输入或默认值 | 道路等级 |
| `display_scale` | number | 比例 | 否 | 用户输入或默认值 | 支持 1、10、20、50、100 |
| `design_speed` | number | km/h | 否 | 用户输入 | MVP 记录和展示，不直接推导模板 |
| `subgrade_width` | number | m | 否 | 用户输入 | MVP 记录和提示，不自动拆分部件 |
| `insertion_point_required` | bool | 无 | 是 | 规则 | 写入 DWG 前必须由本地 Adapter 点取 |

`road_grade` 枚举沿用 RoadProto 领域模型：

```text
Expressway
FirstClass
SecondClass
ThirdClass
FourthClass
UrbanExpressway
UrbanArterial
UrbanSubArterial
UrbanBranch
```

## 规则

### 默认值规则

- `template_name` 缺失时使用 `默认路基模板`。
- `road_grade` 缺失时使用 `Expressway`。
- `display_scale` 缺失时使用 `10`。
- `insertion_point_required` 固定为 `true`。

### 标准化规则

- “高速”“高速公路”标准化为 `Expressway`。
- “一级公路”标准化为 `FirstClass`。
- “二级公路”标准化为 `SecondClass`。
- “城市快速路”标准化为 `UrbanExpressway`。
- `1:10`、`10`、`比例10` 标准化为数值 `10`。

### 业务校验规则

- `display_scale` 必须是 1、10、20、50、100。
- `road_grade` 必须在 RoadProto 支持枚举中。
- `template_name` 为空时可由规则补默认值。
- `subgrade_width` 小于等于 0 时阻断。
- `subgrade_width` 有值但无法安全映射到部件宽度时，只展示为用户输入，不自动修改 `SubgradeTemplateDefaults` 的部件组成。

### 风险和审批规则

- `create_subgrade_template_draft`：低风险，不需要审批。
- `preview_subgrade_template`：低风险，不需要审批。
- `apply_subgrade_template`：中风险，需要审批。
- 覆盖保存 DWG：MVP 不支持。

## 工具计划

MVP 工具顺序：

```text
get_current_project_info
get_project_default_params
create_subgrade_template_draft
preview_subgrade_template
apply_subgrade_template
```

`apply_subgrade_template` 执行前必须满足：

- Schema 校验通过。
- 规则校验通过。
- DryRun 成功。
- WPF 展示结构化执行计划。
- 用户在 WPF 中确认。
- RoadProto 本地 Adapter 点取插入点成功。

## 结构化执行计划

WPF 审批前必须展示：

- 将执行的 Agent：`roadproto_engineering_agent`。
- 将执行的 Skill：`subgrade_template`。
- 将执行的 Intent：`subgrade_template.create`。
- 将调用的本地工具：`apply_subgrade_template`。
- 将创建的 CAD 对象：`DnSubgradeTemplateEntity`。
- 模板参数摘要。
- 默认值来源。
- 规则命中结果。
- 风险等级。
- 是否需要点取插入点。
- 是否支持回滚。
- 本次任务的 `TraceId`。

用户确认后才能进入正式执行。

## DryRun 输出

DryRun 展示：

- 将创建的对象类型：`DnSubgradeTemplateEntity`。
- 模板名称。
- 道路等级。
- 显示比例。
- 默认部件数量。
- 左右侧部件摘要。
- 规则命中结果。
- 需要点取插入点。
- 写入前需要用户确认。

DryRun 不写入 DWG，不创建临时实体，不触发保存。

## 执行结果

执行成功后返回：

- 新实体 handle。
- 创建对象类型。
- 插入点。
- 模板名称。
- 部件数量。
- 是否支持回滚。
- `TraceId` / `TaskId`。

执行失败时返回：

- 错误码。
- 用户可读错误。
- 是否写入部分对象。
- 是否已回滚。
- 人工处理建议。

## 流转日志

该创建意图必须记录以下步骤：

- 用户输入。
- Agent 路由。
- Skill 识别。
- Intent 识别。
- 模型 Provider 和模型名。
- 参数提取结果。
- Schema 校验结果。
- 规则命中结果。
- 工具计划。
- DryRun 请求和结果。
- WPF 展示执行计划。
- 用户确认或取消。
- RoadProto 本地 Adapter 点取插入点。
- Bridge 调用现有路基模板能力。
- `DnSubgradeTemplateEntity` 创建结果。
- 错误、重试、取消。

日志不得记录明文 API Key。

## 验收标准

- 自然语言能识别为路基模板创建意图。
- 低置信度不会自动执行。
- 参数能带来源和置信度展示。
- 默认值由规则生成，不由 LLM 直接补。
- `display_scale` 非法时阻断。
- `subgrade_width` 不被模型自由拆分为部件宽度。
- DryRun 展示将创建的模板摘要。
- WPF 展示结构化执行计划。
- 未审批不能写入。
- 审批后调用现有 RoadProto 能力创建 `DnSubgradeTemplateEntity`。
- Trace 能看到意图、参数、规则、工具、DryRun、审批和执行结果。
- 流转日志能定位卡在哪一段。
