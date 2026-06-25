#include "cad_adapter/objectarx/cross_section/ObjectArxFullRoadPavementTemplateCommand.h"

#ifndef ROADPROTO_TEST_BUILD
#include "app/startup/ApplicationContext.h"
#include "application/cross_section/FullRoadPavementTemplateCreateService.h"
#include "cad_adapter/common/IEditor.h"
#include "cad_adapter/objectarx/cross_section/DnFullRoadPavementTemplateEntity.h"
#include "cad_adapter/objectarx/cross_section/DnSubgradeTemplateEntity.h"
#include "cad_adapter/objectarx/cross_section/FullRoadPavementTemplateDialogBridge.h"

#include "aced.h"
#include "adscodes.h"
#include "dbapserv.h"
#include "dbsymtb.h"

#include <cwctype>
#include <sstream>
#include <string>
#endif

namespace roadproto::cad_adapter::objectarx::cross_section {
namespace {

#ifndef ROADPROTO_TEST_BUILD

using roadproto::domain::cross_section::FullRoadPavementTemplateData;
using roadproto::domain::cross_section::FullRoadPavementTemplateRules;
using roadproto::domain::cross_section::SubgradeTemplateData;

std::wstring trimDialogCommandPath(std::wstring path)
{
    while (!path.empty() && std::iswspace(path.front()) != 0) {
        path.erase(path.begin());
    }
    while (!path.empty() && std::iswspace(path.back()) != 0) {
        path.pop_back();
    }
    if (path.size() >= 2) {
        const auto first = path.front();
        const auto last = path.back();
        if ((first == L'"' && last == L'"') || (first == L'\'' && last == L'\'')) {
            path = path.substr(1, path.size() - 2);
        }
    }
    return path;
}

bool appendEntityToModelSpace(AcDbEntity* entity, AcDbObjectId& entityId)
{
    AcDbDatabase* database = acdbHostApplicationServices()->workingDatabase();
    if (database == nullptr) {
        return false;
    }

    AcDbBlockTable* blockTable = nullptr;
    if (database->getBlockTable(blockTable, AcDb::kForRead) != Acad::eOk || blockTable == nullptr) {
        return false;
    }

    AcDbBlockTableRecord* modelSpace = nullptr;
    const auto status = blockTable->getAt(ACDB_MODEL_SPACE, modelSpace, AcDb::kForWrite);
    blockTable->close();
    if (status != Acad::eOk || modelSpace == nullptr) {
        return false;
    }

    const auto appendStatus = modelSpace->appendAcDbEntity(entityId, entity);
    modelSpace->close();
    return appendStatus == Acad::eOk;
}

bool resolveObjectIdFromHandle(const std::wstring& handleText, AcDbObjectId& entityId)
{
    AcDbDatabase* database = acdbHostApplicationServices()->workingDatabase();
    if (database == nullptr || handleText.empty()) {
        return false;
    }

    const AcDbHandle handle(handleText.c_str());
    return database->getAcDbObjectId(entityId, false, handle) == Acad::eOk && !entityId.isNull();
}

std::wstring entityHandleText(AcDbEntity* entity)
{
    if (entity == nullptr) {
        return L"";
    }
    AcDbHandle handle;
    entity->getAcDbHandle(handle);
    ACHAR handleText[32] = {};
    handle.getIntoAsciiBuffer(handleText);
    return handleText;
}

bool promptInsertionPoint(AcGePoint3d& insertionPoint)
{
    ads_point raw;
    if (acedGetPoint(nullptr, L"\n请选择整幅路路面结构层模板插入位置: ", raw) != RTNORM) {
        return false;
    }
    insertionPoint = AcGePoint3d(raw[0], raw[1], raw[2]);
    return true;
}

int normalizedCurrentComponentIndex(int currentComponentIndex, const FullRoadPavementTemplateData& data)
{
    if (data.components.empty()) {
        return -1;
    }
    if (currentComponentIndex < 0 || currentComponentIndex >= static_cast<int>(data.components.size())) {
        return 0;
    }
    return currentComponentIndex;
}

void preserveWindowGeneralFields(
    const FullRoadPavementTemplateData& source,
    FullRoadPavementTemplateData& target)
{
    if (!source.properties.name.empty()) {
        target.properties.name = source.properties.name;
    }
    target.properties.displayScale = source.properties.displayScale;
}

bool queueDialogForFullRoadPavementTemplate(AcDbObjectId entityId)
{
    auto& editor = app::ApplicationContext::instance().editor();

    DnFullRoadPavementTemplateEntity* entity = nullptr;
    if (acdbOpenObject(entity, entityId, AcDb::kForRead) != Acad::eOk || entity == nullptr) {
        editor.writeError(L"无法打开整幅路路面结构层模板实体。");
        return false;
    }

    FullRoadPavementTemplateDialogRequest request;
    request.handle = entityHandleText(entity);
    request.insertionPoint = entity->insertionPoint();
    request.data = entity->templateData();
    request.currentComponentIndex = normalizedCurrentComponentIndex(0, request.data);
    entity->close();

    std::wstring errorMessage;
    if (!queueFullRoadPavementTemplateWpfDialog(request, errorMessage)) {
        editor.writeError(L"打开整幅路路面结构层模板 WPF 参数窗口失败: " + errorMessage);
        return false;
    }
    return true;
}

bool promptReferenceSubgradeTemplateForFullRoad(
    SubgradeTemplateData& subgrade,
    std::wstring& subgradeHandle,
    std::wstring& subgradeName)
{
    auto& editor = app::ApplicationContext::instance().editor();
    editor.writeMessage(L"请选择作为快照来源的路基模板实体。");

    ads_name entityName;
    ads_point pickedPoint;
    if (acedEntSel(L"\n请选择路基模板实体: ", entityName, pickedPoint) != RTNORM) {
        editor.writeWarning(L"未选择路基模板实体。");
        return false;
    }

    AcDbObjectId entityId;
    if (acdbGetObjectId(entityId, entityName) != Acad::eOk || entityId.isNull()) {
        editor.writeWarning(L"无法识别所选路基模板实体。");
        return false;
    }

    AcDbEntity* entity = nullptr;
    if (acdbOpenObject(entity, entityId, AcDb::kForRead) != Acad::eOk || entity == nullptr) {
        editor.writeError(L"无法打开所选路基模板实体。");
        return false;
    }
    if (!entity->isKindOf(DnSubgradeTemplateEntity::desc())) {
        entity->close();
        editor.writeWarning(L"选择对象不是 RoadProto 路基模板实体。");
        return false;
    }

    auto* subgradeEntity = static_cast<DnSubgradeTemplateEntity*>(entity);
    subgradeHandle = entityHandleText(subgradeEntity);
    subgrade = subgradeEntity->templateData();
    subgradeName = subgrade.properties.name.empty() ? subgradeHandle : subgrade.properties.name;
    subgradeEntity->close();
    return !subgradeHandle.empty();
}

bool handlePickReferenceSubgradeTemplateAction(const FullRoadPavementTemplateDialogResponse& response)
{
    auto& editor = app::ApplicationContext::instance().editor();

    FullRoadPavementTemplateDialogRequest request;
    request.handle = response.handle;
    request.insertionPoint = response.insertionPoint;
    request.currentComponentIndex = response.currentComponentIndex;
    request.data = response.data;
    request.applyDefaultPresets = response.data.components.empty();

    SubgradeTemplateData subgrade;
    std::wstring subgradeHandle;
    std::wstring subgradeName;
    if (promptReferenceSubgradeTemplateForFullRoad(subgrade, subgradeHandle, subgradeName)) {
        auto refreshed = response.data.components.empty()
            ? FullRoadPavementTemplateRules::createFromSubgradeSnapshot(subgrade, subgradeHandle, subgradeName)
            : FullRoadPavementTemplateRules::refreshFromSubgradeSnapshot(response.data, subgrade, subgradeHandle, subgradeName);
        preserveWindowGeneralFields(response.data, refreshed);
        request.data = std::move(refreshed);
        request.currentComponentIndex = normalizedCurrentComponentIndex(response.currentComponentIndex, request.data);
    }

    std::wstring errorMessage;
    if (!queueFullRoadPavementTemplateWpfDialog(request, errorMessage)) {
        editor.writeError(L"重新打开整幅路路面结构层模板 WPF 参数窗口失败: " + errorMessage);
        return false;
    }
    return true;
}

bool hasUsableReferenceSnapshot(const FullRoadPavementTemplateData& data)
{
    return !data.properties.referenceSubgradeTemplateHandle.empty() && !data.components.empty();
}

void writeCreatedMessage(
    roadproto::cad_adapter::IEditor& editor,
    const std::wstring& handle,
    const FullRoadPavementTemplateData& data)
{
    std::wstringstream stream;
    stream << L"已创建整幅路路面结构层模板实体，handle: " << handle
           << L"，部件数: " << data.components.size()
           << L"，参考路基模板: " << data.properties.referenceSubgradeTemplateName << L"。";
    editor.writeMessage(stream.str());
}

void runFullRoadPavementTemplateCreateCommand()
{
    auto& editor = app::ApplicationContext::instance().editor();
    editor.writeMessage(L"RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_CREATE: 正在打开整幅路路面结构层模板参数窗口。");

    const roadproto::application::cross_section::FullRoadPavementTemplateCreateService service;
    const auto result = service.create({});
    if (!result.succeeded) {
        editor.writeError(result.errorMessage.empty() ? L"整幅路路面结构层模板默认数据生成失败。" : result.errorMessage);
        return;
    }

    FullRoadPavementTemplateDialogRequest request;
    request.insertionPoint = AcGePoint3d::kOrigin;
    request.data = result.data;
    request.currentComponentIndex = normalizedCurrentComponentIndex(0, request.data);
    std::wstring errorMessage;
    if (!queueFullRoadPavementTemplateWpfDialog(request, errorMessage)) {
        editor.writeError(L"打开整幅路路面结构层模板 WPF 参数窗口失败: " + errorMessage);
    }
}

void runFullRoadPavementTemplateEditHandleCommand()
{
    auto& editor = app::ApplicationContext::instance().editor();
    ACHAR handleBuffer[64] = {};
    if (acedGetString(Adesk::kFalse, L"\n整幅路路面结构层模板 handle: ", handleBuffer) != RTNORM) {
        return;
    }

    AcDbObjectId entityId;
    if (!resolveObjectIdFromHandle(handleBuffer, entityId)) {
        editor.writeWarning(L"未找到 handle 对应的整幅路路面结构层模板实体。");
        return;
    }

    DnFullRoadPavementTemplateEntity* entity = nullptr;
    if (acdbOpenObject(entity, entityId, AcDb::kForRead) != Acad::eOk || entity == nullptr) {
        editor.writeError(L"无法打开整幅路路面结构层模板实体。");
        return;
    }
    const auto isFullRoadTemplate = entity->isKindOf(DnFullRoadPavementTemplateEntity::desc());
    entity->close();
    if (!isFullRoadTemplate) {
        editor.writeWarning(L"handle 对应对象不是整幅路路面结构层模板实体。");
        return;
    }

    queueDialogForFullRoadPavementTemplate(entityId);
}

void runFullRoadPavementTemplateApplyDialogFileCommand()
{
    auto& editor = app::ApplicationContext::instance().editor();
    ACHAR pathBuffer[1024] = {};
    if (acedGetString(Adesk::kTrue, L"\nRoadProto full road pavement template dialog response file: ", pathBuffer) != RTNORM) {
        return;
    }

    FullRoadPavementTemplateDialogResponse response;
    std::wstring errorMessage;
    const auto responsePath = trimDialogCommandPath(pathBuffer);
    if (!readFullRoadPavementTemplateDialogResponse(responsePath, response, errorMessage)) {
        editor.writeError(L"读取整幅路路面结构层模板对话框结果失败: " + errorMessage);
        return;
    }

    if (response.action == FullRoadPavementTemplateDialogAction::PickReferenceSubgradeTemplate) {
        handlePickReferenceSubgradeTemplateAction(response);
        return;
    }

    if (!response.accepted) {
        return;
    }

    if (response.handle.empty()) {
        if (!hasUsableReferenceSnapshot(response.data)) {
            editor.writeWarning(L"请先选择路基模板提取整幅路参数。");
            return;
        }
        if (!promptInsertionPoint(response.insertionPoint)) {
            editor.writeWarning(L"整幅路路面结构层模板创建已取消。");
            return;
        }

        auto* entity = new DnFullRoadPavementTemplateEntity();
        if (entity->setTemplateData(response.data) != Acad::eOk) {
            delete entity;
            editor.writeError(L"整幅路路面结构层模板对话框结果无效。");
            return;
        }
        entity->setInsertionPoint(response.insertionPoint);

        AcDbObjectId entityId;
        if (!appendEntityToModelSpace(entity, entityId)) {
            delete entity;
            editor.writeError(L"插入 DnFullRoadPavementTemplateEntity 失败。");
            return;
        }

        const auto handle = entityHandleText(entity);
        entity->close();
        acedUpdateDisplay();
        writeCreatedMessage(editor, handle, response.data);
        return;
    }

    AcDbObjectId entityId;
    if (!resolveObjectIdFromHandle(response.handle, entityId)) {
        editor.writeWarning(L"未找到对话框结果对应的整幅路路面结构层模板实体。");
        return;
    }

    DnFullRoadPavementTemplateEntity* entity = nullptr;
    if (acdbOpenObject(entity, entityId, AcDb::kForWrite) != Acad::eOk || entity == nullptr) {
        editor.writeError(L"无法打开整幅路路面结构层模板实体。");
        return;
    }
    if (!entity->isKindOf(DnFullRoadPavementTemplateEntity::desc())) {
        entity->close();
        editor.writeWarning(L"handle 对应对象不是整幅路路面结构层模板实体。");
        return;
    }

    if (!hasUsableReferenceSnapshot(response.data)) {
        entity->close();
        editor.writeWarning(L"请先选择路基模板提取整幅路参数。");
        return;
    }

    if (entity->setTemplateData(response.data) != Acad::eOk) {
        entity->close();
        editor.writeError(L"整幅路路面结构层模板对话框结果无效。");
        return;
    }
    entity->setInsertionPoint(response.insertionPoint);
    entity->close();
    acedUpdateDisplay();
    editor.writeMessage(L"整幅路路面结构层模板参数已更新。");
}

#else

void runFullRoadPavementTemplateCreateCommand()
{
}

void runFullRoadPavementTemplateEditHandleCommand()
{
}

void runFullRoadPavementTemplateApplyDialogFileCommand()
{
}

#endif

} // namespace

core::CommandProcedure fullRoadPavementTemplateCreateCommandProcedure()
{
    return &runFullRoadPavementTemplateCreateCommand;
}

core::CommandProcedure fullRoadPavementTemplateEditHandleCommandProcedure()
{
    return &runFullRoadPavementTemplateEditHandleCommand;
}

core::CommandProcedure fullRoadPavementTemplateApplyDialogFileCommandProcedure()
{
    return &runFullRoadPavementTemplateApplyDialogFileCommand;
}

} // namespace roadproto::cad_adapter::objectarx::cross_section
