# RoadProto Agent MVP 实践记录

本文档记录 RoadProto 当前 Agent MVP 实践中已经验证、修正或暴露的问题。它的目的不是替代 RoadProto 项目文档，而是把实践经验沉淀为下一个项目可复用的 Agent Builder 经验。

## 1. 当前实践形态

RoadProto 采用：

```text
RoadProto 主仓库
  WPF Agent Console
  RoadProto 本地 AGENT 薄模块
  本地 Tool Adapter

独立后端仓库
  F:\0_GPT_RoadProtoAgentBackend
  .NET 8 / ASP.NET Core
  Orchestrator
  Model Gateway
  Settings / Credential
  Rule / Skill / Intent
  Trace / Logs
```

该形态证明了“宿主软件薄接入 + 外置 Agent 后端”适合工程软件 MVP。

## 2. 已融合的实践结论

### 2.1 外置后端更适合长期扩展

最初可以想象把 Agent 直接写进宿主软件，但实践中更推荐独立后端。原因：

- 模型 Provider 和 API Key 管理不污染宿主软件。
- 状态机、Trace、评测和规则更容易测试。
- 后续可服务多个宿主软件或多个插件。
- 宿主软件只保留受控 Adapter，更符合工程数据安全边界。

### 2.2 不需要单独 Web 前端

RoadProto 当前使用 AutoCAD 内 WPF 可停靠 Palette。MVP 证明：

- 聊天输入、模型配置、追问、确认、日志都能放在宿主软件内。
- 用户不用切换到浏览器。
- Adapter 和当前图纸上下文更容易连接。

可复用结论：下一个项目优先把交互做进宿主软件内。除非产品要求后台管理或跨端协同，否则不要为了 Agent MVP 单独做 Web 前端。

### 2.3 Provider 配置必须可替换

当前要求支持：

- DeepSeek
- Qwen
- GLM
- GPT

实践结论：

- Model Gateway 要做 OpenAI-compatible 抽象。
- Provider 配置包括 base URL、model、API Key、enabled。
- API Key 要加密保存。
- 未配置 API Key 时应返回结构化 Failed Run，不应让前端收到 500。
- 受控闲聊或咨询通道调用模型失败时，也应返回结构化 `Failed Run`，并记录 `RunFailed` 事件；不能只保护工程 Intent 识别路径，否则“你好”等 `ChatOnly` 输入仍可能因为 Provider/API 异常冒成 HTTP 500。

### 2.4 日志目录要独立管理

RoadProto 将日志放到：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto
```

保留策略：

```text
14 天或最多 1GB
```

可复用结论：日志和设置要分开。API Key 设置可以留在用户配置目录，运行日志应放到可控运行期目录，避免配置目录膨胀。

### 2.5 Skill / Intent 要早于 Tool

实践中曾从“路基模板创建 Agent”逐步收敛为：

```text
Agent: roadproto_engineering_agent
Skill: subgrade_template
Intent:
  subgrade_template.create
  subgrade_template.modify
  subgrade_template.delete
  subgrade_template.query
```

可复用结论：

- 不要把单个动作当成长期 Agent 边界。
- 先定义 Skill，再定义 Intent，再绑定 Tool。
- 增、删、改、查应拆成不同 Intent。

### 2.6 缺参追问必须结构化返回

问题：用户输入“创建路基模板”，系统知道缺少道路等级，但前端只显示“请补充必要信息。”

修正：

- Rule Engine 生成具体追问：“请问道路等级是什么？”
- `AgentRun` 增加 `followUpMessage`。
- WPF 优先展示 run 级 `followUpMessage`。

可复用结论：追问是产品交互字段，不只是日志事件或内部规则结果。

### 2.7 模型输出解析要兼容标量和对象

问题：模型可能输出：

```json
{
  "roadGrade": "Expressway"
}
```

也可能输出：

```json
{
  "roadGrade": {
    "value": "Expressway",
    "sourceText": "高速公路",
    "confidence": 0.9
  }
}
```

可复用结论：

- Parser 要兼容两种形式。
- Schema 校验仍然要执行。
- 参数元数据缺失时按低置信或默认元数据处理。

### 2.8 前端要显示 Skill / Intent / Tool

RoadProto WPF 面板会展示：

- 当前 Skill。
- 当前 Intent。
- 风险等级。
- Tool 名称。
- TraceId。
- 流转日志。

可复用结论：用户不是只看自然语言回答。可控 Agent 必须让用户知道系统准备做什么。

### 2.9 入口路由应在模型意图识别前拦截

RoadProto 已实现本地入口路由层：

- `ChatOnly`：闲聊不进入模型意图识别，不调用 Tool，但进入受控对话通道调用大模型自然回复。
- `HelpOnly`：咨询和解释不进入工程执行流程，不调用 Tool，但进入受控咨询通道调用大模型解释。
- `WorkflowCandidate`：提到工程对象但动作不完整时进入追问。
- `WorkflowCommand`：明确工程动作和业务对象时进入现有 Agent / Skill / Intent 识别。

可复用结论：

- 入口路由是正式状态机阶段，应记录 `EntryRouted`。
- 路由结果应进入 `AgentRun.entryRoute`，前端可展示 `routeType`、`confidence` 和 `reason`。
- 不要让每一句话都消耗模型意图识别，更不能让闲聊或咨询误触发 Tool。
- “不触发 Tool”不等于“不调用模型”。闲聊和咨询需要自然回答时，应走受控大模型对话，系统提示必须禁止声称已修改 CAD、创建对象或保存 DWG。
- 当前任务处于 `AwaitingUserInput` 时，用户短回答应作为工作流补充继续原任务。
- `AwaitingUserInput` 不能无条件吞掉后续所有输入。RoadProto 修正为：补充内容若明确是闲聊、咨询、运行时事实问题、未接入对象或新的完整工程指令，先记录 `ContinuationRerouted` 并重新执行入口路由；只有“高速公路”“宽度 3.75 米”等能回答当前追问的短补充，才继续原任务。

### 2.10 受控对话必须先过运行时事实层

问题：接入受控对话后，用户问“今天是几号”时模型回答了错误日期；问“你是什么大模型版本”时模型猜测了未提供的底层架构。

修正：

- 运行时日期、时间、当前 Provider、当前模型名和 Agent 身份由后端事实层提供。
- 命中日期、时间、模型身份等事实问题时，后端直接回答并记录 `RuntimeFactsAnswered`，不调用模型。
- 普通闲聊或咨询仍可调用模型，但 prompt 必须注入运行时事实，并要求未知信息不得猜测。

可复用结论：工程 Agent 的“自然对话”不能等同于“自由模型聊天”。事实类问题应优先由系统确定性回答，模型只负责表达和解释，不负责编造运行状态。

### 2.11 相邻工程对象必须显式分流

问题：用户补充“道路模型”时，系统可能沿用上一轮“创建”上下文，误进入路基模板 Skill。

修正：

- 入口路由增加 `road_model` 独立候选。
- `道路模型` 和 `路基模板` 同时出现时返回歧义追问。
- 对象未定的 `WorkflowCandidate` 在用户补充对象时重新执行入口路由。
- 明确未接入的 `road_model` 请求返回终止性 `UnsupportedWorkflow`，不再保持 `AwaitingUserInput`，避免后续输入被旧任务粘住。
- `subgrade_template.create` 的负向样例加入“道路模型”“创建道路模型”。

可复用结论：不能用最近已接入 Skill 吞掉尚未接入的业务对象。相邻对象要在入口路由、Intent 负向样例和 Skill 边界里同时声明。未接入对象应终止当前 Run 或明确进入单独候选，不应反复追问同一个旧任务。

### 2.12 默认值应由规则文件控制

问题：创建路基模板时，默认宽度、坡率和单位曾分散在后端代码和 RoadProto 本地 DTO 中，后续调整默认值会变成多处修改。

修正：

- `subgrade_template.create.yaml` 增加 `defaultSource: agent-rule` 和 `defaultValue`。
- 后端 `IntentRuleService` 只从 Intent 规则读取默认值。
- RoadProto 本地 Tool Adapter 改为只校验必需参数，缺少默认值时返回失败。

可复用结论：默认值属于规则层，不属于 LLM，也不属于宿主软件 Adapter。宿主软件本地只能执行和校验。

### 2.13 复杂工程默认值必须结构化

问题：Agent 创建“高速公路路基模板”时，早期规则只传 `laneWidth`、`hardShoulderWidth`、`earthShoulderWidth`、`medianWidth`、`slopeRatio` 等标量。RoadProto 本地桥接层再把这些标量拼成模板，导致行车道宽度、右侧颜色、土路肩坡度、中分带路缘石等参数与原生“路基模板-高速公路”预设不一致；其中 `slopeRatio=1.5` 还被误算成土路肩坡度 `0.6667`。

修正：

- `subgrade_template.create.yaml` 增加 `defaultComponentsByRoadGrade`，按道路等级保存完整组件默认值。
- `defaultComponentsByRoadGrade` 必须覆盖所有当前可识别的道路等级，包括 `Expressway`、`FirstClass`、`SecondClass`、`ThirdClass`、`FourthClass`、`UrbanExpressway`、`UrbanArterial`、`UrbanSubArterial` 和 `UrbanBranch`。
- 每个道路等级默认值都按 RoadProto 原生 `SubgradeTemplateDefaults::create` 下发完整部件，例如高速公路下发 8 个部件，一级公路下发 10 个部件，城市主干路下发 12 个部件。
- 每个组件保存宽度、高度、固定坡度、坡度模式、RGB、变宽表、坡度变化表、内外侧路缘石和路面结构层引用。
- RoadProto 本地 Bridge 改为反序列化完整 `Components` 并映射到 `SubgradeComponentDto`，不再用标量和本地公式二次生成模板。
- 后端测试必须校验每个支持等级都有非空默认部件；`Components=0` 代表规则覆盖缺失，不能让宿主 Adapter 静默按本地默认值补齐。

可复用结论：当默认值代表工程对象结构时，规则层必须输出完整结构，不应压扁成几个标量再交给 Adapter 推导。标量只适合表达用户覆盖项，例如“行车道加宽 1 米”。

### 2.14 可见日志必须映射成人话

问题：前端流转日志曾直接显示 `RunUpdated: Task task_xxx Succeeded` 等内部事件，用户难以判断发生了什么。

修正：

- WPF 可见日志增加 stage 映射，例如 `EntryRouted` 显示为“入口路由”，`RuntimeFactsAnswered` 显示为“已由运行时事实层回答”。
- WPF 聊天区也翻译 Skill / Intent / 风险 / Tool 机器码，不直接显示 `subgrade_template`、`subgrade_template.create`、`medium` 或 `SubgradeTemplate.Create`。
- MVP 联调阶段，可见流转日志允许技术串，但技术值和文字解释必须内联放在一起。比如 `Status=FollowUpRequired（需要补充信息）; FollowUp=请问道路等级是什么？（追问用户的问题）; BlockReason=-（没有阻断原因，不是错误）`，不要把技术串和解释分成两段让用户自己对应。
- 原始 stage / message 继续写入 JSON Lines 文件日志，方便机器检索；界面日志也应通过缩进区分事件首行和技术串、识别依据、规则解释、下一步等详情行。每个新的可见句子或详情行建议以 `---` 开头，便于在密集调试日志中扫描。

可复用结论：Trace 要同时服务用户定位和开发排查。界面日志要可读，也可以带技术串；关键是每个 `Key=Value` 旁边必须有中文解释，文件日志要继续保持可检索，两者不要混成一种格式。

### 2.15 可见日志要由后端事件流驱动

问题：WPF 早期只在前端按几个状态拼接日志，用户看不到每一层的实际输出，例如意图识别结果、规则结果、工具参数摘要和 Tool 调度参数。这样一旦识别错或参数错，只能去翻文件日志，不利于联调。

修正：

- 后端 `AgentRun.events` 返回完整阶段事件。
- Orchestrator 在意图识别、规则应用、计划生成、工具参数准备、用户确认、Tool 调度、本地结果回传等阶段写事件。
- WPF DTO 接收 `events`，按 `taskId + timestamp + stage + message` 去重后增量显示。
- WPF 可见日志使用中文标题，同时保留后端输出摘要，例如 `RoadGrade=Expressway; Components=8`。

可复用结论：Agent Console 的流转日志不应是前端临时拼接的状态栏，而应是后端状态机事件的用户可读投影。事件必须包含阶段输出，不能只有阶段名称。

### 2.16 宿主内控制台要处理输入焦点、复制和审批位置

问题：AutoCAD Palette 内输入中文时，焦点可能被 CAD 命令行抢走；发送后输入框未清空；确认按钮固定在发送按钮旁边，容易让普通发送和执行审批混淆；聊天和日志不能自动换行和局部复制。

修正：

- Palette 不依赖 `KeepFocus` 强控宿主焦点，输入框只在用户点击、发送完成或取消完成后异步恢复焦点。
- 输入框支持 Enter 发送，发送成功后清空。
- 聊天流和日志流使用可选择的只读文本控件，关闭横向滚动，按面板宽度自动换行。
- 在 AutoCAD Palette 内不要用 `ListBox.ItemTemplate` 给每条聊天或日志消息嵌套可聚焦 `TextBox`；这会让复制能力和焦点恢复混在一起，增加宿主焦点重入和启动崩溃风险。
- 写入类任务进入确认状态时，在对话区域显示“确认执行 / 取消”，不再把确认按钮放在发送按钮旁。
- 面板打开时从后端读取模型配置，恢复上次保存的 Provider、Base URL、模型名和启用状态。

可复用结论：宿主软件内 Agent Console 不是普通网页聊天框。要把宿主焦点、命令行抢输入、日志复制、窗口停靠宽度变化和审批按钮语义都作为 MVP 可用性要求处理。

后续修正：在 AutoCAD Palette 中，打开面板时同步强制 `Focus` / `Keyboard.Focus`，或依赖 `KeepFocus` 强控宿主焦点，可能触发宿主焦点重入甚至崩溃。更稳的做法是：

- 面板打开时不主动抢输入框焦点。
- 用户点击输入框、发送完成或取消完成后，用 Dispatcher 异步恢复焦点。
- 不在 `GotKeyboardFocus` 中再次调用 `Keyboard.Focus`，避免递归。
- Palette 使用公开的 `Dock` / `DockEnabled` 设置默认右侧停靠，避免反射或宿主内部属性。
- 初始宽度要比普通工具条更宽，给聊天区、配置区和 Trace 区留出阅读空间。
- 对话和日志需要局部复制时，先采用单体只读多行文本面板或不可聚焦文本项；等宿主环境验证稳定后，再考虑更复杂的富文本消息列表。

### 2.17 宿主 Palette 的 WPF 承载方式要单独验证

问题：RoadProto Agent Console 曾在 AutoCAD 2021 中点击 Ribbon 打开即崩溃或卡死。逐层探针定位到 Palette 承载阶段：纯 WinForms 控件可以通过 `PaletteSet.Add`，最小 WPF `TextBlock` 可以通过 `PaletteSet.AddVisual`，但 `ElementHost` 被加入 Palette 时会卡住；把完整复杂的 WPF 控件直接交给 `AddVisual` 也存在启动风险。

修正：

- 不使用 `ElementHost` 作为 AutoCAD Palette 的直接或间接承载路径。
- 不把完整复杂 Agent Console 直接传给 `PaletteSet.AddVisual`。
- 先创建空 WPF 宿主容器，例如 `Grid`，调用 `PaletteSet.AddVisual("Agent", hostGrid, false)`。
- 等空宿主注册成功并完成 `Visible / Dock / Activate` 后，再通过 UI Dispatcher 的空闲优先级创建完整 Agent Console，并调用 `hostGrid.Children.Add(agentConsole)` 挂载。
- Ribbon 点击事件不要直接实例化面板或调用托管 Palette 创建方法。按钮应排队发送宿主命令，由宿主命令处理插件加载、命令转发和面板创建，避免 UI 事件、加载逻辑、Palette 激活和 WPF 初始化在同一回调栈内重入。
- 启动探针要覆盖宿主命令、Palette 创建、空宿主注册、Palette 激活、完整 WPF 控件创建、`InitializeComponent`、`Loaded` 和 ViewModel 初始化，便于定位崩溃前最后一个成功阶段。
- 对 Ribbon 点击路径做真实宿主验证，而不是只验证命令行命令或普通 WPF 窗口。

可复用结论：宿主软件内的“可停靠 WPF 面板”不是普通 WPF 窗口。必须把 Ribbon / 菜单事件、宿主命令队列、宿主承载 API、WPF 初始化顺序、焦点策略和真实点击验证作为独立兼容性测试项。若宿主提供原生 WPF 承载接口，优先使用原生接口；不要默认用 `ElementHost` 混合 WinForms 和 WPF。复杂面板应优先采用“宿主容器先稳定，复杂 UI 后挂载”的两阶段启动策略。

### 2.18 复杂 XAML 不稳定时应降级为安全面板，但保留宿主停靠能力

问题：RoadProto 继续真实点击验证后发现，两阶段 Palette 仍会在完整 `AgentConsolePalette` 加入宿主后崩溃；改用 AutoCAD `Application.ShowModelessWindow` 后，如果窗口在显示前直接放入完整 XAML 控件，也会在 `ShowModelessWindow` 阶段崩溃；即使先显示最小 WPF shell，再延迟把完整 XAML 控件赋给 `Window.Content`，仍会在控件布局 / Loaded 前后导致宿主退出。随后用户反馈 modeless 窗口不能吸附、默认也没有停靠在侧边栏，说明降级不能把宿主原生停靠能力一并放弃。

修正：

- Ribbon 仍只排队发送宿主命令，不直接创建 UI。
- 托管命令使用 AutoCAD 原生 `PaletteSet` 作为外层宿主，设置 `DockEnabled = Left | Right`、`Dock = Right`、`Snappable` 和 `SingleColDock`，保持默认右侧停靠和可吸附能力。
- `PaletteSet` 只通过 `AddVisual("Agent", hostGrid, false)` 注册最小 WPF 宿主，先完成 `Visible / Dock / Activate`。
- `Visible / Dock / Activate` 后，再用 Dispatcher 空闲优先级在宿主 `Grid` 内挂载代码构建的 `AgentConsoleSafePanel`；挂载前必须清除启动宿主残留的 `Auto` 行列、外边距和固定尺寸约束。
- `AgentConsoleSafePanel` 不走 XAML / BAML，不调用 `InitializeComponent`，手动构建基础 WPF 控件并手动同步 ViewModel。
- `AgentConsoleSafePanel` 自身和根布局必须 `Stretch`，聊天区和日志区使用比例 `Star` 行，而不是固定日志高度，保证用户拖动停靠面板时内容同步调整。
- `PaletteSet.AddVisual` 中的 WPF 子树不能只依赖 `Stretch`。AutoCAD 2021 可能仍按 WPF 内容自然尺寸显示小块白色面板；需要监听 `PaletteSet.SizeChanged`，以 `PaletteSet.PaletteSize` 回退到 `PaletteSet.Size` 显式更新宿主 `Grid` 和安全面板的宽高。
- 保留启动探针，覆盖 `PaletteSet` 创建、`AddVisual`、`Visible / Dock / Activate`、安全面板构建、Loaded 和 ViewModel 初始化。
- 用真实 AutoCAD 2021 Ribbon 鼠标点击验收；只跑命令行或单元测试不足以证明宿主稳定，也不足以证明默认停靠状态正确。

可复用结论：桌面宿主里的 Agent Console 要准备“降级 UI 策略”，但优先降级内部 UI 复杂度，而不是放弃宿主原生停靠容器。如果完整 XAML、富文本消息列表或复杂模板触发宿主崩溃，应保留已验证的可停靠外壳，改用代码构建的基础控件、单体只读文本区和手动 ViewModel 同步。稳定性先于视觉复杂度；停靠、吸附、关闭、重开、拖动 resize 和输入路径都必须在真实宿主中验证。对 CAD / BIM 这类原生停靠宿主，WPF `Stretch` 和宿主 resize 尺寸同步是两层契约，缺一层都可能出现内容只占左上角的问题。

### 2.19 目标引用要区分最近上下文和执行时点选

问题：用户说“修改刚才创建的模板”“删除这个模板”时，早期规则层只能检查是否已有 handle 或名称。没有 handle/name 就返回缺目标追问，导致两个问题：一是刚刚由 Agent 创建或修改过的对象不能被自然引用；二是“这个模板 / this template”这种需要在宿主软件里点选的表达被误判为缺参。

修正：

- 后端按 `SessionId` 保存最近创建、最近修改和最近触碰的路基模板引用。
- 本地 Tool 执行成功回调在创建或修改成功时更新会话上下文。
- 规则层优先把“刚才创建的 / 上一次创建的”解析到最近创建对象，把“刚才修改的 / 上一个模板”解析到最近修改或最近触碰对象。
- `SubgradeTemplateToolArguments` 增加 `TargetMode`、`TargetRef` 和 `ComponentOperations`。
- 用户说“这个模板 / this template”时，规则层生成计划并下发 `TargetMode=PickOnExecute`、`TargetRef=原始表达`，由 RoadProto 本地 Adapter 在 AutoCAD 执行阶段点选和校验对象类型。

可复用结论：目标解析不能只有“已解析 / 缺失”两种状态。工程 Agent 应把目标定位模式结构化为 `ByHandle`、`ByName`、`PickOnExecute`、`All` 等可执行契约；会话最近对象上下文属于规则层输入，宿主点选属于 Adapter 执行能力，二者都不应由 LLM 臆造。

### 2.20 归一化不能只覆盖标准词

问题：用户说“二级道路”可以创建路基模板，但说“二级路”时，本地 Tool 收到原始道路等级文本并返回“不支持的道路等级编码”。这说明规则层只覆盖了标准表达，没有覆盖用户真实口语表达；更危险的是，无法归一化的自然语言被继续下发给 Adapter。

修正：

- 后端规则层把道路等级、侧别、部件类型、操作类型和插入位置都作为可归一化字段处理。
- 同一语义的不同说法，例如“二级”“2”“二级路”“二级道路”“先生成一个二级的吧”，统一落到稳定编码 `SecondClass`。
- 归一化失败时返回追问或阻断，不把原始自然语言继续传给 RoadProto 本地 Adapter。
- 模型漏抽取某个受控槽位时，规则层仍要从原句和参数 sourceText 兜底归一化。比如模型只给出 `width=10`，但原句是“把行车道改为 10”，规则层必须补出 `ComponentType=TravelLane`，否则本地 Adapter 无法知道要改哪个部件。
- 后端增加回归测试覆盖口语化等级、未知位置、误把模板名称当部件类型等 false positive。

可复用结论：LLM 输出和用户表达都不能被当作稳定参数。工程 Agent 的 Rule Engine 必须有一层确定性归一化，且归一化失败要停下来解释，不要让 Adapter 承担自然语言猜测。

### 2.21 修改和删除要先定位对象，再追问部件细节

问题：用户要实现修改、删除时，第一步是选中目标对象，第二步才是修改参数或删除部件。早期规则容易先检查侧别、部件等参数缺失，导致“修改这个模板”没有进入点选目标流程，用户体验上像系统没听懂“这个”。

修正：

- `SubgradeTemplateToolArguments` 使用 `TargetMode`、`TargetRef`、`TargetHandle` 和 `TargetName` 表达目标定位。
- “刚才创建的 / 刚才修改的 / 上一个模板”优先从会话最近对象上下文解析。
- “这个模板 / this template”生成 `TargetMode=PickOnExecute`，由 AutoCAD Adapter 在执行时点选并校验 `DnSubgradeTemplateEntity`。
- 目标模式明确后，再追问“左侧、右侧还是两侧”“修改哪个部件”“宽度还是坡度”等二级参数。

可复用结论：修改和删除类 Agent 流程应按“目标定位 -> 变更内容 -> 风险确认 -> 执行”组织。目标定位是独立契约，不应被参数缺失检查打断。

### 2.22 部件级操作要进领域规则，不进 Adapter

问题：路基模板修改不只是几个标量参数，用户会说“加宽行车道”“删除右侧土路肩”“在两侧行车道外侧新增 3 米人行道”“改路缘石参数”“写入变宽表”。如果把这些逻辑写在 WPF 或 ObjectARX Adapter 中，会很快破坏业务边界。

修正：

- 后端把自然语言归一化为 `componentOperations`，每条操作包含 `operation`、`sideScope`、`componentType`、`occurrence`、`positionMode`、`anchorType` 和 `patch`。
- WPF Bridge 只把结构化操作序列化到请求文件。
- ObjectARX Adapter 只定位实体并把请求转成领域对象。
- 真正的部件匹配、插入、删除和参数覆盖落到 `SubgradeTemplateRules::applyComponentOperation`。
- 变宽表和坡度变化表作为 patch 的一部分端到端传递，避免 UI / Adapter 漏字段。

可复用结论：工程对象的局部编辑应设计为领域层可测试的操作列表。Adapter 不是业务规则层；它只应把已授权、已归一化、已确认的操作带到宿主软件事务里执行。

### 2.23 空修改计划必须在规则层拦截

问题：用户只说“修改路基模板”时，模型能够识别到 `subgrade_template.modify`，但没有提取任何实际修改项。早期规则层仍生成 `SubgradeTemplate.Modify` 计划并进入用户确认，用户点击确认后本地工具被调用，但因为 `ComponentOperations=0` 且没有模板级参数变更，最终没有任何有效修改。

修正：

- 修改类 Intent 必须同时满足目标定位和修改项提取两个条件。
- 目标可通过名称、handle、最近上下文或 `PickOnExecute` 表达；但目标明确不代表可以执行。
- 如果目标和修改项都缺失，追问必须同时说明“要操作哪个模板”和“要修改哪个参数”。
- 如果目标已明确但修改项缺失，追问“请问要修改哪个参数？例如行车道宽度、硬路肩宽度、坡度、颜色，或要新增/删除哪个部件。”
- `componentOperations` 为空且没有 `templateName`、`roadGrade`、`displayScale` 等模板级修改字段时，不允许进入 `AwaitingUserConfirmation`。

可复用结论：Intent 识别成功不等于执行计划完整。写入类 Agent 必须把“动作识别”“对象定位”“变更内容”拆成独立闸门，任何一个闸门缺失都应追问，而不是把空计划交给审批页。

### 2.24 工具请求文件要做编码和空白归一化

问题：WPF Bridge 写出的本地 Tool 请求文件使用了带 BOM 的 UTF-8。ObjectARX Adapter 逐行解析 `key=value` 时，首行 key 实际变成了 `\uFEFFoperation`，导致 `operation=modify` 读取为空，本地工具误报“未知路基模板工具操作”。这类问题在日志里看起来像 Tool 不支持操作，真实根因却是文件边界协议没有规范编码。

修正：

- WPF Bridge 写请求文件时使用 UTF-8 无 BOM。
- WPF Bridge 写请求文件时固定使用 LF 换行，不依赖 `WriteAllLines` 的平台默认换行，避免旧 Adapter 把 `operation=modify\r` 当成未知操作。
- ObjectARX Adapter 读取请求文件时，对首个 key 容错剥离 UTF-8 BOM。
- key 和 value 两端统一 trim 空格、制表符、`\r` 和 `\n`。
- 核心测试和托管 Bridge 测试加入源码契约检查，防止后续重新引入带 BOM 写入或首行 key 不归一化。

可复用结论：宿主软件 Adapter 常用临时文件或管道传递 Tool 请求时，编码、换行和空白也是协议的一部分。协议字段必须用稳定 key/value 归一化读取，不能让 BOM、CRLF 或本地编码差异影响 Tool 调度字段。

### 2.25 单位字段要进入 Schema，但不能算修改项

问题：用户说“把右侧行车道为 20 米”时，模型可能把“米”抽取成独立 `unit` 参数。早期 `subgrade_template.modify` 规则只声明了 `width`、`laneWidth` 等数值槽位，没有声明 `unit`，导致模型输出在 Schema 解析阶段被拒绝，前端只能看到 `Parameter 'unit' is not declared by intent 'subgrade_template.modify'.`

修正：

- 修改、创建等涉及长度的 Intent 都应显式声明 `unit` 或采用统一的参数元数据策略。
- `unit` 允许进入 Schema，用于解释宽度、高度、路缘石和结构层厚度等长度值。
- `unit` 不得被当作独立修改项。只有单位而没有宽度、坡度、颜色、部件增删等实际变更时，规则层仍应追问“要修改哪个参数”。
- 回归测试应覆盖模型把单位作为参数对象元数据输出，以及把单位作为独立 `unit` 参数输出两种情况。

可复用结论：工程 Agent 的 Schema 白名单不能只列“业务主字段”，也要列模型常见的辅助槽位，例如单位、方向、目标引用来源等。但辅助槽位只服务解析和解释，不能绕过写入类动作的完整性校验。

### 2.26 连续编辑优先使用会话最近对象

问题：用户创建路基模板后，下一句常说“修改这个路基模板右行车道为 20 米”“把两侧行车道宽度改为 10”。早期入口路由和目标解析把“这个模板”一律解释为执行时点选，或在省略“路基模板”时追问工程对象，导致用户刚创建完还要再次选择同一个对象。

修正：

- 前端必须延续后端返回的 `sessionId`，不能每次输入都以新会话启动，否则后端无法使用最近对象记忆。
- 写入类 Tool 成功后，后端要把返回的 handle / 名称写入会话上下文，例如 `LastCreatedSubgradeTemplate`、`LastModifiedSubgradeTemplate` 和 `LastTouchedSubgradeTemplate`。
- 修改类意图在没有显式 handle / 名称时，若同一会话已有最近触碰模板，应优先生成 `TargetMode=ByHandle`。
- “这个模板 / 这个路基模板”在有最近模板时指向最近对象；没有最近模板时才进入 `PickOnExecute`。
- 省略工程对象但出现“行车道、硬路肩、土路肩、中分带、宽度、坡度”等路基模板部件词，且同一会话已有最近路基模板时，入口路由可视为连续路基模板修改。
- “当前选中模板”这类明确选择集表达仍保留执行时点选语义，避免会话记忆抢占用户真正想选的 CAD 对象。

可复用结论：工程 Agent 的目标定位优先级建议为：显式 handle / 名称 > 会话最近对象 > 当前选择 / 点选 > 追问。会话记忆必须随 UI 会话 ID 贯穿多轮输入，否则规则层即使设计了最近对象也不会生效。

### 2.27 部件名不能覆盖最近工程对象目标

问题：用户创建路基模板后继续说“修改行车道为 20 米”，模型可能把“行车道”抽取到 `targetName`。如果规则层无条件信任 `targetName`，就会生成 `TargetMode=ByName; TargetName=行车道`，本地 Adapter 会去找名叫“行车道”的路基模板，最终报“未找到指定名称的路基模板”。真实语义其实是修改最近创建模板里的行车道部件。

修正：

- 修改类规则解析 `targetName` / `target.name` 时，先判断它是否命中受控部件别名。
- 若 `targetName` 是“行车道、硬路肩、土路肩、中分带、人行道”等部件名，不把它作为工程对象名称，而是作为部件线索参与 `componentOperations` 归一化。
- 目标定位继续按显式 handle、真实模板名称、会话最近对象、当前选择 / 点选的优先级执行。
- 回归测试必须覆盖模型把部件名误填为目标名的情况，确认最终仍使用最近对象 `TargetMode=ByHandle`。

可复用结论：同一个名词在工程软件里可能既像“对象名”，也像“对象内部部件名”。Rule Engine 不能只看字段名，还要做跨字段语义校验。写入类任务中，部件别名、属性名、单位词和模板名应分层归一化，避免模型某个槽位填错后覆盖更可靠的上下文目标。

### 2.28 泛称目标、最近对象和当前选择要分层处理

问题：用户创建一个路基模板后，既可能紧接着说“修改这个模板右行车道为 20 米”，也可能隔了几句话后泛称“修改路基模板”。如果规则层把所有“路基模板”都绑定最近创建对象，场景中有多个模板时容易误改；如果把“选中的模板”也总是转成执行时点选，又浪费用户已经在 CAD 中选中对象的上下文。

修正：

- 目标优先级拆成显式 handle / 名称、显式最近对象引用、当前 CAD 选择集、泛称工程对象和省略对象连续编辑几类。
- “刚才创建的 / 上一次创建的 / 这个模板”这类强指代在同一会话有最近对象时可以使用最近对象。
- “把两侧行车道宽度改为 10”这类省略工程对象但明显是路基模板部件编辑的连续表达，在同一会话有最近对象时可以使用最近对象。
- “修改路基模板，把行车道宽度改为 20 米”这类泛称工程对象不长期绑定最近对象；有实际修改项时进入 `PickOnExecute`，让宿主软件点选目标。
- “修改路基模板”这类既泛称目标又缺修改项的输入，不应生成点选执行计划，应同时追问目标和修改项。
- “修改选中的模板 / 当前选中路基模板”这类强选择集表达由前端读取宿主软件 implied selection。只有唯一选中对象类型正确时才传 handle；普通泛称不能偷偷带上选择集。
- 创建成功后的图形反馈属于宿主 Adapter 职责。写入实体并刷新显示后，应把视口聚焦到新建实体，帮助用户确认本次 CAD 写入结果。

可复用结论：工程 Agent 的“上下文”不是一个扁平最近对象。会话记忆、当前选择、泛称对象和省略对象应有不同触发词和不同风险策略；越接近真实写 CAD，越要让用户意图中的指向性足够明确。宿主软件的选择集和视口反馈应由前端 / Adapter 明确采集或执行，不能让 LLM 推断。

### 2.29 追问续跑要保存已确认的目标对象

问题：用户可能分两步完成一次修改，例如先说“修改选中的路基模板”，系统追问要改什么参数；用户再说“把右侧硬路肩改为 20 米宽”。如果续跑只拼接文本、不保存第一步已经明确的模板 handle，第二句话会被当成缺少工程对象的新任务，甚至把“硬路肩”误解析成模板名称。

修正：

- `AwaitingUserInput` 不只保存 follow-up 文案，还要保存已经解析出来的 pending 目标对象，例如 `TargetHandle` / `TargetName`。
- 后续补充输入进入同一个 Run 时，先合并 pending 目标，再做 Schema / Rule 校验。
- 如果补充内容是明确的新任务、闲聊或运行时事实问题，仍应按入口路由跳出旧追问，避免旧任务无条件吞掉所有输入。
- 前端在缺目标追问中应提供宿主软件选择入口，例如 CAD 场景中的“点选”按钮；点选成功后把 handle 作为补充上下文提交，而不是让用户必须记住或输入实体名称。

可复用结论：多轮工程写入流程里，“目标对象”和“修改参数”经常分轮确认。Rule Engine 的续跑状态应像表单草稿一样保存已确认字段；UI 也要把宿主软件的选择能力变成追问答案入口。否则用户分开说话时，Agent 会看似听懂，实际在执行层丢失目标。

### 2.30 基于蓝本的创建不能误派到修改

问题：用户说“创建路基模板，基于原本的路基模板，最右侧增加一个 10 米的行车道”时，句子里同时有“创建”和“增加”。早期如果只按局部动词判断，容易把“增加行车道”派到修改类 Intent；如果仍按普通空白创建处理，又会先追问道路等级，无法体现“基于原本参数再改一点”的语义。

修正：

- 入口路由和 Intent 识别以主动作“创建”和工程对象“路基模板”为准，局部“增加 / 修改 / 删除”只作为新对象参数补丁。
- 规则层将这类表达归为 `CreateFromBase + ParameterPatch`：先读取或生成蓝本完整参数，再把用户的局部参数变化应用到新对象参数上，最后调用 `SubgradeTemplate.Create`。
- `subgrade_template.create` Schema 显式声明 `componentOperation`、`componentType`、`sideScope`、`positionMode`、`width`、路缘石、颜色、变宽表和坡度表等补丁槽位，避免模型输出 patch 时被 Schema 拒绝。
- 当前 MVP 对“原本 / 默认路基模板”先使用规则默认蓝本，默认按 `Expressway` 完整组件生成；后续“刚才那个 / 现有这个 / 名称为 X 的模板”应通过会话上下文、名称、handle 或点选读取真实蓝本。
- RoadProto 后端在创建阶段也复用 `componentOperations`，但结果仍下发完整 `Components` 给本地创建 Tool；本地 Adapter 不解释自然语言“最右侧增加 10 米行车道”。

可复用结论：创建类任务里出现局部操作词，不代表要修改已有对象。凡是可创建、带完整初始参数的工程实体，都应支持“蓝本完整参数 + 开放参数 patch -> 新实体”的创建语义。参数 patch 的范围就是该实体 Schema 开放出来的可写参数，不能靠模型自由发挥，也不能让 Adapter 猜。

### 2.31 后端运行资源不能依赖宿主工作目录

问题：RoadProto 由 AutoCAD WPF 面板自动启动独立 Agent 后端时，进程路径指向发布目录，但工作目录可能继承 AutoCAD 或其他宿主目录。后端健康检查只依赖端口和基础路径，因此能返回 `healthy`；真正处理 `/api/agent/runs` 时才实例化 Skill / Intent 仓库，如果按 ASP.NET `ContentRoot` 或当前工作目录找 `rules/`，就会找不到 `subgrade_template` 规则并返回 HTTP 500。

修正：

- 后端规则目录解析优先使用显式配置，其次使用 `AppContext.BaseDirectory/rules`，再回退 `ContentRoot/rules` 和当前工作目录。
- 前端 / 宿主启动独立后端时显式设置 `WorkingDirectory` 为后端 exe 所在目录。
- 缺少必需 Skill 时返回带 rules root 和 skills directory 的明确异常信息，避免只看到 `Sequence contains no matching element`。
- 回归测试覆盖 ContentRoot 与输出目录不一致时仍能从 bin / publish 目录找到规则。

可复用结论：插件式宿主软件启动外部 Agent 服务时，健康检查不能只证明进程活着，还要证明运行资源可读。规则、模板、Prompt、Schema 这类运行资源应以 exe / bin / publish 所在目录或显式配置为锚点；宿主当前目录只能作为兜底，不能作为默认事实。

## 3. 当前仍需继续沉淀的问题

- 入口路由层已完成 MVP 规则版实现，受控对话已加入运行时事实层；后续需要通过真实用户表达继续扩充样例和评测集。
- DryRun 预览仍比较轻，需要在具体业务上增强结构化预览。
- 评测样例目录和失败样例自动沉淀还需要完善。
- Tool Registry 的机器可读能力声明还可以继续规范。
- 回滚能力当前主要是设计边界，后续需要在具体 Adapter 中验证。

## 4. 对下一个项目的建议

新项目不要从“接一个模型 API”开始。推荐先定：

1. 第一个验证业务对象。
2. 第一个 Skill。
3. 2 到 4 个 Intent。
4. Tool 白名单。
5. DryRun 和审批方式。
6. Trace 字段。
7. 宿主软件 Adapter 边界。

然后再接模型。
