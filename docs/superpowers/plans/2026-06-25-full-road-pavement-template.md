# 整幅路路面结构层模板 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在横断面模块新增独立的整幅路路面结构层模板，支持从路基模板提取快照、逐部件配置结构层、DWG 持久化和 WPF/CAD 预览。

**Architecture:** 新增 domain 数据模型承载“参考路基模板快照 + 每部件内嵌路面结构层”。C++ ObjectARX 负责点选参考路基模板、创建/编辑自定义实体和 DWG 持久化，WPF 只负责参数展示、预览和用户动作写回。现有单部件 `DnPavementLayerTemplateEntity` 不改使用方式。

**Tech Stack:** C++17、ObjectARX 2021、WPF .NET Framework 4.8、UTF-8 key-value Bridge、MSBuild、RoadProto core tests、托管 Bridge tests。

---

## 文件结构

- 新增 `src/domain/cross_section/FullRoadPavementTemplateModel.h/.cpp`：整幅路模板领域数据、快照生成、刷新匹配、归一化和部件显示顺序。
- 新增 `src/application/cross_section/FullRoadPavementTemplateCreateService.h/.cpp`：创建空整幅路模板默认数据。
- 新增 `src/cad_adapter/objectarx/cross_section/DnFullRoadPavementTemplateEntity.h/.cpp`：自定义实体显示、DWG 持久化、范围、变换、后续读取口。
- 新增 `src/cad_adapter/objectarx/cross_section/FullRoadPavementTemplateDialogBridge.h/.cpp`：C++ 请求/响应文件桥接。
- 新增 `src/cad_adapter/objectarx/cross_section/ObjectArxFullRoadPavementTemplateCommand.h/.cpp`：创建、编辑、WPF 回写和参考路基模板点选流程。
- 修改 `src/modules/cross_section/CrossSectionModule.cpp`：注册三条命令和业务文档路径。
- 修改 `src/app/arx_entry/RoadProtoArxEntry.cpp`：初始化/卸载新实体类。
- 修改 `src/app/RoadProtoArx.vcxproj` 与 `tests/RoadProtoCoreTests.vcxproj`：纳入新增 C++ 源文件。
- 新增 `src/ui/wpf/RoadProto.Terrain.UI/Bridge/FullRoadPavementTemplateDialogDtos.cs`：托管 DTO。
- 新增 `src/ui/wpf/RoadProto.Terrain.UI/Bridge/FullRoadPavementTemplateDialogFile.cs`：托管请求/响应文件读写。
- 新增 `src/ui/wpf/RoadProto.Terrain.UI/Bridge/PavementLayerTemplateLayerEditorHelper.cs`：结构层编辑辅助复用。
- 新增 `src/ui/wpf/RoadProto.Terrain.UI/AutoCad/FullRoadPavementTemplateDialogCommands.cs`：托管 WPF 弹窗命令和响应转发。
- 新增 `src/ui/wpf/RoadProto.Terrain.UI/FullRoadPavementTemplateWindow.xaml/.cs`：整幅路模板 WPF 窗口。
- 修改 `src/ui/wpf/RoadProto.Terrain.UI/PavementLayerTemplateWindow.xaml.cs`：最小接入 helper，避免复制旧结构层默认/颜色逻辑。
- 修改 `src/ui/wpf/RoadProto.Terrain.UI/AutoCad/RoadProtoRibbonExtension.cs`：新增 Ribbon 按钮、DXF 双击入口。
- 修改 `tests/RoadProtoManagedBridgeTests/RoadProtoManagedBridgeTests.csproj` 与 `tests/RoadProtoManagedBridgeTests/Program.cs`：新增托管 bridge 和 WPF 源码契约测试。
- 修改 `tests/core_tests.cpp`：新增 domain、application、命令元数据、源码契约和文档契约测试。
- 新增业务文档、复用说明并更新 README、模块文档、复用目录、版本记录和测试说明。

## Task 1: Domain 红灯测试

**Files:**
- Modify: `tests/core_tests.cpp`
- Modify: `tests/RoadProtoCoreTests.vcxproj`

- [ ] **Step 1: 添加失败测试**

在 `tests/core_tests.cpp` 中新增测试函数：

```cpp
void fullRoadPavementTemplateBuildsAndRefreshesSnapshots()
{
    SubgradeTemplateData subgrade = SubgradeTemplateDefaults::create(RoadGrade::Expressway);
    FullRoadPavementTemplateData data = FullRoadPavementTemplateRules::createFromSubgradeSnapshot(
        subgrade,
        L"ABC",
        subgrade.properties.name);
    CHECK(data.properties.referenceSubgradeTemplateHandle == L"ABC");
    CHECK(!data.components.empty());
    CHECK(data.components.front().key.sameSideTypeOrdinal == 0);

    auto laneIt = std::find_if(data.components.begin(), data.components.end(), [](const auto& component) {
        return component.key.side == SubgradeSide::Left &&
            component.key.type == SubgradeComponentType::TravelLane;
    });
    CHECK(laneIt != data.components.end());
    laneIt->pavement.layers.push_back(PavementLayerTemplateLayer{});
    laneIt->pavement.layers.front().name = L"保留层";

    auto refreshed = FullRoadPavementTemplateRules::refreshFromSubgradeSnapshot(
        data,
        subgrade,
        L"DEF",
        L"刷新路基模板");
    auto refreshedLaneIt = std::find_if(refreshed.components.begin(), refreshed.components.end(), [](const auto& component) {
        return component.key.side == SubgradeSide::Left &&
            component.key.type == SubgradeComponentType::TravelLane;
    });
    CHECK(refreshedLaneIt != refreshed.components.end());
    CHECK(refreshedLaneIt->pavement.layers.size() == 1);
    CHECK(refreshedLaneIt->pavement.layers.front().name == L"保留层");
    CHECK(refreshed.properties.referenceSubgradeTemplateHandle == L"DEF");
}
```

- [ ] **Step 2: 运行测试确认失败**

运行：

```powershell
& 'D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe' tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
```

预期：编译失败，提示 `FullRoadPavementTemplateData` 或 `FullRoadPavementTemplateRules` 未定义。

## Task 2: Domain 实现

**Files:**
- Create: `src/domain/cross_section/FullRoadPavementTemplateModel.h`
- Create: `src/domain/cross_section/FullRoadPavementTemplateModel.cpp`
- Modify: `tests/RoadProtoCoreTests.vcxproj`

- [ ] **Step 1: 写最小领域模型**

实现 `FullRoadPavementTemplateProperties`、`FullRoadPavementComponentKey`、`FullRoadPavementComponentSnapshot`、`FullRoadPavementTemplateData`、`FullRoadPavementTemplateDefaults` 和 `FullRoadPavementTemplateRules`。

- [ ] **Step 2: 纳入核心测试项目**

在 `tests/RoadProtoCoreTests.vcxproj` 中加入：

```xml
<ClCompile Include="..\src\domain\cross_section\FullRoadPavementTemplateModel.cpp" />
```

- [ ] **Step 3: 运行测试确认变绿**

运行核心测试 Debug 构建和 `artifacts\x64\Debug\RoadProtoCoreTests.exe`。预期新增测试通过，输出 `All RoadProto core tests passed.`。

## Task 3: Application 与命令元数据测试

**Files:**
- Modify: `tests/core_tests.cpp`
- Modify: `tests/RoadProtoCoreTests.vcxproj`

- [ ] **Step 1: 添加失败测试**

新增测试覆盖：

```cpp
void fullRoadPavementTemplateCreateServiceReturnsEmptyTemplate()
{
    const FullRoadPavementTemplateCreateService service;
    auto result = service.create({});
    CHECK(result.succeeded);
    CHECK(result.data.properties.name == L"整幅路路面结构层模板");
    CHECK(result.data.components.empty());
}
```

同时在命令元数据测试中检查 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_CREATE`、`RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_EDIT_HANDLE`、`RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE`。

- [ ] **Step 2: 运行测试确认失败**

预期：编译失败或命令不存在。

## Task 4: Application 与 C++ 命令骨架

**Files:**
- Create: `src/application/cross_section/FullRoadPavementTemplateCreateService.h/.cpp`
- Create: `src/cad_adapter/objectarx/cross_section/ObjectArxFullRoadPavementTemplateCommand.h/.cpp`
- Modify: `src/modules/cross_section/CrossSectionModule.cpp`
- Modify: `src/app/RoadProtoArx.vcxproj`
- Modify: `tests/RoadProtoCoreTests.vcxproj`

- [ ] **Step 1: 实现应用服务**

服务返回 `FullRoadPavementTemplateDefaults::create()`，错误字段沿用现有 create service 风格。

- [ ] **Step 2: 实现命令过程函数**

先实现三条命令过程函数和空流程占位，保证模块可注册；后续任务补实体和 Bridge 实际流程。

- [ ] **Step 3: 注册命令**

在 `CrossSectionModule.cpp` 中新增三条命令，用户命令 `ribbonAttachable=true`，内部命令为 `false`，业务文档路径指向新增整幅路文档。

- [ ] **Step 4: 运行核心测试**

预期命令元数据测试通过。

## Task 5: Bridge 红灯测试与实现

**Files:**
- Modify: `tests/RoadProtoManagedBridgeTests/RoadProtoManagedBridgeTests.csproj`
- Modify: `tests/RoadProtoManagedBridgeTests/Program.cs`
- Create: `src/ui/wpf/RoadProto.Terrain.UI/Bridge/FullRoadPavementTemplateDialogDtos.cs`
- Create: `src/ui/wpf/RoadProto.Terrain.UI/Bridge/FullRoadPavementTemplateDialogFile.cs`
- Create: `src/cad_adapter/objectarx/cross_section/FullRoadPavementTemplateDialogBridge.h/.cpp`

- [ ] **Step 1: 托管 Bridge 失败测试**

测试内容：UTF-8 转义、InvariantCulture 数值、`action=pickReferenceSubgradeTemplate`、部件快照字段、每部件结构层字段往返。

- [ ] **Step 2: C++ Bridge 源码契约失败测试**

在 `tests/core_tests.cpp` 检查 C++ Bridge 源码包含 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_SHOW_WPF_DIALOG`、`pickReferenceSubgradeTemplate`、`component.` 和 `layer.` 字段前缀。

- [ ] **Step 3: 实现 DTO 与文件读写**

托管 DTO 复用 `PavementLayerTemplateDto` 和 `PavementLayerTemplateLayerDto`；新增部件 DTO 保存 `Side`、`Type`、`SameSideTypeOrdinal`、路基参数和 `Pavement`。

- [ ] **Step 4: 实现 C++ Bridge**

按现有 `PavementLayerTemplateDialogBridge` key-value 风格读写请求/响应。

- [ ] **Step 5: 运行托管 Bridge 测试与核心测试**

预期新增测试全部通过。

## Task 6: 自定义实体红灯测试与实现

**Files:**
- Modify: `tests/core_tests.cpp`
- Create: `src/cad_adapter/objectarx/cross_section/DnFullRoadPavementTemplateEntity.h/.cpp`
- Modify: `src/app/arx_entry/RoadProtoArxEntry.cpp`
- Modify: `src/app/RoadProtoArx.vcxproj`

- [ ] **Step 1: 添加源码契约失败测试**

检查：

```cpp
CHECK(fileContains("DnFullRoadPavementTemplateEntity.cpp", "DNFULLROADPAVEMENTTEMPLATEENTITY"));
CHECK(fileContains("DnFullRoadPavementTemplateEntity.cpp", "FullRoadPavementTemplateData"));
CHECK(fileContains("DnFullRoadPavementTemplateEntity.cpp", "worldDraw"));
CHECK(fileContains("RoadProtoArxEntry.cpp", "initializeFullRoadPavementTemplateEntityClass"));
```

- [ ] **Step 2: 实现实体**

实现 DXF 名 `DNFULLROADPAVEMENTTEMPLATEENTITY`、版本 `1`、`templateData()`、`setTemplateData()`、`insertionPoint()`、`setInsertionPoint()`、DWG 读写、简化绘制、范围、移动变换和初始化/卸载函数。

- [ ] **Step 3: 运行核心测试与 ARX Debug 构建**

预期核心测试通过，ARX Debug 编译通过。

## Task 7: C++ 创建/编辑/刷新流程

**Files:**
- Modify: `src/cad_adapter/objectarx/cross_section/ObjectArxFullRoadPavementTemplateCommand.cpp`
- Modify: `tests/core_tests.cpp`

- [ ] **Step 1: 添加源码契约失败测试**

检查命令源码包含 `selectTypedEntity<DnSubgradeTemplateEntity>`、`refreshFromSubgradeSnapshot`、`acedGetPoint`、`DnFullRoadPavementTemplateEntity`。

- [ ] **Step 2: 实现流程**

创建命令打开 WPF；回写命令处理取消、选择参考路基模板、确认创建和确认编辑；刷新参考模板时按领域规则保留结构层。

- [ ] **Step 3: 运行核心测试和 ARX Debug 构建**

预期通过。

## Task 8: WPF 窗口、helper 和 Ribbon

**Files:**
- Create: `src/ui/wpf/RoadProto.Terrain.UI/Bridge/PavementLayerTemplateLayerEditorHelper.cs`
- Create: `src/ui/wpf/RoadProto.Terrain.UI/AutoCad/FullRoadPavementTemplateDialogCommands.cs`
- Create: `src/ui/wpf/RoadProto.Terrain.UI/FullRoadPavementTemplateWindow.xaml/.cs`
- Modify: `src/ui/wpf/RoadProto.Terrain.UI/PavementLayerTemplateWindow.xaml.cs`
- Modify: `src/ui/wpf/RoadProto.Terrain.UI/AutoCad/RoadProtoRibbonExtension.cs`
- Modify: `tests/RoadProtoManagedBridgeTests/Program.cs`

- [ ] **Step 1: 添加托管源码契约失败测试**

检查窗口包含提示文案、参考路基模板按钮、当前路基部件、左/右按钮、上/下结构层按钮、`PavementLayerTemplatePresetFactory.Create`、`MainlineLane`、`MainlineShoulder`，并检查 Ribbon 中有 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_CREATE` 和 `DNFULLROADPAVEMENTTEMPLATEENTITY`。

- [ ] **Step 2: 实现 helper**

集中实现结构层克隆、默认层创建、颜色补齐、行车道/硬路肩默认预设。

- [ ] **Step 3: 实现托管命令**

读取 pending 请求，打开 `FullRoadPavementTemplateWindow`，写响应文件并发送 `RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE`。

- [ ] **Step 4: 实现 WPF 窗口**

完成参考模板选择/刷新、部件切换、结构层切换、新增/删除层、编辑字段、预览点击部件、无参考模板禁用确认。

- [ ] **Step 5: 更新 Ribbon 与双击**

新增按钮和 DXF 双击 handle 转发。

- [ ] **Step 6: 运行托管测试和 WPF Debug 构建**

预期托管 Bridge 测试通过，WPF Debug 构建通过。

## Task 9: 文档与版本

**Files:**
- Create: `docs/business/cross_section/整幅路路面结构层模板_创建.md`
- Create: `docs/business/cross_section/整幅路路面结构层模板_编辑.md`
- Create: `docs/business/cross_section/整幅路路面结构层模板_WPF桥接回写.md`
- Create: `docs/reuse/full_road_pavement_template.md`
- Modify: `README.md`
- Modify: `docs/modules/cross_section.md`
- Modify: `docs/modules/module_index.md`
- Modify: `docs/reuse/capability_catalog.md`
- Modify: `docs/dev/version_log.md`
- Modify: `tests/README.md`

- [ ] **Step 1: 添加文档契约失败测试**

在 `tests/core_tests.cpp` 检查 README、模块文档、复用目录、版本记录和三份业务文档包含新命令、新实体和“不接入道路模型/不做 XML/快照不自动联动”口径。

- [ ] **Step 2: 写业务文档和复用说明**

按 `docs/business/业务文档模板.md` 和 `docs/reuse/_template.md` 编写。

- [ ] **Step 3: 更新总览文档和版本记录**

记录新功能、命令、Ribbon 位置、测试范围和已知限制。

- [ ] **Step 4: 运行核心测试**

预期文档契约通过。

## Task 10: 最终验证

**Files:**
- No new files.

- [ ] **Step 1: 运行核心测试**

```powershell
& 'D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe' tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
artifacts\x64\Debug\RoadProtoCoreTests.exe
```

- [ ] **Step 2: 运行托管 Bridge 测试**

```powershell
dotnet run --project tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj
```

- [ ] **Step 3: 构建 WPF Debug**

```powershell
dotnet build src\ui\wpf\RoadProto.Terrain.UI\RoadProto.Terrain.UI.csproj -c Debug
```

- [ ] **Step 4: 构建 ARX Debug**

```powershell
& 'D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe' src\app\RoadProtoArx.vcxproj /p:Configuration=Debug /p:Platform=x64
```

- [ ] **Step 5: 检查工作区**

```powershell
git status --short
git diff --check
```

预期：无 whitespace 错误，所有改动都在 worktree 分支内。

## 自检

- 设计文档中的创建、编辑、WPF、CAD 显示、刷新参考模板、快照不联动、不接入道路模型、不做 XML 均有任务覆盖。
- 计划没有英文占位词或空白步骤。
- 类型命名统一使用 `FullRoadPavementTemplate*` 与 `DnFullRoadPavementTemplateEntity`。
- 实施顺序先测试后实现，先 domain/application，再 bridge/entity/command，再 WPF/Ribbon，再文档和最终构建。
