# 模块 03：模型网关 / Model Gateway MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 03：模型网关 / Model Gateway
```

模型网关是可控工程 Agent 中统一调用大模型的后端服务层。

它不负责决定业务 Agent 能做什么，也不负责执行工程软件动作，而是负责：

```text
统一接入不同大模型；
统一管理模型 API 调用；
统一处理模型请求和返回；
统一支持结构化输出；
统一处理超时、重试、降级；
统一记录 token、耗时、错误和成本；
统一把模型调用过程写入 Trace。
```

在 MVP 中，前端可以让用户选择大模型和 API 通道，但前端不能直接调用模型 API。

正确关系是：

```text
前端选择模型 / API
↓
后端后台校验模型配置
↓
Agent 配置中心生成 Runtime Agent Config
↓
模型网关根据配置调用具体模型 API
↓
模型结果返回给 Orchestrator
```

---

## 2. 本模块在 MVP 总体流程中的位置

MVP 整体运行链路中，模型网关位于 Agent 配置中心之后，LLM 能力层之前。

流程如下：

```text
用户输入自然语言
↓
前端提交任务，并可附带用户选择的模型 / API 通道
↓
后端后台校验模型 / API 选择是否可用
↓
Agent 配置中心加载总体 Agent 和业务 Agent 配置
↓
生成 Runtime Agent Config
↓
Orchestrator 进入模型调用节点
↓
Model Gateway 读取模型策略
↓
Model Gateway 调用指定模型 API
↓
返回结构化结果
↓
Schema Validator 校验模型输出
↓
继续进入规则、DryRun、执行等后续流程
```

本模块主要覆盖这些节点：

```text
模型配置读取
↓
API 通道选择
↓
模型能力校验
↓
统一请求构造
↓
模型 API 调用
↓
结构化输出处理
↓
超时 / 重试 / 降级
↓
模型结果标准化
↓
调用日志写入 Trace
```

---

## 3. 模块定位

模型网关的定位是：

```text
把不同大模型和不同 API 封装成统一、可控、可切换、可追踪的模型调用服务。
```

它解决的问题是：

```text
1. 不让业务代码直接绑定某个模型厂商；
2. 不让前端直接调用模型 API；
3. 不让每个 Agent 各自写一套模型调用逻辑；
4. 支持用户或管理员选择不同模型；
5. 支持不同业务 Agent 使用不同模型策略；
6. 支持模型失败后的重试和降级；
7. 支持结构化输出，方便后续 Schema 校验；
8. 支持记录模型调用日志，便于调试和评测。
```

模型网关不是：

```text
1. Agent 配置中心；
2. 业务规则引擎；
3. 工作流编排器；
4. 工具调用器；
5. 软件 Adapter；
6. 前端模型选择页面。
```

一句话：

```text
Agent 配置中心决定“用哪个模型”；
模型网关负责“怎么调用模型”；
前端只负责“展示和选择模型”。
```

---

## 4. 前端、后端后台、模型网关的职责边界

### 4.1 前端职责

前端可以提供模型和 API 选择入口。

前端可展示：

```text
当前使用的大模型；
当前使用的 API 通道；
模型能力标签；
是否支持结构化输出；
是否支持工具调用；
是否支持长上下文；
是否为私有化模型；
模型调用状态；
模型错误提示。
```

前端可允许用户选择：

```text
模型厂商；
模型名称；
API 通道；
是否使用默认模型；
是否使用项目指定模型；
是否使用本地模型；
是否使用云端模型。
```

但前端不能：

```text
直接保存 API Key；
直接调用模型 API；
绕过后端校验使用未授权模型；
绕过 Agent 配置中心修改业务 Agent 模型策略；
直接决定模型输出是否可信。
```

---

### 4.2 后端后台职责

MVP 需要一个后端后台，也就是后端管理与配置服务。

后端后台负责：

```text
维护可用模型清单；
维护可用 API 通道；
维护模型能力标签；
管理模型启用 / 禁用状态；
管理 API Key 或 Credential 引用；
校验前端选择是否合法；
校验当前用户是否有权限使用该模型；
校验当前 Agent 是否允许使用该模型；
把最终模型策略写入 Runtime Agent Config；
把模型选择记录写入 Trace。
```

后端后台不直接做模型推理，而是把调用请求交给模型网关。

---

### 4.3 模型网关职责

模型网关负责真实模型调用。

模型网关负责：

```text
读取 Runtime Agent Config；
构造统一模型请求；
适配不同模型 API；
发送请求；
处理返回；
处理流式输出；
处理结构化输出；
处理超时；
处理重试；
处理降级；
记录 token、耗时、错误和成本；
返回统一结构给 Orchestrator。
```

---

## 5. MVP 中必须实现的模型配置能力

MVP 至少需要一个模型清单。

示例：

```json
{
  "models": [
    {
      "model_id": "openai_gpt_4_1",
      "provider": "OpenAI",
      "model_name": "gpt-4.1",
      "api_channel": "openai_official",
      "enabled": true,
      "supports_json_schema": true,
      "supports_tool_calling": true,
      "supports_streaming": true,
      "supports_long_context": true,
      "supports_local_deploy": false,
      "risk_level": "normal"
    },
    {
      "model_id": "deepseek_v3",
      "provider": "DeepSeek",
      "model_name": "deepseek-v3",
      "api_channel": "deepseek_official",
      "enabled": true,
      "supports_json_schema": true,
      "supports_tool_calling": false,
      "supports_streaming": true,
      "supports_long_context": true,
      "supports_local_deploy": false,
      "risk_level": "normal"
    },
    {
      "model_id": "local_qwen",
      "provider": "Local",
      "model_name": "qwen-local",
      "api_channel": "local_openai_compatible",
      "enabled": false,
      "supports_json_schema": false,
      "supports_tool_calling": false,
      "supports_streaming": false,
      "supports_long_context": false,
      "supports_local_deploy": true,
      "risk_level": "private"
    }
  ]
}
```

MVP 至少需要支持这些能力标签：

```text
supports_json_schema
supports_tool_calling
supports_streaming
supports_long_context
supports_reasoning
supports_local_deploy
supports_private_deploy
```

---

## 6. MVP 中必须实现的 API 通道配置

模型和 API 通道要分开。

一个模型可以通过不同 API 通道调用。

示例：

```json
{
  "api_channels": [
    {
      "channel_id": "openai_official",
      "provider": "OpenAI",
      "base_url": "https://api.openai.com/v1",
      "credential_ref": "secret://openai/default",
      "enabled": true,
      "timeout_ms": 60000,
      "max_retries": 2
    },
    {
      "channel_id": "deepseek_official",
      "provider": "DeepSeek",
      "base_url": "https://api.deepseek.com/v1",
      "credential_ref": "secret://deepseek/default",
      "enabled": true,
      "timeout_ms": 60000,
      "max_retries": 2
    },
    {
      "channel_id": "local_openai_compatible",
      "provider": "Local",
      "base_url": "http://127.0.0.1:8000/v1",
      "credential_ref": "",
      "enabled": false,
      "timeout_ms": 120000,
      "max_retries": 0
    }
  ]
}
```

MVP 中 API Key 不建议存在前端。

推荐方式：

```text
前端选择 API 通道；
后端保存 credential_ref；
模型网关根据 credential_ref 读取真实凭证；
Trace 中只记录 credential_ref，不记录真实 API Key。
```

---

## 7. 模型策略 model_policy

每个总体 Agent 和业务 Agent 都可以绑定模型策略。

示例：

```json
{
  "agent_id": "subgrade_template_agent",
  "model_policy": {
    "allow_user_override": true,
    "default_model_id": "openai_gpt_4_1",
    "intent_model_id": "openai_gpt_4_1",
    "param_extract_model_id": "openai_gpt_4_1",
    "explain_model_id": "openai_gpt_4_1",
    "fallback_model_id": "deepseek_v3",
    "temperature": 0.2,
    "max_tokens": 4096,
    "structured_output": true
  }
}
```

其中：

```text
allow_user_override：是否允许用户在前端切换模型；
default_model_id：默认模型；
intent_model_id：意图识别使用的模型；
param_extract_model_id：参数提取使用的模型；
explain_model_id：结果解释使用的模型；
fallback_model_id：失败后降级模型；
structured_output：是否要求结构化输出。
```

MVP 可以先简化为：

```text
一个业务 Agent 默认一个模型；
允许用户从白名单中切换模型；
失败时降级到一个备用模型。
```

---

## 8. 模型网关统一请求结构

Orchestrator 调用模型网关时，不直接关心具体厂商 API。

统一请求结构建议：

```json
{
  "run_id": "",
  "agent_id": "",
  "task_type": "param_extract",
  "model_id": "",
  "api_channel": "",
  "messages": [],
  "context_package": {},
  "response_format": {
    "type": "json_schema",
    "schema": {}
  },
  "temperature": 0.2,
  "max_tokens": 4096,
  "timeout_ms": 60000
}
```

模型网关内部再转换为不同模型厂商的真实请求。

---

## 9. 模型网关统一返回结构

模型网关返回给 Orchestrator 的结果必须统一。

示例：

```json
{
  "run_id": "",
  "model_id": "",
  "provider": "",
  "api_channel": "",
  "task_type": "param_extract",
  "success": true,
  "raw_output": "",
  "parsed_output": {},
  "finish_reason": "",
  "usage": {
    "input_tokens": 0,
    "output_tokens": 0,
    "total_tokens": 0
  },
  "latency_ms": 0,
  "error": null
}
```

注意：

```text
raw_output：模型原始返回；
parsed_output：模型结构化解析结果；
success：只代表模型调用成功，不代表业务执行成功。
```

业务是否成功，需要后续 Schema、规则、DryRun 和执行结果校验判断。

---

## 10. 结构化输出控制

MVP 中模型网关必须支持结构化输出。

至少支持：

```text
JSON 输出；
JSON Schema 输出；
失败后重新格式化；
格式不合法时返回错误；
格式不合法时可重试一次。
```

结构化输出流程：

```text
Orchestrator 传入目标 Schema
↓
Model Gateway 构造结构化输出请求
↓
模型返回 JSON
↓
Model Gateway 尝试解析
↓
解析失败则重试或进入修复
↓
返回 parsed_output
↓
Schema Validator 进一步校验
```

原则：

```text
模型网关只负责让模型尽量输出结构化结果；
最终 Schema 合法性由 Schema Validator 判断。
```

---

## 11. 超时、重试与降级

MVP 中模型网关必须支持基础异常处理。

至少支持：

```text
请求超时；
API 失败；
鉴权失败；
模型返回空；
模型返回格式错误；
模型限流；
模型不可用。
```

处理策略：

```text
超时：重试；
格式错误：重新请求或格式修复；
限流：切换备用模型；
鉴权失败：阻断并提示配置错误；
模型不可用：降级到 fallback_model；
降级失败：返回 Failed 给 Orchestrator。
```

示例流程：

```text
调用主模型失败
↓
判断是否可重试
↓
重试一次
↓
仍失败则调用 fallback_model
↓
fallback 成功则继续
↓
fallback 失败则任务失败
↓
记录完整 Trace
```

---

## 12. 模型调用 Trace

每次模型调用都必须进入 Trace。

Trace 至少记录：

```text
run_id；
agent_id；
task_type；
model_id；
provider；
api_channel；
prompt_version；
schema_version；
skill_version；
是否用户指定模型；
是否使用默认模型；
是否发生重试；
是否发生降级；
raw_output；
parsed_output；
token 用量；
耗时；
错误信息。
```

敏感信息不能进入 Trace：

```text
API Key；
完整 Credential；
用户隐私数据；
工程敏感原始数据。
```

---

## 13. MVP 不做范围

模型网关 MVP 暂不做：

```text
复杂模型路由；
多模型投票；
多模型自动评测选择；
动态成本优化；
复杂并发调度；
模型调用队列；
完整计费系统；
复杂 Prompt A/B 平台；
复杂模型性能看板；
多租户模型用量结算。
```

但需要预留：

```text
模型能力标签；
模型版本；
API 通道；
Credential 引用；
降级模型；
token 统计；
成本字段；
模型调用 Trace；
用户是否允许切换模型。
```

---

## 14. 与其他模块的关系

模型网关依赖：

```text
Agent 配置中心；
后端后台模型配置；
API 通道配置；
Credential 管理；
Runtime Agent Config；
Prompt；
Schema；
Context Package。
```

模型网关服务于：

```text
Orchestrator；
IntentRouter；
LLM 参数提取；
结果解释；
Trace Logger；
Evaluation。
```

模型网关不能直接：

```text
决定调用哪个业务 Agent；
执行规则判断；
调用工程工具；
修改工程数据；
跳过 Schema 校验；
跳过用户审批；
把模型输出直接当成工程结果。
```

---

## 15. MVP 验收标准

本模块完成后，应满足：

```text
1. 系统能维护可用模型清单；
2. 系统能维护 API 通道清单；
3. 前端可以展示并选择允许范围内的大模型；
4. 前端可以展示并选择允许范围内的 API 通道；
5. 后端能校验用户选择是否合法；
6. API Key 不暴露给前端；
7. Orchestrator 只调用模型网关，不直接调用具体厂商 API；
8. 模型网关能统一调用至少一个云端模型；
9. 模型网关接口预留多模型接入能力；
10. 模型网关能返回统一结构；
11. 模型网关能支持结构化输出；
12. 模型输出能交给 Schema Validator 校验；
13. 模型调用失败时能重试；
14. 主模型失败时能降级到备用模型；
15. 每次模型调用都能记录 Trace；
16. Trace 中能看到本次用了哪个模型、哪个 API 通道、是否用户手动选择；
17. 新增一个模型时，不需要修改业务 Agent 主流程。
```

---

## 16. 本模块结论

模型网关 MVP 的核心不是追求接入很多模型，而是建立统一、可控、可替换的模型调用机制。

正确设计应是：

```text
前端允许用户选择模型和 API；
后端后台管理模型清单和 API 通道；
Agent 配置中心决定默认模型策略；
模型网关负责真实 API 调用；
Orchestrator 只面对统一模型接口；
Trace 记录每次模型调用。
```

一句话：

```text
选模型在前端可见；
管模型在后端后台；
配模型在 Agent 配置中心；
调模型在模型网关；
验结果在 Schema 层；
用结果在工作流中。
```

MVP 中至少要打通：

```text
一个默认模型；
一个可选模型；
一个 API 通道配置；
一个 Credential 引用；
一次结构化输出调用；
一次失败重试；
一次 Trace 记录。
```

这样后续更换模型、增加私有化模型、增加客户自有 API，都不需要重写业务 Agent。
