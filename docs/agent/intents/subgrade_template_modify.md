# 意图：修改路基模板

## 1. 意图基本信息

| 项 | 内容 |
| --- | --- |
| 意图 ID | `subgrade_template.modify` |
| 所属 Agent | `roadproto_engineering_agent` |
| 所属 Skill | `subgrade_template` |
| 所属模块 | `AGENT` |
| 验证业务模块 | `CROSS_SECTION` |
| Skill 文档 | `docs/agent/skills/subgrade_template_skill.md` |
| 通用模板 | `docs/agent/intent_rule_template.md` |
| 关联 RoadProto 功能文档 | `docs/business/cross_section/路基模板_编辑.md` |
| 当前状态 | 草案 |

本意图负责把用户对已有路基模板的修改请求转成受控修改计划。修改必须以既有 `DnSubgradeTemplateEntity` 的当前参数为基础，只覆盖用户明确指定的字段。

## 2. 用户表达

用户可能这样说：

- 修改当前模板的行车道宽度。
- 把这个路基模板的行车道加宽 1 米。
- 把刚才创建的模板行车道加宽 1 米。
- 修改上一个模板的土路肩宽度。
- 将主线路基模板的硬路肩改成 3 米。
- 把当前路基模板名称改为主线标准路基。
- 左侧土路肩宽度改成 0.75 米。
- 把这个模板两侧行车道各加宽 0.5 米。
- 在两侧行车道外侧新增 3 米人行道。
- 删除右侧土路肩。
- 把刚才创建的模板左侧硬路肩颜色改成红色。

不应匹配的表达：

- 帮我创建高速公路路基模板。
- 删除这个路基模板。
- 当前图里有哪些路基模板？
- 用当前模板进行戴帽。
- 修改路面结构层模板。

## 3. 意图边界

本意图负责：

- 修改已有路基模板。
- 解析用户指定的目标模板。
- 解析用户指定的字段变更。
- 保留未提及参数的实体原值。
- 展示修改前后差异。
- 用户确认后调用 RoadProto 本地工具修改实体。

本意图不负责：

- 创建新模板。
- 删除模板。
- 执行道路模型戴帽。
- 修改路面结构层模板实体。
- 根据总宽自动分配所有部件。

## 4. 必要参数

| 参数 | 类型 | 是否必填 | 来源 | 缺失时处理 |
| --- | --- | --- | --- | --- |
| `targetHandle` / `TargetMode` / 目标模板 | string | 是 | 最近对象上下文、当前选择集、用户输入、候选选择、执行时点选 | 没有目标引用、名称、handle 和可用上下文时追问 |
| `changeSet` / 修改项 | object | 是 | 用户输入 | 没有明确修改项时追问 |
| `confirmation` / 用户确认 | bool | 写 CAD 前必填 | WPF 确认面板 | 未确认不得写入 CAD |

## 5. 可选参数

| 参数 | 类型 | 默认值来源 | 说明 |
| --- | --- | --- | --- |
| `targetName` | string | 空 | 按模板名称定位，名称不唯一时必须追问 |
| `targetMode` | `ByHandle` / `ByName` / `PickOnExecute` | 空 | 显式 handle / 名称优先；强指代“这个 / 刚才 / 上次 / 选中的”按上下文定位；泛称“路基模板”不长期绑定最近模板 |
| `targetRef` | string | 空 | 保留用户原始目标表达，方便日志解释 |
| `templateName` | string | 实体原值 | 用户明确要求改名时覆盖 |
| `roadGrade` | enum | 实体原值 | 修改道路等级时需要重新校验默认结构影响 |
| `displayScale` | number | 实体原值 | 支持 RoadProto 允许值 |
| `laneWidth` | number | 实体原值 | 可按左右侧或双侧覆盖 |
| `laneWidthDelta` | number | 无 | 在实体原值基础上增减 |
| `hardShoulderWidth` | number | 实体原值 | 可按左右侧或双侧覆盖 |
| `earthShoulderWidth` | number | 实体原值 | 可按左右侧或双侧覆盖 |
| `medianWidth` | number | 实体原值 | 修改中央分隔带 |
| `unit` | string | 空 | 用户明确提到的长度单位，例如 `m` 或“米”；用于解释宽度字段，不作为独立修改项 |
| `sideScope` | `Left` / `Right` / `Both` | `Both` | 歧义会影响风险时追问 |
| `componentOperations` | array | 空 | 部件级增删改操作 |

`componentOperations` 中单个操作包含：

| 字段 | 说明 |
| --- | --- |
| `operation` | `modifyComponent`、`addComponent` 或 `deleteComponent` |
| `sideScope` | `Left`、`Right` 或 `Both`；用户未说清且操作会影响左右侧时必须追问 |
| `componentType` | 稳定部件编码，例如 `TravelLane`、`HardShoulder`、`EarthShoulder`、`Sidewalk` |
| `occurrence` | `all`、`first`、`second` 或具体序号，用于同侧多个同类部件 |
| `positionMode` / `anchorType` | 新增部件时的插入位置，例如在 `TravelLane` 外侧新增 |
| `patch` | 宽度、宽度增量、高度、坡度、颜色、内外侧路缘石、路面结构层、变宽表和坡度变化表 |

## 6. 默认值规则

- 修改以实体当前值为默认值。
- 用户未提及字段保持原值。
- `Delta` 类型参数在原值基础上增减。
- 用户同时给出绝对值和增量时，以绝对值为准，并在确认页展示冲突处理。
- LLM 不得自行补目标 handle。
- 用户说“刚才创建的 / 上一次创建的”时，规则层优先使用会话最近创建的路基模板上下文。
- 用户说“刚才修改的 / 上一个模板”时，规则层优先使用会话最近修改或最近触碰的路基模板上下文。
- 用户刚创建或刚修改路基模板后，继续说“修改这个路基模板”“这个模板”“把两侧行车道宽度改为 10”等强指代或省略对象的连续修改表达时，规则层优先使用同一会话的最近触碰模板，生成 `TargetMode=ByHandle`。
- 用户只泛称“修改路基模板，把行车道宽度改为 20 米”时，不应长期绑定最近触碰模板；如果已经有实际修改项，生成 `TargetMode=PickOnExecute` 让本地 Adapter 点选目标。如果只是“修改路基模板”且没有修改项，则同时追问“哪个模板”和“修改哪个参数”。
- 用户明确说“当前选中模板 / 选中的模板 / selected template”时，WPF 可在发送请求前读取 AutoCAD implied selection；若选择集中唯一对象是 `DnSubgradeTemplateEntity`，将其 handle 作为 `CurrentTemplateHandle` 传给后端，规则层直接生成 `TargetMode=ByHandle`。没有唯一选中路基模板时，才退回执行时点选或追问。
- 修改意图中的 `targetName` 只有在它确实指向模板名称时才可作为 `ByName` 目标；如果模型把“行车道”“硬路肩”“土路肩”“中分带”“人行道”等部件名误填到 `targetName` / `target.name`，规则层必须把它当作部件线索忽略，继续按显式 handle、真实模板名称或同会话最近触碰模板定位，不得下发 `TargetMode=ByName; TargetName=行车道`。
- 用户说“这个模板 / this template”但同一会话没有最近模板 handle 或名称时，不追问目标，生成 `TargetMode=PickOnExecute`，由 RoadProto 本地 Adapter 在执行时点选。
- 用户明确说“当前选中模板”时不被最近模板上下文抢占；有唯一 CAD 选择集实体时直接按 handle 修改，没有唯一选择时再进入 `PickOnExecute`。
- 用户使用中文、数字或口语化等级表达时，规则层先归一化为 RoadProto 稳定枚举。例如“二级”“2”“二级路”“二级道路”都归一化为 `SecondClass`；无法归一化时追问，不把原文传给本地 Adapter。
- 用户原句已经出现部件名，但模型只抽取到通用 `width` / `widthDelta` 等补丁字段、漏掉 `componentType` 时，规则层必须从 `matchedExpression` / 参数 sourceText 中兜底归一化部件类型。例如“把行车道改为 10”必须生成 `ComponentType=TravelLane`，不得下发空部件类型。
- 规则层按“先找对象，再问部件细节”的顺序工作。没有目标时先解析最近上下文、名称、handle 或 `PickOnExecute`；只有目标定位模式明确后，才追问“左侧、右侧还是两侧”这类部件级参数。
- 修改类请求必须同时具备“目标模板”和“实际修改项”两个条件，才能进入确认和 Tool 调用。用户只说“修改路基模板”“修改这个模板”时，即使目标可点选，也必须先追问要修改哪个参数或部件，不得生成空的 `SubgradeTemplate.Modify` 计划。
- `AwaitingUserInput` 续跑必须保留已明确的目标模板上下文。用户先说“修改选中的路基模板”或通过“点选”补齐目标后，后续再说“把右侧硬路肩改为 20 米宽”，规则层应复用 pending `TargetHandle` / `TargetName`，不得把第二句话当成没有目标的新任务，也不得把“硬路肩”误当成模板名。
- `unit` 是所有宽度、高度、路缘石等长度参数的辅助单位槽位。模型输出 `unit=m` 或 `unit=米` 时必须通过 Schema 校验；但只有 `unit` 而没有宽度、坡度、颜色、部件增删等实际修改项时，仍然按缺修改项追问。

## 7. 校验规则

- 必须定位唯一 `DnSubgradeTemplateEntity`。
- `TargetMode=PickOnExecute` 的唯一性由 RoadProto 本地 Adapter 在 AutoCAD 点选阶段校验。
- 部件别名不得作为模板 `ByName` 目标下发；例如 `TargetName=行车道` 应先按部件名处理，再结合最近模板或点选目标定位。
- 修改项不得为空。
- `componentOperations` 为空且 `templateName`、`roadGrade`、`displayScale` 等模板级可修改字段也未出现时，规则层返回 `FollowUpRequired`，不得进入 `AwaitingUserConfirmation`。
- 只有 `unit` 不算有效修改项；例如用户或模型只补出“米”，不能触发 Tool。
- 宽度必须大于 `0`。
- `displayScale` 必须属于支持值。
- `componentOperations` 中的 `componentType`、`sideScope`、`operation` 和 `positionMode` 必须能归一化成稳定编码；未知值不得直接下发。
- 新增部件必须有可执行的宽度和部件类型；删除或修改部件必须能匹配到实体中已有部件。
- 变宽表和坡度变化表必须按桩号和值成对传递，空表表示不覆盖原表。
- 修改后模板必须能生成有效断面结构。
- 写 CAD 前必须完成 DryRun 和用户确认。

## 8. 追问规则

必须追问：

- 无法确定目标模板。
- 没有目标引用、目标名称、目标 handle，也没有可用会话上下文。
- 当前选择集没有路基模板。
- 当前选择集有多个路基模板。
- 用户只说“改一下”“优化一下”，没有明确修改项。
- 用户只说“修改路基模板”“修改这个模板”“修改刚才创建的模板”，没有说明宽度、坡度、颜色、道路等级、新增部件、删除部件等实际修改项。
- 用户只给总宽但未指定分配方式。
- 缺目标时的追问应允许两种补充：用户在输入框输入模板名称，或在 WPF 面板中点击“点选”并从 CAD 图中选择一个 `DnSubgradeTemplateEntity`。

追问话术：

- 请问要修改哪个路基模板？
- 当前选择了多个路基模板，请指定要修改哪一个。
- 请问要修改哪个参数？
- 请先明确要操作的路基模板目标，并说明要修改哪个参数？
- 你希望把总宽变化分配到哪些部件？

## 9. 确认前展示

确认页必须展示：

- 当前 Agent、Skill、Intent。
- 目标模板名称和 handle。
- 修改项列表。
- 修改前值和修改后值。
- 未修改参数保持原值说明。
- 风险等级。
- 将调用的 Tool。
- TraceId / TaskId。

用户确认前允许修改所有 `changeSet` 字段。

## 10. 工具绑定

| 项 | 内容 |
| --- | --- |
| 后端 Tool 名称 | `SubgradeTemplate.Modify` |
| RoadProto 本地适配入口 | `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` |
| CAD 写入对象 | `DnSubgradeTemplateEntity` |
| 是否写 CAD | 是 |
| 是否必须用户确认 | 是 |
| 是否需要 DryRun | 是 |

## 11. 执行结果

成功时返回：

- 模板修改成功。
- 模板 handle。
- 修改项摘要。
- 修改后预览或参数摘要。

失败时返回：

- 目标不存在。
- 目标不唯一。
- 参数校验失败。
- 本地 Adapter 执行失败。
- 用户取消。

## 12. 日志与 Trace

必须记录：

- Agent 路由。
- Skill 识别。
- Intent 识别。
- 目标解析。
- 参数抽取。
- 实体原值读取。
- 修改差异生成。
- 参数校验。
- DryRun。
- 用户确认。
- Tool 调用。
- Adapter 执行。
- 结果返回。
