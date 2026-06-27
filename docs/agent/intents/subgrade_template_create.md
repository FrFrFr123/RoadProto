# 意图：创建路基模板

## 1. 意图基本信息

| 项 | 内容 |
| --- | --- |
| 意图 ID | `subgrade_template.create` |
| 所属 Agent | `roadproto_engineering_agent` |
| 所属 Skill | `subgrade_template` |
| 所属模块 | `AGENT` |
| 验证业务模块 | `CROSS_SECTION` |
| Skill 文档 | `docs/agent/skills/subgrade_template_skill.md` |
| 业务文档 | `docs/business/agent/路基模板创建Agent_MVP验证.md` |
| 关联 RoadProto 功能文档 | `docs/business/cross_section/路基模板_创建.md` |
| 通用模板 | `docs/agent/intent_rule_template.md` |
| 当前状态 | 草案 |

本意图用于把用户的自然语言创建请求转成受控的路基模板创建计划。它属于 `subgrade_template` Skill，只负责创建新的独立路基模板实体，不负责修改已有模板、删除模板或执行道路模型戴帽。

创建意图在十二层流程中的位置：

```text
04 Orchestrator 选择 roadproto_engineering_agent / subgrade_template
06 LLM 识别 subgrade_template.create 并抽取参数
07 Schema 校验结构化输出
08 Rule Engine 补默认值、校验和追问
09 Tool Registry 校验 SubgradeTemplate.Create 在 Skill 白名单中
11 执行控制层 DryRun、审批和执行
12 Trace 记录 AgentId / SkillId / IntentId / ToolName
```

## 2. 用户表达

用户可能这样说：

- 帮我创建高速公路路基模板。
- 帮我创建路基模板，行车道比默认参数增加 1 米。其他参数按默认配置。
- 新建一个城市快速路路基模板，硬路肩宽度按 3 米。
- 做一个二级公路路基模板，左右土路肩都用 0.75 米。
- 创建路基模板，名称叫主线路基模板，显示比例 1:10。
- 创建路基模板，基于原本的路基模板，最右侧增加一个 10 米的行车道。

不应匹配的表达：

- 修改当前模板的行车道宽度。
- 删除路基模板。
- 路基模板怎么设置？
- 用当前模板进行戴帽。
- 创建路面结构模板。
- 创建道路模型。
- 道路模型。
- 给这个道路模型重新分配路基模板。

## 3. 意图边界

本意图负责：

- 创建新的路基模板。
- 根据 Agent 规则文件和道路等级语义加载默认参数。
- 根据用户输入覆盖部分参数。
- 处理“基于原本 / 默认模板再局部调整”的派生创建，局部“增加 / 修改 / 删除”只作为新模板参数补丁，不修改蓝本模板。
- 生成模板预览数据。
- 生成写入前确认信息。
- 在用户确认后调用 RoadProto 本地工具创建 `DnSubgradeTemplateEntity`。

本意图不负责：

- 修改已有模板。除非用户明确说“修改原模板 / 直接改这个模板”，否则“基于原本模板创建并增加某部件”仍属于创建意图。
- 删除模板。
- 执行戴帽设计。
- 土石方计算。
- 横断面出图。
- 创建、修改、删除或查询道路模型。
- 自动绑定道路中线。
- 自动创建或复制路面结构层模板。
- 根据路基总宽自由反推所有部件宽度。

## 4. 必要参数

| 参数 | 类型 | 是否必填 | 来源 | 缺失时处理 |
| --- | --- | --- | --- | --- |
| `roadGrade` / 道路等级 | enum | 是 | 用户输入 | 追问 |
| `insertionPoint` / 插入点 | CAD 点 | 执行时必填 | CAD 交互 / 当前工具默认策略 | 确认执行前提示用户点选，或使用系统配置的默认插入点策略 |
| `templateData` / 模板数据 | object | 是 | 默认规则 + 用户覆盖参数 | 先按道路等级生成默认模板，再应用用户明确指定的覆盖项 |
| `components` / 部件列表 | array | 是 | 默认规则 + 用户覆盖参数 | 不允许为空；默认规则无法生成时拒绝创建 |
| `confirmation` / 用户确认 | bool | 写 CAD 前必填 | WPF 确认面板 | 未确认不得写入 CAD |

### roadGrade 可选值

| 值 | 中文含义 |
| --- | --- |
| `Expressway` | 高速公路 |
| `FirstClass` | 一级公路 |
| `SecondClass` | 二级公路 |
| `ThirdClass` | 三级公路 |
| `FourthClass` | 四级公路 |
| `UrbanExpressway` | 城市快速路 |
| `UrbanArterial` | 城市主干路 / 城市主干道 |
| `UrbanSubArterial` | 城市次干路 / 城市次干道 |
| `UrbanBranch` | 城市支路 |

### components 最小结构

每个部件至少包含：

| 参数 | 类型 | 是否必填 | 说明 |
| --- | --- | --- | --- |
| `side` | `Left` / `Right` | 是 | 左侧或右侧 |
| `type` | enum | 是 | 中分带、行车道、硬路肩、土路肩等 |
| `width` | number | 是 | 部件宽度，单位 m |
| `slopeMode` | `Fixed` / `VariableByStation` | 是 | 固定坡度或按桩号变化 |
| `fixedSlope` | number | 固定坡度时必填 | 坡度值，例如 `0.02` |
| `color` | RGB | 是 | 默认颜色可由规则补全 |

`type` 使用 RoadProto 领域模型 `SubgradeComponentType`，当前支持：

| 值 | 中文含义 |
| --- | --- |
| `Median` | 中分带 |
| `TravelLane` | 行车道 |
| `HardShoulder` | 硬路肩 |
| `EarthShoulder` | 土路肩 |
| `SideMedian` | 侧分带 |
| `Sidewalk` | 人行道 |
| `BikeLane` | 慢车道 |
| `CurbStrip` | 路缘带 |

## 5. 可选参数

| 参数 | 类型 | 默认值来源 | 说明 |
| --- | --- | --- | --- |
| `templateName` / 模板名称 | string | Agent 规则文件 | 用户未指定时由规则文件给出默认名称 |
| `displayScale` / 显示比例 | number | Agent 规则文件或 RoadProto 校验允许值 | 对应 WPF / CAD 显示比例，支持 `1`、`10`、`20`、`50`、`100` |
| `roadCenterlineHandle` / 道路中线 handle | string | 空 | 当前创建模板不绑定道路中线，后续扩展时使用 |
| `totalWidth` / 路基总宽度 | number | 从部件宽度求和派生 | 不建议作为直接写入字段，除非用户明确要求按总宽反推部件 |
| `laneWidth` / 行车道宽度 | number | 用户明确覆盖 | 用户可说“行车道加宽 1 米”或“行车道宽 4 米”；未指定时使用组件级默认值 |
| `laneWidthDelta` / 行车道宽度增量 | number | 用户明确覆盖 | 用户说“行车道比默认增加 1 米”时作用到左右行车道组件 |
| `laneCount` / 车道数 | number | Agent 规则文件 / 后续等级配置 | 当前 MVP 不在 RoadProto 本地补车道数 |
| `hardShoulderWidth` / 硬路肩宽度 | number | 用户明确覆盖 | 可按左右侧分别覆盖；未指定时使用组件级默认值 |
| `earthShoulderWidth` / 土路肩宽度 | number | 用户明确覆盖 | 可按左右侧分别覆盖；未指定时使用组件级默认值 |
| `medianWidth` / 中央分隔带宽度 | number | 用户明确覆盖 | 当前按总宽拆成左右半幅组件写入；未指定时使用组件级默认值 |
| `sideMedianWidth` / 侧分带宽度 | number | 道路等级默认值 | 城市道路可用 |
| `sidewalkWidth` / 人行道宽度 | number | 道路等级默认值 | 城市道路可用 |
| `bikeLaneWidth` / 慢车道宽度 | number | 道路等级默认值 | 城市道路可用 |
| `travelLaneSlope` / 行车道坡度 | number | 默认坡度规则 | 左侧默认正值，右侧默认负值 |
| `hardShoulderSlope` / 硬路肩坡度 | number | 默认坡度规则 | 未指定时保持默认 |
| `earthShoulderSlope` / 土路肩坡度 | number | 默认坡度规则 | 未指定时保持默认 |
| `wideningTable` / 变宽表 | array | 空 | 当前不按桩号实时驱动图形，先保存参数 |
| `slopeTable` / 坡度变化表 | array | 空 | `slopeMode` 为 `VariableByStation` 时使用 |
| `innerCurb` / 内侧路缘石 | object | 默认未启用 | 包含启用、宽度、高度、埋深 |
| `outerCurb` / 外侧路缘石 | object | 中分带外侧默认启用，其余按默认规则 | 包含启用、宽度、高度、埋深 |
| `pavementLayerTemplateRef` / 路面结构层模板引用 | object | 空 | 包含 handle、名称、厚度；需要 CAD 点选绑定 |
| `colorOverride` / 颜色覆盖 | RGB / ACI | 按部件默认色 | 用户明确指定颜色时才覆盖 |
| `sideScope` / 作用侧 | `Left` / `Right` / `Both` | Agent 规则文件 | 用户说“左侧行车道加宽”时只作用左侧 |
| `baseRef` / 创建蓝本引用 | object / enum | 默认规则或上下文 | 用户说“基于原本 / 默认 / 刚才那个 / 现有模板”时使用 |
| `componentOperation` / 部件操作 | enum | 用户明确补丁 | 创建派生模板时可为 `addComponent`、`modifyComponent`、`deleteComponent` |
| `componentType` / 部件类型 | enum | 用户明确补丁或规则兜底 | 如 `TravelLane`、`HardShoulder`、`Sidewalk` |
| `anchorComponentType` / 锚定部件 | enum | 用户明确补丁 | 如“行车道外侧新增人行道”中的行车道 |
| `positionMode` / 插入位置 | enum | 用户明确补丁或位置词归一化 | 如 `OutsideOf`、`InsideOf`、`Before`、`After`；“最右侧 / 最左侧”归一化为外侧插入 |
| `width` / 部件宽度 | number | 用户明确补丁 | 新增或修改部件时使用，例如 10 米 |
| `widthDelta` / 部件宽度增量 | number | 用户明确补丁 | 表达“加宽 1 米”时基于蓝本部件宽度增量应用 |

当前 MVP 的核心创建默认值由外置后端规则文件控制：

```text
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml
```

该文件采用组件级默认值：`templateName`、`displayScale`、`unit` 是参数级默认；道路等级对应的完整部件列表由规则文件 `defaultComponentsByRoadGrade` 提供。RoadProto 本地 Tool Adapter 不再补默认组件；若后端规则没有下发必需值，RoadProto 本地只返回校验失败。各道路等级默认部件必须与 RoadProto 原生 `SubgradeTemplateDefaults::create` 一致，单侧顺序均为从道路中线向外，右侧同序对称：

| 道路等级编码 | 单侧默认部件，从中线向外 | 总部件数 |
| --- | --- | ---: |
| `Expressway` | `Median 1.5`、`TravelLane 7.5`、`HardShoulder 3.0`、`EarthShoulder 0.75` | 8 |
| `FirstClass` | `Median 1.0`、`TravelLane 3.75`、`TravelLane 3.75`、`HardShoulder 2.5`、`EarthShoulder 0.75` | 10 |
| `SecondClass` | `TravelLane 3.75`、`HardShoulder 1.5`、`EarthShoulder 0.75` | 6 |
| `ThirdClass` | `TravelLane 3.5`、`HardShoulder 0.75`、`EarthShoulder 0.75` | 6 |
| `FourthClass` | `TravelLane 3.0`、`HardShoulder 0.25`、`EarthShoulder 0.5` | 6 |
| `UrbanExpressway` | `Median 1.0`、`TravelLane 7.5`、`SideMedian 1.0`、`BikeLane 3.0`、`Sidewalk 4.0` | 10 |
| `UrbanArterial` | `Median 1.5`、`TravelLane 3.5`、`TravelLane 3.5`、`SideMedian 1.5`、`BikeLane 2.5`、`Sidewalk 3.0` | 12 |
| `UrbanSubArterial` | `TravelLane 3.5`、`TravelLane 3.5`、`BikeLane 2.5`、`Sidewalk 3.0` | 8 |
| `UrbanBranch` | `TravelLane 3.25`、`Sidewalk 2.0` | 4 |

所有默认部件的 `height` 为 `0`，`slopeMode` 为 `Fixed`，`wideningTable` 和 `variableSlopeTable` 为空，内侧路缘石默认未启用，路面结构层引用默认未绑定。所有默认中分带 `Median` 的外侧路缘石启用，宽度、高度和埋深均为 `0.15`。

`totalWidth` 是派生展示值。用户只说“路基总宽 26 米”时，Agent 不得自行拆分各部件宽度，必须追问希望调整哪些部件，或使用专门的总宽分配规则。

## 6. 默认值规则

- 默认值由外置后端规则文件补全，当前文件为 `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml`。
- 用户未指定参数时，模板名称、显示比例和单位使用规则文件中的 `defaultValue`；部件数据使用规则文件中的 `defaultComponentsByRoadGrade`。
- 用户指定参数优先级高于默认值。
- 仅覆盖用户明确指定的参数。
- 未提及参数保持默认配置。
- 默认值补全由规则层完成，不由 LLM 自行臆造，也不由 RoadProto 本地 Tool Adapter 兜底。
- RoadProto 本地只负责执行和校验：缺少道路等级、模板名称、显示比例、单位或组件列表时返回失败，不猜测默认值。
- 空白新建请求仍应优先追问道路等级；但用户明确说“基于原本 / 默认路基模板”时，当前 MVP 使用规则默认蓝本继续创建，默认蓝本编码为 `Expressway`，再应用用户给出的局部参数补丁。
- 派生创建按 `CreateFromBase + ParameterPatch` 执行：先得到完整蓝本 `Components`，再应用 `componentOperations` 或标量覆盖项，最后调用 `SubgradeTemplate.Create`。蓝本模板本身不被写入。
- `Expressway`、`FirstClass`、`SecondClass`、`ThirdClass`、`FourthClass`、`UrbanExpressway`、`UrbanArterial`、`UrbanSubArterial`、`UrbanBranch` 必须均生成左右对称的非空默认模板。
- 默认模板初始部件不包含路缘带，`CurbStrip` 只作为用户手动新增类型保留。
- 默认模板中出现的中分带部件，外侧路缘石默认启用，宽度、高度和埋深均为 `0.15`。
- 默认坡度按左右侧写入：左侧行车道、硬路肩、路缘带为 `0.02`，右侧为 `-0.02`；左侧土路肩为 `0.03`，右侧为 `-0.03`；其他部件默认 `0`。
- 默认颜色按 RoadProto `SubgradeTemplateDefaults::defaultColorFor(side, type)` 生成。
- `roadCenterlineHandle` 默认空，MVP 不自动绑定道路中线。
- 路面结构层模板引用默认空。除非用户在 CAD 中点选有效 `DnPavementLayerTemplateEntity`，否则不得伪造 handle。

## 7. 校验规则

- 道路等级必须存在，并且必须属于 `RoadGrade` 支持枚举。
- 后端规则层必须把中文道路等级归一化为 RoadProto 枚举编码，例如“高速公路”归一化为 `Expressway`，再下发给本地 Tool Adapter。
- 路基宽度必须大于 `0`。
- 各个部件宽度必须大于 `0`。
- 用户修改后的参数必须满足规范范围。
- 模板必须能够成功生成断面结构。
- `displayScale` 必须是 RoadProto 支持值：`1`、`10`、`20`、`50`、`100`。
- `components` 不得为空。
- `components.side`、`components.type`、`components.slopeMode` 必须是 RoadProto 支持枚举。
- `slopeMode=Fixed` 时，`fixedSlope` 必须存在。
- `slopeMode=VariableByStation` 时，`slopeTable` 至少应包含可用于预览的首行数据；没有数据时必须追问或阻断。
- 内外侧路缘石未启用时，其宽度、高度、埋深按 `0` 保存。
- 内外侧路缘石启用时，宽度、高度、埋深不得为负。
- `pavementLayerTemplateRef` 必须来自 CAD 点选结果，不允许 LLM 直接生成 handle。
- 写 CAD 前必须完成 DryRun 和用户确认。
- 后端不得直接写 DWG；正式写入必须通过 RoadProto 本地 Adapter。

## 8. 追问规则

当出现以下情况时必须追问：

- 无法确定道路等级。
- 用户表达存在歧义。
- 用户只给出路基总宽，但没有说明调整哪些部件。
- 用户说“加宽”“减窄”，但没有说明作用部件。
- 用户说“左侧”“右侧”以外的侧向范围不清楚，且默认 `Both` 会改变风险。
- 派生创建中蓝本引用不唯一，例如“基于现有模板”但场景中有多个模板，且没有最近上下文、名称、handle 或点选结果。
- 用户要求绑定路面结构层模板，但没有点选模板实体。
- 用户要求修改已有模板、删除模板、戴帽、出图等不属于本意图的动作。

追问话术：

- 请问道路等级是什么？
- 请问采用哪种标准路基配置？
- 你希望把总宽变化分配到哪些部件？
- 请问要调整左侧、右侧，还是左右两侧都调整？
- 你要创建新的路基模板，还是修改当前已有模板？
- 如果要绑定路面结构层模板，请在 CAD 中点选对应模板实体。

## 9. 确认前展示

确认页必须展示：

- 道路等级。
- 模板名称。
- 显示比例。
- 路基总宽度、坡度摘要。
- 行车道宽度、坡度。
- 硬路肩宽度、坡度。
- 土路肩宽度、坡度。
- 中央分隔带宽度。
- 侧分带、慢车道、人行道等城市道路部件。
- 内侧路缘石和外侧路缘石启用状态。
- 路面结构层模板绑定状态。
- 插入点策略。
- 默认值来源。
- 用户覆盖项和覆盖前后差异。
- 将调用的 Tool。
- 将创建的 CAD 对象：`DnSubgradeTemplateEntity`。
- 风险等级。
- `TraceId` / `TaskId`。

用户确认前允许修改：

- 所有模板参数。
- 模板名称。
- 显示比例。
- 道路等级。
- 部件宽度、坡度、颜色。
- 变宽表和坡度变化表。
- 内外侧路缘石参数。
- 路面结构层模板引用。
- 插入点策略。

用户未确认前，不得写入 CAD。

## 10. 工具绑定

| 项 | 内容 |
| --- | --- |
| 后端 Tool 名称 | `SubgradeTemplate.Create` |
| 后端参数 Schema | `SubgradeTemplateCreateArguments` |
| RoadProto 本地适配入口 | `RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE` |
| CAD 写入对象 | `DnSubgradeTemplateEntity` |
| 是否写 CAD | 是 |
| 是否必须用户确认 | 是 |
| 是否需要 DryRun | 是 |
| 所属 Skill 白名单 | `subgrade_template` |

工具边界：

- 后端负责生成结构化 Tool 调用计划、参数 Schema 校验、规则校验、DryRun 和 Trace。
- WPF 负责展示参数、预览、追问、用户修改和最终确认。
- RoadProto 本地 `AGENT` 模块负责把后端 Tool 调用转成受控的本地执行请求。
- C++ ObjectARX Adapter 负责点取插入点、调用现有路基模板桥接能力并写入 `DnSubgradeTemplateEntity`。
- C++ ObjectARX Adapter 创建成功后应刷新显示并把当前视口聚焦到新建 `DnSubgradeTemplateEntity`，避免用户确认成功后仍看不到新模板位置。
- 外置后端不能直接写 DWG。
- WPF 不能直接操作 `AcDbEntity`、`AcDbObjectId`、`ads_name` 等 ObjectARX 类型。

当前 MVP 已将创建类 Tool 参数升级为组件级 DTO。后端规则层下发 `Components`，RoadProto 本地 Adapter 将每个组件映射为 `SubgradeComponentDto`，并保留宽度、高度、固定坡度、坡度模式、颜色、变宽表、坡度变化表、内外侧路缘石和路面结构层引用字段。

创建类 Tool 也支持由后端规则层先应用 `componentOperations`。例如“基于原本的路基模板，最右侧增加一个 10 米的行车道”会先生成默认蓝本完整 `Components`，再新增一条 `Right / TravelLane / width=10` 部件，最终仍调用 `SubgradeTemplate.Create`，不会调用 `SubgradeTemplate.Modify` 修改原模板。

## 11. 执行结果

成功时返回：

- 模板创建成功。
- 模板 ID / CAD 实体 handle。
- AutoCAD 当前视口已聚焦到新建模板实体。
- 模板名称。
- 插入点。
- 模板预览数据。
- 部件数量和部件摘要。
- 实际使用的默认值和用户覆盖项。
- `TraceId` / `TaskId`。

失败时返回：

- 参数校验失败。
- 模板生成失败。
- 规则校验失败。
- 用户取消。
- 插入点点取失败。
- RoadProto 本地 Adapter 执行失败。
- 是否已写入部分对象。
- 是否已回滚。
- 人工处理建议。

## 12. 日志与 Trace

必须记录的阶段：

- 用户输入。
- 意图识别。
- 参数抽取。
- 默认值补全。
- 参数校验。
- DryRun。
- 用户确认。
- Tool 调用。
- RoadProto 本地 Adapter 点取插入点。
- Bridge 调用现有路基模板能力。
- `DnSubgradeTemplateEntity` 创建结果。
- 结果返回。
- 错误、取消和重试。

每条 Trace 至少包含：

- `TraceId`。
- `SessionId`。
- `TaskId`。
- Agent ID。
- Skill ID。
- 意图 ID。
- Tool 名称。
- 当前阶段。
- 输入摘要。
- 输出摘要。
- 错误码和错误消息。

日志不得记录明文 API Key，不得泄露敏感配置。
