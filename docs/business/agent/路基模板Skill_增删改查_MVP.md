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

`RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 使用请求文件传递 `operation`、`traceId`、`taskId`、`agentId`、`skillId`、`intentId`、`targetHandle`、`targetName`、模板参数和 `resultPath`。命令执行后必须写回 `succeeded`、`entityId`、`templateName` 和 `message`。

## 默认值来源

路基模板创建 MVP 的核心默认值位于：

```text
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml
```

该规则文件声明 `templateName`、`displayScale`、`unit` 的参数级默认值，并通过 `defaultComponentsByRoadGrade` 声明道路等级对应的完整部件默认值。高速公路默认模板必须与 RoadProto 原生“路基模板-高速公路”预设一致：左右各包含中分带、行车道、硬路肩和土路肩，宽度、坡度、RGB、内外侧路缘石、变宽表、坡度变化表和路面结构层引用字段均由规则文件下发。

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

## MVP 限制

- 修改、删除、查询的原生命令入口已建立，请求文件和结果文件协议已固定；实体级 CRUD 细节可在后续版本继续加厚。
- 删除类请求在存在引用风险时由规则层阻断。
- 本 MVP 不执行横断面戴帽、土石方计算或横断面出图。
