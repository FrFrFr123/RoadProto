# 整幅路路面结构层模板_WPF桥接回写

## 基本信息

- 功能名称：整幅路路面结构层模板 WPF 桥接回写
- 所属模块：横断面设计
- 命令名称：`RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE`
- 对应代码入口：`src/cad_adapter/objectarx/cross_section/ObjectArxFullRoadPavementTemplateCommand.cpp`
- 业务文档维护人：RoadProto 项目
- 原型版本：`v0.1.36`
- 是否可复用：部分可复用

## 功能背景

整幅路模板窗口仍遵守 RoadProto “C++ ObjectARX 核心 + WPF UI”边界。WPF 只负责参数编辑、部件预览、点选动作发起和响应文件写出；C++ 负责 CAD 对象点选、类型校验、路基模板读取、领域规则刷新、实体创建和 DWG 回写。

## 业务目标

- 通过 UTF-8 key-value 临时文件在 C++ 与 WPF 之间传递整幅路模板数据。
- 支持 WPF 返回三类动作：取消、确认、点选/刷新参考路基模板。
- C++ 收到点选动作后提示用户选择 `DnSubgradeTemplateEntity`，并重新打开 WPF。
- C++ 收到确认动作后，新建或更新 `DnFullRoadPavementTemplateEntity`。
- 桥接字段覆盖模板通用参数、参考路基模板信息、部件快照、变宽/变坡表、路缘石和每部件结构层。
- WPF 不直接操作 `AcDbEntity`、`AcDbObjectId`、`ads_name`。

## 输入条件

- CAD 选择对象：点选动作中由 C++ 提示选择参考路基模板；确认动作中由 handle 定位既有整幅路模板。
- 用户输入参数：WPF 写出的响应文件路径。
- 已有设计实体：可选既有 `DnFullRoadPavementTemplateEntity`，可选参考 `DnSubgradeTemplateEntity`。
- 外部数据：WPF 请求/响应临时文件。

## 输出结果

- CAD 图形实体：新建或更新 `DnFullRoadPavementTemplateEntity`
- 领域实体：`FullRoadPavementTemplateData`
- 表格或报告：无
- 更新通知或重建请求：无。

## 操作流程

1. C++ 通过 `FullRoadPavementTemplateDialogBridge` 写出请求文件。
2. C++ 发送 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_SHOW_WPF_DIALOG`。
3. WPF 读取请求，打开 `FullRoadPavementTemplateWindow`。
4. 用户点击 `选择/刷新参考路基模板` 时，WPF 写出 `action=pickReferenceSubgradeTemplate` 响应并发送 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE <responsePath>`。
5. C++ 读取响应，提示用户点选 `DnSubgradeTemplateEntity`，生成或刷新快照，并重新进入步骤 1。
6. 用户点击确认时，WPF 写出 `accepted=true` 响应。
7. C++ 新建流程在读取确认响应后提示点取插入点；编辑流程直接回写既有实体。
8. 用户取消时，C++ 不创建或更新实体。

## 桥接字段

- 模板字段：`handle`、`templateName`、`displayScale`、`insertionX/Y/Z`、`currentComponentIndex`。
- 参考路基模板字段：`referenceSubgradeTemplateHandle`、`referenceSubgradeTemplateName`、`referenceRoadGrade`。
- 部件字段：`component.N.side`、`component.N.type`、`component.N.sameSideTypeOrdinal`、`width`、`height`、`fixedSlope`、`slopeMode`、`color`。
- 部件表字段：`component.N.wideningTable.*`、`component.N.variableSlopeTable.*`。
- 路缘石字段：`component.N.innerCurb.*`、`component.N.outerCurb.*`。
- 结构层字段：`component.N.pavement.*` 和 `component.N.pavement.layer.M.*`，语义复用既有 `PavementLayerTemplateData`。

## 关键业务规则

- 响应文件采用 UTF-8 key-value 文本格式。
- `pickReferenceSubgradeTemplate` 是窗口请求 CAD 点选的动作，不代表用户已确认创建或更新实体。
- WPF 发起点选动作时必须保留当前窗口里已编辑的模板数据，C++ 重新打开窗口时继续携带这些数据。
- 新建流程确认后才点取整幅路模板实体插入点；窗口打开前不点取插入点。
- 编辑流程沿用实体已有插入点，不因确认动作重新点取插入点。
- C++ 侧读取参考路基模板时，只生成部件快照，不建立自动联动关系。
- 刷新参考路基模板时，领域规则按 `侧别 + 类型 + 同侧同类型序号` 保留结构层。
- 桥接层只做数据转换和调用转发，不实现结构层几何算法。
- 本次不写入 XML 导入导出字段。

## 可复用性说明

- 可复用内容：整幅路模板 Bridge 协议、WPF 点选动作往返、每部件内嵌结构层字段。
- 临时原型内容：临时文件路径和命令行回调。
- 正式复用前需要改造的内容：进程内托管/非托管调用、字段级错误提示、模板差异比对。

## 与其他模块的依赖关系

- 上游模块：`DnSubgradeTemplateEntity`。
- 下游模块：`DnFullRoadPavementTemplateEntity`，后续道路模型和横断面图配置读取入口。
- 实体联动行为：无自动联动。

## 后续对接正式 EICAD 功能的注意事项

- 后续可把整幅路模板作为横断面图配置的默认结构层来源。
- 后续应补充引用方重建提示和模板版本识别。
