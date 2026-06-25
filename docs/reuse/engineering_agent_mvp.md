# 可控工程 Agent MVP 底座

## 能力分类

通用 Agent 能力

## 能力说明

可控工程 Agent MVP 底座用于把大模型理解能力安全接入 RoadProto。它通过独立 `.NET 8 / ASP.NET Core` 后端服务、RoadProto 本地 `AGENT` 薄模块、WPF 可停靠 Agent Console、Schema、规则、Tool Registry、DryRun、审批、Trace 和日志，形成可控、可测、可扩展的工程智能体链路。

该能力值得沉淀，因为后续平面、纵断面、横断面、出图出表等业务 Agent 都应复用同一套底座，而不是各自实现临时 Prompt 和工具调用。

## 当前实现

- 源码路径：当前处于文档设计阶段，RoadProto 规划源码见 `docs/agent/directory_and_config_structure.md`。
- 可复用方法文档：`docs/agent_builder/README.md`。
- 独立后端仓库：`F:\0_GPT_RoadProtoAgentBackend`。
- 后端技术栈：`.NET 8 / ASP.NET Core`。
- 对外类型/函数：规划为 Agent Schema、Agent Client Service、Agent Backend Client、Backend Process Supervisor、Local Tool Adapter、Execution Control 和 Trace。
- 当前使用该能力的命令：`RD_AGENT_CONSOLE`、`RD_AGENT_HEALTH`、`RD_AGENT_LOGS`、`RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE`。

## 可复用内容

- WPF 可停靠 Agent Console 交互模式。
- 后端健康检查和本地 companion process 自动启动。
- 独立后端服务契约。
- DeepSeek、阿里千问、GLM、GPT 的模型 Provider 配置模式。
- API Key 由后端 DPAPI 加密保存的 Credential 模式。
- Agent / Skill / Intent 规则注册表。
- Schema 校验链路。
- 默认值、推导、业务校验、风险和审批规则分层。
- 组件列表、材料层、配置行等复杂工程默认值由规则层结构化下发，宿主软件 Adapter 只执行和校验。
- Tool Registry 和工具六道门。
- RoadProto 本地 Adapter 的 read / dry_run / execute / rollback 模式。
- Trace、流转日志和失败样例沉淀。
- 后端 `AgentRun.events` 驱动前端可见日志，按阶段展示入口路由、意图、Schema、规则、计划、工具参数、审批、Adapter 和执行结果。
- 宿主软件内 Agent Console 的输入体验：模型配置启动恢复、Enter 发送、发送后清空并保持焦点、聊天和日志自动换行、局部复制、确认 / 取消内嵌到对话区域。
- 跨项目 Agent Builder 文档、12 层模块说明、入口路由规则、Skill / Intent / Tool 模板和维护清单。

## 不可复用或临时内容

- 路基模板 Skill 只是首个增删改查验证场景，不代表 Agent 底座绑定横断面模块。
- MVP 不包含复杂权限后台、多客户隔离、多 Agent 自治和完整 RAG 平台。
- MVP 不包含独立 Web 前端，也不使用 WebView。
- MVP 不安装 Windows Service，只预留后续部署方式。

## 依赖关系

- domain 依赖：Agent Schema、Trace、风险和工具契约不依赖 ObjectARX。
- application 依赖：RoadProto 本地 Agent 客户端、HTTP 客户端、后端进程 supervisor 和执行控制。
- cad_adapter 依赖：RoadProto 本地工具执行需要通过 `cad_adapter/objectarx/agent/` 访问 AutoCAD。
- ui 依赖：WPF 可停靠面板负责输入、展示、审批、模型设置和日志查看。
- 模块依赖：`AGENT` 模块可通过工具调用复用其他业务模块能力，但不能直接跨层写业务对象。

## 扩展说明

新增业务能力时，应新增：

- 若涉及可复用 Agent 方法或实践结论，应同步更新 `docs/agent_builder/`。
- Skill 文档和机器规则。
- Intent 文档和机器规则。
- ParamSchema 和 DraftObjectSchema。
- 业务规则。
- 工具 scope。
- Prompt。
- 评测样例。
- 业务文档。

不应新增一套独立模型调用、独立审批、独立 Trace 或独立日志机制。

## 验证方式

- 后端测试：模型网关 fake provider、Schema 校验、规则、状态机、Tool Registry、DPAPI 配置保存、日志清理。
- RoadProto 核心测试：Agent Schema、状态映射、工具契约和路基模板参数映射。
- 托管 Bridge 测试：WPF Agent Console DTO、模型设置 DTO 和后端响应 DTO。
- AutoCAD 手工验证：加载 ARX 和托管 WPF 插件后，运行 `RD_AGENT_CONSOLE`，确认面板自动启动后端，完成路基模板创建 Agent 的 DryRun、审批和实体创建。
