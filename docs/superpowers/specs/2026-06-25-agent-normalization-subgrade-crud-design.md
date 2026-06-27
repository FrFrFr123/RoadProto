# Agent 归一化与路基模板修改删除设计

## 背景

当前 Agent 已能创建路基模板，并由规则文件下发不同道路等级的完整默认部件。联调中发现两个问题：

- 用户说“二级路”时，模型把原始值 `二级路` 直接下发给 Tool，RoadProto 本地只接受稳定编码 `SecondClass`，因此提示“不支持的道路等级编码”。
- `subgrade_template.modify` 和 `subgrade_template.delete` 已有 Intent 和本地命令入口，但本地 `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 仍是占位，只校验目标字段，尚未真正查找、修改或删除 DWG 中的路基模板实体。

本设计同时解决两类问题：一是把自然语言表达统一归一化为稳定工程编码；二是让路基模板修改、删除具备目标选择和部件级操作能力。

## 目标

1. 用户表达道路等级时，不要求穷举固定句子。`二级`、`二级路`、`二级道路`、`二级公路`、`2级`、`2 级` 在道路等级上下文中都归一化为 `SecondClass`。
2. 归一化能力不只服务道路等级，也服务后续对话中的侧别、部件类型、颜色、目标引用、操作动词、数值单位和相对位置。
3. 修改和删除必须先明确目标模板。目标可来自点选、模板名称、当前上下文、刚才创建的模板、上一次修改的模板。
4. 修改支持部件级操作：修改部件、增加部件、删除部件、替换部件类型，并支持宽度、坡度、颜色、内外侧路缘石、路面结构层引用等参数。
5. 修改或删除部件时，若用户未说明左侧、右侧或两侧，必须追问，不得默认改两侧。
6. Agent 可见日志中，每个句子前继续使用 `---`；每次用户输入一次后，聊天区和流转日志区都空两行再打印新流程，便于人工区分。

## 非目标

- 本轮不实现道路模型 Skill，不把道路模型相关表达归入路基模板。
- 本轮不自动按路基总宽反推所有部件宽度。
- 本轮不让后端直接读取或写入 DWG；DWG 操作仍全部走 RoadProto 本地 Adapter。
- 本轮不让模型生成 AutoCAD handle。handle 只能来自上下文、点选或本地查询结果。

## 总体方案

采用五层能力：

```text
用户表达
-> 模型抽取 raw 参数
-> 归一化器 raw -> canonical + confidence + provenance
-> 规则层校验、追问、生成 Tool 参数
-> RoadProto 本地 Adapter 查找实体、应用补丁、写回或删除
```

模型只负责抽取用户原始表达和大致结构；规则层负责确定性归一化和安全判断；本地 Adapter 负责 CAD 实体查找、读取、校验和写入。

## 通用归一化设计

新增通用归一化服务，建议命名为 `NormalizationService`，由多个字段归一化器组成：

| 字段 | 示例 raw | canonical |
| --- | --- | --- |
| 道路等级 `roadGrade` | `二级路`、`2级`、`二级的` | `SecondClass` |
| 侧别 `sideScope` | `左边`、`右幅`、`两边` | `Left` / `Right` / `Both` |
| 部件类型 `componentType` | `车道`、`行车道`、`慢车道`、`非机动车道` | `TravelLane` / `BikeLane` |
| 目标引用 `targetRef` | `这个`、`刚才创建的`、`上一次修改的` | `PickOnExecute` / `LastCreated` / `LastModified` |
| 操作动词 `operation` | `加宽`、`减窄`、`删掉`、`换成` | `modifyComponent` / `deleteComponent` / `replaceComponentType` |
| 相对位置 `position` | `外侧`、`内侧`、`行车道外侧` | `OutsideOf` / `InsideOf` |
| 颜色 `color` | `红色`、`255,0,0` | RGB |

每个归一化结果必须保留：

```text
rawValue        用户或模型原始值
canonicalValue 进入 Tool 的稳定编码
confidence     规则置信度
provenance     alias / regex / context / model / user_confirmed
explanation    给可见日志用的人话解释
```

归一化要有上下文保护。例如：

- 当前 Run 正在追问“道路等级是什么？”时，用户只回 `2`，可以归一化为 `SecondClass`。
- 用户说“创建 2 个模板”时，`2` 是数量，不是道路等级，不能归一化为 `SecondClass`。
- 用户说“2 米宽”时，`2` 是宽度，不是道路等级。

因此归一化器不能只看字符串，还要接收当前槽位、Intent、追问状态和参数附近词。

## 路基模板目标选择

修改和删除都必须先解析目标，目标来源按优先级处理：

1. `targetHandle`：来自点选结果、当前选择、上一次 Tool 结果或明确 handle。
2. `targetName`：用户明确说“名为 X 的模板”。
3. `targetRef=PickOnExecute`：用户说“这个模板”“选这个模板”“修改这个”，确认后由 AutoCAD 命令行提示点选路基模板实体。
4. `targetRef=LastCreated`：用户说“刚才创建的”“上一次创建的”。
5. `targetRef=LastModified`：用户说“刚才修改的”“上一次修改的”。
6. 缺失或歧义：追问用户点选、输入名称，或说明使用上一次创建/修改的模板。

Agent 后端应维护会话级最近对象上下文：

```text
lastCreatedSubgradeTemplateHandle
lastModifiedSubgradeTemplateHandle
lastTouchedSubgradeTemplateHandle
lastTouchedSubgradeTemplateName
```

创建成功后更新 `lastCreated` 和 `lastTouched`；修改成功后更新 `lastModified` 和 `lastTouched`；删除成功后清理对应最近对象，避免用户后续再引用已删除实体。

## 修改参数协议

`SubgradeTemplate.Modify` 不再只使用 `laneWidth`、`laneWidthDelta` 等少量字段，而是增加 `componentOperations`。建议结构如下：

```text
componentOperations:
  - operation: modifyComponent | addComponent | deleteComponent | replaceComponentType
    sideScope: Left | Right | Both | Ask
    selector:
      componentType: TravelLane | HardShoulder | EarthShoulder | Median | SideMedian | BikeLane | Sidewalk | CurbStrip
      occurrence: first | second | all | index
      relativeTo: componentType 或 componentIndex
    position:
      mode: InsideOf | OutsideOf | Before | After | AtEnd
      anchorType: TravelLane 等
    patch:
      width / widthDelta
      fixedSlope / slopeMode / variableSlopeTable
      colorR / colorG / colorB
      type
      innerCurb / outerCurb
      pavementLayerRef
```

示例映射：

- “把两侧行车道加宽 0.5 米” -> `modifyComponent`，`sideScope=Both`，`componentType=TravelLane`，`patch.widthDelta=0.5`。
- “左侧硬路肩宽度改成 3 米” -> `modifyComponent`，`sideScope=Left`，`componentType=HardShoulder`，`patch.width=3`。
- “两侧行车道外侧新增 3 米人行道” -> `addComponent`，`sideScope=Both`，`position=OutsideOf TravelLane`，新增部件 `type=Sidewalk`，`width=3`。
- “删除右侧土路肩” -> `deleteComponent`，`sideScope=Right`，`componentType=EarthShoulder`。
- “把左侧慢车道改成人行道” -> `replaceComponentType`，`sideScope=Left`，`selector.componentType=BikeLane`，`patch.type=Sidewalk`。

如果用户说“修改行车道宽度”但没有说明左、右或两侧，规则层必须追问：

```text
请问修改左侧、右侧，还是两侧行车道？
```

如果用户说“新增人行道”但没有说明位置，规则层必须追问：

```text
请问人行道加在左侧、右侧还是两侧？加在什么部件的内侧或外侧？
```

## 删除设计

删除分为两类：

- 删除整个路基模板实体：Intent 为 `subgrade_template.delete`，Tool 为 `SubgradeTemplate.Delete`。
- 删除模板内部部件：Intent 仍为 `subgrade_template.modify`，Tool 为 `SubgradeTemplate.Modify`，`componentOperations.operation=deleteComponent`。

删除整个模板的目标选择规则与修改一致。若用户说“删除这个模板”，确认后进入 AutoCAD 点选。若用户说“删除刚才创建的模板”，使用 `lastCreatedSubgradeTemplateHandle`。若没有目标，追问。

删除部件必须同时明确目标模板和部件选择。缺少侧别、部件类型或位置时追问。

删除风险等级为 `high`，必须展示影响范围并要求确认。修改部件风险等级为 `medium`，但批量修改两侧多个部件或删除部件时可以提升为 `high`。

## RoadProto 本地执行设计

`RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 需要从占位入口升级为实体执行器：

1. 读取请求文件。
2. 校验 `skillId`、`operation`、`intentId` 和 Tool 白名单。
3. 按 `targetHandle`、`targetName` 或 `PickOnExecute` 查找 `DnSubgradeTemplateEntity`。
4. `query` 读取实体摘要，返回 handle、名称、道路等级和部件列表摘要。
5. `modify` 读取当前完整模板数据，应用 `componentOperations` 或简单覆盖字段，调用 `SubgradeTemplateRules` 校验，写回实体。
6. `delete` 检查引用关系和用户确认上下文，通过 ObjectARX 删除实体。
7. 写回结果文件：`succeeded`、`entityId`、`templateName`、`message`，并建议扩展 `operationSummary` 和 `changedComponentCount`。

本地 Adapter 不做自然语言猜测，只执行后端已经归一化后的稳定编码和补丁指令。

## 日志与展示

日志分两层：

- 文件日志：保持机器可检索，不额外插入展示空行。
- WPF 可见日志和聊天区：用于人读，允许空行和 `---` 标记。

展示规则：

1. 每次用户输入前，聊天区插入两个空行，再显示 `--- 我: ...`。
2. 同一轮后端事件第一次进入流转日志前，也插入两个空行。
3. 聊天区所有 Agent 行改成 `--- Agent: ...`。
4. 下方日志区继续由 `AgentLogFormatter` 保证每个可见句子前有 `---`。
5. 归一化事件必须打印 raw 与 canonical，例如：

```text
--- 归一化 RoadGradeRaw=二级路（用户原始表达）; RoadGrade=SecondClass（二级公路）; NormalizeRule=road_grade_alias（二级道路等级别名规则）
```

## 分阶段实施

### 第一阶段：归一化、目标解析、日志分组、目标框架

- 新增通用归一化服务和道路等级归一化测试。
- 扩展 `roadGrade` 支持 `二级路`、`2级`、`2 级`、`先生成一个二级的吧` 等上下文表达。
- 新增侧别、目标引用、部件类型的基础归一化。
- 扩展目标选择参数：`targetRef`、`targetMode=PickOnExecute`、`lastTouched` 上下文。
- WPF 聊天区和日志区按用户输入分组，插入两行空行，并统一 `---`。
- 修改/删除如果只有“这个模板”，确认后允许 AutoCAD 提示点选。

### 第二阶段：完整修改和删除执行

- 扩展 Tool 参数 DTO 和请求文件协议，支持 `componentOperations`。
- 后端规则层生成部件级补丁计划。
- RoadProto 本地命令实现查找、点选、按名称搜索、读取实体、应用补丁、校验、写回和删除。
- 后端与 WPF 展示 DryRun 摘要，例如“将修改两侧行车道宽度 +0.5 米，共 4 个部件”。
- 增加托管 Bridge 测试、后端规则测试和核心域规则测试。

## 验证标准

第一阶段完成后应通过：

- “创建二级路路基模板”成功下发 `RoadGrade=SecondClass`。
- “先生成一个二级的吧”在创建路基模板上下文中识别为二级公路。
- “创建 2 个模板”不会误识别为二级公路。
- “修改这个模板的行车道宽度”能进入点选目标流程，但因缺少侧别追问。
- 聊天区和日志区每轮用户输入之间有两行空行，每句以 `---` 开头。

第二阶段完成后应通过：

- “把刚才创建的模板两侧行车道加宽 0.5 米”能定位上次创建模板并修改。
- “把名为默认路基模板的左侧硬路肩宽度改成 3 米”能按名称定位并修改。
- “在两侧行车道外侧新增 3 米人行道”能新增部件。
- “删除右侧土路肩”能删除模板内部部件。
- “删除这个模板”确认后提示用户点选并删除整个模板实体。
- 目标缺失、侧别缺失、部件选择歧义时必须追问，不得写 CAD。

## 文档同步范围

实施时需要同步：

- `docs/agent/skills/subgrade_template_skill.md`
- `docs/agent/intents/subgrade_template_modify.md`
- `docs/agent/intents/subgrade_template_delete.md`
- `docs/business/agent/路基模板Skill_增删改查_MVP.md`
- `docs/business/agent/Agent控制台_MVP.md`
- `docs/agent_builder/skill_intent_tool_authoring.md`
- `docs/agent_builder/roadproto_practice_log.md`
- `docs/reuse/capability_catalog.md`
- `docs/dev/version_log.md`
