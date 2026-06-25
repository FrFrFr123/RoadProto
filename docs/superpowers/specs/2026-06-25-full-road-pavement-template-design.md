# 整幅路路面结构层模板设计

## 背景

现有 `DnPavementLayerTemplateEntity` 是“单个路基部件”的路面结构层模板。它通过路基模板部件保存 handle 引用，后续道路模型生成时按部件读取该单部件模板。本次新增的“整幅路路面结构层模板”是横断面模块里的独立模板功能，目标是让用户基于一个 `DnSubgradeTemplateEntity` 快照，一次性看到整幅路所有部件，并逐个部件配置结构层。

新功能不改变现有单部件路面结构层模板的使用方式；它新增独立实体 `DnFullRoadPavementTemplateEntity`，把参考路基模板的部件参数快照和每个部件的结构层配置直接保存在同一个实体里。

## 设计目标

- 在 `RoadProto / 横断面设计` Ribbon 中新增“整幅路路面结构层模板”入口。
- 新增用户命令 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_CREATE`。
- 新增内部命令 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_EDIT_HANDLE` 和 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE`。
- 新增独立自定义实体 `DnFullRoadPavementTemplateEntity`，支持 DWG 持久化、CAD 显示和双击编辑。
- 参考路基模板只作为参数快照来源，不建立自动联动关系。
- 未选择参考路基模板时，WPF 预览区提示“请选择路基模板提取参数”，结构层编辑区禁用，不能确认生成实体。
- 创建流程先打开 WPF 参数窗口，确认后再回到 CAD 点取插入点。
- WPF 组织沿用现有路面结构层模板窗口：左侧预览，右侧参数，当前结构层编辑、层新增删除、材料推荐、颜色、填充、厚度/加宽/坡度一致复选框保持旧交互语义。
- 新窗口增加参考路基模板、当前路基部件、左/右部件切换；原有上/下结构层切换继续保留。
- 预览图显示所有路基部件和所有已配置结构层，并支持点选部件切换当前部件。
- 只给行车道、硬路肩自动套用现有预设；其他部件默认无结构层。
- 本次不接入道路模型、横断面图生成、工程量统计或 XML 导入导出，只预留后续按 handle 读取整幅路模板数据的口子。

## 不做范围

- 不修改 `DnPavementLayerTemplateEntity` 的创建、编辑、引用或 XML 导入导出流程。
- 不把整幅路模板自动接入 `RoadModelBuilder` 或横断面图配置。
- 不把参考路基模板注册为实体依赖，不做自动联动重建。
- 不做 `.rpavement.xml` 或新的 XML 流转格式。
- 不在 WPF 中直接读取 CAD 实体；参考路基模板点选和实体读取仍由 C++ ObjectARX 完成。

## 领域模型

新增 `src/domain/cross_section/FullRoadPavementTemplateModel.*`。

核心数据：

- `FullRoadPavementTemplateProperties`
  - `name`：模板名称。
  - `displayScale`：显示比例。
  - `referenceSubgradeTemplateHandle`：参考路基模板 handle。
  - `referenceSubgradeTemplateName`：参考路基模板名称。
  - `referenceRoadGrade`：参考路基模板道路等级。
- `FullRoadPavementComponentKey`
  - `side`：左侧或右侧。
  - `type`：路基部件类型。
  - `sameSideTypeOrdinal`：同侧同类型序号，从 `0` 开始。
- `FullRoadPavementComponentSnapshot`
  - `key`：部件匹配键。
  - `subgrade`：参考路基模板部件快照，直接复用 `SubgradeTemplateComponent`，保存宽度、坡度、类型、颜色、变宽表、坡度变化表、内外侧路缘石宽度/高度/埋深等参数。
  - `pavement`：该部件内嵌的 `PavementLayerTemplateData`。
- `FullRoadPavementTemplateData`
  - `properties`。
  - `components`：所有路基部件快照和各自结构层数据。

领域规则：

- `FullRoadPavementTemplateDefaults::create()` 创建空整幅路模板，默认名称为“整幅路路面结构层模板”，默认显示比例沿用横断面模板比例。
- `FullRoadPavementTemplateRules::createFromSubgradeSnapshot(...)` 从 `SubgradeTemplateData` 生成部件快照，保留路基部件当前参数，不引用单部件结构层模板 handle。
- `FullRoadPavementTemplateRules::refreshFromSubgradeSnapshot(...)` 刷新参考路基模板时，按 `侧别 + 类型 + 同侧同类型序号` 匹配旧部件；匹配成功时保留旧部件 `pavement.layers` 和结构层通用参数，只刷新路基快照字段；匹配失败的新部件结构层为空。
- `FullRoadPavementTemplateRules::normalize(...)` 归一化显示比例、参考信息、部件快照、结构层数据和同侧同类型序号。
- 部件显示顺序按横断面图面几何：左侧由外到内，再右侧由内到外。左/右按钮按该顺序循环切换。
- 自动预设不放入 C++ domain，以避免复制 WPF 文档预设。WPF 收到没有结构层的新行车道或硬路肩部件时，分别调用现有 `PavementLayerTemplatePresetFactory.Create(Asphalt, MainlineLane)` 和 `PavementLayerTemplatePresetFactory.Create(Asphalt, MainlineShoulder)` 初始化；其他部件保持空。

## Application 与命令

新增 `FullRoadPavementTemplateCreateService`，只生成空模板默认数据，不处理 CAD 点选和 WPF 交互。

C++ 命令入口新增在横断面模块：

- `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_CREATE`
  - 生成空 `FullRoadPavementTemplateData`。
  - 写入 WPF 请求文件并打开 `FullRoadPavementTemplateWindow`。
  - 不先点插入点。
- `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_EDIT_HANDLE`
  - 按 handle 打开既有 `DnFullRoadPavementTemplateEntity`。
  - 读取实体保存的参考路基信息、部件快照和每部件结构层。
- `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE`
  - 读取 WPF 响应。
  - 如果动作为选择/刷新参考路基模板，则回到 CAD 点选 `DnSubgradeTemplateEntity`，读取其模板数据，生成或刷新整幅路快照，再重新打开 WPF。
  - 如果用户取消，则不修改实体。
  - 如果用户确认且没有参考路基模板或没有部件，提示错误并拒绝创建。
  - 如果确认创建新实体，则提示点取插入点，然后创建 `DnFullRoadPavementTemplateEntity`。
  - 如果确认编辑既有实体，则回写原实体。

命令注册只能通过 `CommandRegistry`，Ribbon 面板只能通过横断面模块和托管 Ribbon 同步暴露。

## Bridge 协议

新增 `FullRoadPavementTemplateDialogBridge.*` 和托管端 `FullRoadPavementTemplateDialogFile.cs`。

请求/响应仍采用现有 UTF-8 key-value 临时文件模式，字段包括：

- 通用字段：`accepted`、`action`、`handle`、`responsePath`、`name`、`displayScale`、插入点。
- 参考字段：`referenceSubgradeTemplateHandle`、`referenceSubgradeTemplateName`、`referenceRoadGrade`。
- 部件字段：部件数量、当前部件索引、每个部件的 side、type、sameSideTypeOrdinal、width、height、fixedSlope、slopeMode、color、变宽表、坡度变化表、内外侧路缘石字段。
- 每部件结构层字段：复用 `PavementLayerTemplateProperties` 与 `PavementLayerTemplateLayer` 语义，字段名带部件索引前缀，例如 `component.0.layer.0.type`。

动作：

- `none`：普通确认或取消。
- `pickReferenceSubgradeTemplate`：WPF 请求 C++ 回到 CAD 选择或刷新参考路基模板。

Bridge 只做数据转换、临时文件读写和命令转发，不实现结构层几何算法。

## WPF 交互

新增 `FullRoadPavementTemplateWindow.xaml/.cs`。

窗口结构：

- 左侧大预览。
- 右侧参数区。
- 顶部通用参数：模板名称、显示比例、参考路基模板。
- 当前路基部件区：显示“当前路基部件：左侧行车道”等文本，提供左/右按钮。
- 当前结构层区：沿用旧模板的上/下按钮、当前层输入、结构层类型、材料名称、颜色、显示方式、填充类型、填充角度、填充比例、厚度、加宽、坡度、一致复选框、新增/删除层。
- 未选参考路基模板时，预览区显示“请选择路基模板提取参数”，结构层编辑区禁用，确认按钮禁用。
- 当前部件无结构层时显示“当前部件未配置结构层”，仍提供“新增结构层”入口。

复用策略：

- 抽取 `PavementLayerTemplateLayerEditorHelper`，集中放置结构层 DTO 克隆、默认层创建、层颜色补齐、部件类型到默认预设的映射等编辑辅助逻辑。
- 旧 `PavementLayerTemplateWindow` 可保留原布局，但在默认层创建、颜色补齐或克隆处调用该 helper，避免整幅路窗口复制全部结构层数据规则。
- 新窗口复用现有 `PavementLayerTemplateDialogDtos`、`PavementLayerTemplatePresetFactory` 和 `PavementLayerTemplateLabels`。

预览规则：

- 显示所有参考路基部件的轮廓、颜色和中线。
- 对每个部件绘制其结构层色块；结构层几何复用现有单部件结构层语义，内侧仍表示更靠近道路中线一侧，外侧表示更远离道路中线一侧。
- 每个部件建立透明命中区域，点击后切换当前路基部件。
- CAD 实体显示比 WPF 简化，不绘制复杂尺寸标注和每层详细引线文字。

## CAD 自定义实体

新增 `DnFullRoadPavementTemplateEntity`。

实体保存：

- `FullRoadPavementTemplateData`。
- 插入点、局部 X/Y 方向。

实体显示：

- 绘制完整整幅路示意：所有参考路基部件、已配置结构层、中线标记、模板名称和参考路基模板名称。
- 路基部件使用快照颜色和轮廓。
- 结构层使用每层保存 RGB 生成弱化填充和边线。
- 不绘制复杂尺寸标注，不绘制每层详细引线文字。
- 提供几何范围、移动变换和 DWG 保存重开。

DWG 持久化：

- 数据版本从 `1` 开始。
- 写入参考模板信息、道路等级、部件数量、每个部件快照字段和每部件结构层字段。
- 设置合理上限，避免损坏文件造成无限读取，例如部件数量、变宽表、坡度表和结构层数量。

后续读取口：

- 实体提供 `const FullRoadPavementTemplateData& templateData() const` 与 `setTemplateData(...)`。
- 后续道路模型或横断面图配置可按 handle 打开实体并读取 `FullRoadPavementTemplateData`，再按部件匹配使用，但本次不接入。

## 刷新参考路基模板

刷新流程：

1. WPF 当前数据写入响应文件，`action=pickReferenceSubgradeTemplate`。
2. C++ 点选 `DnSubgradeTemplateEntity`，读取其 `SubgradeTemplateData`、handle 和名称。
3. C++ 使用领域规则生成新快照。
4. 对每个新部件计算 `side + type + sameSideTypeOrdinal`。
5. 找到旧部件匹配键时，把旧部件内嵌结构层复制到新部件。
6. 未匹配的新部件结构层为空，WPF 打开后只对行车道和硬路肩补默认预设。
7. 重新打开 WPF，用户继续编辑。

## 文档与测试

新增业务文档：

- `docs/business/cross_section/整幅路路面结构层模板_创建.md`
- `docs/business/cross_section/整幅路路面结构层模板_编辑.md`
- `docs/business/cross_section/整幅路路面结构层模板_WPF桥接回写.md`

新增复用说明：

- `docs/reuse/full_road_pavement_template.md`

同步更新：

- `README.md`
- `docs/modules/cross_section.md`
- `docs/modules/module_index.md`
- `docs/reuse/capability_catalog.md`
- `docs/dev/version_log.md`
- `tests/README.md`

测试覆盖：

- domain：快照生成、几何顺序、按 `侧别 + 类型 + 序号` 刷新保留结构层、未匹配新部件为空。
- application：默认整幅路模板创建。
- modules：三条命令元数据、业务文档路径、Ribbon attach。
- source contract：实体初始化卸载、DWG 字段、CAD 绘制重点字段、托管 Ribbon 按钮、双击编辑入口。
- managed bridge：请求/响应文件 UTF-8、InvariantCulture、嵌套部件结构层字段、选择参考路基模板动作、行车道/硬路肩默认预设、无 XML 入口。

## 风险与处理

- WPF 旧窗口代码较大，直接拆大组件风险高。本次只抽取结构层数据编辑 helper，避免大规模 UI 重构。
- 整幅路实体的 CAD 显示需要复用路基部件和结构层几何语义，但不追求完全等同 WPF 标注细节，减少 ObjectARX 绘制复杂度。
- 参考路基模板刷新采用快照，不联动。文档和 UI 文案必须清楚表达“刷新”是手动动作。
- 旧道路模型仍按路基部件 handle 引用单部件模板生成结构层。本次整幅路模板只提供后续读取入口，避免改动道路模型行为面。
