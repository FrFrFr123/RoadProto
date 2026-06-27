# 路基模板 Skill 增删改查 MVP

## 功能定位

本功能用于验证 RoadProto 可控工程 Agent 的 Skill 优先受控流程。路基模板只是首个业务样例，功能归属 RoadProto 本地 `AGENT` 模块承接，不表示 Agent 架构隶属于横断面模块。

## Skill 与 Intent

```text
Agent: roadproto_engineering_agent
Skill: subgrade_template
Intent:
  subgrade_template.create
  subgrade_template.modify
  subgrade_template.delete
  subgrade_template.query
```

本功能支持路基模板的创建、修改、删除和查询四类动作。创建复用现有路基模板实体创建能力；修改、删除、查询通过 `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 统一进入 RoadProto 本地受控 Tool Adapter。

## 分层边界

- 后端负责 Agent / Skill / Intent 规则、模型网关、Schema 解析、规则裁决、状态机、Tool 白名单和 Trace。
- RoadProto WPF 负责输入、追问、确认、展示和本地工具桥接。
- RoadProto ObjectARX Adapter 负责 DWG 读写入口。
- 后端不直接写 DWG。
- WPF 不直接操作 `AcDbEntity`、`AcDbObjectId`、`ads_name` 等 ObjectARX 类型。
- LLM 不允许直接输出 AutoCAD 命令或直接调用 Tool。
- `道路模型` 是独立工程对象，不属于本 Skill。当前 MVP 未接入道路模型 Skill 时，只能提示尚未接入，不得误调用路基模板 Tool。
- 创建默认值由后端规则文件控制，RoadProto 本地 Tool Adapter 只负责执行和校验。

## Tool 白名单与审批

| Tool | Intent | 写 CAD | 风险 | 审批 |
| --- | --- | --- | --- | --- |
| `SubgradeTemplate.Create` | `subgrade_template.create` | 是 | 中 | 需要 |
| `SubgradeTemplate.Modify` | `subgrade_template.modify` | 是 | 中 | 需要 |
| `SubgradeTemplate.Delete` | `subgrade_template.delete` | 是 | 高 | 需要 |
| `SubgradeTemplate.Query` | `subgrade_template.query` | 否 | 低 | 不需要 |

任何未出现在 `subgrade_template` Skill 白名单内的 Tool 必须被后端规则层拒绝。

## 执行流转

```text
WPF Agent Console
-> 后端 Run 状态机
-> Skill / Intent 规则裁决
-> 追问或确认
-> RoadProto 本地 Tool Bridge
-> ObjectARX Adapter 命令
-> Tool Result 回传后端
-> Trace / 日志记录
```

查询类请求可以不进入用户确认，但仍必须经过 Agent、Skill、Intent、Schema、Rule 和 Trace。

## 本地命令

| 命令 | 说明 |
| --- | --- |
| `RD_AGENT_CONSOLE` | 打开可停靠 Agent Console |
| `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` | 执行路基模板 Skill 的本地 Tool 请求文件 |
| `RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE` | 创建类请求复用的既有路基模板回写命令 |

`RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 使用请求文件传递 `operation`、`traceId`、`taskId`、`agentId`、`skillId`、`intentId`、`targetMode`、`targetRef`、`targetHandle`、`targetName`、模板参数、部件级操作和 `resultPath`。命令执行后必须写回 `succeeded`、`entityId`、`templateName` 和 `message`。

请求文件必须采用 UTF-8 无 BOM 的 key-value 文本，并固定使用 LF 换行。WPF Bridge 写文件时使用无 BOM 编码和 `\n` 行分隔；ObjectARX Adapter 读取时仍要容错清理首个 key 前的 UTF-8 BOM，以及 key / value 两端的空格、制表符和 CRLF。`operation=modify` 等控制字段属于本地 Tool 调度入口，不能因为文件编码或换行导致读取为空，否则会误报“未知路基模板工具操作”。

修改、删除和查询的目标定位支持：

| 模式 | 说明 |
| --- | --- |
| `ByHandle` | 后端已解析到模板 handle，本地 Adapter 按 handle 打开实体 |
| `ByName` | 用户输入模板名称，本地 Adapter 在模型空间查找并校验唯一性 |
| `PickOnExecute` | 没有可用最近模板、handle 或名称时，本地 Adapter 在执行时提示点选并校验 `DnSubgradeTemplateEntity` |
| `All` | 查询全部路基模板摘要 |

同一 Agent 面板会话内，创建或修改成功后必须记住最近触碰的路基模板 handle。用户紧接着说“修改这个路基模板右行车道为 20 米”“修改刚才创建的模板行车道加宽 1 米”“把两侧行车道宽度改为 10”这类强指代或省略对象的连续修改表达时，默认目标就是最近创建或最近修改的模板，后端应生成 `TargetMode=ByHandle`。

泛称“修改路基模板”不能长期抢占最近对象。用户说“修改路基模板，把行车道宽度改为 20 米”时，如果没有显式 handle / 名称 / “刚才创建的” / “这个模板” / “选中的模板”等强目标引用，应生成 `TargetMode=PickOnExecute`，由 AutoCAD 本地 Adapter 让用户点选目标；如果用户只说“修改路基模板”且没有实际修改项，应同时追问要操作哪个模板和要修改哪个参数。

用户明确说“修改选中的模板”“修改当前选中路基模板”时，WPF Agent Console 可读取 AutoCAD implied selection。若选择集中唯一对象是 `DnSubgradeTemplateEntity`，WPF 将该实体 handle 通过 `currentTemplateHandle` 发送给后端，后端直接按 `TargetMode=ByHandle` 生成计划；若未选中或选中多个路基模板，则退回 `PickOnExecute` 或追问，避免最近对象覆盖用户当前选择意图。

当后端追问“要操作哪个路基模板目标”时，WPF 面板必须给用户两个补充入口：输入框可直接输入模板名称；对话区显示“点选”按钮，点击后回到 CAD 图中选择 `DnSubgradeTemplateEntity`，再把选择到的 handle 作为补充输入提交给同一个等待中的 Run。后端在后续追问里必须保存该 pending 目标；用户下一句只补“把右侧硬路肩改为 20 米宽”时，应继续作用于刚点选或刚选中的模板。

修改意图中要防止模型把部件名当作模板名。`targetName` / `TargetName` 只有在它确实是路基模板名称时才用于 `ByName` 定位；如果值是“行车道”“硬路肩”“土路肩”“中分带”“人行道”等部件别名，后端规则层必须把它当作部件线索，继续使用显式 handle、真实模板名称或同一会话最近触碰模板，不能把 `TargetMode=ByName; TargetName=行车道` 下发给 RoadProto 本地 Adapter。

修改类请求支持 `componentOperation.*` 行协议。单个操作可表达修改、增加或删除部件，字段包括 `operation`、`sideScope`、`componentType`、`occurrence`、`positionMode`、`anchorType` 和 `patch.*`。`patch` 可覆盖宽度、宽度增量、高度、固定坡度、坡度模式、RGB 颜色、内外侧路缘石、路面结构层引用、变宽表和坡度变化表。RoadProto 本地 Adapter 只执行稳定编码，不解释“二级路”“左边这个”这类自然语言。

修改类请求不得为空执行。后端规则层必须确认至少存在一个模板级修改字段，或至少存在一条 `componentOperation.*` 部件级修改、新增、删除操作；否则返回追问，让用户说明“修改哪个参数或部件”。例如“修改路基模板”不能直接进入确认页；“修改默认路基模板，把行车道改为 10”缺侧别时先追问左侧、右侧还是两侧，用户补充后再生成 `patch.width=10` 的结构化修改计划。

用户说“20 米”“0.5m”时，模型可以把单位输出为 `unit` 槽位。`unit` 只作为长度参数的辅助解释字段，允许通过 Schema，但不算实际修改项；只有单位而没有宽度、坡度、颜色、部件增删等变更时仍然追问。

创建类请求支持“基于蓝本的派生创建”。用户说“创建路基模板，基于原本的路基模板，最右侧增加一个 10 米的行车道”时，主动作仍是 `subgrade_template.create`，局部“增加”只表示新模板参数补丁。后端规则层先使用蓝本完整参数，当前 MVP 中“原本 / 默认路基模板”使用 `Expressway` 默认组件作为蓝本，再应用 `componentOperation=addComponent; sideScope=Right; componentType=TravelLane; positionMode=OutsideOf; width=10`，最终调用 `SubgradeTemplate.Create` 创建新实体，不修改原模板。

后续若用户说“基于刚才那个模板 / 基于现有这个模板 / 基于名称为 X 的模板”，应先通过会话最近对象、名称、handle 或点选读取真实蓝本参数。蓝本不唯一或无法定位时必须追问，不能猜测；已经开放到 Schema 的模板参数、部件参数、颜色、路缘石、变宽表、坡度变化表和结构层引用都可以作为派生创建的 patch 字段。

## 默认值来源

路基模板创建 MVP 的核心默认值位于：

```text
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml
```

该规则文件声明 `templateName`、`displayScale`、`unit` 的参数级默认值，并通过 `defaultComponentsByRoadGrade` 声明道路等级对应的完整部件默认值。`Expressway`、`FirstClass`、`SecondClass`、`ThirdClass`、`FourthClass`、`UrbanExpressway`、`UrbanArterial`、`UrbanSubArterial` 和 `UrbanBranch` 均必须与 RoadProto 原生 `SubgradeTemplateDefaults::create` 的默认预设一致；宽度、坡度、RGB、内外侧路缘石、变宽表、坡度变化表和路面结构层引用字段均由规则文件下发。

`laneWidth`、`laneWidthDelta`、`hardShoulderWidth`、`earthShoulderWidth` 和 `medianWidth` 只作为用户明确覆盖项。RoadProto 本地 Bridge 只校验和执行 `Components`，如果后端未下发组件列表，直接返回校验失败，不在本地补默认部件。

## Trace 要求

Trace 至少记录：

- 用户输入。
- Agent 路由。
- Skill 路由。
- Intent 识别。
- Schema 校验。
- 规则裁决。
- 追问或用户确认。
- Tool 计划。
- 本地 Tool 调用。
- ObjectARX Adapter 返回结果。

每条关键流转记录必须包含 `TraceId`、`TaskId`、`AgentId`、`SkillId`、`IntentId`、`ToolName` 和当前状态。WPF 面板显示的日志应为中文可读摘要，原始 stage / message 保留在文件日志中。

修改类 Tool 参数日志必须同时展示目标定位和部件操作预览。目标定位用于说明本次修改的是哪个路基模板，例如 `TargetMode=ByHandle; TargetHandle=ABCD; TargetName=高速路基模板`；部件操作预览用于说明修改模板内部哪个部件，例如 `ComponentOperation1=modifyComponent; componentType=TravelLane; side=Both; width=20`。二者不能混在同一个“名称”字段里解释。

## MVP 限制

- 修改、删除、查询已通过 `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 执行实体级 CRUD：查询可返回列表或指定模板摘要，修改可按 handle / 名称 / 执行时点选定位并回写 `DnSubgradeTemplateEntity`，删除会打开目标实体并执行删除。
- 部件级修改已覆盖 MVP 中最常见的增删改路径，包括行车道加宽、指定侧修改、指定侧删除、相对已有部件新增、人行道等新增部件，以及变宽表和坡度变化表补丁。
- 删除类请求在存在引用风险时由规则层阻断。
- 本 MVP 不执行横断面戴帽、土石方计算或横断面出图。
