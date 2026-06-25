# Agent Builder 文档维护规则

## 1. 目标

本目录用于沉淀跨项目可复用的 Agent 搭建能力。它不是一次性说明，也不是 RoadProto 当前实现的附属品。每次 Agent 相关实践发生变化，都要把可复用结论同步回来。

## 2. 必须同步更新的修改类型

凡涉及以下内容，必须检查并更新 `docs/agent_builder/`：

| 修改类型 | 需要检查的文档 |
| --- | --- |
| Agent 总体架构变化 | `reusable_mvp_architecture.md`、`twelve_layer_modules.md` |
| 前端交互或面板状态变化 | `twelve_layer_modules.md`、`roadproto_practice_log.md` |
| 闲聊 / 咨询 / 工作流分流变化 | `entry_routing.md` |
| 后端服务、部署、健康检查、自动启动变化 | `reusable_mvp_architecture.md`、`roadproto_practice_log.md` |
| 模型 Provider、API Key、安全策略变化 | `reusable_agent_principles.md`、`reusable_mvp_architecture.md` |
| Agent / Skill / Intent 规则变化 | `skill_intent_tool_authoring.md`、`templates/` |
| Schema、Rule、Tool、Adapter 变化 | `twelve_layer_modules.md`、`skill_intent_tool_authoring.md` |
| DryRun、审批、回滚、执行控制变化 | `twelve_layer_modules.md`、`reusable_agent_principles.md` |
| Trace、日志、评测变化 | `twelve_layer_modules.md`、`roadproto_practice_log.md` |
| RoadProto 实践发现 bug 或修正 | `roadproto_practice_log.md` |

## 3. 原始归档规则

`original_mvp_template/` 是用户最初提供的模板模块说明归档。

规则：

- 保留原貌。
- 不做持续编辑。
- 不作为最新规范入口。
- 如需引用其中内容，应融合到本目录其他文档。

## 4. 更新顺序

Agent 相关修改建议按以下顺序同步文档：

1. 当前项目具体文档，例如 `docs/agent/`、`docs/business/agent/`。
2. 可复用抽象文档，例如 `docs/agent_builder/`。
3. 复用能力目录，例如 `docs/reuse/capability_catalog.md`。
4. 版本记录，例如 `docs/dev/version_log.md`。

## 5. 每次收尾检查清单

```text
[ ] 本次是否改变 Agent 架构或流程
[ ] 本次是否改变入口路由
[ ] 本次是否改变 Skill / Intent
[ ] 本次是否改变 Schema / Rule / Tool
[ ] 本次是否改变 Adapter / Execution
[ ] 本次是否改变 Trace / 日志 / 评测
[ ] 是否有可复用经验应写入 roadproto_practice_log.md
[ ] 是否需要更新模板文件
[ ] 是否需要更新 docs/reuse/capability_catalog.md
[ ] 是否需要更新 docs/dev/version_log.md
```

## 6. 文档质量要求

- 写成跨项目方法，不只写 RoadProto 当前实现。
- 保留实践来源，避免凭空泛化。
- 明确“不做范围”。
- 明确责任层，不把前端、模型、规则、工具、Adapter 混写。
- 新规则要能落到 Schema、测试或 Trace。

