#include "cad_adapter/objectarx/agent/ObjectArxAgentSubgradeTemplateToolCommand.h"

#include <algorithm>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <map>
#include <optional>
#include <sstream>
#include <string>
#include <vector>

#ifndef ROADPROTO_TEST_BUILD
#include "cad_adapter/objectarx/cross_section/DnSubgradeTemplateEntity.h"
#include "domain/cross_section/SubgradeTemplateModel.h"

#include "aced.h"
#include "adscodes.h"
#include "dbapserv.h"
#include "dbents.h"
#include "dbsymtb.h"

#include <Windows.h>
#endif

namespace roadproto::cad_adapter::objectarx::agent {
namespace {

std::wstring trimCommandPath(const wchar_t* raw)
{
    std::wstring value = raw == nullptr ? L"" : raw;
    while (!value.empty() && (value.front() == L'"' || value.front() == L' ' || value.front() == L'\t')) {
        value.erase(value.begin());
    }
    while (!value.empty() && (value.back() == L'"' || value.back() == L' ' || value.back() == L'\t')) {
        value.pop_back();
    }
    return value;
}

std::string unescapeValue(const std::string& value)
{
    std::ostringstream stream;
    for (std::size_t i = 0; i < value.size(); ++i) {
        if (value[i] == '%' && i + 2 < value.size()) {
            const auto hex = value.substr(i + 1, 2);
            char* end = nullptr;
            const auto code = std::strtol(hex.c_str(), &end, 16);
            if (end != nullptr && *end == '\0') {
                stream << static_cast<char>(code);
                i += 2;
                continue;
            }
        }
        stream << value[i];
    }
    return stream.str();
}

std::string stripUtf8Bom(const std::string& value)
{
    if (value.size() >= 3
        && static_cast<unsigned char>(value[0]) == 0xEF
        && static_cast<unsigned char>(value[1]) == 0xBB
        && static_cast<unsigned char>(value[2]) == 0xBF) {
        return value.substr(3);
    }

    return value;
}

std::string trimRequestToken(std::string value)
{
    const auto isTrimmed = [](unsigned char ch) {
        return ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n';
    };

    while (!value.empty() && isTrimmed(static_cast<unsigned char>(value.front()))) {
        value.erase(value.begin());
    }

    while (!value.empty() && isTrimmed(static_cast<unsigned char>(value.back()))) {
        value.pop_back();
    }

    return value;
}

std::string escapeValue(const std::string& value)
{
    std::ostringstream stream;
    stream << std::uppercase << std::hex;
    for (const auto ch : value) {
        const auto byte = static_cast<unsigned char>(ch);
        if (ch == '\n') {
            stream << "%0A";
        } else if (ch == '\r') {
            stream << "%0D";
        } else if (ch == '%' || ch == '=') {
            stream << '%' << std::setw(2) << std::setfill('0') << static_cast<int>(byte);
        } else {
            stream << ch;
        }
    }
    return stream.str();
}

std::map<std::string, std::string> readRequest(const std::wstring& path)
{
    std::ifstream stream{std::filesystem::path(path)};
    std::map<std::string, std::string> values;
    std::string line;
    while (std::getline(stream, line)) {
        const auto separator = line.find('=');
        if (separator == std::string::npos || separator == 0) {
            continue;
        }
        const auto key = trimRequestToken(stripUtf8Bom(line.substr(0, separator)));
        if (key.empty()) {
            continue;
        }

        values[key] = unescapeValue(trimRequestToken(line.substr(separator + 1)));
    }
    return values;
}

std::string getValue(const std::map<std::string, std::string>& values, const std::string& key)
{
    const auto item = values.find(key);
    return item == values.end() ? std::string{} : item->second;
}

void writeResult(
    const std::string& resultPath,
    bool succeeded,
    const std::string& entityId,
    const std::string& templateName,
    const std::string& message)
{
    if (resultPath.empty()) {
        return;
    }

    std::ofstream stream(resultPath, std::ios::trunc);
    stream << "succeeded=" << (succeeded ? "1" : "0") << '\n';
    stream << "entityId=" << escapeValue(entityId) << '\n';
    stream << "templateName=" << escapeValue(templateName) << '\n';
    stream << "message=" << escapeValue(message) << '\n';
}

#ifndef ROADPROTO_TEST_BUILD

using roadproto::domain::cross_section::RoadGrade;
using roadproto::domain::cross_section::SubgradeComponentOperation;
using roadproto::domain::cross_section::SubgradeComponentOperationKind;
using roadproto::domain::cross_section::SubgradeComponentOccurrence;
using roadproto::domain::cross_section::SubgradeComponentPatch;
using roadproto::domain::cross_section::SubgradeComponentPositionMode;
using roadproto::domain::cross_section::SubgradeComponentSideScope;
using roadproto::domain::cross_section::SubgradeComponentType;
using roadproto::domain::cross_section::SubgradeSide;
using roadproto::domain::cross_section::SubgradeSlopeMode;
using roadproto::domain::cross_section::SubgradeStationValue;
using roadproto::domain::cross_section::SubgradeTemplateRgbColor;
using roadproto::domain::cross_section::SubgradeTemplateRules;

std::wstring utf8ToWide(const std::string& value)
{
    if (value.empty()) {
        return {};
    }

    const int size = MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0);
    if (size <= 0) {
        return {};
    }

    std::wstring output(static_cast<std::size_t>(size), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), output.data(), size);
    return output;
}

std::string wideToUtf8(const std::wstring& value)
{
    if (value.empty()) {
        return {};
    }

    const int size = WideCharToMultiByte(
        CP_UTF8,
        0,
        value.c_str(),
        static_cast<int>(value.size()),
        nullptr,
        0,
        nullptr,
        nullptr);
    if (size <= 0) {
        return {};
    }

    std::string output(static_cast<std::size_t>(size), '\0');
    WideCharToMultiByte(
        CP_UTF8,
        0,
        value.c_str(),
        static_cast<int>(value.size()),
        output.data(),
        size,
        nullptr,
        nullptr);
    return output;
}

std::optional<double> getDouble(const std::map<std::string, std::string>& values, const std::string& key)
{
    const auto raw = getValue(values, key);
    if (raw.empty()) {
        return std::nullopt;
    }

    char* end = nullptr;
    const auto parsed = std::strtod(raw.c_str(), &end);
    if (end == raw.c_str() || (end != nullptr && *end != '\0')) {
        return std::nullopt;
    }

    return parsed;
}

std::optional<int> getInt(const std::map<std::string, std::string>& values, const std::string& key)
{
    const auto raw = getValue(values, key);
    if (raw.empty()) {
        return std::nullopt;
    }

    char* end = nullptr;
    const auto parsed = std::strtol(raw.c_str(), &end, 10);
    if (end == raw.c_str() || (end != nullptr && *end != '\0')) {
        return std::nullopt;
    }

    return static_cast<int>(parsed);
}

std::optional<bool> getBool(const std::map<std::string, std::string>& values, const std::string& key)
{
    const auto raw = getValue(values, key);
    if (raw.empty()) {
        return std::nullopt;
    }

    if (raw == "1" || raw == "true" || raw == "True" || raw == "TRUE") {
        return true;
    }
    if (raw == "0" || raw == "false" || raw == "False" || raw == "FALSE") {
        return false;
    }

    return std::nullopt;
}

std::vector<SubgradeStationValue> parseStationRows(
    const std::map<std::string, std::string>& values,
    const std::string& prefix)
{
    std::vector<SubgradeStationValue> rows;
    const auto count = getInt(values, prefix + "Count").value_or(0);
    for (int i = 0; i < count; ++i) {
        const auto rowPrefix = prefix + "." + std::to_string(i);
        const auto station = getDouble(values, rowPrefix + ".station");
        const auto value = getDouble(values, rowPrefix + ".value");
        if (station.has_value() && value.has_value()) {
            rows.push_back(SubgradeStationValue{*station, *value});
        }
    }
    return rows;
}

std::optional<SubgradeComponentType> parseComponentType(const std::string& value)
{
    if (value == "Median") {
        return SubgradeComponentType::Median;
    }
    if (value == "TravelLane") {
        return SubgradeComponentType::TravelLane;
    }
    if (value == "HardShoulder") {
        return SubgradeComponentType::HardShoulder;
    }
    if (value == "EarthShoulder") {
        return SubgradeComponentType::EarthShoulder;
    }
    if (value == "SideMedian") {
        return SubgradeComponentType::SideMedian;
    }
    if (value == "Sidewalk") {
        return SubgradeComponentType::Sidewalk;
    }
    if (value == "BikeLane") {
        return SubgradeComponentType::BikeLane;
    }
    if (value == "CurbStrip") {
        return SubgradeComponentType::CurbStrip;
    }
    return std::nullopt;
}

std::optional<SubgradeSlopeMode> parseSlopeMode(const std::string& value)
{
    if (value == "Fixed") {
        return SubgradeSlopeMode::Fixed;
    }
    if (value == "VariableByStation") {
        return SubgradeSlopeMode::VariableByStation;
    }
    return std::nullopt;
}

SubgradeComponentOperationKind parseOperationKind(const std::string& value)
{
    if (value == "addComponent") {
        return SubgradeComponentOperationKind::AddComponent;
    }
    if (value == "deleteComponent") {
        return SubgradeComponentOperationKind::DeleteComponent;
    }
    return SubgradeComponentOperationKind::ModifyComponent;
}

SubgradeComponentSideScope parseSideScope(const std::string& value)
{
    if (value == "Left") {
        return SubgradeComponentSideScope::Left;
    }
    if (value == "Right") {
        return SubgradeComponentSideScope::Right;
    }
    return SubgradeComponentSideScope::Both;
}

void parseOccurrence(const std::string& value, SubgradeComponentOperation& operation)
{
    if (value == "first") {
        operation.occurrence = SubgradeComponentOccurrence::First;
    } else if (value == "second") {
        operation.occurrence = SubgradeComponentOccurrence::Second;
    } else if (!value.empty() && value != "all") {
        char* end = nullptr;
        const auto parsed = std::strtol(value.c_str(), &end, 10);
        if (end != value.c_str() && end != nullptr && *end == '\0') {
            operation.occurrence = SubgradeComponentOccurrence::Index;
            operation.occurrenceIndex = static_cast<int>(std::max<long>(0, parsed - 1));
        }
    }
}

std::optional<SubgradeComponentPositionMode> parsePositionMode(const std::string& value)
{
    if (value == "OutsideOf") {
        return SubgradeComponentPositionMode::OutsideOf;
    }
    if (value == "InsideOf") {
        return SubgradeComponentPositionMode::InsideOf;
    }
    if (value == "Before") {
        return SubgradeComponentPositionMode::Before;
    }
    if (value == "After") {
        return SubgradeComponentPositionMode::After;
    }
    return std::nullopt;
}

SubgradeComponentPatch parsePatch(
    const std::map<std::string, std::string>& values,
    const std::string& prefix)
{
    SubgradeComponentPatch patch;
    patch.type = parseComponentType(getValue(values, prefix + ".type"));
    patch.width = getDouble(values, prefix + ".width");
    patch.widthDelta = getDouble(values, prefix + ".widthDelta");
    patch.height = getDouble(values, prefix + ".height");
    patch.fixedSlope = getDouble(values, prefix + ".fixedSlope");
    patch.slopeMode = parseSlopeMode(getValue(values, prefix + ".slopeMode"));

    const auto colorR = getInt(values, prefix + ".colorR");
    const auto colorG = getInt(values, prefix + ".colorG");
    const auto colorB = getInt(values, prefix + ".colorB");
    if (colorR.has_value() && colorG.has_value() && colorB.has_value()) {
        patch.color = SubgradeTemplateRgbColor{*colorR, *colorG, *colorB};
    }

    const auto wideningRows = parseStationRows(values, prefix + ".widening");
    if (!wideningRows.empty()) {
        patch.wideningTable = wideningRows;
    }
    const auto slopeRows = parseStationRows(values, prefix + ".slopeTable");
    if (!slopeRows.empty()) {
        patch.variableSlopeTable = slopeRows;
    }

    patch.hasInnerCurb = getBool(values, prefix + ".hasInnerCurb");
    patch.innerCurbWidth = getDouble(values, prefix + ".innerCurbWidth");
    patch.innerCurbHeight = getDouble(values, prefix + ".innerCurbHeight");
    patch.innerCurbEmbedDepth = getDouble(values, prefix + ".innerCurbEmbedDepth");
    patch.hasOuterCurb = getBool(values, prefix + ".hasOuterCurb");
    patch.outerCurbWidth = getDouble(values, prefix + ".outerCurbWidth");
    patch.outerCurbHeight = getDouble(values, prefix + ".outerCurbHeight");
    patch.outerCurbEmbedDepth = getDouble(values, prefix + ".outerCurbEmbedDepth");
    patch.pavementLayerLinked = getBool(values, prefix + ".pavementLayerLinked");

    const auto pavementLayerHandle = getValue(values, prefix + ".pavementLayerHandle");
    if (!pavementLayerHandle.empty()) {
        patch.pavementLayerHandle = utf8ToWide(pavementLayerHandle);
    }
    const auto pavementLayerName = getValue(values, prefix + ".pavementLayerName");
    if (!pavementLayerName.empty()) {
        patch.pavementLayerName = utf8ToWide(pavementLayerName);
    }
    patch.pavementLayerThickness = getDouble(values, prefix + ".pavementLayerThickness");

    return patch;
}

std::vector<SubgradeComponentOperation> parseComponentOperations(const std::map<std::string, std::string>& values)
{
    std::vector<SubgradeComponentOperation> operations;
    const auto count = getInt(values, "componentOperationCount").value_or(0);
    for (int i = 0; i < count; ++i) {
        const auto prefix = "componentOperation." + std::to_string(i);
        SubgradeComponentOperation operation;
        operation.kind = parseOperationKind(getValue(values, prefix + ".operation"));
        operation.sideScope = parseSideScope(getValue(values, prefix + ".sideScope"));
        operation.componentType = parseComponentType(getValue(values, prefix + ".componentType"));
        operation.anchorType = parseComponentType(getValue(values, prefix + ".anchorType"));
        operation.positionMode = parsePositionMode(getValue(values, prefix + ".positionMode"));
        parseOccurrence(getValue(values, prefix + ".occurrence"), operation);
        operation.patch = parsePatch(values, prefix + ".patch");
        operations.push_back(operation);
    }
    return operations;
}

bool resolveObjectIdFromHandle(const std::string& handleText, AcDbObjectId& entityId)
{
    AcDbDatabase* database = acdbHostApplicationServices()->workingDatabase();
    if (database == nullptr || handleText.empty()) {
        return false;
    }

    const AcDbHandle handle(utf8ToWide(handleText).c_str());
    return database->getAcDbObjectId(entityId, false, handle) == Acad::eOk && !entityId.isNull();
}

std::string entityHandleText(const DnSubgradeTemplateEntity* entity)
{
    AcDbHandle handle;
    entity->getAcDbHandle(handle);
    ACHAR handleText[32] = {};
    handle.getIntoAsciiBuffer(handleText);
    return wideToUtf8(handleText);
}

bool openSubgradeTemplate(
    const AcDbObjectId& entityId,
    AcDb::OpenMode mode,
    DnSubgradeTemplateEntity*& entity,
    std::string& errorMessage)
{
    entity = nullptr;
    if (acdbOpenObject(entity, entityId, mode) != Acad::eOk || entity == nullptr) {
        errorMessage = "无法打开目标路基模板实体。";
        return false;
    }

    if (!entity->isKindOf(DnSubgradeTemplateEntity::desc())) {
        entity->close();
        entity = nullptr;
        errorMessage = "目标对象不是 RoadProto 路基模板实体。";
        return false;
    }

    return true;
}

bool findSubgradeTemplateByName(const std::wstring& targetName, AcDbObjectId& entityId)
{
    AcDbDatabase* database = acdbHostApplicationServices()->workingDatabase();
    if (database == nullptr || targetName.empty()) {
        return false;
    }

    AcDbBlockTable* blockTable = nullptr;
    if (database->getBlockTable(blockTable, AcDb::kForRead) != Acad::eOk || blockTable == nullptr) {
        return false;
    }

    AcDbBlockTableRecord* modelSpace = nullptr;
    const auto modelSpaceStatus = blockTable->getAt(ACDB_MODEL_SPACE, modelSpace, AcDb::kForRead);
    blockTable->close();
    if (modelSpaceStatus != Acad::eOk || modelSpace == nullptr) {
        return false;
    }

    AcDbBlockTableRecordIterator* iterator = nullptr;
    if (modelSpace->newIterator(iterator) != Acad::eOk || iterator == nullptr) {
        modelSpace->close();
        return false;
    }

    bool found = false;
    for (; !iterator->done(); iterator->step()) {
        AcDbEntity* rawEntity = nullptr;
        if (iterator->getEntity(rawEntity, AcDb::kForRead) != Acad::eOk || rawEntity == nullptr) {
            continue;
        }

        if (rawEntity->isKindOf(DnSubgradeTemplateEntity::desc())) {
            auto* subgrade = static_cast<DnSubgradeTemplateEntity*>(rawEntity);
            if (subgrade->templateData().properties.name == targetName) {
                entityId = rawEntity->objectId();
                found = true;
                rawEntity->close();
                break;
            }
        }

        rawEntity->close();
    }

    delete iterator;
    modelSpace->close();
    return found;
}

int countAllSubgradeTemplates()
{
    AcDbDatabase* database = acdbHostApplicationServices()->workingDatabase();
    if (database == nullptr) {
        return 0;
    }

    AcDbBlockTable* blockTable = nullptr;
    if (database->getBlockTable(blockTable, AcDb::kForRead) != Acad::eOk || blockTable == nullptr) {
        return 0;
    }

    AcDbBlockTableRecord* modelSpace = nullptr;
    const auto modelSpaceStatus = blockTable->getAt(ACDB_MODEL_SPACE, modelSpace, AcDb::kForRead);
    blockTable->close();
    if (modelSpaceStatus != Acad::eOk || modelSpace == nullptr) {
        return 0;
    }

    AcDbBlockTableRecordIterator* iterator = nullptr;
    if (modelSpace->newIterator(iterator) != Acad::eOk || iterator == nullptr) {
        modelSpace->close();
        return 0;
    }

    int count = 0;
    for (; !iterator->done(); iterator->step()) {
        AcDbEntity* rawEntity = nullptr;
        if (iterator->getEntity(rawEntity, AcDb::kForRead) != Acad::eOk || rawEntity == nullptr) {
            continue;
        }
        if (rawEntity->isKindOf(DnSubgradeTemplateEntity::desc())) {
            ++count;
        }
        rawEntity->close();
    }

    delete iterator;
    modelSpace->close();
    return count;
}

bool promptSubgradeTemplateTarget(AcDbObjectId& entityId, std::string& errorMessage)
{
    ads_name entityName = {};
    ads_point point = {};
    if (acedEntSel(L"\n请选择要操作的路基模板实体: ", entityName, point) != RTNORM) {
        errorMessage = "用户取消点选路基模板。";
        return false;
    }

    if (acdbGetObjectId(entityId, entityName) != Acad::eOk || entityId.isNull()) {
        errorMessage = "未取得点选对象。";
        return false;
    }

    DnSubgradeTemplateEntity* entity = nullptr;
    if (!openSubgradeTemplate(entityId, AcDb::kForRead, entity, errorMessage)) {
        return false;
    }
    entity->close();
    return true;
}

bool resolveSubgradeTemplateTarget(
    const std::map<std::string, std::string>& values,
    AcDbObjectId& entityId,
    std::string& targetHandle,
    std::string& targetName,
    std::string& errorMessage)
{
    targetHandle = getValue(values, "targetHandle");
    targetName = getValue(values, "targetName");

    if (!targetHandle.empty()) {
        if (!resolveObjectIdFromHandle(targetHandle, entityId)) {
            errorMessage = "未找到目标路基模板 handle。";
            return false;
        }
        return true;
    }

    if (!targetName.empty()) {
        if (!findSubgradeTemplateByName(utf8ToWide(targetName), entityId)) {
            errorMessage = "未找到指定名称的路基模板。";
            return false;
        }
        return true;
    }

    if (getValue(values, "targetMode") == "PickOnExecute") {
        if (!promptSubgradeTemplateTarget(entityId, errorMessage)) {
            return false;
        }
        DnSubgradeTemplateEntity* entity = nullptr;
        if (!openSubgradeTemplate(entityId, AcDb::kForRead, entity, errorMessage)) {
            return false;
        }
        targetHandle = entityHandleText(entity);
        targetName = wideToUtf8(entity->templateData().properties.name);
        entity->close();
        return true;
    }

    errorMessage = "缺少目标路基模板。";
    return false;
}

bool applyLegacyWidthOperation(
    roadproto::domain::cross_section::SubgradeTemplateData& data,
    const std::map<std::string, std::string>& values,
    const std::string& key,
    SubgradeComponentType componentType,
    bool isDelta,
    std::wstring& errorMessage)
{
    const auto value = getDouble(values, key);
    if (!value.has_value()) {
        return true;
    }

    SubgradeComponentOperation operation;
    operation.kind = SubgradeComponentOperationKind::ModifyComponent;
    operation.sideScope = parseSideScope(getValue(values, "sideScope"));
    operation.componentType = componentType;
    operation.occurrence = SubgradeComponentOccurrence::All;
    if (isDelta) {
        operation.patch.widthDelta = *value;
    } else {
        operation.patch.width = *value;
    }

    return SubgradeTemplateRules::applyComponentOperation(data, operation, errorMessage) > 0;
}

bool applyLegacyWidthOperations(
    roadproto::domain::cross_section::SubgradeTemplateData& data,
    const std::map<std::string, std::string>& values,
    std::wstring& errorMessage)
{
    return applyLegacyWidthOperation(data, values, "laneWidth", SubgradeComponentType::TravelLane, false, errorMessage)
        && applyLegacyWidthOperation(data, values, "laneWidthDelta", SubgradeComponentType::TravelLane, true, errorMessage)
        && applyLegacyWidthOperation(data, values, "hardShoulderWidth", SubgradeComponentType::HardShoulder, false, errorMessage)
        && applyLegacyWidthOperation(data, values, "earthShoulderWidth", SubgradeComponentType::EarthShoulder, false, errorMessage)
        && applyLegacyWidthOperation(data, values, "medianWidth", SubgradeComponentType::Median, false, errorMessage);
}

std::string queryMessageFor(const DnSubgradeTemplateEntity* entity)
{
    std::ostringstream message;
    const auto& data = entity->templateData();
    message << "路基模板查询成功：名称=" << wideToUtf8(data.properties.name)
            << "；部件数=" << data.components.size() << "。";
    return message.str();
}

void runAgentSubgradeTemplateToolFileCommand()
{
    ACHAR pathBuffer[1024] = {};
    if (acedGetString(Adesk::kTrue, L"\nRoadProto Agent subgrade template tool request file: ", pathBuffer) != RTNORM) {
        return;
    }
    const auto requestPath = trimCommandPath(pathBuffer);
    const auto values = readRequest(requestPath);
    const auto resultPath = getValue(values, "resultPath");
    const auto skillId = getValue(values, "skillId");
    const auto operation = getValue(values, "operation");
    const auto requestTemplateName = getValue(values, "templateName");

    if (skillId != "subgrade_template") {
        writeResult(resultPath, false, getValue(values, "targetHandle"), requestTemplateName, "skillId 不是 subgrade_template，已拒绝执行。");
        return;
    }

    if (operation != "modify" && operation != "delete" && operation != "query") {
        writeResult(resultPath, false, getValue(values, "targetHandle"), requestTemplateName, "未知路基模板工具操作。");
        return;
    }

    if (operation == "query"
        && getValue(values, "targetHandle").empty()
        && getValue(values, "targetName").empty()
        && getValue(values, "targetMode") != "PickOnExecute") {
        const auto count = countAllSubgradeTemplates();
        writeResult(resultPath, true, "", "", "路基模板查询成功：当前模型空间数量=" + std::to_string(count) + "。");
        return;
    }

    AcDbObjectId entityId;
    std::string targetHandle;
    std::string targetName;
    std::string errorMessage;
    if (!resolveSubgradeTemplateTarget(values, entityId, targetHandle, targetName, errorMessage)) {
        writeResult(resultPath, false, targetHandle, targetName.empty() ? requestTemplateName : targetName, errorMessage);
        return;
    }

    if (operation == "query") {
        DnSubgradeTemplateEntity* entity = nullptr;
        if (!openSubgradeTemplate(entityId, AcDb::kForRead, entity, errorMessage)) {
            writeResult(resultPath, false, targetHandle, targetName, errorMessage);
            return;
        }

        const auto handle = entityHandleText(entity);
        const auto name = wideToUtf8(entity->templateData().properties.name);
        const auto message = queryMessageFor(entity);
        entity->close();
        writeResult(resultPath, true, handle, name, message);
        return;
    }

    DnSubgradeTemplateEntity* entity = nullptr;
    if (!openSubgradeTemplate(entityId, AcDb::kForWrite, entity, errorMessage)) {
        writeResult(resultPath, false, targetHandle, targetName, errorMessage);
        return;
    }

    const auto handle = entityHandleText(entity);
    auto data = entity->templateData();
    if (!requestTemplateName.empty() && operation == "modify") {
        data.properties.name = utf8ToWide(requestTemplateName);
    }
    const auto displayScale = getDouble(values, "displayScale");
    if (displayScale.has_value() && operation == "modify") {
        data.properties.displayScale = *displayScale;
    }
    const auto roadGrade = getValue(values, "roadGrade");
    if (!roadGrade.empty() && operation == "modify") {
        data.properties.roadGrade = roadproto::domain::cross_section::roadGradeFromCode(utf8ToWide(roadGrade), data.properties.roadGrade);
    }

    if (operation == "delete") {
        const auto name = wideToUtf8(data.properties.name);
        const auto eraseStatus = entity->erase();
        entity->close();
        writeResult(
            resultPath,
            eraseStatus == Acad::eOk,
            handle,
            name,
            eraseStatus == Acad::eOk ? "路基模板实体已删除。" : "删除路基模板实体失败。");
        return;
    }

    std::wstring wideError;
    if (!applyLegacyWidthOperations(data, values, wideError)) {
        entity->close();
        writeResult(resultPath, false, handle, wideToUtf8(data.properties.name), wideToUtf8(wideError));
        return;
    }

    const auto componentOperations = parseComponentOperations(values);
    for (const auto& componentOperation : componentOperations) {
        if (SubgradeTemplateRules::applyComponentOperation(data, componentOperation, wideError) == 0 && !wideError.empty()) {
            entity->close();
            writeResult(resultPath, false, handle, wideToUtf8(data.properties.name), wideToUtf8(wideError));
            return;
        }
    }

    if (!SubgradeTemplateRules::normalize(data, wideError)) {
        entity->close();
        writeResult(resultPath, false, handle, wideToUtf8(data.properties.name), wideToUtf8(wideError));
        return;
    }

    entity->setTemplateData(data);
    entity->close();
    writeResult(resultPath, true, handle, wideToUtf8(data.properties.name), "路基模板实体已修改。");
}

#else

void runAgentSubgradeTemplateToolFileCommand()
{
}

#endif

} // namespace

core::CommandProcedure agentSubgradeTemplateToolFileCommandProcedure()
{
    return &runAgentSubgradeTemplateToolFileCommand;
}

} // namespace roadproto::cad_adapter::objectarx::agent
