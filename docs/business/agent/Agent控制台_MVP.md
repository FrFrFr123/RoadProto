# Agent 控制台 MVP

## 基本信息

- 功能名称：Agent 控制台 MVP
- 所属模块：`AGENT`
- 命令名称：`RD_AGENT_CONSOLE`
- 对应代码入口：`src/modules/agent/AgentModule.*`、`src/cad_adapter/objectarx/agent/ObjectArxAgentConsoleCommand.*`、`src/ui/wpf/RoadProto.Terrain.UI/Agent/AgentConsoleCommands.cs`、`src/ui/wpf/RoadProto.Terrain.UI/Agent/AgentConsoleSafePanel.cs`
- 业务文档维护人：RoadProto
- 原型版本：V0.1 Agent MVP 文档阶段
- 是否可复用：是

## 功能背景

RoadProto 后续需要让大模型能力进入道路设计原型流程，但不能让模型直接控制 CAD 或自由调用工程软件能力。Agent 控制台用于提供统一自然语言入口，并把用户输入纳入 Schema、规则、Tool Registry、DryRun、审批、执行日志和 Trace 的受控链路。

## 业务目标

- 提供 AutoCAD 内 WPF Agent 入口；AutoCAD 2021 当前采用默认右侧停靠的 `PaletteSet`，内部延迟挂载代码构建的安全面板，避开复杂 XAML 宿主崩溃。
- 自动检查并拉起独立 Agent 后端服务。
- 连接 `F:\0_GPT_RoadProtoAgentBackend` 中的 `.NET 8 / ASP.NET Core` 后端。
- 支持 DeepSeek、阿里千问、GLM、GPT 的 API 配置和测试连接。
- 自主判断用户输入是闲聊、咨询、工作流候选、工作流补充还是明确工程指令。
- 展示本次路由到的业务 Agent。
- 展示结构化参数、字段来源、规则命中和风险。
- 展示 DryRun 预览和影响范围。
- 在用户审批后触发受控执行。
- 支持查看本次运行 Trace 和全流程流转日志。

## 适用场景

- 用户希望通过自然语言发起 RoadProto 任务。
- 任务可以映射到已注册业务 Agent。
- 任务涉及写入 DWG 时，需要先预演和审批。
- 用户需要查看每一步流转，定位后端、模型、规则、Bridge 或 CAD 执行问题。
- 当前 MVP 首个验证任务为路基模板创建。

## 输入条件

- CAD 选择对象：可为空，后续由业务 Agent 决定是否需要读取选中对象。
- 用户输入参数：自然语言任务、用户选择模型/API 通道、用户审批动作。
- 模型配置：Provider、API Base URL、API Key、默认模型名、启用状态。
- 已有设计实体：由本地 `AGENT` 模块按摘要形式提供给后端。
- 外部数据：独立 Agent 后端配置、Prompt、Schema、规则和工具注册表。

## 输出结果

- CAD 图形实体：由具体业务 Agent 和工具决定，Agent 控制台本身不直接创建实体。
- 领域实体：Agent 运行状态、参数结构、规则命中、工具计划、Trace。
- 日志：后端日志和 RoadProto 本地日志。
- 表格或报告：MVP 不输出表格。
- 更新通知或重建请求：MVP 不做跨模块联动更新。

## 操作流程

1. 用户运行 `RD_AGENT_CONSOLE`，或点击 Ribbon `Agent 控制台` 按钮。
2. Ribbon 点击时，托管 Ribbon 只通过 AutoCAD 命令队列发送 `RD_AGENT_CONSOLE`，不得直接在按钮事件中创建 WPF 窗口或 Palette。
3. 原生 ObjectARX 命令确认托管插件已加载后，转发到托管 `RD_AGENT_CONSOLE_UI`。
4. 托管命令创建 `PaletteSet("Agent 控制台")`，只注册最小安全 WPF 宿主 `Grid`。
5. `PaletteSet` 显示后设置 `Dock = DockSides.Right` 并 `Activate(0)`，系统在 WPF Dispatcher 空闲优先级中延迟创建并挂载 `AgentConsoleSafePanel`。
6. 托管命令监听 `PaletteSet.SizeChanged`，把 Palette 实际尺寸同步给 WPF 宿主和 `AgentConsoleSafePanel`，确保内容布满右侧停靠区域并随用户拖动自适应。
7. WPF 打开 Agent 控制台，使用较宽初始尺寸，默认停靠在 AutoCAD 右侧侧边栏并支持左右吸附。
8. 控制台访问 `http://127.0.0.1:17861/health`。
9. 如果后端未运行，RoadProto 自动启动 `F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe`。
10. 控制台轮询健康检查并在流转日志中打印启动状态。
11. 控制台读取后端已保存模型配置，并恢复上次使用的 Provider、Base URL、模型名和启用状态。
12. 用户配置或选择允许范围内的模型和 API 通道。
13. API Key 通过后端保存到 `%APPDATA%\RoadProtoAgent\settings.json`，由 DPAPI 加密。
14. 用户输入自然语言任务，按 Enter 或点击发送提交；发送成功后输入框清空并保持焦点。
15. RoadProto 本地 `AGENT` 模块提交任务和当前 DWG 摘要。
16. 独立后端创建 `TraceId` / `SessionId` / `TaskId` 并运行状态机。
17. WPF 保存后端返回的 `SessionId`，后续新输入继续沿用同一会话；创建或修改工具成功后，后端才能用同一会话的最近模板上下文解析“这个路基模板”“刚才创建的模板”“把两侧行车道宽度改为 10”。
18. 后端先执行入口路由：闲聊进入受控对话，咨询进入受控咨询。受控对话先检查运行时事实问题，日期、时间、当前 Provider、当前模型名和 Agent 身份由后端直接回答；其他闲聊和咨询可调用大模型自然回答但不进入工程 Skill / Intent、不调用 Tool；工作流候选进入追问；明确工程指令继续进入 Agent / Skill / Intent。
19. 若用户表达为“道路模型”或“创建道路模型”，后端把它作为独立 `road_model` 候选处理；当前 MVP 未接入该 Skill 时返回终止性提示，不调用路基模板 Tool，也不保持等待补充状态。
20. WPF 展示入口路由结果、意图、路由业务 Agent、参数、规则、风险和 DryRun。
21. 如需补充信息，用户在 WPF 中填写并重新提交；短参数回答继续原任务，对象未定、明确闲聊、咨询、未接入对象或新的完整工程指令会重新入口路由。
22. 如 DryRun 通过，WPF 展示结构化执行计划。
23. 用户在对话区域内点击确认执行或取消，不在发送按钮旁边审批。
24. 用户确认后，后端请求 RoadProto 本地 Adapter 执行已授权工具。
25. WPF 展示执行结果、Trace 和全流程日志。

## 关键业务规则

- WPF 不直接调用模型 API。
- WPF 不直接调用 ObjectARX。
- 外置后端不能直接写 DWG。
- RoadProto 本地 `AGENT` 模块只做薄模块，不承载 Orchestrator 主状态机。
- 模型输出必须经过 Schema 校验。
- 闲聊和咨询必须在入口路由层从工程执行流程中截断；可调用大模型自然回答，但不得调用本地 Tool，不得声称已修改 CAD 或保存 DWG。
- 日期、时间、当前模型身份、Provider 和 Agent 身份属于后端运行时事实，不得由模型猜测；命中这类问题时应直接返回事实并记录 `RuntimeFactsAnswered`。
- 工作流候选但动作或对象不完整时必须追问，不得进入执行。
- `道路模型` 是独立工程对象，不得被识别为路基模板 Skill。
- 未接入工程对象不得进入循环追问；返回提示后，下一句用户输入应作为新任务重新入口路由。
- 等待补充状态不得无限粘连。若用户在追问期间输入“你好”“今天是几号”“路基模板怎么设置？”“创建路基模板”“道路模型”等明显不是当前追问答案的内容，后端应记录 `ContinuationRerouted`，按新输入重新路由，并让 WPF 跳出旧追问。
- 默认值和强规则必须由规则引擎处理；当前路基模板创建默认值由后端规则文件控制，RoadProto 本地只负责执行和校验。
- 写入类工具必须先 DryRun，再审批，再执行。
- 用户取消后任务不得继续执行。
- 每次任务必须生成 `TraceId` / `SessionId` / `TaskId`。
- 全流程必须打印流转日志；WPF 面板显示中文可读日志，原始 stage / message 写入文件日志。聊天区不得只展示 Skill / Intent / Tool 机器码。
- WPF 流转日志必须使用后端 `AgentRun.events` 增量展示每个阶段及阶段输出，包括入口路由、意图识别、Schema、规则、计划、工具参数、确认、Tool 调度、本地校验和执行结果。Agent MVP 初期调试阶段，用户可见流转日志可以显示技术串，但每个技术字段必须把中文解释紧贴在字段旁边，例如 `Status=FollowUpRequired（需要补充信息）; FollowUp=请问道路等级是什么？（追问用户的问题）; BlockReason=-（没有阻断原因，不是错误）`，避免技术串和解释分离后难以对应。
- 可见流转日志应按层级缩进并使用 `---` 作为新句标记：每条事件首行缩进两格，事件内技术串、识别依据、规则解释和下一步等详情缩进四格；每个可见句子或详情行都应以 `---` 开头，方便在密集日志中定位阶段。
- 对话区和流转日志区必须自动换行、随面板宽度自适应，并允许选择局部文字复制。
- Agent 控制台内部内容必须随 AutoCAD 停靠 Palette 拉伸。安全面板挂载时必须清除启动宿主残留的 `Auto` 行列和外边距，聊天区与流转日志区使用比例高度，不得固定成顶部小块。仅设置 WPF `Stretch` 不足以覆盖 AutoCAD 2021 的 `PaletteSet.AddVisual` 宿主测量差异，必须监听 `PaletteSet.SizeChanged` 并显式同步宿主 `Grid` 与安全面板的 `Width / Height`。
- 对话区和流转日志区在 AutoCAD 宿主内不得使用每行嵌套可聚焦文本框的列表结构。当前 MVP 使用单个只读多行文本面板展示可复制内容，减少宿主焦点重入和面板启动崩溃风险。
- 写入类任务的确认和取消按钮必须出现在对话区域，不得固定放在发送按钮旁。
- 模型配置必须在面板打开时从后端读取上次保存值，不能每次回到前端默认 `GPT / gpt-4.1`。
- WPF 打开时不得同步强制输入框焦点，也不得依赖 `KeepFocus` 强行控制宿主焦点；输入框焦点只在用户交互后异步恢复，避免 AutoCAD 宿主焦点重入导致崩溃。
- AutoCAD 2021 中，Agent Console 默认使用 `PaletteSet` 打开可停靠面板，先通过 `AddVisual("Agent", hostGrid, false)` 注册最小 WPF 宿主，再在 `Visible / Dock / Activate` 后延迟挂载 `AgentConsoleSafePanel`。不得把完整 `AgentConsolePalette` XAML 控件直接放入 Palette、`ElementHost` 或 modeless Window；这些路径已通过真实 Ribbon 点击验证会导致 AutoCAD 崩溃或卡死。
- Agent Console 启动探针必须写入 `F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\agent_palette_startup_probe.log`，覆盖 Ribbon 到 `PaletteSet` 创建、`AddVisual`、`Visible / Dock / Activate`、`AgentConsoleSafePanel` 构建、Loaded 和 ViewModel 初始化的关键阶段。
- 日志不得暴露明文 API Key。

## 配置和日志

模型配置保存位置：

```text
%APPDATA%\RoadProtoAgent\settings.json
```

日志保存位置：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend\
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\
```

默认保留最近 14 天，或总量最多 1GB。

## 可复用性说明

- 可复用内容：WPF Agent Console、后端自动启动、后端连接、状态展示、模型配置、参数面板、规则面板、DryRun 面板、审批区、流转日志和 Trace 查看。
- 本轮新增可复用内容：宿主软件内聊天输入焦点保持、Enter 发送并清空、聊天/日志可复制自动换行、停靠面板内容随宿主拖动自适应、避免列表项嵌套可聚焦文本框、确认按钮内嵌到对话流、后端事件流驱动前端可见日志、Provider 配置启动恢复、Ribbon 命令排队进入宿主命令系统、可停靠 Palette 宿主延迟挂载代码构建安全面板和启动探针。
- 临时原型内容：MVP 只验证本地单用户和一个业务 Agent。
- 正式复用前需要改造的内容：后端服务部署、Windows Service 封装、Credential 管理、权限策略、评测集和多业务 Agent 配置。

## 与其他模块的依赖关系

- 上游模块：无固定上游，用户从 Ribbon 或命令行进入。
- 下游模块：由业务 Agent 的 Tool Plan 决定，首个验证下游为 `CROSS_SECTION` 的路基模板能力。
- 实体联动行为：MVP 不新增实体联动关系。

## 后续对接正式 EICAD 功能的注意事项

- Agent 控制台只作为统一入口，不应承载业务算法。
- 独立后端服务应可替换模型和部署方式。
- Tool Registry 和 RoadProto 本地 Adapter 的工具契约必须版本化。
- Trace 中不得暴露敏感工程数据和 API Key。
- 后续产品化可把同一后端封装为 Windows Service，但不改变 RoadProto 与后端的进程隔离边界。
