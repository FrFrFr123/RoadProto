# 模块 02：Agent 配置中心 / Agent Config Center MVP 能力说明（修订版）

## 1. 当前模块

当前讨论模块：

```text
模块 02：Agent 配置中心 / Agent Config Center
```

该模块负责管理总体 Agent 和业务 Agent 的配置，包括 Agent 清单、能力边界、路由规则、模型策略、API 通道、工具权限、审批策略、风险策略、工作流绑定、Schema 绑定、Prompt / Skill / Rule 版本绑定等。

在 MVP 中，需要明确加入前后端体系：

```text
前端：提供模型 / API 选择入口；
后端后台：管理可用模型、API 通道、Agent 配置、权限、策略和版本；
模型网关：根据后端配置真正调用模型 API。
```

因此，MVP 版本需要一个后端后台，但它不是复杂平台后台，而是一个“最小后端管理与配置服务”。

---

## 2. 本模块在 MVP 总体流程中的位置

MVP 整体运行链路中，Agent 配置中心位于前端之后、模型网关和工作流之前。

整体流程如下：

```text
用户输入自然语言
↓
前端提交用户输入、当前项目、当前对象、模型/API 选择
↓
后端后台接收请求
↓
Agent 配置中心加载总体 Agent 配置
↓
加载可用业务 Agent 清单
↓
加载可用模型清单和 API 通道配置
↓
校验用户选择的模型/API 是否可用、是否授权、是否适合当前 Agent
↓
加载 AgentRouter 路由配置
↓
IntentRouter 识别用户意图
↓
AgentRouter 根据配置判断业务 Agent
↓
Agent 配置中心加载业务 Agent 配置
↓
绑定该业务 Agent 的模型策略、Schema、Prompt、Skill、规则、工具、审批策略
↓
生成 Runtime Agent Config
↓
交给 Orchestrator 执行后续流程
```

本模块主要覆盖这些节点：

```text
总体 Agent 配置加载
↓
业务 Agent 注册清单加载
↓
模型与 API 通道配置加载
↓
用户模型选择校验
↓
Agent 路由配置加载
↓
业务 Agent 配置加载
↓
模型 / 工具 / 规则 / Schema / Prompt / Skill 版本绑定
↓
运行时配置包生成
↓
传递给工作流编排层
```

---

## 3. 模块定位

Agent 配置中心的定位是：

```text
让每个 Agent 的能力、边界、工具、规则、模型、API 通道和审批策略都显式化、可配置、可追踪。
```

它解决的问题是：

```text
1. 系统有哪些 Agent；
2. 用户面对的是哪个总体 Agent；
3. 当前任务可以调用哪些业务 Agent；
4. 每个业务 Agent 能做什么、不能做什么；
5. 每个业务 Agent 可以调用哪些工具；
6. 每个业务 Agent 绑定哪些 Schema、Prompt、Skill、Rule；
7. 每个业务 Agent 默认使用什么模型策略；
8. 用户是否可以覆盖默认模型选择；
9. 用户可以选择哪些 API 通道；
10. 哪些 API 通道允许用于当前项目、当前用户、当前 Agent；
11. 哪些动作需要 DryRun；
12. 哪些动作需要用户审批；
13. 哪些配置版本参与了本次运行。
```

Agent 配置中心不是：

```text
1. 业务规则执行器；
2. 工作流执行器；
3. 模型 API 调用器；
4. 工具调用器；
5. 前端页面；
6. 具体业务 Agent 的代码实现。
```

它只负责“配置管理”和“运行时配置装配”。

---

## 4. MVP 需要后端后台吗？

结论：需要。

但 MVP 需要的是：

```text
最小后端管理与配置服务
```

不是：

```text
复杂企业级后台管理平台
```

MVP 后端后台至少要提供：

```text
1. Agent 配置读取；
2. Agent 注册表管理；
3. 业务 Agent 路由配置读取；
4. 模型清单管理；
5. API 通道配置管理；
6. 用户模型选择校验；
7. API Key / Credential 引用管理；
8. 权限策略校验；
9. Runtime Agent Config 生成；
10. 配置版本写入 Trace。
```

MVP 后台可以先不做复杂可视化页面，配置可以先用文件方式实现：

```text
JSON；
YAML；
Markdown；
环境变量；
本地配置文件；
简单数据库。
```

但必须有后端服务层统一读取、校验和输出配置。

---

## 5. MVP 中的 Agent 分层

MVP 中 Agent 建议分成两层：

```text
总体 Agent
业务 Agent
```

### 5.1 总体 Agent

总体 Agent 是用户看到的统一入口。

示例：

```text
EICAD 工程智能助手
```

总体 Agent 的配置内容包括：

```text
总体 Agent ID；
总体 Agent 名称；
适用软件；
适用模块范围；
默认模型策略；
是否允许用户选择模型；
是否允许用户选择 API 通道；
可路由的业务 Agent 清单；
默认问答 Agent；
默认失败兜底策略；
统一输出风格；
权限边界；
版本号。
```

示例：

```json
{
  "agent_id": "engineering_assistant",
  "agent_name": "EICAD 工程智能助手",
  "agent_type": "main_agent",
  "software": "EICAD",
  "available_business_agents": [
    "subgrade_template_agent",
    "cross_section_capping_agent",
    "qa_agent"
  ],
  "default_agent": "qa_agent",
  "router_policy": "intent_and_context_based",
  "model_selection_policy": {
    "allow_user_select_model": true,
    "allow_user_select_api_channel": true,
    "default_model_id": "company_default_model",
    "default_api_channel_id": "company_default_api"
  },
  "version": "1.0.0"
}
```

---

### 5.2 业务 Agent

业务 Agent 是系统自动路由后实际调用的具体能力模块。

示例：

```text
路基模板创建 Agent
横断面戴帽 Agent
软件功能问答 Agent
专业知识问答 Agent
```

业务 Agent 的配置内容包括：

```text
业务 Agent ID；
业务 Agent 名称；
适用任务；
不适用任务；
绑定工作流；
绑定模型策略；
是否允许用户覆盖模型；
是否允许用户覆盖 API 通道；
绑定 Prompt；
绑定 Skill；
绑定 Schema；
绑定规则；
绑定工具范围；
绑定审批策略；
绑定风险策略；
绑定评测集；
版本号；
负责人。
```

示例：

```json
{
  "agent_id": "subgrade_template_agent",
  "agent_name": "路基模板创建 Agent",
  "agent_type": "business_agent",
  "task_scope": [
    "创建路基模板",
    "补全路基模板参数",
    "预览路基模板影响范围"
  ],
  "out_of_scope": [
    "直接进行全线路基设计",
    "直接覆盖原始工程文件",
    "无预览批量修改断面"
  ],
  "workflow": "subgrade_template_workflow.yaml",
  "schema": "subgrade_template_schema.json",
  "skill": "subgrade_template_skill.md",
  "rules": "subgrade_template_rules.yaml",
  "tool_scope": "subgrade_template_tools.json",
  "approval_policy": "default_engineering_approval_policy.json",
  "risk_policy": "subgrade_template_risk_policy.json",
  "model_policy": {
    "default_model_id": "company_default_model",
    "allow_user_override": true,
    "required_capabilities": [
      "structured_output"
    ]
  },
  "version": "1.0.0"
}
```

---

## 6. 模型与 API 配置

MVP 配置中心必须加入模型与 API 配置对象。

### 6.1 模型清单 Model Registry

模型清单描述系统可用的大模型。

示例：

```json
{
  "models": [
    {
      "model_id": "gpt_4_1",
      "model_name": "GPT-4.1",
      "provider_id": "openai",
      "supports_json_schema": true,
      "supports_tool_calling": true,
      "supports_streaming": true,
      "supports_private_deploy": false,
      "status": "enabled"
    },
    {
      "model_id": "deepseek_v3",
      "model_name": "DeepSeek-V3",
      "provider_id": "deepseek",
      "supports_json_schema": true,
      "supports_tool_calling": true,
      "supports_streaming": true,
      "supports_private_deploy": false,
      "status": "enabled"
    },
    {
      "model_id": "local_private_model",
      "model_name": "本地私有化模型",
      "provider_id": "local",
      "supports_json_schema": true,
      "supports_tool_calling": false,
      "supports_streaming": true,
      "supports_private_deploy": true,
      "status": "enabled"
    }
  ]
}
```

---

### 6.2 API 通道配置 API Channel Registry

API 通道描述调用模型的接口来源。

示例：

```json
{
  "api_channels": [
    {
      "api_channel_id": "company_default_api",
      "channel_name": "公司默认 API",
      "provider_id": "openai",
      "base_url": "https://api.xxx.com/v1",
      "credential_ref": "secret://company/openai/default",
      "status": "enabled",
      "allowed_users": ["all"],
      "allowed_agents": ["all"]
    },
    {
      "api_channel_id": "project_private_api",
      "channel_name": "项目私有 API",
      "provider_id": "local",
      "base_url": "http://local-model-gateway/v1",
      "credential_ref": "secret://project/private/model",
      "status": "enabled",
      "allowed_users": ["project_members"],
      "allowed_agents": ["subgrade_template_agent", "qa_agent"]
    }
  ]
}
```

注意：

```text
配置中心保存 credential_ref；
不在前端返回明文 API Key；
不在 Trace 中记录明文 API Key；
实际调用由 Model Gateway 根据 credential_ref 获取凭证。
```

---

### 6.3 模型策略 Model Policy

模型策略用于描述当前 Agent 在不同任务中使用哪个模型。

示例：

```json
{
  "model_policy_id": "subgrade_template_model_policy",
  "default_model_id": "gpt_4_1",
  "default_api_channel_id": "company_default_api",
  "allow_user_select_model": true,
  "allow_user_select_api_channel": true,
  "fallback_model_id": "deepseek_v3",
  "tasks": {
    "intent_recognition": {
      "model_id": "gpt_4_1",
      "temperature": 0.1
    },
    "param_extract": {
      "model_id": "gpt_4_1",
      "temperature": 0.1,
      "structured_output": true
    },
    "result_explain": {
      "model_id": "gpt_4_1",
      "temperature": 0.3
    }
  }
}
```

MVP 中可以先简化为：

```text
每个 Agent 一个默认模型；
用户可选择模型；
用户可选择 API 通道；
后端校验模型是否满足当前 Agent 能力要求；
失败时使用 fallback_model。
```

---

## 7. Agent 配置中心 MVP 必须管理的配置对象

MVP 中至少需要管理以下配置对象：

```text
1. main_agent_manifest
2. business_agent_manifest
3. agent_registry
4. router_config
5. model_registry
6. api_channel_registry
7. model_policy
8. workflow_config
9. prompt_config
10. skill_config
11. schema_config
12. rule_config
13. tool_scope
14. permission_policy
15. approval_policy
16. risk_policy
17. version_info
```

这些配置不一定都要做成复杂系统，但必须有清晰文件和加载机制。

---

## 8. MVP 推荐配置文件结构

建议目录如下：

```text
backend/
├─ config_service/
│  ├─ agent_config_loader/
│  ├─ model_config_loader/
│  ├─ api_channel_loader/
│  ├─ permission_checker/
│  └─ runtime_config_builder/
│
├─ model_configs/
│  ├─ model_registry.json
│  ├─ api_channel_registry.json
│  └─ model_policies/
│
└─ secrets/
   └─ credential_refs.env

agents/
├─ main_agent/
│  ├─ agent_manifest.json
│  ├─ router_config.yaml
│  ├─ model_policy.json
│  ├─ permission_policy.json
│  └─ fallback_policy.json
│
├─ subgrade_template_agent/
│  ├─ agent_manifest.json
│  ├─ workflow.yaml
│  ├─ model_policy.json
│  ├─ prompts/
│  │  ├─ intent_prompt.md
│  │  ├─ param_extract_prompt.md
│  │  └─ result_explain_prompt.md
│  ├─ skills/
│  │  └─ subgrade_template_skill.md
│  ├─ schemas/
│  │  └─ subgrade_template_schema.json
│  ├─ rules/
│  │  └─ subgrade_template_rules.yaml
│  ├─ tools/
│  │  └─ tool_scope.json
│  ├─ policies/
│  │  ├─ approval_policy.json
│  │  └─ risk_policy.json
│  └─ tests/
│     └─ eval_cases.json
│
└─ qa_agent/
   ├─ agent_manifest.json
   ├─ workflow.yaml
   ├─ prompts/
   ├─ skills/
   ├─ schemas/
   └─ tests/
```

另外需要一个全局 Agent 注册表：

```text
agent_registry.json
```

示例：

```json
{
  "main_agents": [
    {
      "agent_id": "engineering_assistant",
      "agent_name": "EICAD 工程智能助手",
      "manifest_path": "agents/main_agent/agent_manifest.json"
    }
  ],
  "business_agents": [
    {
      "agent_id": "subgrade_template_agent",
      "agent_name": "路基模板创建 Agent",
      "manifest_path": "agents/subgrade_template_agent/agent_manifest.json",
      "status": "enabled"
    },
    {
      "agent_id": "qa_agent",
      "agent_name": "软件功能问答 Agent",
      "manifest_path": "agents/qa_agent/agent_manifest.json",
      "status": "enabled"
    }
  ]
}
```

---

## 9. AgentRouter 路由配置

因为用户不需要手动选择业务 Agent，所以配置中心必须支持 AgentRouter。

AgentRouter 的配置至少包括：

```text
业务 Agent ID；
可识别意图；
关键词提示；
适用模块；
适用上下文；
优先级；
置信度阈值；
低置信度处理方式；
默认兜底 Agent。
```

示例：

```yaml
router_rules:
  - agent_id: subgrade_template_agent
    intents:
      - create_subgrade_template
      - modify_subgrade_template
    keywords:
      - 路基模板
      - 创建模板
      - 路幅
      - 边坡
      - 戴帽模板
    module_scope:
      - cross_section
      - subgrade
    priority: 100
    confidence_threshold: 0.8

  - agent_id: qa_agent
    intents:
      - software_qa
      - professional_qa
    keywords:
      - 怎么用
      - 是什么
      - 解释
      - 帮我查
    module_scope:
      - all
    priority: 10
    confidence_threshold: 0.5

fallback_agent: qa_agent
low_confidence_policy: ask_user_confirm
```

MVP 中可以先支持简单规则：

```text
意图 + 关键词 + 当前模块 + 置信度阈值
```

后续再扩展为更复杂的 RouterAgent。

---

## 10. 运行时配置包 Runtime Agent Config

配置中心最终要输出一个运行时配置包，供 Orchestrator 使用。

示例：

```json
{
  "run_id": "",
  "main_agent": {
    "agent_id": "engineering_assistant",
    "agent_name": "EICAD 工程智能助手",
    "version": "1.0.0"
  },
  "routed_agent": {
    "agent_id": "subgrade_template_agent",
    "agent_name": "路基模板创建 Agent",
    "version": "1.0.0"
  },
  "workflow": {
    "workflow_id": "subgrade_template_workflow",
    "version": "1.0.0"
  },
  "model_runtime": {
    "model_id": "gpt_4_1",
    "provider_id": "openai",
    "api_channel_id": "company_default_api",
    "credential_ref": "secret://company/openai/default",
    "temperature": 0.2,
    "structured_output": true
  },
  "schemas": {
    "param_schema": "subgrade_template_schema.json",
    "version": "1.0.0"
  },
  "prompts": {
    "param_extract_prompt": "param_extract_prompt.md",
    "version": "1.0.0"
  },
  "skills": {
    "skill_file": "subgrade_template_skill.md",
    "version": "1.0.0"
  },
  "rules": {
    "rule_file": "subgrade_template_rules.yaml",
    "version": "1.0.0"
  },
  "tool_scope": {
    "allowed_tools": [
      "get_current_project_info",
      "preview_subgrade_template",
      "apply_subgrade_template"
    ]
  },
  "approval_policy": {
    "requires_approval": true
  },
  "risk_policy": {
    "default_risk_level": "medium"
  }
}
```

这个配置包必须进入 Trace。

注意：

```text
Runtime Agent Config 可以包含 credential_ref；
Trace 只能记录脱敏后的 credential_ref；
不能记录明文 API Key。
```

---

## 11. MVP 必须支持的配置校验

配置中心不能只负责读取文件，还必须做基础校验。

MVP 至少校验：

```text
Agent ID 是否唯一；
业务 Agent 是否注册；
业务 Agent 状态是否启用；
绑定的 Prompt 文件是否存在；
绑定的 Skill 文件是否存在；
绑定的 Schema 文件是否存在；
绑定的 Rule 文件是否存在；
绑定的 Tool Scope 是否存在；
工具是否在 Tool Registry 中注册；
审批策略是否存在；
风险策略是否存在；
模型是否存在；
模型是否启用；
API 通道是否存在；
API 通道是否启用；
用户是否有权限使用该 API 通道；
业务 Agent 是否允许用户覆盖模型；
业务 Agent 是否允许用户覆盖 API 通道；
模型是否满足当前任务能力要求；
版本号是否存在。
```

如果配置不完整，应阻断启动或阻断该 Agent 调用。

示例错误：

```text
subgrade_template_agent 绑定的 schema 文件不存在，无法启动该业务 Agent。
```

示例错误：

```text
当前用户选择的 API 通道不允许用于路基模板创建 Agent，请切换为系统默认 API。
```

---

## 12. MVP 中不做范围

Agent 配置中心 MVP 暂不做：

```text
复杂可视化后台；
多人协同配置审批；
配置灰度发布；
多客户配置隔离；
多项目配置继承；
复杂权限矩阵；
在线热更新；
配置差异对比；
配置回滚平台；
配置发布流水线。
```

但需要预留：

```text
配置版本号；
配置状态；
配置负责人；
配置生效范围；
配置更新时间；
配置校验结果；
模型能力标签；
API 通道状态；
Credential 引用；
配置 Trace 记录。
```

---

## 13. 与其他模块的关系

Agent 配置中心向下游提供配置。

它依赖：

```text
配置文件系统；
Agent Registry；
Model Registry；
API Channel Registry；
Credential Store；
Tool Registry；
Schema 文件；
Prompt 文件；
Skill 文件；
Rule 文件。
```

它服务于：

```text
前端交互层；
IntentRouter；
AgentRouter；
Model Gateway；
Orchestrator；
Schema Validator；
Rule Engine；
Tool Registry；
Approval Gate；
Trace Logger。
```

它不能直接：

```text
调用模型 API；
执行工具；
修改工程数据；
替代规则引擎；
替代工作流编排；
替代用户审批。
```

---

## 14. MVP 验收标准

本模块完成后，应满足：

```text
1. 系统能加载总体 Agent 配置；
2. 系统能加载业务 Agent 注册表；
3. 系统能知道当前有哪些可用业务 Agent；
4. 系统能加载模型清单；
5. 系统能加载 API 通道清单；
6. 用户能在前端选择模型；
7. 用户能在前端选择 API 通道；
8. 后端能校验用户选择的模型和 API 是否可用；
9. 后端能阻止未授权模型/API 通道；
10. 用户不手动选择业务 Agent 时，系统能根据配置路由业务 Agent；
11. 每个业务 Agent 的能力边界是显式配置的；
12. 每个业务 Agent 绑定的 Prompt、Skill、Schema、Rule、Tool 是显式配置的；
13. 每个业务 Agent 的工具权限是显式配置的；
14. 每个业务 Agent 的审批策略是显式配置的；
15. 配置缺失时系统能阻断并说明原因；
16. 每次运行能记录使用了哪个总体 Agent、哪个业务 Agent、哪个模型、哪个 API 通道、哪些配置版本；
17. Trace 不记录明文 API Key；
18. 后续新增一个业务 Agent 时，只需要新增配置、Schema、Skill、Prompt、Rule、Tool Scope，不需要改主链路；
19. 后续新增一个模型或 API 通道时，只需要新增 Model Registry / API Channel Registry 配置，不需要改业务 Agent 主流程。
```

---

## 15. 本模块结论

Agent 配置中心的 MVP 核心不是做一个复杂平台，而是先建立一套稳定的 Agent、模型、API 和策略配置契约。

正确设计应是：

```text
总体 Agent 负责统一入口；
Agent Registry 管理所有业务 Agent；
Model Registry 管理可用模型；
API Channel Registry 管理 API 通道；
AgentRouter 根据配置判断业务 Agent；
业务 Agent 配置绑定自己的能力、规则、工具和工作流；
模型策略决定默认模型和是否允许用户覆盖；
后端校验用户选择；
模型网关实际调用 API；
Orchestrator 根据 Runtime Agent Config 执行任务。
```

MVP 中可以先用本地文件实现，但必须保证：

```text
配置显式；
边界清楚；
模型可选；
API 可控；
权限可校验；
版本可追踪；
工具可控；
后续可扩展。
```

推荐命名：

```text
Agent Config Center / Agent 配置中心
```

MVP 不是为了做完整后台，而是为了保证：

```text
每一个 Agent 是谁；
能干什么；
不能干什么；
用什么模型；
可选哪些模型；
可用哪些 API；
看什么 Skill；
用什么 Schema；
走什么规则；
能调什么工具；
什么时候需要审批；
本次运行用了哪个版本。
```
