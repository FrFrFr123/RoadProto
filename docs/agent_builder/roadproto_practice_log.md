# RoadProto Agent MVP 实践记录

本文档记录 RoadProto 当前 Agent MVP 实践中已经验证、修正或暴露的问题。它的目的不是替代 RoadProto 项目文档，而是把实践经验沉淀为下一个项目可复用的 Agent Builder 经验。

## 1. 当前实践形态

RoadProto 采用：

```text
RoadProto 主仓库
  WPF 可停靠 Agent Console
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
- 高速公路默认值按 RoadProto 原生预设下发 8 个部件：左右中分带、行车道、硬路肩和土路肩。
- 每个组件保存宽度、高度、固定坡度、坡度模式、RGB、变宽表、坡度变化表、内外侧路缘石和路面结构层引用。
- RoadProto 本地 Bridge 改为反序列化完整 `Components` 并映射到 `SubgradeComponentDto`，不再用标量和本地公式二次生成模板。

可复用结论：当默认值代表工程对象结构时，规则层必须输出完整结构，不应压扁成几个标量再交给 Adapter 推导。标量只适合表达用户覆盖项，例如“行车道加宽 1 米”。

### 2.14 可见日志必须映射成人话

问题：前端流转日志曾直接显示 `RunUpdated: Task task_xxx Succeeded` 等内部事件，用户难以判断发生了什么。

修正：

- WPF 可见日志增加 stage 映射，例如 `EntryRouted` 显示为“入口路由”，`RuntimeFactsAnswered` 显示为“已由运行时事实层回答”。
- WPF 聊天区也翻译 Skill / Intent / 风险 / Tool 机器码，不直接显示 `subgrade_template`、`subgrade_template.create`、`medium` 或 `SubgradeTemplate.Create`。
- 原始 stage / message 继续写入 JSON Lines 文件日志，方便机器检索。

可复用结论：Trace 要同时服务用户定位和开发排查。界面日志要可读，文件日志要可检索，两者不要混成一种格式。

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
- 等空宿主注册成功后，再创建完整 Agent Console，并调用 `hostGrid.Children.Add(agentConsole)` 挂载。
- 对 Ribbon 点击路径做真实宿主验证，而不是只验证命令行命令或普通 WPF 窗口。

可复用结论：宿主软件内的“可停靠 WPF 面板”不是普通 WPF 窗口。必须把宿主承载 API、WPF 初始化顺序、焦点策略和真实 Ribbon 点击作为独立兼容性测试项。若宿主提供原生 WPF 承载接口，优先使用原生接口；不要默认用 `ElementHost` 混合 WinForms 和 WPF。

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
