# Agent Builder 十二层模块说明

本文档把最初 12 个 MVP 模块说明整理为跨项目可复用版本。具体项目可以替换前端技术、后端语言、宿主软件和 Adapter，但职责边界不建议随意合并。

## 01 前端交互层 Agent Console

定位：用户唯一可见入口。

必须提供：

- Agent 状态展示。
- 模型 / API 配置入口。
- 自然语言输入。
- 入口路由结果展示。
- Agent / Skill / Intent 结果展示。
- 参数抽取结果展示。
- 缺失参数追问。
- 规则命中结果。
- DryRun 预览。
- 用户审批。
- 执行结果。
- Trace 查看和日志入口。

MVP 不应把业务规则写进前端事件。

前端交互层还应满足以下可用性要求：

- 输入框按产品约定提交，例如桌面宿主中可用 Enter 发送；发送成功后清空输入并异步恢复焦点。
- 聊天区和日志区必须自动换行、随窗口宽度自适应，并允许用户选择局部文字复制。
- 写入、修改、删除类任务的确认 / 取消按钮应嵌入本次对话或任务卡片内，不应和普通发送按钮混在一起。
- 前端显示日志应消费 Orchestrator 返回的结构化事件流，按事件增量去重，不应只自己拼接粗粒度状态。
- 宿主软件容易抢焦点时，前端应做温和的异步焦点恢复，避免用户输入被送到宿主命令行；不得在面板加载或获得焦点事件中同步强制焦点，避免宿主焦点重入。
- 在 CAD、BIM 或其他桌面宿主面板中，可复制聊天和日志不应通过“列表项内再嵌套可聚焦输入控件”的方式实现。优先使用单体只读文本面板、不可聚焦文本项或宿主验证过的富文本控件，减少停靠面板启动、追加日志和选择文本时的焦点重入风险。

## 02 Agent 配置中心

定位：管理 Agent、Skill、Intent、模型、Provider、路由策略和运行时配置。

必须管理：

- Agent 清单。
- Skill 清单。
- Intent 清单。
- Model Provider 清单。
- API 通道配置。
- 模型优先级。
- Tool 白名单。
- 运行时启用状态。

配置中心可以先是后端配置文件，后续再演进为后台管理界面。

前端打开时应从配置中心读取已保存配置并恢复最近使用的 Provider、Base URL、模型名和启用状态。API Key 只显示“已配置/未配置”，不得回填明文。

## 03 模型网关 Model Gateway

定位：屏蔽模型 Provider 差异。

必须提供：

- 统一请求结构。
- 统一返回结构。
- Provider 选择。
- API Key 获取。
- 超时控制。
- 错误归一化。
- 结构化输出解析。
- 模型调用 Trace。

MVP 推荐优先支持 OpenAI-compatible Provider，例如 DeepSeek、Qwen、GLM、GPT。

## 04 工作流编排层 Orchestrator

定位：Agent Run 状态机和流程主控。

必须负责：

- 创建 `traceId`、`sessionId`、`taskId`。
- 组织入口路由、模型调用、规则、工具、审批、执行。
- 保存当前运行状态。
- 处理中断、取消、失败和继续输入。
- 把阶段事件写入 Trace。

任何跨层流程都应从 Orchestrator 进入，不应让前端或 Adapter 直接跳层。

## 05 上下文管理层 Context Manager

定位：给模型和规则提供可控上下文。

Context Package 应包含：

- 用户原文。
- 当前会话状态。
- 当前 Agent / Skill 候选。
- 当前项目摘要。
- 当前软件状态。
- 当前选中对象摘要。
- 当前草稿对象。
- 可用工具列表。
- 历史对话摘要。

不应直接放入：

- 大量原始图形数据。
- 明文 API Key。
- 未脱敏个人信息。
- 未结构化的大型日志。

## 06 LLM 能力层

定位：理解自然语言，不负责强规则和执行。

只做：

- 入口分类辅助。
- 意图识别。
- 参数抽取。
- 缺参追问建议。
- 结果解释。

必须输出结构化结果。模型输出不可信，必须进入 Schema 和 Rule。

## 07 Schema 结构化控制层

定位：把模型输出和工具输入压到明确结构里。

建议 Schema 类型：

- InputSchema
- RouteSchema
- IntentSchema
- ParamSchema
- DraftObjectSchema
- ValidationSchema
- RuleHitSchema
- RiskSchema
- ToolPlanSchema
- DryRunSchema
- ApprovalSchema
- ExecutionSchema
- TraceSchema

每个字段建议带来源标记，例如 `user_explicit`、`context`、`default_rule`、`derived_rule`、`adapter_read`。

## 08 规则引擎层 Rule Engine

定位：默认值、推导、校验、风险和审批。

规则分类：

- 默认值规则。
- 推导规则。
- 业务校验规则。
- 风险判断规则。
- 审批规则。
- 自动修复规则。
- 工具选择规则。

规则执行顺序建议：

```text
字段标准化
-> 默认值补全
-> 派生计算
-> 业务校验
-> 风险分级
-> 审批判定
-> Tool Plan 生成
```

## 09 工具与原子函数层 Tool Registry

定位：把可执行能力注册为受控工具。

Tool Registry 必须提供：

- 工具元数据。
- 输入输出 Schema。
- 风险等级。
- Tool Scope。
- 白名单校验。
- DryRun 支持声明。
- Execute 支持声明。
- 错误码。

工具调用六道门：

```text
Tool 已注册
Intent 已绑定
Skill 白名单允许
Schema 校验通过
风险和审批规则通过
Adapter 能力声明支持
```

## 10 软件 Adapter 层

定位：把通用 ToolCall 转成宿主软件内受控操作。

Adapter 模式：

- `read`
- `dry_run`
- `execute`
- `rollback`

Adapter 不应调用模型，不应自行解释用户意图，不应绕过宿主软件事务和业务服务。

## 11 执行控制层

定位：保护工程数据安全。

主流程：

```text
Plan
-> DryRun
-> Preview
-> Approval
-> Execute
-> Save
-> Result Validation
-> Rollback if needed
```

写入、修改、删除必须走审批。高风险动作必须展示风险原因、影响对象和回滚能力。

## 12 Trace / 评测 / 治理层

定位：让 Agent 可回放、可定位、可评测、可治理。

必须记录：

- 输入。
- 入口路由。
- Agent / Skill / Intent。
- 参数。
- Schema。
- Rule。
- Tool。
- Adapter。
- 审批。
- 执行。
- 结果。
- 错误。

失败样例应进入评测集，用于后续 Prompt、Schema、Rule 和 Tool 的回归测试。

Trace 事件需要同时服务界面和文件：

- API 返回结构化事件列表，前端用中文可读映射展示阶段和阶段输出。
- 文件日志保留原始 `stage`、`message`、`traceId`、`sessionId`、`taskId` 和结构化数据，便于机器检索。
- 阶段事件不能只有“进入某层”，还要记录该层输出，例如路由原因、意图结果、规则结果、工具参数摘要、审批动作、Adapter 返回值。
- 日志摘要不得包含明文 API Key 或未脱敏敏感配置。
