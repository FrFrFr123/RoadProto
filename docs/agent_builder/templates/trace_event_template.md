# Trace 事件模板

## 1. Trace 基本字段

| 字段 | 说明 |
| --- | --- |
| `traceId` | 单次用户任务追踪 ID |
| `sessionId` | 会话 ID |
| `taskId` | Agent Run ID |
| `timestamp` | 事件时间 |
| `stage` | 阶段 |
| `level` | Info / Warning / Error |
| `message` | 可读消息 |
| `data` | 结构化数据 |

## 2. 推荐阶段

```text
InputReceived
EntryRouted
AgentRouted
SkillRouted
IntentRecognized
ParamsExtracted
SchemaValidated
RulesApplied
AwaitingUserInput
PlanGenerated
DryRunExecuted
AwaitingUserConfirmation
UserConfirmed
ToolDispatchRequested
AdapterExecuted
ToolResultPosted
RunSucceeded
RunFailed
UserCancelled
```

## 3. 敏感信息规则

- 不记录明文 API Key。
- 不记录完整用户私密配置。
- 大对象只记录摘要、数量、handle 或 ID。
- 错误堆栈进入后端日志，前端只展示可理解错误。

