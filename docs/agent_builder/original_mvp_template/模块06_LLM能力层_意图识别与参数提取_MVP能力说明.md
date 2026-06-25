# 模块 06：LLM 能力层 / 意图识别与参数提取 MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 06：LLM 能力层 / LLM Capability
```

LLM 能力层负责调用大模型完成自然语言理解类任务。

MVP 中，LLM 不直接控制工程软件，不直接调用 CAD / BIM / EICAD 函数，不直接决定执行结果是否成功。

LLM 主要负责：

```text
1. 理解用户想做什么；
2. 判断用户输入属于哪类任务；
3. 提取结构化参数；
4. 发现缺失参数；
5. 生成追问；
6. 辅助解释结果；
7. 辅助生成用户可读说明。
```

一句话：

```text
LLM 负责理解和表达，不负责最终工程决策和工程执行。
```

---

## 2. 本模块在 MVP 总体流程中的位置

LLM 能力层位于上下文管理层之后，Schema 校验层之前。

流程如下：

```text
用户输入
↓
前端提交任务
↓
Agent 配置中心加载配置
↓
Orchestrator 创建状态机实例
↓
Context Manager 生成 Context Package
↓
LLM 能力层识别意图
↓
AgentRouter 根据意图路由业务 Agent
↓
LLM 能力层提取参数
↓
Schema Validator 校验结构
↓
Rule Engine 补默认值、推导、校验
↓
DryRun / 审批 / 执行
```

LLM 能力层主要覆盖这些节点：

```text
IntentRecognized
AgentRouteCandidateGenerated
ParamsExtracted
MissingFieldsDetected
FollowupQuestionGenerated
ResultExplained
```

---

## 3. 模块定位

LLM 能力层的定位是：

```text
把用户自然语言转成可被系统继续处理的结构化任务信息。
```

它解决的问题是：

```text
1. 用户不会按表单输入；
2. 用户输入经常是不完整的；
3. 用户表达可能包含专业术语、口语、缩写；
4. 系统需要从一句话中识别任务类型；
5. 系统需要从一句话中提取参数；
6. 系统需要判断是否缺少关键参数；
7. 系统需要用自然语言向用户解释系统结果。
```

LLM 能力层不是：

```text
1. 规则引擎；
2. Schema 校验器；
3. Tool Registry；
4. 软件 Adapter；
5. DryRun Executor；
6. 用户审批器；
7. 工程执行器。
```

---

## 4. MVP 中 LLM 只做四类核心任务

MVP 中不要让 LLM 一开始承担太多职责。

建议只做四类核心任务：

```text
1. 意图识别；
2. 参数提取；
3. 缺失参数追问；
4. 结果解释。
```

暂时不让 LLM 做：

```text
1. 自主规划复杂工具链；
2. 自由选择未注册工具；
3. 自由改写业务规则；
4. 自由决定执行是否成功；
5. 自由保存或覆盖工程数据；
6. 直接生成工程对象。
```

---

## 5. 意图识别

### 5.1 作用

意图识别负责判断用户输入属于什么任务。

示例输入：

```text
帮我创建一个高速公路路基模板，设计速度 80，路基宽度 24.5 米。
```

LLM 应识别为：

```text
create_subgrade_template
```

### 5.2 输出结构

意图识别输出必须是结构化结果。

示例：

```json
{
  "intent": "create_subgrade_template",
  "confidence": 0.91,
  "candidate_agents": [
    {
      "agent_id": "subgrade_template_agent",
      "agent_name": "路基模板创建 Agent",
      "confidence": 0.91
    }
  ],
  "need_clarification": false,
  "clarification_question": ""
}
```

### 5.3 低置信度处理

如果置信度不足，不直接进入业务 Agent。

处理方式：

```text
低置信度 → 返回候选任务 → 前端让用户确认
```

示例：

```text
我理解你可能想执行以下任务：
1. 创建路基模板
2. 横断面戴帽
3. 查询路基模板操作方法

请确认你要执行哪一类任务。
```

---

## 6. 参数提取

### 6.1 作用

参数提取负责把用户自然语言中的参数抽取出来，形成 ParamSchema 可以校验的结构。

示例输入：

```text
帮我创建一个高速公路路基模板，设计速度 80，路基宽度 24.5 米。
```

LLM 输出：

```json
{
  "road_grade": {
    "value": "高速公路",
    "source": "user_input",
    "confidence": 0.95
  },
  "design_speed": {
    "value": 80,
    "unit": "km/h",
    "source": "user_input",
    "confidence": 0.96
  },
  "subgrade_width": {
    "value": 24.5,
    "unit": "m",
    "source": "user_input",
    "confidence": 0.97
  }
}
```

### 6.2 参数提取原则

```text
只提取用户明确表达的参数；
不把模型猜测当作用户输入；
不直接补默认值；
不直接推导业务参数；
不覆盖用户明确输入；
无法确定时标记 uncertain。
```

默认值补全交给 Rule Engine，不交给 LLM。

---

## 7. 缺失参数识别与追问

LLM 可以辅助生成追问，但缺失字段判断应基于 Schema。

流程：

```text
参数提取
↓
Schema 判断缺少必填字段
↓
LLM 根据缺失字段生成自然语言追问
↓
前端展示给用户
```

示例：

```json
{
  "missing_fields": [
    {
      "field": "slope_type",
      "required": true,
      "reason": "创建路基模板需要明确边坡策略"
    }
  ],
  "followup_question": "还需要确认边坡类型。是否采用项目默认边坡，还是手动指定？"
}
```

注意：

```text
缺失字段由 Schema 判断；
追问话术由 LLM 生成；
是否允许继续由 Orchestrator 判断。
```

---

## 8. 结果解释

执行完成或失败后，LLM 可以参与结果解释。

输入给 LLM 的不是完整工程数据，而是结构化结果摘要：

```json
{
  "execution_status": "success",
  "created_objects": ["路基模板草稿"],
  "modified_objects": ["当前选中断面"],
  "rule_hits": [],
  "warnings": [],
  "rollback_supported": true
}
```

LLM 输出用户可读解释：

```text
已创建路基模板草稿，并应用到当前选中断面。本次操作未自动保存工程文件，支持撤销。
```

注意：

```text
LLM 只能解释系统返回的结构化结果；
不能把失败解释成成功；
不能编造执行结果；
不能编造规则来源。
```

---

## 9. LLM 输入输出约束

### 9.1 输入

LLM 输入应来自：

```text
用户输入；
Context Package；
当前业务 Agent Skill；
Prompt；
目标 Schema；
可用工具摘要；
规则版本摘要。
```

### 9.2 输出

LLM 输出必须是：

```text
结构化 JSON；
符合指定 Schema；
包含置信度；
包含来源标记；
可被 Schema Validator 校验。
```

### 9.3 禁止输出

LLM 不应直接输出：

```text
工程软件执行命令；
未注册工具调用；
CAD 内部函数；
保存 / 覆盖 / 删除动作；
无来源规范结论；
最终业务成功判断。
```

---

## 10. Prompt 配置

MVP 至少需要以下 Prompt：

```text
意图识别 Prompt；
参数提取 Prompt；
追问生成 Prompt；
结果解释 Prompt。
```

Prompt 应按业务 Agent 分开管理。

示例目录：

```text
agents/
└─ subgrade_template_agent/
   └─ prompts/
      ├─ intent_prompt.md
      ├─ param_extract_prompt.md
      ├─ followup_prompt.md
      └─ result_explain_prompt.md
```

Prompt 必须版本化。

---

## 11. Skill 使用边界

LLM 能力层需要读取 Skill，让模型理解业务术语和软件能力。

Skill 包括：

```text
业务概念；
软件功能；
参数含义；
典型任务；
禁止事项；
输出要求；
常见错误。
```

但 Skill 不是规则引擎。

原则：

```text
Skill 让模型理解业务；
Rule Engine 负责最终规则判断；
Schema 负责字段结构约束；
Tool Registry 负责工具权限。
```

---

## 12. 与其他模块的关系

LLM 能力层依赖：

```text
模型网关；
Context Manager；
Agent 配置中心；
Prompt；
Skill；
Schema；
Orchestrator。
```

LLM 能力层服务于：

```text
IntentRouter；
AgentRouter；
参数提取；
追问生成；
结果解释；
Trace Logger；
Evaluation。
```

LLM 能力层不能直接：

```text
调用工程工具；
修改工程数据；
执行规则判断；
执行 DryRun；
执行审批；
保存结果。
```

---

## 13. MVP 不做范围

LLM 能力层 MVP 暂不做：

```text
复杂自主 Agent 规划；
模型自主多工具调用；
多模型辩论；
复杂 CoT 展示；
模型自动写规则；
模型自动生成原子函数；
模型直接生成 CAD 脚本并执行；
复杂长期记忆推理。
```

但需要预留：

```text
多模型调用；
多 Prompt 版本；
Prompt A/B；
失败样例回归；
参数提取评测；
意图识别评测；
解释质量评测。
```

---

## 14. MVP 验收标准

本模块完成后，应满足：

```text
1. 用户自然语言可以被识别为结构化意图；
2. 意图识别有置信度；
3. 低置信度时不会强行执行；
4. 用户明确输入的参数可以被结构化提取；
5. 参数带有来源和置信度；
6. LLM 不直接补默认值；
7. LLM 不直接推导强业务规则；
8. 缺失字段可通过 Schema 识别；
9. LLM 能根据缺失字段生成追问；
10. LLM 输出必须经过 Schema Validator；
11. LLM 不能直接调用工具；
12. LLM 不能直接修改工程数据；
13. LLM 只能解释系统结构化结果；
14. 模型原始输出和解析结果能进入 Trace；
15. 失败样例能进入评测集。
```

---

## 15. 本模块结论

LLM 能力层的 MVP 核心不是让模型“自主完成工程任务”，而是让模型稳定完成自然语言理解任务。

正确设计是：

```text
LLM 负责理解用户意图；
LLM 负责提取用户明确表达的参数；
LLM 负责生成追问和解释；
Schema 负责约束结构；
Rule Engine 负责默认值、推导和校验；
Tool Registry 负责工具可控；
Orchestrator 负责流程控制。
```

一句话：

```text
LLM 是理解层，不是执行层。
```

本模块已确认的关键原则：

```text
LLM 不直接补默认值；
LLM 不直接推导强业务规则。
```

默认值和强业务规则统一交给规则引擎层处理。
