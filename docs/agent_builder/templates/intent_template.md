# Intent：<意图名称>

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| Intent ID | `<skill_id>.<action>` |
| 所属 Agent | `<agent_id>` |
| 所属 Skill | `<skill_id>` |
| 业务对象 | `<domain_object>` |
| 风险等级 | read / low / medium / high / blocked |
| 是否需要审批 | 是 / 否 |

## 2. 用户表达

应匹配：

- 

不应匹配：

- 

## 3. 意图边界

本意图负责：

- 

本意图不负责：

- 

## 4. 必要参数

| 参数 | 类型 | 说明 | 缺失追问 |
| --- | --- | --- | --- |

## 5. 可选参数

| 参数 | 类型 | 默认值来源 | 说明 |
| --- | --- | --- | --- |

## 6. 默认值规则

- 

## 7. 校验规则

- 

## 8. 追问规则

- 

## 9. 确认前展示

- 

## 10. Tool 绑定

| 项 | 内容 |
| --- | --- |
| Tool Name | `<tool_name>` |
| Tool Mode | read / dry_run / execute |
| Input Schema | `<schema>` |
| Output Schema | `<schema>` |

## 11. 执行结果

成功返回：

- 

失败返回：

- 

## 12. 日志与 Trace

必须记录：

- 

