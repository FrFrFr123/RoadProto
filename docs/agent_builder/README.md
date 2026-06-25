# 可复用 Agent 搭建能力文档区

本目录用于沉淀“可控工程 Agent 如何搭建”的通用方法。它来自 RoadProto Agent MVP 的实践，但不绑定 RoadProto、AutoCAD、EICAD 或某一个业务模块。后续做下一个项目时，应优先从本目录复制方法、模板和检查清单，再按目标软件替换 Adapter、UI 和业务规则。

本目录和 `docs/agent/` 的区别：

| 文档区 | 作用 |
| --- | --- |
| `docs/agent/` | RoadProto 当前项目内的 Agent 架构、服务契约、配置、Skill / Intent 和 WPF Console 规则 |
| `docs/agent_builder/` | 跨项目复用的 Agent 搭建方法、12 层模块说明、模板和实践经验 |
| `docs/agent_builder/original_mvp_template/` | 用户最初提供的 MVP 模板模块说明原始归档，只做来源留存 |

## 文档地图

| 文档 | 用途 |
| --- | --- |
| `reusable_agent_principles.md` | 可控 Agent 的核心原则和边界 |
| `reusable_mvp_architecture.md` | 跨项目可复用 MVP 架构、运行形态和最小闭环 |
| `twelve_layer_modules.md` | 12 层模块职责、输入输出、MVP 交付物 |
| `entry_routing.md` | 用户一句话如何自主判断为闲聊、咨询、工作流候选或工作流补充 |
| `skill_intent_tool_authoring.md` | Skill、Intent、Schema、Rule、Tool 的编写和绑定方法 |
| `maintenance_policy.md` | 后续每次 Agent 相关修改时，本目录如何同步更新 |
| `roadproto_practice_log.md` | RoadProto 当前实践带来的经验、修正和可复用结论 |
| `templates/` | 新项目可复制的 Skill、Intent、Tool、Trace 模板 |
| `original_mvp_template/` | 初始 12 层 MVP 模板原始文件归档 |

## 可复用 Agent 的目标

可复用 Agent 不是一个 Prompt，也不是一个聊天面板。它是一套可解释、可校验、可审批、可追踪的工程任务执行框架。

最小闭环必须包含：

```text
用户输入
-> 入口路由
-> Agent / Skill / Intent 识别
-> 参数抽取
-> Schema 结构化校验
-> 规则引擎补全、校验、风险判断
-> Tool Registry 白名单匹配
-> 软件 Adapter DryRun
-> 用户审批
-> 正式执行
-> 结果校验
-> Trace / 日志 / 评测样例沉淀
```

## 当前实践融合结论

RoadProto MVP 已经验证出几条应写入通用手册的结论：

- 后端服务建议外置，主软件只保留薄客户端、UI 和 Adapter。
- 前端可以是宿主软件内的 WPF、Qt、MFC、WebView 或原生面板，但 MVP 不应强依赖独立 Web 页面。
- 模型网关必须屏蔽 DeepSeek、Qwen、GLM、GPT 等 Provider 差异。
- API Key 必须由后端或安全配置中心保存，不能进入前端日志、Trace 或 Git。
- LLM 只负责识别、抽取、追问和解释，默认值、强规则、风险、工具选择必须由规则层控制。
- Skill 是业务能力边界，Intent 是动作边界，Tool 是受控执行边界，三者不能混在一起。
- 写入、修改、删除类动作必须经过 DryRun 和用户审批。
- 每个运行任务必须带 `traceId`、`sessionId`、`taskId`，并记录关键阶段。
- 缺参追问必须作为结构化字段返回给前端，不能只藏在日志事件里。
- 入口路由应由 Agent 自主判断，但低置信度时必须追问，不能误执行。
- 受控对话必须先经过运行时事实层；日期、时间、模型身份、Provider 和宿主状态等事实由系统提供，不能让模型猜。
- 相邻工程对象必须在入口路由和 Intent 负向样例中显式排除；`道路模型` 不能被误识别为 `路基模板`。
- 默认值应由机器可读规则文件控制，宿主软件 Adapter 只负责执行和校验。
- 复杂工程默认值应以完整结构下发，例如组件列表、材料层或配置行，不要压扁成几个标量再让 Adapter 推导。
- 前端可见流转日志要映射成人能理解的中文文案，原始 stage / message 保留给文件 Trace。
- 前端可见流转日志应由后端结构化事件驱动，并显示每个阶段的关键输出；不要只在前端拼接“任务成功/失败”的粗状态。
- 宿主软件内 Agent Console 要记住上次模型 Provider 配置，打开时从后端配置中心恢复，不应每次回到硬编码默认模型。
- 写入类确认动作应嵌入对话流或任务卡片内，靠近本次执行计划；不要把确认按钮长期固定在输入框旁边。
- 聊天区和日志区应自动换行并允许局部复制。工程 Agent 的首要排查入口就是用户能复制一段 Trace 或错误给开发者。

## 后续维护规则

凡是修改以下内容，必须同步检查本目录：

- Agent 总体架构、后端部署、前端交互、模型 Provider。
- 入口路由、闲聊和工作流分流规则。
- Agent / Skill / Intent 体系。
- Schema、Rule、Tool、Adapter、DryRun、审批、Trace。
- 日志、评测、安全、API Key、回滚。
- RoadProto 实践中发现的新问题和新结论。

具体维护清单见 `maintenance_policy.md`。
