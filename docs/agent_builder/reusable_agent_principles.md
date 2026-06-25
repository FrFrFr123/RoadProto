# 可控工程 Agent 可复用原则

## 1. 定位

可控工程 Agent 是把大模型理解能力接入工程软件的受控执行框架。它不让模型直接操作工程数据，也不把自然语言直接翻译成脚本执行，而是把用户表达变成可审查、可校验、可审批、可追踪的任务流。

## 2. 核心原则

### 2.1 架构完整优先

MVP 可以只验证一个业务对象，但链路必须完整。不能为了快，把模型调用、参数补全、工具调用和软件写入堆在一个函数或一个提示词里。

最小链路应包含：

```text
入口路由
Agent / Skill / Intent
Context
LLM
Schema
Rule
Tool
Adapter
Execution Control
Trace
```

### 2.2 后端主控，宿主软件薄接入

推荐形态是外置后端服务主控 Agent，宿主软件只做：

- 面板和用户交互。
- 后端健康检查和自动启动。
- 本地上下文摘要。
- 受控 Adapter。
- DryRun 和执行桥接。
- 本地日志镜像。

这样做的好处是后续可以更换宿主软件，也可以在不同项目复用同一套 Agent 后端能力。

### 2.3 LLM 不能直接拥有执行权

LLM 只能做四件事：

- 识别用户表达。
- 抽取用户明确给出的参数。
- 发现缺失参数并提出追问建议。
- 对计划和结果做解释。

LLM 不应该：

- 自行补默认值。
- 自行判断强业务规则。
- 自行选择未注册工具。
- 自行执行写入动作。
- 生成工程软件脚本并直接运行。

### 2.4 Skill 是业务边界，Intent 是动作边界

一个可维护 Agent 不应只按“用户说什么”随意绑定工具。推荐结构是：

```text
Agent
  Skill
    Intent
      Schema
      Rule
      Tool Binding
      Trace
```

例如：

```text
roadproto_engineering_agent
  subgrade_template
    subgrade_template.create
    subgrade_template.modify
    subgrade_template.delete
    subgrade_template.query
```

Skill 负责定义业务对象和能力边界，Intent 负责定义具体动作，Tool 负责承接受控执行。

### 2.5 默认值和规则必须从 Rule Engine 出来

用户没说的参数不能由 LLM 猜。默认值优先级应固定：

```text
用户明确输入
> 当前任务上下文
> 当前对象上下文
> 业务标准默认值
> 系统保守默认值
```

每个默认值都应记录来源。确认页应能区分“用户指定”和“系统补全”。

默认值最好以机器可读规则文件承载，例如 Intent 参数上的 `defaultSource` / `defaultValue`。宿主软件 Adapter 不应因为方便而补一套本地默认值；它只校验后端下发值是否合法。这样调默认参数时只改规则文件，不需要改宿主软件代码。

相邻工程对象必须显式排除。不要用“模板”这种泛化词把 `路基模板`、`边坡模板`、`路面结构模板` 混成一个 Skill；也不要把 `道路模型` 误路由成 `路基模板`。没有接入的对象应返回不支持或追问，而不是落到最近的已接入 Skill。

### 2.6 写入动作必须审批

查询和解释可以直接返回。创建、修改、删除、保存、回滚等动作必须进入执行控制层：

```text
Plan
-> DryRun
-> Preview
-> Approval
-> Execute
-> Result Validation
-> Trace
```

用户确认前不得修改工程数据。

### 2.7 Trace 是产品能力，不是调试附属品

Agent 每次运行必须记录：

- 输入原文。
- 入口路由结果。
- Agent、Skill、Intent。
- 参数抽取结果和字段来源。
- Schema 校验结果。
- 规则命中结果。
- Tool 计划。
- DryRun 结果。
- 审批动作。
- 执行结果。
- 错误和阻断原因。

没有 Trace 的 Agent 后期无法定位问题，也无法沉淀评测集。

### 2.8 低置信度必须追问

Agent 可以自主判断闲聊还是工作流，但必须保守。以下情况应追问或转咨询，不应执行：

- 没有明确动作。
- 没有明确业务对象。
- 多个 Intent 都可能匹配。
- 缺少必要参数。
- 用户表达像闲聊或解释请求。
- 当前工作流状态不允许执行。

### 2.9 文档和规则要双轨同步

可控 Agent 的规则不能只存在代码里。每个 Skill、Intent、Tool、Schema、Rule 都要有文档说明，并有机器可读配置或测试用例承接。

## 3. RoadProto 实践已经验证的修正

- 独立后端比内嵌后端更适合长期扩展。
- WPF 可停靠面板足够承载 MVP 交互，不需要单独 Web 页面。
- Provider 设置应支持 DeepSeek、Qwen、GLM、GPT 这类 OpenAI-compatible API。
- 日志应放到可控运行期目录，避免用户配置目录膨胀。
- 缺参追问要进入 API 响应字段，前端不能只从 Plan 里兜底。
- 模型输出可能把参数写成标量，也可能写成对象，解析层要兼容并继续做 Schema 校验。
- WPF 可见日志需要翻译成用户能读懂的流转文案，原始 stage / message 保留在文件日志中。
- 路基模板创建默认值已从 RoadProto 本地代码收敛到后端规则文件，本地 Adapter 只负责执行和校验。
- `道路模型` 已作为独立工程对象被入口路由拦截，未接入时不调用路基模板 Tool。
