# 意图：查询路基模板

## 1. 意图基本信息

| 项 | 内容 |
| --- | --- |
| 意图 ID | `subgrade_template.query` |
| 所属 Agent | `roadproto_engineering_agent` |
| 所属 Skill | `subgrade_template` |
| 所属模块 | `AGENT` |
| 验证业务模块 | `CROSS_SECTION` |
| Skill 文档 | `docs/agent/skills/subgrade_template_skill.md` |
| 通用模板 | `docs/agent/intent_rule_template.md` |
| 关联 RoadProto 功能文档 | `docs/business/cross_section/路基模板_创建.md` / `docs/business/cross_section/路基模板_编辑.md` |
| 当前状态 | 草案 |

本意图负责查询当前 DWG 中的路基模板摘要或指定模板详情。查询是只读动作，不写 CAD，默认不需要用户确认。

## 2. 用户表达

用户可能这样说：

- 当前图里有哪些路基模板？
- 查询当前路基模板参数。
- 这个路基模板的行车道宽度是多少？
- 查询刚才修改的模板参数。
- 列出所有路基模板。
- 主线路基模板有哪些部件？

不应匹配的表达：

- 创建一个路基模板。
- 修改当前模板的行车道宽度。
- 删除这个路基模板。
- 查询路面结构层模板。
- 用当前模板生成道路模型。

## 3. 意图边界

本意图负责：

- 查询路基模板列表。
- 查询当前选中模板详情。
- 按名称或 handle 查询模板。
- 返回参数摘要和关键部件信息。

本意图不负责：

- 修改任何参数。
- 删除模板。
- 创建模板。
- 生成道路模型。
- 查询非路基模板对象。

## 4. 必要参数

| 参数 | 类型 | 是否必填 | 来源 | 缺失时处理 |
| --- | --- | --- | --- | --- |
| `queryScope` | enum | 是 | 用户输入或默认规则 | 缺失时默认查询全部模板摘要 |

`queryScope` 支持：

- `All`：查询全部路基模板。
- `CurrentSelection`：查询当前选中模板。
- `ByHandle`：按 handle 查询。
- `ByName`：按名称查询。

## 5. 可选参数

| 参数 | 类型 | 默认值来源 | 说明 |
| --- | --- | --- | --- |
| `targetHandle` | string | 空 | 按 handle 查询 |
| `targetName` | string | 空 | 按名称查询 |
| `detailLevel` | `Summary` / `Detail` | `Summary` | 是否返回详细部件参数 |

## 6. 默认值规则

- 未指定目标时默认查询全部模板摘要。
- 用户说“刚才创建的 / 刚才修改的 / 上一个模板”时，优先使用会话最近对象上下文。
- 用户说“这个模板 / this template”时，不追问目标，生成 `TargetMode=PickOnExecute`，由 RoadProto 本地 Adapter 在执行时点选。
- 用户问具体参数时，`detailLevel` 默认为 `Detail`。
- 查询不补写入参数。

## 7. 校验规则

- 查询不写 CAD。
- `TargetMode=PickOnExecute` 查询时，唯一性和类型检查由 RoadProto 本地 Adapter 在 AutoCAD 点选阶段完成。
- 按名称查询匹配多个时返回候选列表，不自动选第一个。
- 查询结果不得包含不必要的大段 DWG 几何数据。

## 8. 追问规则

必须追问：

- 没有目标引用、目标名称、目标 handle，也没有可用会话上下文，但用户又不是查询全部。
- 按名称查询匹配多个模板。
- 用户要求查询的对象不是路基模板。

追问话术：

- 请问要查询哪个路基模板？
- 当前匹配到多个模板，请选择一个。
- 当前选择对象不是路基模板，请重新选择。

## 9. 确认前展示

查询类不需要确认页，但 WPF 必须展示：

- 当前 Agent、Skill、Intent。
- 查询范围。
- 目标模板。
- 查询结果摘要。
- TraceId / TaskId。

## 10. 工具绑定

| 项 | 内容 |
| --- | --- |
| 后端 Tool 名称 | `SubgradeTemplate.Query` |
| RoadProto 本地适配入口 | `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` |
| 是否写 CAD | 否 |
| 是否必须用户确认 | 否 |
| 是否需要 DryRun | 否 |

本地 Adapter 执行时可在模型空间统计全部 `DnSubgradeTemplateEntity`，也可按 handle、名称或 `TargetMode=PickOnExecute` 查询单个模板。查询结果只返回模板数量、handle、名称和部件数量等摘要，不返回大段几何点列。

## 11. 执行结果

成功时返回：

- 模板数量。
- 模板列表摘要。
- 指定模板详情。
- handle、名称、道路等级、总宽、关键部件宽度和坡度。

失败时返回：

- 未找到模板。
- 目标不唯一。
- 当前选择无效。
- 本地 Adapter 查询失败。

## 12. 日志与 Trace

必须记录：

- Agent 路由。
- Skill 识别。
- Intent 识别。
- 查询范围。
- 目标解析。
- Tool 调用。
- 查询结果摘要。
- 错误或追问。
