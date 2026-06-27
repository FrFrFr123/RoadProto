# WPF Agent Console 交互规范

## 定位

Agent Console 是 RoadProto 中可控工程 Agent 的唯一前端交互入口。MVP 不建设独立 Web 页面，不嵌入 WebView，也不把后台配置平台放进浏览器。所有用户输入、模型设置、参数确认、DryRun 预览、审批、流转日志和 Trace 查看都放在 AutoCAD 内的 WPF 面板中。AutoCAD 2021 当前采用 AutoCAD `PaletteSet` 承载，默认停靠在右侧侧边栏，并启用吸附能力。

交互与视觉参考：

```text
E:\Download\google\道路设计软件对话框.zip
```

参考包只用于 WPF 设计转译：侧边栏密度、聊天流、可折叠 thinking、内嵌选项按钮、滑入设置面板、模型/API 设置、深浅色切换。实现时不得引入独立 Web 前端。

## 入口

MVP 规划新增命令：

```text
RD_AGENT_CONSOLE
```

Ribbon 位置规划：

```text
RoadProto / 工程 Agent / Agent 控制台
```

打开后显示 AutoCAD 内 WPF Agent Console。AutoCAD 2021 当前实测稳定路径为 `PaletteSet` 可停靠面板，初始尺寸为 `560x720`，最小尺寸为 `480x560`，默认 `DockSides.Right`，允许左右侧停靠和吸附；用户可以关闭并重新打开。

## AutoCAD 2021 宿主承载规则

AutoCAD 2021 中 Agent Console 默认使用 AutoCAD 原生 `PaletteSet`，但不得把完整复杂 `AgentConsolePalette` XAML 控件直接交给宿主。实测 `ElementHost` 加入 Palette 会卡死，完整复杂 `AgentConsolePalette` 直接或延迟加入 Palette / modeless Window 后都会导致 AutoCAD 宿主崩溃。当前稳定方案是 `PaletteSet` + 最小 WPF 宿主 `Grid` + 代码构建的安全面板 `AgentConsoleSafePanel`。

Ribbon 按钮点击不得直接在 Ribbon 事件回调中实例化 `AgentConsoleCommands` 或创建任何 WPF 窗口。按钮只允许通过 `SendStringToExecute` 排队发送 `RD_AGENT_CONSOLE`，由原生 ObjectARX 命令负责确认托管插件已加载，再转发到托管 `RD_AGENT_CONSOLE_UI`。这样可以避免 Ribbon UI 事件、命令加载、窗口创建和 WPF 初始化在同一宿主回调栈中重入。

稳定实现顺序为：

1. 创建 `PaletteSet("Agent 控制台")`，设置 `Size = 560x720`、`MinimumSize = 480x560`、`DockEnabled = Left | Right`。
2. 设置 `PaletteSetStyles.Snappable`、`SingleColDock`、关闭按钮、自动隐藏按钮和属性菜单。
3. 创建最小安全启动内容，只包含轻量 `Grid` / `TextBlock`。
4. 调用 `PaletteSet.AddVisual("Agent", hostGrid, false)` 注册 WPF 宿主。
5. 设置 `Visible = true`，再设置 `Dock = DockSides.Right` 并调用 `Activate(0)`。
6. 在 `hostGrid.Dispatcher.BeginInvoke(..., DispatcherPriority.ContextIdle)` 中延迟创建 `AgentConsoleSafePanel`。
7. 挂载安全面板前清空启动宿主的 `RowDefinitions` / `ColumnDefinitions`，移除启动外边距，并把宿主和面板都设置为 `Stretch`。
8. 监听 `PaletteSet.SizeChanged`，用 `PaletteSet.PaletteSize` 回退到 `PaletteSet.Size` 显式更新 `hostGrid.Width / Height` 和 `AgentConsoleSafePanel.Width / Height`。
9. 调用 `hostGrid.Children.Clear()` 和 `hostGrid.Children.Add(panel)` 挂载安全面板，并立即按当前 Palette 尺寸同步一次。

该规则属于 AutoCAD 宿主兼容性要求。后续重构 Agent Console 时，不得改回 `AddVisual("Agent", new AgentConsolePalette())`、`PaletteSet.Add("Agent", new ElementHost { Child = ... })`、`window.Content = new AgentConsolePalette()`，也不得把默认入口降级为不可停靠的 modeless Window；除非重新完成真实宿主点击、关闭、重开和停靠验证。Ribbon 点击事件也不得直接调用 `ShowAgentConsole()`。

面板启动探针写入：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\agent_palette_startup_probe.log
```

探针至少覆盖 `RD_AGENT_CONSOLE_UI` 入口、`PaletteSet` 创建、最小 WPF 宿主注册、`Visible / Dock / Activate`、延迟挂载调度、`AgentConsoleSafePanel` 构建、`Loaded` 和 `ViewModel.InitializeAsync` 前后。若宿主仍崩溃，应优先根据该文件定位最后成功阶段。

## 面板启动流程

1. 用户运行 `RD_AGENT_CONSOLE` 或点击 Ribbon 按钮。
2. RoadProto 创建或激活右侧停靠的 WPF Agent Console Palette。
3. 面板进入 `BackendHealthChecking` 状态。
4. RoadProto 请求 `http://127.0.0.1:17861/health`。
5. 若后端不可用，按配置启动 `F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe`。
6. 面板显示“正在启动后端”并持续打印流转日志。
7. 后端可用后进入 `BackendConnected` 状态。
8. 面板读取后端已保存的模型 Provider、Base URL、模型名和启用状态，并优先显示最近配置过 API Key 的 Provider。
9. 启动失败时显示后端路径、端口、错误摘要、重试按钮和手动选择后端 exe 入口。

## 页面区域

Agent Console 至少包含以下区域：

1. 顶部状态栏
2. 后端状态和启动日志条
3. 模型和 API 通道设置区
4. 自然语言输入和聊天流
5. 业务 Agent 路由结果区
6. 参数确认面板
7. 规则和风险面板
8. DryRun 预览和影响范围面板
9. 审批和执行区
10. 流转日志区
11. Trace 查看区

## 顶部状态栏

状态栏展示：

- 总体 Agent 名称。
- 当前运行状态。
- 当前 `TraceId` / `TaskId`。
- 后端服务连接状态。
- 当前模型 Provider 和模型名。
- 当前 DWG 摘要。
- 当前 RoadProto 版本。

状态栏不展示：

- 明文 API Key。
- 未脱敏文件路径。
- 模型内部推理过程。

## 后端状态和启动日志条

后端状态区展示：

- `/health` 检查结果。
- 后端监听地址 `http://127.0.0.1:17861`。
- 后端 exe 路径。
- 自动启动状态。
- 最近一次错误。
- 重试按钮。
- 手动选择后端 exe 按钮。

该区域必须能让用户快速判断：后端没有启动、启动中、已连接、启动失败、版本不兼容。

## 模型和 API 通道设置区

MVP 支持：

- DeepSeek
- 阿里千问
- GLM
- GPT

每个 Provider 的设置项：

- Provider
- API Base URL
- API Key
- 默认模型名
- 是否启用
- 测试连接

WPF 允许输入和保存配置，但保存动作必须调用后端接口。后端将配置写入：

```text
%APPDATA%\RoadProtoAgent\settings.json
```

API Key 由后端使用 Windows DPAPI 加密。WPF 不保存密钥文件，不在 Trace 中展示密钥原文。

面板每次打开时必须从后端 `/api/settings/models` 读取上次保存的配置，不能只显示前端默认值。若多个 Provider 有配置，优先显示最近更新且已配置 API Key 的 Provider；API Key 输入框仍保持为空，只通过“已配置/未配置”提示状态。

## 自然语言输入和聊天流

聊天流参考 JS 高保真中的结构，但以 WPF 控件实现：

- 用户消息右侧显示。
- Agent 回复左侧显示。
- thinking 状态可折叠，展示当前状态摘要而非模型内部推理。
- 后端状态变化可以作为系统消息进入流转日志，不混入最终工程结论。
- 支持内嵌选项按钮，例如“继续修改参数”“重新 DryRun”“确认执行”“取消任务”。
- 输入框支持 Enter 发送。当前 MVP 输入框自动随宽度换行显示，发送成功后必须清空并异步重新聚焦，减少 AutoCAD 命令行抢焦点。
- WPF 必须保存后端返回的 `SessionId`，后续普通新输入继续用同一个会话发起 `StartRun`。只有同一会话贯穿创建、确认、工具结果回传和后续修改，后端才能把“这个路基模板”“刚才创建的模板”“把两侧行车道宽度改为 10”解析到最近创建或最近修改的模板。
- 用户表达包含“选中的模板 / 当前选中路基模板 / selected template”等强选择集指代时，WPF 在发送请求前读取 AutoCAD implied selection。只有选择集中唯一对象是 `DnSubgradeTemplateEntity` 时，才把该实体 handle 作为 `currentTemplateHandle` 传给后端；普通泛称“修改路基模板”不得携带当前选择上下文，避免用户只是泛称时误改选中或最近对象。
- 当后端处于 `AwaitingUserInput` 且追问内容是在确认“哪个路基模板目标”时，WPF 应在聊天流中显示“点选”按钮，同时保留输入框。用户可以输入模板名称，也可以点击“点选”从 CAD 图中选择 `DnSubgradeTemplateEntity`；点选成功后，WPF 使用选择到的 handle 作为 `currentTemplateHandle` 调用补充输入接口，并在聊天流显示“点选路基模板”。
- 面板打开和控件获得焦点时不得同步强制 `Focus` / `Keyboard.Focus`，避免 AutoCAD Palette 宿主焦点重入。只允许在用户点击输入框、发送完成或取消完成后异步恢复输入焦点。
- `AgentConsoleSafePanel` 必须填满 Palette 宿主区域。不得让启动占位内容的 `Auto` 行定义、外边距或固定高度继续约束正式面板；聊天区和流转日志区应使用比例 `Star` 行随用户拖动 Palette 自适应分配高度。AutoCAD 2021 的 `PaletteSet.AddVisual` 不保证仅靠 WPF `Stretch` 填满宿主，必须把 `PaletteSet.SizeChanged` 中的实际尺寸同步到 WPF 宿主和安全面板。
- 聊天消息必须支持自动换行、随面板宽度自适应，并允许用户选中一段文字复制。
- AutoCAD 宿主中的聊天区和日志区不得使用 `ListBox.ItemTemplate` 为每一行嵌套可聚焦 `TextBox`。这类结构容易放大宿主焦点和消息循环问题；MVP 采用单个只读多行文本面板承载可复制内容，后续若改为富文本列表，也必须保证列表项不可递归抢焦点。

示例输入：

```text
创建一个高速公路路基模板，名称叫主线路基模板，显示比例 1:10。
```

输入后 WPF 创建运行请求，等待后端返回状态和参数结果。

## 业务 Agent 路由结果区

路由结果区展示：

- 入口路由类型：`ChatOnly`、`HelpOnly`、`WorkflowCandidate`、`WorkflowContinuation`、`WorkflowCommand`。
- 入口路由原因和置信度。
- 本次调用的业务 Agent。
- 路由原因。
- 意图置信度。
- 低置信度时的候选 Agent。
- 是否需要用户确认业务方向。

低置信度时，不得进入工具计划和执行阶段。

当入口路由识别为闲聊或咨询时，后端进入受控对话通道。受控对话先检查运行时事实问题：日期、时间、当前 Provider、当前模型名和 Agent 身份由后端直接回答，并在流转日志记录 `RuntimeFactsAnswered`；其他闲聊或咨询才调用已配置的大模型生成自然回复，并记录 `ConversationModelRequested`。这两类都不得进入工程 Skill / Intent，不得展示确认按钮，不得调用本地 Tool。聊天流只展示最终自然回复。若识别为工作流候选但动作不完整，聊天流展示追问，例如“你是想创建、修改、删除还是查询路基模板？”。

当用户表达为“道路模型”或“创建道路模型”时，入口路由必须把它识别为独立工程对象，不得进入 `subgrade_template` Skill。当前 MVP 尚未接入道路模型 Skill 时，后端返回终止性 `UnsupportedWorkflow`，聊天流提示“道路模型不是路基模板，当前 Agent MVP 还没有接入道路模型 Skill”，确认按钮保持禁用，当前 Run 不保持 `AwaitingUserInput`，后续用户输入应作为新任务重新路由。

当当前 Run 处于 `AwaitingUserInput` 时，WPF 可以继续调用后端补充输入接口，但后端不得无条件把所有输入当作旧任务补充。后端必须先判断补充内容本身是否是明确闲聊、咨询、运行时事实问题、未接入对象或新的完整工程指令；若是，则记录 `ContinuationRerouted`，按该输入重新入口路由，并让聊天流跳出旧追问。例如“请先明确要操作的路基模板目标”之后，用户输入“你好”应进入 `ChatOnly`，不再重复追问模板目标。

若旧任务已经通过输入名称、当前选择集或“点选”补齐了路基模板目标，后端续跑应保存 pending 目标上下文。之后用户只补“把右侧硬路肩改为 20 米宽”这类参数句时，WPF 仍调用同一个等待中的 Run，后端应把该参数应用到已保存目标，而不是重新追问模板目标或把部件名当成模板名称。

## 参数确认面板

参数面板展示字段：

- 字段名。
- 当前值。
- 单位。
- 来源。
- 置信度。
- 是否必填。
- 是否可编辑。
- 校验状态。

来源包含：

```text
user_input
llm_extracted
system_context
project_config
default_rule
rule_derived
manual_confirmed
tool_result
```

用户可在 WPF 中修改可编辑字段。修改后的字段来源标记为 `manual_confirmed`，规则引擎不得被模型输出覆盖。

## 规则和风险面板

规则面板展示：

- 命中规则 ID。
- 规则名称。
- 规则类型。
- 命中字段。
- 处理结果。
- 是否阻断。
- 解释文本。

风险面板展示：

- 风险等级。
- 是否需要审批。
- 影响范围。
- 是否支持回滚。
- 不可回滚说明。

## DryRun 预览面板

DryRun 面板展示：

- 工具调用计划。
- 将创建或修改的对象类型。
- 参数摘要。
- 预览数据。
- 受影响对象。
- 警告和阻断原因。

DryRun 不修改 DWG。DryRun 失败时，审批和执行按钮必须禁用。

## 审批和执行区

审批区至少提供：

- 确认执行。
- 取消任务。
- 返回修改参数。
- 重新 DryRun。

用户确认后，WPF 只把审批结果提交给后端和本地 `AGENT` 模块，不直接调用写入工具。正式执行仍由 Orchestrator 和本地 Adapter 控制。

确认和取消不应作为输入框旁边的固定按钮出现。写入类任务进入 `AwaitingUserConfirmation` 后，WPF 应在聊天流 / 对话区域内展示本次操作的“确认执行”和“取消”按钮，让审批动作贴近本次 Agent 回复。确认前不得调用本地 Tool；取消后任务进入 `Cancelled`，后续输入作为新任务处理。

## 流转日志区

流转日志区用于定位问题，是 MVP 必做能力。它应以时间线形式展示：

- 面板启动。
- 后端健康检查。
- 后端自动启动。
- 当前模型 Provider。
- 用户输入摘要。
- 后端生成执行计划。
- 等待用户确认。
- 用户确认、取消或修改参数。
- 下发 RoadProto 本地动作。
- Bridge / Adapter 调用结果。
- CAD 执行结果。
- 成功、失败、重试、回滚。

流转日志必须消费后端 `AgentRun.events`，而不是只在前端拼接粗粒度状态。每个关键阶段都要显示阶段输出，例如入口路由原因、意图识别结果、规则结果、Tool 计划、参数摘要、用户确认、Tool 调度参数、本地校验结果和最终结果。界面应按事件增量去重，避免确认或回传结果时重复打印同一批旧事件。

面板中的可见日志必须让人能直接判断发生了什么。Agent MVP 初期为了方便联调，允许同一条可见日志显示技术串，但不能只显示 `Status=...`、`Intent=...` 这类机器串。技术串里的每个字段必须采用 `Key=Value（中文解释）`，把解释紧贴在字段旁边；每个新的可见句子或详情行以 `---` 开头，并用缩进区分阶段首行和诊断详情行。例如：

| 原始阶段 | 可见文案示例 |
| --- | --- |
| `EntryRouted: WorkflowCandidate ...` | `入口路由：工作流候选。用户表达包含动作，但未明确工程对象。` |
| `RunUpdated: Task task_xxx Succeeded` | `任务状态已更新：任务已成功完成。TaskId=task_xxx` |
| `RuntimeFactsAnswered` | `已由运行时事实层回答：未调用大模型或 CAD 工具。` |
| `ConversationModelRequested` | `已调用大模型生成对话回复：未进入工程执行工作流。` |
| `IntentRecognized` | `--- 意图已识别：` + `--- 技术串 Status=FollowUpRequired（需要补充信息）; Intent=subgrade_template.create（创建路基模板意图）` + `--- 识别依据 matchedExpression=创建路基模板（命中的用户原文片段）` |
| `RulesApplied` | `--- 规则已应用：` + `--- 技术串 Status=FollowUpRequired（需要补充信息）; FollowUp=请问道路等级是什么？（追问用户的问题）; BlockReason=-（没有阻断原因，不是错误）` + `--- 下一步 FollowUp=...（请直接回复道路等级...）` |
| `PlanOutput` | `--- 计划输出：` + `--- 计划字段 Agent=...（RoadProto工程Agent）; Skill=...（路基模板能力域）; Intent=...（创建路基模板意图）; Tool=...（本地创建路基模板工具）` |
| `ToolArgumentsPrepared` | `--- 工具参数：` + `--- 工具参数 RoadGrade=Expressway（高速公路）; Components=8（规则层下发的默认部件数量）; DisplayScale=10（模板显示比例）` |

原始 `stage`、`message`、`TraceId`、`TaskId` 仍应写入 JSON Lines 文件日志，供机器检索和问题定位。WPF 提供“复制 TraceId”和“打开日志目录”按钮。可见日志使用单体只读文本面板时，每条事件首行缩进两格，事件内的技术串、识别依据、规则解释和下一步等详情缩进四格；WPF 格式器必须兜底给未带标记的本地日志行补 `---`，便于从密集日志中扫出阶段层级。

聊天区和流转日志区都要关闭横向滚动，按面板宽度自动换行，并允许选择局部文字复制，方便用户把一段 Trace 或错误信息发给开发者。

在 AutoCAD / CAD 类宿主软件内实现可复制日志时，应优先使用稳定的单体只读文本面板，或使用不可聚焦的列表项文本。不要在日志列表的每一行放置独立可聚焦输入控件，避免打开面板、追加日志或选择文字时触发宿主 Palette 焦点重入。

聊天区同样要显示可读名称：

| 机器码 | 聊天区文案 |
| --- | --- |
| `subgrade_template` | `路基模板` |
| `subgrade_template.create` | `创建路基模板` |
| `medium` | `中` |
| `SubgradeTemplate.Create` | `创建路基模板工具` |

## Trace 查看区

Trace 查看区展示：

- 状态链路。
- 模型调用摘要。
- Schema 校验结果。
- 规则命中结果。
- 工具计划。
- DryRun 结果。
- 审批记录。
- 执行结果。
- 错误和处理建议。

Trace 只展示可读摘要。敏感信息和 API Key 必须隐藏。

## 交互边界

- WPF 可以展示参数、规则、风险、DryRun、日志和 Trace。
- WPF 可以提交用户修改、模型配置和审批动作。
- WPF 不直接调用模型 API。
- WPF 不直接调用 ObjectARX。
- WPF 不直接写入 DWG。
- WPF 不承担业务规则和默认值推导。
- WPF / RoadProto 本地 Tool Adapter 不补 Agent 创建默认值；默认值必须由后端规则文件下发，本地只做执行前校验。

## MVP 验收

- 能从 Ribbon 打开 Agent Console，AutoCAD 2021 中不得导致宿主崩溃。
- Agent Console 使用较宽初始尺寸；当前 AutoCAD 2021 稳定入口为默认右侧停靠的 `PaletteSet`。
- Agent Console 内部白色内容区必须填满停靠 Palette，拖动 Palette 宽高时聊天区、输入区和流转日志区保持自适应比例。
- 面板能自动检查并拉起后端服务。
- 后端连接失败时能展示错误、重试和手动选择后端 exe。
- 能配置 DeepSeek、阿里千问、GLM、GPT。
- 能输入 API Key 并由后端加密保存。
- 能选择后端允许的模型和 API 通道。
- 重新打开面板时能恢复上次保存的 Provider、Base URL、模型名和启用状态。
- 能输入自然语言任务。
- 输入框按 Enter 发送，发送成功后清空并保持输入焦点。
- 能展示业务 Agent 路由结果。
- 能展示参数来源、规则命中和风险。
- 能展示 DryRun 结果。
- DryRun 失败时不能执行。
- 未审批时不能执行。
- 确认和取消按钮位于对话区域内，不放在发送按钮旁边。
- 用户取消后任务进入 `Cancelled`。
- 执行完成后能查看 Trace。
- 全流程日志可见，能显示每个流程阶段及其输出，能自动换行、选中复制，并能打开日志目录。
