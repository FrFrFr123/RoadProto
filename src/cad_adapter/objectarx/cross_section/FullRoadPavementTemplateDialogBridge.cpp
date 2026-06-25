#include "cad_adapter/objectarx/cross_section/FullRoadPavementTemplateDialogBridge.h"

#ifndef ROADPROTO_TEST_BUILD
#include "acdocman.h"
#endif

#include <Windows.h>

#include <chrono>
#include <cctype>
#include <cmath>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <locale>
#include <sstream>
#include <unordered_map>
#include <utility>
#include <vector>

namespace roadproto::cad_adapter::objectarx::cross_section {
namespace {

namespace domain = roadproto::domain::cross_section;

constexpr int kMaxDialogComponents = 2000;
constexpr int kMaxDialogLayers = 1000;
constexpr int kMaxDialogTableRows = 2000;
constexpr const wchar_t* kPavementLayerKeySegment = L".pavement.layer.";

std::string wideToUtf8(const std::wstring& value)
{
    if (value.empty()) {
        return {};
    }
    const int size = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0) {
        return {};
    }
    std::string output(static_cast<std::size_t>(size), '\0');
    WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), output.data(), size, nullptr, nullptr);
    return output;
}

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

std::string escapeValue(const std::wstring& value)
{
    const auto utf8 = wideToUtf8(value);
    std::ostringstream escaped;
    escaped << std::uppercase << std::hex;
    for (const unsigned char ch : utf8) {
        if (ch == '%' || ch == '\r' || ch == '\n') {
            escaped << '%' << std::setw(2) << std::setfill('0') << static_cast<int>(ch);
        } else {
            escaped << static_cast<char>(ch);
        }
    }
    return escaped.str();
}

std::wstring unescapeValue(const std::string& value)
{
    std::string output;
    output.reserve(value.size());
    for (std::size_t i = 0; i < value.size(); ++i) {
        if (value[i] == '%' && i + 2 < value.size()
            && std::isxdigit(static_cast<unsigned char>(value[i + 1]))
            && std::isxdigit(static_cast<unsigned char>(value[i + 2]))) {
            const auto code = value.substr(i + 1, 2);
            output.push_back(static_cast<char>(std::strtoul(code.c_str(), nullptr, 16)));
            i += 2;
        } else {
            output.push_back(value[i]);
        }
    }
    return utf8ToWide(output);
}

std::wstring tempFilePath(const wchar_t* suffix)
{
    wchar_t tempPath[MAX_PATH] = {};
    GetTempPathW(MAX_PATH, tempPath);
    const auto now = std::chrono::high_resolution_clock::now().time_since_epoch().count();
    std::wstringstream name;
    name << L"RoadProtoFullRoadPavementTemplate_" << GetCurrentProcessId() << L"_" << now << suffix;
    return (std::filesystem::path(tempPath) / name.str()).wstring();
}

std::wstring pendingRequestPath()
{
    wchar_t tempPath[MAX_PATH] = {};
    GetTempPathW(MAX_PATH, tempPath);
    std::wstringstream name;
    name << L"RoadProtoFullRoadPavementTemplateDialog_" << GetCurrentProcessId() << L".pending";
    return (std::filesystem::path(tempPath) / name.str()).wstring();
}

bool writePendingRequestPath(const std::wstring& requestPath, std::wstring& errorMessage)
{
    std::ofstream stream(std::filesystem::path(pendingRequestPath()), std::ios::binary);
    if (!stream) {
        errorMessage = L"Cannot write full road pavement template dialog pending request path.";
        return false;
    }
    stream << wideToUtf8(requestPath);
    return true;
}

void writeKeyValue(std::ostream& stream, const std::wstring& key, const std::wstring& value)
{
    stream << wideToUtf8(key) << '=' << escapeValue(value) << '\n';
}

void writeKeyValue(std::ostream& stream, const std::wstring& key, const wchar_t* value)
{
    writeKeyValue(stream, key, std::wstring(value == nullptr ? L"" : value));
}

void writeKeyValue(std::ostream& stream, const std::wstring& key, bool value)
{
    stream << wideToUtf8(key) << '=' << (value ? 1 : 0) << '\n';
}

void writeKeyValue(std::ostream& stream, const std::wstring& key, int value)
{
    stream << wideToUtf8(key) << '=' << value << '\n';
}

void writeKeyValue(std::ostream& stream, const std::wstring& key, std::size_t value)
{
    stream << wideToUtf8(key) << '=' << value << '\n';
}

void writeKeyValue(std::ostream& stream, const std::wstring& key, double value)
{
    const auto previousLocale = stream.getloc();
    stream.imbue(std::locale::classic());
    stream << wideToUtf8(key) << '=' << std::setprecision(17) << value << '\n';
    stream.imbue(previousLocale);
}

template <typename T, typename CodeFunction>
std::wstring joinEnumCodes(const std::vector<T>& values, CodeFunction code)
{
    std::wstring output;
    for (const auto value : values) {
        if (!output.empty()) {
            output += L";";
        }
        output += code(value);
    }
    return output;
}

void writePavementData(std::ostream& stream, const std::wstring& prefix, const domain::PavementLayerTemplateData& data)
{
    writeKeyValue(stream, prefix + L".templateName", data.properties.name);
    writeKeyValue(stream, prefix + L".displayScale", data.properties.displayScale);
    writeKeyValue(stream, prefix + L".previewWidth", data.properties.previewWidth);
    writeKeyValue(stream, prefix + L".displayMode", domain::PavementLayerTemplateRules::displayModeCode(data.properties.displayMode));
    writeKeyValue(stream, prefix + L".showAllGeneralParameters", data.properties.showAllGeneralParameters);
    writeKeyValue(stream, prefix + L".structureCode", data.properties.structureCode);
    writeKeyValue(
        stream,
        prefix + L".subgradeMoistureTypes",
        joinEnumCodes(data.properties.subgradeMoistureTypes, domain::pavementSubgradeMoistureTypeCode));
    writeKeyValue(stream, prefix + L".pavementType", domain::pavementSurfaceTypeCode(data.properties.pavementType));
    writeKeyValue(
        stream,
        prefix + L".subgradeSoilGroups",
        joinEnumCodes(data.properties.subgradeSoilGroups, domain::pavementSubgradeSoilGroupCode));
    writeKeyValue(stream, prefix + L".designDeflection", data.properties.designDeflection);
    writeKeyValue(stream, prefix + L".cumulativeAxleLoads", data.properties.cumulativeAxleLoads);
    writeKeyValue(stream, prefix + L".layerCount", data.layers.size());

    for (std::size_t i = 0; i < data.layers.size(); ++i) {
        const auto& layer = data.layers[i];
        const auto layerPrefix = prefix + L".layer." + std::to_wstring(i);
        writeKeyValue(stream, layerPrefix + L".type", domain::pavementLayerTypeCode(layer.type));
        writeKeyValue(stream, layerPrefix + L".name", layer.name);
        writeKeyValue(stream, layerPrefix + L".uniformThickness", layer.uniformThickness);
        writeKeyValue(stream, layerPrefix + L".thickness", layer.thickness);
        writeKeyValue(stream, layerPrefix + L".innerThickness", layer.innerThickness);
        writeKeyValue(stream, layerPrefix + L".outerThickness", layer.outerThickness);
        writeKeyValue(stream, layerPrefix + L".innerWidening", layer.innerWidening);
        writeKeyValue(stream, layerPrefix + L".outerWidening", layer.outerWidening);
        writeKeyValue(stream, layerPrefix + L".innerSlope", layer.innerSlope);
        writeKeyValue(stream, layerPrefix + L".outerSlope", layer.outerSlope);
        writeKeyValue(stream, layerPrefix + L".colorR", layer.color.r);
        writeKeyValue(stream, layerPrefix + L".colorG", layer.color.g);
        writeKeyValue(stream, layerPrefix + L".colorB", layer.color.b);
        writeKeyValue(stream, layerPrefix + L".hatchPattern", layer.hatchPattern);
        writeKeyValue(stream, layerPrefix + L".hatchAngle", layer.hatchAngle);
        writeKeyValue(stream, layerPrefix + L".hatchScale", layer.hatchScale);
    }
}

void writeStationTable(
    std::ostream& stream,
    const std::wstring& prefix,
    const std::vector<domain::SubgradeStationValue>& rows)
{
    writeKeyValue(stream, prefix + L"Count", rows.size());
    for (std::size_t i = 0; i < rows.size(); ++i) {
        const auto rowPrefix = prefix + L"." + std::to_wstring(i);
        writeKeyValue(stream, rowPrefix + L".station", rows[i].station);
        writeKeyValue(stream, rowPrefix + L".value", rows[i].value);
    }
}

bool writeRequestFile(
    const FullRoadPavementTemplateDialogRequest& request,
    const std::wstring& requestPath,
    const std::wstring& responsePath,
    std::wstring& errorMessage)
{
    std::ofstream stream(std::filesystem::path(requestPath), std::ios::binary);
    if (!stream) {
        errorMessage = L"Cannot write full road pavement template dialog request file.";
        return false;
    }

    writeKeyValue(stream, L"handle", request.handle);
    writeKeyValue(stream, L"responsePath", responsePath);
#ifndef ROADPROTO_TEST_BUILD
    writeKeyValue(stream, L"insertionX", request.insertionPoint.x);
    writeKeyValue(stream, L"insertionY", request.insertionPoint.y);
    writeKeyValue(stream, L"insertionZ", request.insertionPoint.z);
#endif
    writeKeyValue(stream, L"currentComponentIndex", request.currentComponentIndex);
    writeKeyValue(stream, L"applyDefaultPresets", request.applyDefaultPresets);
    writeKeyValue(stream, L"templateName", request.data.properties.name);
    writeKeyValue(stream, L"displayScale", request.data.properties.displayScale);
    writeKeyValue(stream, L"referenceSubgradeTemplateHandle", request.data.properties.referenceSubgradeTemplateHandle);
    writeKeyValue(stream, L"referenceSubgradeTemplateName", request.data.properties.referenceSubgradeTemplateName);
    writeKeyValue(stream, L"referenceRoadGrade", domain::roadGradeCode(request.data.properties.referenceRoadGrade));
    writeKeyValue(stream, L"componentCount", request.data.components.size());

    for (std::size_t i = 0; i < request.data.components.size(); ++i) {
        const auto& component = request.data.components[i];
        const auto prefix = L"component." + std::to_wstring(i);
        writeKeyValue(stream, prefix + L".side", domain::subgradeSideCode(component.subgrade.side));
        writeKeyValue(stream, prefix + L".type", domain::subgradeComponentTypeCode(component.subgrade.type));
        writeKeyValue(stream, prefix + L".sameSideTypeOrdinal", component.key.sameSideTypeOrdinal);
        writeKeyValue(stream, prefix + L".width", component.subgrade.width);
        writeKeyValue(stream, prefix + L".height", component.subgrade.height);
        writeKeyValue(stream, prefix + L".fixedSlope", component.subgrade.fixedSlope);
        writeKeyValue(stream, prefix + L".slopeMode", domain::subgradeSlopeModeCode(component.subgrade.slopeMode));
        writeKeyValue(stream, prefix + L".colorR", component.subgrade.color.r);
        writeKeyValue(stream, prefix + L".colorG", component.subgrade.color.g);
        writeKeyValue(stream, prefix + L".colorB", component.subgrade.color.b);
        writeStationTable(stream, prefix + L".widening", component.subgrade.wideningTable);
        writeStationTable(stream, prefix + L".variableSlope", component.subgrade.variableSlopeTable);
        writeKeyValue(stream, prefix + L".hasInnerCurb", component.subgrade.hasInnerCurb);
        writeKeyValue(stream, prefix + L".innerCurbWidth", component.subgrade.innerCurbWidth);
        writeKeyValue(stream, prefix + L".innerCurbHeight", component.subgrade.innerCurbHeight);
        writeKeyValue(stream, prefix + L".innerCurbEmbedDepth", component.subgrade.innerCurbEmbedDepth);
        writeKeyValue(stream, prefix + L".hasOuterCurb", component.subgrade.hasOuterCurb);
        writeKeyValue(stream, prefix + L".outerCurbWidth", component.subgrade.outerCurbWidth);
        writeKeyValue(stream, prefix + L".outerCurbHeight", component.subgrade.outerCurbHeight);
        writeKeyValue(stream, prefix + L".outerCurbEmbedDepth", component.subgrade.outerCurbEmbedDepth);
        writePavementData(stream, prefix + L".pavement", component.pavement);
    }
    return true;
}

std::unordered_map<std::wstring, std::wstring> readKeyValueFile(const std::wstring& path)
{
    std::ifstream stream(std::filesystem::path(path), std::ios::binary);
    std::unordered_map<std::wstring, std::wstring> values;
    std::string line;
    while (std::getline(stream, line)) {
        if (!line.empty() && line.back() == '\r') {
            line.pop_back();
        }
        if (line.empty() || line[0] == '#') {
            continue;
        }
        const auto separator = line.find('=');
        if (separator == std::string::npos) {
            continue;
        }
        auto key = utf8ToWide(line.substr(0, separator));
        if (!key.empty() && key.front() == 0xFEFF) {
            key.erase(key.begin());
        }
        values[key] = unescapeValue(line.substr(separator + 1));
    }
    return values;
}

std::wstring valueOrDefault(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    const std::wstring& fallback = L"")
{
    const auto found = values.find(key);
    return found == values.end() ? fallback : found->second;
}

bool boolValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    bool fallback = false)
{
    const auto value = valueOrDefault(values, key, fallback ? L"1" : L"0");
    return value == L"1" || value == L"true" || value == L"True";
}

int intValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    int fallback = 0)
{
    try {
        std::size_t parsedLength = 0;
        const auto value = valueOrDefault(values, key);
        if (value.empty()) {
            return fallback;
        }
        const auto parsed = std::stoi(value, &parsedLength);
        return parsedLength == value.size() ? parsed : fallback;
    } catch (...) {
        return fallback;
    }
}

double doubleValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    double fallback = 0.0)
{
    try {
        std::size_t parsedLength = 0;
        const auto value = valueOrDefault(values, key);
        if (value.empty()) {
            return fallback;
        }
        const auto parsed = std::stod(value, &parsedLength);
        return parsedLength == value.size() && std::isfinite(parsed) ? parsed : fallback;
    } catch (...) {
        return fallback;
    }
}

std::vector<domain::PavementSubgradeMoistureType> parseMoistureTypeList(const std::wstring& value)
{
    std::vector<domain::PavementSubgradeMoistureType> result;
    std::wistringstream stream(value);
    std::wstring token;
    while (std::getline(stream, token, L';')) {
        const auto parsed = domain::pavementSubgradeMoistureTypeFromCode(token);
        if (token == domain::pavementSubgradeMoistureTypeCode(parsed)) {
            result.push_back(parsed);
        }
    }
    return result;
}

std::vector<domain::PavementSubgradeSoilGroup> parseSoilGroupList(const std::wstring& value)
{
    std::vector<domain::PavementSubgradeSoilGroup> result;
    std::wistringstream stream(value);
    std::wstring token;
    while (std::getline(stream, token, L';')) {
        const auto parsed = domain::pavementSubgradeSoilGroupFromCode(token);
        if (token == domain::pavementSubgradeSoilGroupCode(parsed)) {
            result.push_back(parsed);
        }
    }
    return result;
}

bool parseIntStrict(const std::wstring& value, int& result)
{
    std::wistringstream parsed(value);
    parsed.imbue(std::locale::classic());
    parsed >> result;
    return parsed && parsed.eof();
}

bool parseDoubleStrict(const std::wstring& value, double& result)
{
    std::wistringstream parsed(value);
    parsed.imbue(std::locale::classic());
    parsed >> result;
    return parsed && parsed.eof() && std::isfinite(result);
}

bool requiredValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    std::wstring& result,
    std::wstring& errorMessage)
{
    const auto found = values.find(key);
    if (found == values.end()) {
        errorMessage = L"Missing full road pavement template dialog field: " + key;
        return false;
    }
    result = found->second;
    return true;
}

bool requiredIntValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    int& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    if (!parseIntStrict(value, result)) {
        errorMessage = L"Invalid full road pavement template numeric field: " + key;
        return false;
    }
    return true;
}

bool requiredDoubleValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    double& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    if (!parseDoubleStrict(value, result)) {
        errorMessage = L"Invalid full road pavement template numeric field: " + key;
        return false;
    }
    return true;
}

bool requiredBoolValue(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    bool& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    if (value == L"1" || value == L"true" || value == L"True") {
        result = true;
        return true;
    }
    if (value == L"0" || value == L"false" || value == L"False") {
        result = false;
        return true;
    }
    errorMessage = L"Invalid full road pavement template boolean field: " + key;
    return false;
}

bool requiredSubgradeSide(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    domain::SubgradeSide& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    result = domain::subgradeSideFromCode(value);
    if (value != domain::subgradeSideCode(result)) {
        errorMessage = L"Unknown subgrade side code: " + value;
        return false;
    }
    return true;
}

bool requiredComponentType(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    domain::SubgradeComponentType& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    result = domain::subgradeComponentTypeFromCode(value);
    if (value != domain::subgradeComponentTypeCode(result)) {
        errorMessage = L"Unknown subgrade component type code: " + value;
        return false;
    }
    return true;
}

bool requiredSlopeMode(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    domain::SubgradeSlopeMode& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    result = domain::subgradeSlopeModeFromCode(value);
    if (value != domain::subgradeSlopeModeCode(result)) {
        errorMessage = L"Unknown subgrade slope mode code: " + value;
        return false;
    }
    return true;
}

bool requiredPavementLayerType(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& key,
    domain::PavementLayerType& result,
    std::wstring& errorMessage)
{
    std::wstring value;
    if (!requiredValue(values, key, value, errorMessage)) {
        return false;
    }
    result = domain::pavementLayerTypeFromCode(value);
    if (value != domain::pavementLayerTypeCode(result)) {
        errorMessage = L"Unknown pavement layer type code: " + value;
        return false;
    }
    return true;
}

bool readStationTable(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& prefix,
    std::vector<domain::SubgradeStationValue>& rows,
    std::wstring& errorMessage)
{
    int rowCount = 0;
    if (!requiredIntValue(values, prefix + L"Count", rowCount, errorMessage)) {
        return false;
    }
    if (rowCount < 0 || rowCount > kMaxDialogTableRows) {
        errorMessage = L"Full road pavement template dialog response has invalid table row count.";
        return false;
    }

    rows.clear();
    rows.reserve(static_cast<std::size_t>(rowCount));
    for (int i = 0; i < rowCount; ++i) {
        const auto rowPrefix = prefix + L"." + std::to_wstring(i);
        domain::SubgradeStationValue row;
        if (!requiredDoubleValue(values, rowPrefix + L".station", row.station, errorMessage)
            || !requiredDoubleValue(values, rowPrefix + L".value", row.value, errorMessage)) {
            return false;
        }
        rows.push_back(row);
    }
    return true;
}

bool readPavementData(
    const std::unordered_map<std::wstring, std::wstring>& values,
    const std::wstring& prefix,
    domain::PavementLayerTemplateData& data,
    std::wstring& errorMessage)
{
    data.properties.name = valueOrDefault(values, prefix + L".templateName");
    data.properties.displayScale = doubleValue(values, prefix + L".displayScale", 10.0);
    data.properties.previewWidth = doubleValue(values, prefix + L".previewWidth", 3.0);
    data.properties.displayMode = domain::PavementLayerTemplateRules::displayModeFromCode(
        valueOrDefault(values, prefix + L".displayMode", L"Color"));
    data.properties.showAllGeneralParameters = boolValue(values, prefix + L".showAllGeneralParameters", false);
    data.properties.structureCode = valueOrDefault(values, prefix + L".structureCode");
    data.properties.subgradeMoistureTypes =
        parseMoistureTypeList(valueOrDefault(values, prefix + L".subgradeMoistureTypes"));
    data.properties.pavementType =
        domain::pavementSurfaceTypeFromCode(valueOrDefault(values, prefix + L".pavementType", L"Asphalt"));
    data.properties.subgradeSoilGroups =
        parseSoilGroupList(valueOrDefault(values, prefix + L".subgradeSoilGroups"));
    data.properties.designDeflection = valueOrDefault(values, prefix + L".designDeflection");
    data.properties.cumulativeAxleLoads = valueOrDefault(values, prefix + L".cumulativeAxleLoads");

    int layerCount = 0;
    if (!requiredIntValue(values, prefix + L".layerCount", layerCount, errorMessage)) {
        return false;
    }
    if (layerCount < 0 || layerCount > kMaxDialogLayers) {
        errorMessage = L"Full road pavement template dialog response has invalid pavement layer count.";
        return false;
    }

    data.layers.clear();
    data.layers.reserve(static_cast<std::size_t>(layerCount));
    for (int i = 0; i < layerCount; ++i) {
        const auto layerPrefix = prefix + L".layer." + std::to_wstring(i);
        domain::PavementLayerTemplateLayer layer;
        if (!requiredPavementLayerType(values, layerPrefix + L".type", layer.type, errorMessage)
            || !requiredValue(values, layerPrefix + L".name", layer.name, errorMessage)
            || !requiredBoolValue(values, layerPrefix + L".uniformThickness", layer.uniformThickness, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".thickness", layer.thickness, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".innerThickness", layer.innerThickness, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".outerThickness", layer.outerThickness, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".innerWidening", layer.innerWidening, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".outerWidening", layer.outerWidening, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".innerSlope", layer.innerSlope, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".outerSlope", layer.outerSlope, errorMessage)
            || !requiredIntValue(values, layerPrefix + L".colorR", layer.color.r, errorMessage)
            || !requiredIntValue(values, layerPrefix + L".colorG", layer.color.g, errorMessage)
            || !requiredIntValue(values, layerPrefix + L".colorB", layer.color.b, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".hatchAngle", layer.hatchAngle, errorMessage)
            || !requiredDoubleValue(values, layerPrefix + L".hatchScale", layer.hatchScale, errorMessage)) {
            return false;
        }
        layer.hatchPattern = valueOrDefault(values, layerPrefix + L".hatchPattern", L"SOLID");
        data.layers.push_back(std::move(layer));
    }
    return true;
}

bool readTemplateData(
    const std::unordered_map<std::wstring, std::wstring>& values,
    domain::FullRoadPavementTemplateData& data,
    std::wstring& errorMessage)
{
    if (!requiredValue(values, L"templateName", data.properties.name, errorMessage)
        || !requiredDoubleValue(values, L"displayScale", data.properties.displayScale, errorMessage)
        || !requiredValue(values, L"referenceSubgradeTemplateHandle", data.properties.referenceSubgradeTemplateHandle, errorMessage)
        || !requiredValue(values, L"referenceSubgradeTemplateName", data.properties.referenceSubgradeTemplateName, errorMessage)) {
        return false;
    }
    data.properties.referenceRoadGrade = domain::roadGradeFromCode(
        valueOrDefault(values, L"referenceRoadGrade", L"Expressway"));

    int componentCount = 0;
    if (!requiredIntValue(values, L"componentCount", componentCount, errorMessage)) {
        return false;
    }
    if (componentCount < 0 || componentCount > kMaxDialogComponents) {
        errorMessage = L"Full road pavement template dialog response has invalid component count.";
        return false;
    }

    data.components.clear();
    data.components.reserve(static_cast<std::size_t>(componentCount));
    for (int i = 0; i < componentCount; ++i) {
        const auto prefix = L"component." + std::to_wstring(i);
        domain::FullRoadPavementComponentSnapshot component;
        if (!requiredSubgradeSide(values, prefix + L".side", component.subgrade.side, errorMessage)
            || !requiredComponentType(values, prefix + L".type", component.subgrade.type, errorMessage)
            || !requiredIntValue(values, prefix + L".sameSideTypeOrdinal", component.key.sameSideTypeOrdinal, errorMessage)
            || !requiredDoubleValue(values, prefix + L".width", component.subgrade.width, errorMessage)
            || !requiredDoubleValue(values, prefix + L".height", component.subgrade.height, errorMessage)
            || !requiredDoubleValue(values, prefix + L".fixedSlope", component.subgrade.fixedSlope, errorMessage)
            || !requiredSlopeMode(values, prefix + L".slopeMode", component.subgrade.slopeMode, errorMessage)
            || !requiredIntValue(values, prefix + L".colorR", component.subgrade.color.r, errorMessage)
            || !requiredIntValue(values, prefix + L".colorG", component.subgrade.color.g, errorMessage)
            || !requiredIntValue(values, prefix + L".colorB", component.subgrade.color.b, errorMessage)
            || !readStationTable(values, prefix + L".widening", component.subgrade.wideningTable, errorMessage)
            || !readStationTable(values, prefix + L".variableSlope", component.subgrade.variableSlopeTable, errorMessage)
            || !requiredBoolValue(values, prefix + L".hasInnerCurb", component.subgrade.hasInnerCurb, errorMessage)
            || !requiredDoubleValue(values, prefix + L".innerCurbWidth", component.subgrade.innerCurbWidth, errorMessage)
            || !requiredDoubleValue(values, prefix + L".innerCurbHeight", component.subgrade.innerCurbHeight, errorMessage)
            || !requiredDoubleValue(values, prefix + L".innerCurbEmbedDepth", component.subgrade.innerCurbEmbedDepth, errorMessage)
            || !requiredBoolValue(values, prefix + L".hasOuterCurb", component.subgrade.hasOuterCurb, errorMessage)
            || !requiredDoubleValue(values, prefix + L".outerCurbWidth", component.subgrade.outerCurbWidth, errorMessage)
            || !requiredDoubleValue(values, prefix + L".outerCurbHeight", component.subgrade.outerCurbHeight, errorMessage)
            || !requiredDoubleValue(values, prefix + L".outerCurbEmbedDepth", component.subgrade.outerCurbEmbedDepth, errorMessage)
            || !readPavementData(values, prefix + L".pavement", component.pavement, errorMessage)) {
            return false;
        }
        component.key.side = component.subgrade.side;
        component.key.type = component.subgrade.type;
        data.components.push_back(std::move(component));
    }

    if (!domain::FullRoadPavementTemplateRules::normalize(data, errorMessage)) {
        return false;
    }
    return true;
}

void removeFileIfExists(const std::wstring& path)
{
    if (path.empty()) {
        return;
    }
    std::error_code error;
    std::filesystem::remove(std::filesystem::path(path), error);
}

} // namespace

const wchar_t* fullRoadPavementTemplateDialogActionCode(FullRoadPavementTemplateDialogAction action)
{
    switch (action) {
    case FullRoadPavementTemplateDialogAction::PickReferenceSubgradeTemplate:
        return L"pickReferenceSubgradeTemplate";
    case FullRoadPavementTemplateDialogAction::None:
    default:
        return L"none";
    }
}

FullRoadPavementTemplateDialogAction fullRoadPavementTemplateDialogActionFromCode(
    const std::wstring& code)
{
    if (code == L"pickReferenceSubgradeTemplate") {
        return FullRoadPavementTemplateDialogAction::PickReferenceSubgradeTemplate;
    }
    return FullRoadPavementTemplateDialogAction::None;
}

bool queueFullRoadPavementTemplateWpfDialog(
    const FullRoadPavementTemplateDialogRequest& request,
    std::wstring& errorMessage)
{
    const auto requestPath = tempFilePath(L".request");
    const auto responsePath = request.responsePath.empty() ? tempFilePath(L".response") : request.responsePath;
    if (!writeRequestFile(request, requestPath, responsePath, errorMessage)) {
        return false;
    }
    if (!writePendingRequestPath(requestPath, errorMessage)) {
        removeFileIfExists(requestPath);
        return false;
    }

#ifdef ROADPROTO_TEST_BUILD
    return true;
#else
    auto* document = acDocManager == nullptr ? nullptr : acDocManager->curDocument();
    if (document == nullptr) {
        removeFileIfExists(pendingRequestPath());
        removeFileIfExists(requestPath);
        removeFileIfExists(responsePath);
        errorMessage = L"No active AutoCAD document is available.";
        return false;
    }

    const std::wstring command = L"RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_SHOW_WPF_DIALOG\n";
    acDocManager->sendStringToExecute(document, command.c_str(), true, false, false);
    return true;
#endif
}

bool readFullRoadPavementTemplateDialogResponse(
    const std::wstring& responsePath,
    FullRoadPavementTemplateDialogResponse& response,
    std::wstring& errorMessage)
{
    std::ifstream test(std::filesystem::path(responsePath), std::ios::binary);
    if (!test) {
        errorMessage = L"Cannot open full road pavement template dialog response file.";
        return false;
    }
    test.close();

    const auto values = readKeyValueFile(responsePath);
    if (!requiredBoolValue(values, L"accepted", response.accepted, errorMessage)) {
        return false;
    }

    response.action = fullRoadPavementTemplateDialogActionFromCode(valueOrDefault(values, L"action", L"none"));
    response.handle = valueOrDefault(values, L"handle");
    response.currentComponentIndex = intValue(values, L"currentComponentIndex", -1);

    if (!response.accepted && response.action == FullRoadPavementTemplateDialogAction::None) {
        removeFileIfExists(responsePath);
        return true;
    }

#ifndef ROADPROTO_TEST_BUILD
    double insertionX = 0.0;
    double insertionY = 0.0;
    double insertionZ = 0.0;
    if (!requiredDoubleValue(values, L"insertionX", insertionX, errorMessage)
        || !requiredDoubleValue(values, L"insertionY", insertionY, errorMessage)
        || !requiredDoubleValue(values, L"insertionZ", insertionZ, errorMessage)) {
        return false;
    }
    response.insertionPoint = AcGePoint3d(insertionX, insertionY, insertionZ);
#endif

    if (!readTemplateData(values, response.data, errorMessage)) {
        return false;
    }

    removeFileIfExists(responsePath);
    return true;
}

} // namespace roadproto::cad_adapter::objectarx::cross_section
