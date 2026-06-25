#include "cad_adapter/objectarx/cross_section/DnFullRoadPavementTemplateEntity.h"

#include "acgi.h"
#include "acutmem.h"
#include "dbcolor.h"
#include "dbproxy.h"
#include "rxregsvc.h"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <utility>
#include <vector>

using roadproto::domain::cross_section::FullRoadPavementComponentSnapshot;
using roadproto::domain::cross_section::FullRoadPavementTemplateData;
using roadproto::domain::cross_section::FullRoadPavementTemplateDefaults;
using roadproto::domain::cross_section::FullRoadPavementTemplateRules;
using roadproto::domain::cross_section::PavementLayerSectionLayer;
using roadproto::domain::cross_section::PavementLayerSectionPoint;
using roadproto::domain::cross_section::PavementLayerTemplateData;
using roadproto::domain::cross_section::PavementLayerTemplateDisplayColor;
using roadproto::domain::cross_section::PavementLayerTemplateDisplayMode;
using roadproto::domain::cross_section::PavementLayerTemplateLayer;
using roadproto::domain::cross_section::PavementLayerTemplateRules;
using roadproto::domain::cross_section::PavementLayerType;
using roadproto::domain::cross_section::PavementSubgradeMoistureType;
using roadproto::domain::cross_section::PavementSubgradeSoilGroup;
using roadproto::domain::cross_section::RoadGrade;
using roadproto::domain::cross_section::SubgradeComponentType;
using roadproto::domain::cross_section::SubgradeSide;
using roadproto::domain::cross_section::SubgradeSlopeMode;
using roadproto::domain::cross_section::SubgradeStationValue;
using roadproto::domain::cross_section::SubgradeTemplateComponent;
using roadproto::domain::cross_section::SubgradeTemplateRgbColor;
using roadproto::domain::cross_section::SubgradeTemplateRules;
using roadproto::domain::cross_section::pavementSubgradeMoistureTypeCode;
using roadproto::domain::cross_section::pavementSubgradeMoistureTypeFromCode;
using roadproto::domain::cross_section::pavementSubgradeSoilGroupCode;
using roadproto::domain::cross_section::pavementSubgradeSoilGroupFromCode;
using roadproto::domain::cross_section::pavementSurfaceTypeCode;
using roadproto::domain::cross_section::pavementSurfaceTypeFromCode;

ACRX_DXF_DEFINE_MEMBERS(
    DnFullRoadPavementTemplateEntity,
    AcDbEntity,
    AcDb::kDHL_CURRENT,
    AcDb::kMReleaseCurrent,
    AcDbProxyEntity::kAllAllowedBits,
    DNFULLROADPAVEMENTTEMPLATEENTITY,
    "RoadProto Full Road Pavement Template");

namespace {

constexpr Adesk::Int16 kEntityVersion = 1;
constexpr Adesk::Int32 kMaxComponents = 2000;
constexpr Adesk::Int32 kMaxLayers = 1000;
constexpr Adesk::Int32 kMaxTableRows = 10000;
constexpr double kMinAxisLength = 1.0e-9;
constexpr double kMaxAxisLength = 1.0e9;
constexpr double kMinAxisSine = 1.0e-6;
constexpr double kExtentsPadding = 12.0;
constexpr double kPreviewFillOpacity = 74.0 / 255.0;
constexpr int kPreviewBackgroundR = 21;
constexpr int kPreviewBackgroundG = 26;
constexpr int kPreviewBackgroundB = 32;
constexpr Adesk::UInt8 kOpaqueTransparencyAlpha = 255;
constexpr double kPi = 3.14159265358979323846;

std::wstring readWideString(AcDbDwgFiler* filer)
{
    ACHAR* value = nullptr;
    filer->readString(&value);
    std::wstring result = value == nullptr ? L"" : value;
    acutDelString(value);
    return result;
}

void writeWideString(AcDbDwgFiler* filer, const std::wstring& value)
{
    filer->writeString(value.c_str());
}

bool readBool(AcDbDwgFiler* filer)
{
    Adesk::Int8 value = 0;
    filer->readInt8(&value);
    return value != 0;
}

void writeBool(AcDbDwgFiler* filer, bool value)
{
    filer->writeInt8(value ? 1 : 0);
}

bool isFinitePoint(const AcGePoint3d& point)
{
    return std::isfinite(point.x) && std::isfinite(point.y) && std::isfinite(point.z);
}

bool isFiniteVector(const AcGeVector3d& vector)
{
    return std::isfinite(vector.x) && std::isfinite(vector.y) && std::isfinite(vector.z);
}

bool areAxesUsable(const AcGeVector3d& xAxis, const AcGeVector3d& yAxis)
{
    if (!isFiniteVector(xAxis) || !isFiniteVector(yAxis)) {
        return false;
    }
    const auto xLength = xAxis.length();
    const auto yLength = yAxis.length();
    if (!std::isfinite(xLength) || !std::isfinite(yLength)
        || xLength < kMinAxisLength || yLength < kMinAxisLength
        || xLength > kMaxAxisLength || yLength > kMaxAxisLength) {
        return false;
    }
    const auto crossLength = xAxis.crossProduct(yAxis).length();
    return std::isfinite(crossLength)
        && crossLength / (xLength * yLength) >= kMinAxisSine;
}

void resetDefaultAxes(AcGeVector3d& xAxis, AcGeVector3d& yAxis)
{
    xAxis = AcGeVector3d::kXAxis;
    yAxis = AcGeVector3d::kYAxis;
}

Acad::ErrorStatus checkFilerStatus(AcDbDwgFiler* filer)
{
    const auto status = filer == nullptr ? Acad::eInvalidInput : filer->filerStatus();
    return status == Acad::eOk ? Acad::eOk : status;
}

bool isValidRoadGradeValue(Adesk::Int32 value)
{
    return value >= static_cast<Adesk::Int32>(RoadGrade::Expressway)
        && value <= static_cast<Adesk::Int32>(RoadGrade::UrbanBranch);
}

bool isValidSubgradeSideValue(Adesk::Int32 value)
{
    return value == static_cast<Adesk::Int32>(SubgradeSide::Left)
        || value == static_cast<Adesk::Int32>(SubgradeSide::Right);
}

bool isValidSubgradeComponentTypeValue(Adesk::Int32 value)
{
    return value >= static_cast<Adesk::Int32>(SubgradeComponentType::Median)
        && value <= static_cast<Adesk::Int32>(SubgradeComponentType::CurbStrip);
}

bool isValidSubgradeSlopeModeValue(Adesk::Int32 value)
{
    return value == static_cast<Adesk::Int32>(SubgradeSlopeMode::Fixed)
        || value == static_cast<Adesk::Int32>(SubgradeSlopeMode::VariableByStation);
}

bool isValidPavementLayerTypeValue(Adesk::Int32 value)
{
    return value >= static_cast<Adesk::Int32>(PavementLayerType::UpperSurface)
        && value <= static_cast<Adesk::Int32>(PavementLayerType::ApproachSlab);
}

void writeMoistureTypes(AcDbDwgFiler* filer, const std::vector<PavementSubgradeMoistureType>& values)
{
    filer->writeInt32(static_cast<Adesk::Int32>(values.size()));
    for (const auto value : values) {
        writeWideString(filer, pavementSubgradeMoistureTypeCode(value));
    }
}

std::vector<PavementSubgradeMoistureType> readMoistureTypes(AcDbDwgFiler* filer)
{
    Adesk::Int32 count = 0;
    filer->readInt32(&count);
    std::vector<PavementSubgradeMoistureType> values;
    if (count < 0 || count > kMaxLayers) {
        return values;
    }
    values.reserve(static_cast<std::size_t>(count));
    for (Adesk::Int32 i = 0; i < count; ++i) {
        const auto code = readWideString(filer);
        const auto parsed = pavementSubgradeMoistureTypeFromCode(code);
        if (code == pavementSubgradeMoistureTypeCode(parsed)) {
            values.push_back(parsed);
        }
    }
    return values;
}

void writeSoilGroups(AcDbDwgFiler* filer, const std::vector<PavementSubgradeSoilGroup>& values)
{
    filer->writeInt32(static_cast<Adesk::Int32>(values.size()));
    for (const auto value : values) {
        writeWideString(filer, pavementSubgradeSoilGroupCode(value));
    }
}

std::vector<PavementSubgradeSoilGroup> readSoilGroups(AcDbDwgFiler* filer)
{
    Adesk::Int32 count = 0;
    filer->readInt32(&count);
    std::vector<PavementSubgradeSoilGroup> values;
    if (count < 0 || count > kMaxLayers) {
        return values;
    }
    values.reserve(static_cast<std::size_t>(count));
    for (Adesk::Int32 i = 0; i < count; ++i) {
        const auto code = readWideString(filer);
        const auto parsed = pavementSubgradeSoilGroupFromCode(code);
        if (code == pavementSubgradeSoilGroupCode(parsed)) {
            values.push_back(parsed);
        }
    }
    return values;
}

void writeStationRows(AcDbDwgFiler* filer, const std::vector<SubgradeStationValue>& rows)
{
    filer->writeInt32(static_cast<Adesk::Int32>(std::min<std::size_t>(rows.size(), kMaxTableRows)));
    for (std::size_t i = 0; i < rows.size() && i < static_cast<std::size_t>(kMaxTableRows); ++i) {
        filer->writeDouble(rows[i].station);
        filer->writeDouble(rows[i].value);
    }
}

std::vector<SubgradeStationValue> readStationRows(AcDbDwgFiler* filer)
{
    Adesk::Int32 count = 0;
    filer->readInt32(&count);
    std::vector<SubgradeStationValue> rows;
    if (count < 0 || count > kMaxTableRows) {
        return rows;
    }
    rows.reserve(static_cast<std::size_t>(count));
    for (Adesk::Int32 i = 0; i < count; ++i) {
        SubgradeStationValue row;
        filer->readDouble(&row.station);
        filer->readDouble(&row.value);
        rows.push_back(row);
    }
    return rows;
}

void writePavementData(AcDbDwgFiler* filer, const PavementLayerTemplateData& data)
{
    writeWideString(filer, data.properties.name);
    filer->writeDouble(data.properties.displayScale);
    filer->writeDouble(data.properties.previewWidth);
    writeWideString(filer, PavementLayerTemplateRules::displayModeCode(data.properties.displayMode));
    writeBool(filer, data.properties.showAllGeneralParameters);
    writeWideString(filer, data.properties.structureCode);
    writeMoistureTypes(filer, data.properties.subgradeMoistureTypes);
    writeWideString(filer, pavementSurfaceTypeCode(data.properties.pavementType));
    writeSoilGroups(filer, data.properties.subgradeSoilGroups);
    writeWideString(filer, data.properties.designDeflection);
    writeWideString(filer, data.properties.cumulativeAxleLoads);

    const auto layerCount = static_cast<Adesk::Int32>(std::min<std::size_t>(data.layers.size(), kMaxLayers));
    filer->writeInt32(layerCount);
    for (std::size_t i = 0; i < data.layers.size() && i < static_cast<std::size_t>(kMaxLayers); ++i) {
        const auto& layer = data.layers[i];
        filer->writeInt32(static_cast<Adesk::Int32>(layer.type));
        writeWideString(filer, layer.name);
        writeBool(filer, layer.uniformThickness);
        filer->writeDouble(layer.thickness);
        filer->writeDouble(layer.innerThickness);
        filer->writeDouble(layer.outerThickness);
        filer->writeDouble(layer.innerWidening);
        filer->writeDouble(layer.outerWidening);
        filer->writeDouble(layer.innerSlope);
        filer->writeDouble(layer.outerSlope);
        filer->writeInt32(static_cast<Adesk::Int32>(layer.color.r));
        filer->writeInt32(static_cast<Adesk::Int32>(layer.color.g));
        filer->writeInt32(static_cast<Adesk::Int32>(layer.color.b));
        writeWideString(filer, layer.hatchPattern);
        filer->writeDouble(layer.hatchAngle);
        filer->writeDouble(layer.hatchScale);
    }
}

Acad::ErrorStatus readPavementData(AcDbDwgFiler* filer, PavementLayerTemplateData& data)
{
    data.properties.name = readWideString(filer);
    filer->readDouble(&data.properties.displayScale);
    filer->readDouble(&data.properties.previewWidth);
    data.properties.displayMode = PavementLayerTemplateRules::displayModeFromCode(readWideString(filer));
    data.properties.showAllGeneralParameters = readBool(filer);
    data.properties.structureCode = readWideString(filer);
    data.properties.subgradeMoistureTypes = readMoistureTypes(filer);
    data.properties.pavementType = pavementSurfaceTypeFromCode(readWideString(filer));
    data.properties.subgradeSoilGroups = readSoilGroups(filer);
    data.properties.designDeflection = readWideString(filer);
    data.properties.cumulativeAxleLoads = readWideString(filer);

    Adesk::Int32 layerCount = 0;
    filer->readInt32(&layerCount);
    if (layerCount < 0 || layerCount > kMaxLayers) {
        return Acad::eInvalidInput;
    }

    data.layers.clear();
    data.layers.reserve(static_cast<std::size_t>(layerCount));
    for (Adesk::Int32 i = 0; i < layerCount; ++i) {
        PavementLayerTemplateLayer layer;
        Adesk::Int32 type = 0;
        filer->readInt32(&type);
        if (!isValidPavementLayerTypeValue(type)) {
            return Acad::eInvalidInput;
        }
        layer.type = static_cast<PavementLayerType>(type);
        layer.name = readWideString(filer);
        layer.uniformThickness = readBool(filer);
        filer->readDouble(&layer.thickness);
        filer->readDouble(&layer.innerThickness);
        filer->readDouble(&layer.outerThickness);
        filer->readDouble(&layer.innerWidening);
        filer->readDouble(&layer.outerWidening);
        filer->readDouble(&layer.innerSlope);
        filer->readDouble(&layer.outerSlope);
        Adesk::Int32 colorR = 0;
        Adesk::Int32 colorG = 0;
        Adesk::Int32 colorB = 0;
        filer->readInt32(&colorR);
        filer->readInt32(&colorG);
        filer->readInt32(&colorB);
        layer.color.r = static_cast<int>(colorR);
        layer.color.g = static_cast<int>(colorG);
        layer.color.b = static_cast<int>(colorB);
        layer.hatchPattern = readWideString(filer);
        filer->readDouble(&layer.hatchAngle);
        filer->readDouble(&layer.hatchScale);
        data.layers.push_back(std::move(layer));
    }
    return checkFilerStatus(filer);
}

AcCmEntityColor colorFromRgb(int r, int g, int b)
{
    AcCmEntityColor color;
    color.setRGB(
        static_cast<Adesk::UInt8>(std::clamp(r, 0, 255)),
        static_cast<Adesk::UInt8>(std::clamp(g, 0, 255)),
        static_cast<Adesk::UInt8>(std::clamp(b, 0, 255)));
    return color;
}

AcGePoint3d sectionPoint(
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    double x,
    double y)
{
    return origin + xAxis * x + yAxis * y;
}

int blendPreviewFillChannel(int source, int background)
{
    const auto blended = background + (std::clamp(source, 0, 255) - background) * kPreviewFillOpacity;
    return static_cast<int>(std::round(blended));
}

AcCmEntityColor pavementLayerStrokeColor(const PavementLayerTemplateDisplayColor& color)
{
    return colorFromRgb(color.r, color.g, color.b);
}

AcCmEntityColor pavementLayerFillColor(const PavementLayerTemplateDisplayColor& color)
{
    return colorFromRgb(
        blendPreviewFillChannel(color.r, kPreviewBackgroundR),
        blendPreviewFillChannel(color.g, kPreviewBackgroundG),
        blendPreviewFillChannel(color.b, kPreviewBackgroundB));
}

bool sameSectionPoint(const PavementLayerSectionPoint& first, const PavementLayerSectionPoint& second)
{
    constexpr double tolerance = 1.0e-9;
    return std::fabs(first.offset - second.offset) <= tolerance
        && std::fabs(first.elevation - second.elevation) <= tolerance;
}

struct SectionPoint2d {
    double x = 0.0;
    double y = 0.0;
};

SectionPoint2d makeSectionPoint(double x, double y)
{
    return SectionPoint2d{x, y};
}

SectionPoint2d scaledSectionPoint(const PavementLayerSectionPoint& point, double scale)
{
    return SectionPoint2d{point.offset * scale, point.elevation * scale};
}

double dot(const SectionPoint2d& first, const SectionPoint2d& second)
{
    return first.x * second.x + first.y * second.y;
}

SectionPoint2d operator+(const SectionPoint2d& first, const SectionPoint2d& second)
{
    return SectionPoint2d{first.x + second.x, first.y + second.y};
}

SectionPoint2d operator-(const SectionPoint2d& first, const SectionPoint2d& second)
{
    return SectionPoint2d{first.x - second.x, first.y - second.y};
}

SectionPoint2d operator*(const SectionPoint2d& point, double scale)
{
    return SectionPoint2d{point.x * scale, point.y * scale};
}

double length(const SectionPoint2d& vector)
{
    return std::hypot(vector.x, vector.y);
}

SectionPoint2d normalized(const SectionPoint2d& vector)
{
    const auto vectorLength = length(vector);
    return vectorLength <= 1.0e-9
        ? SectionPoint2d{1.0, 0.0}
        : SectionPoint2d{vector.x / vectorLength, vector.y / vectorLength};
}

SectionPoint2d perpendicular(const SectionPoint2d& vector)
{
    return SectionPoint2d{-vector.y, vector.x};
}

void drawSectionLine(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const SectionPoint2d& start,
    const SectionPoint2d& end)
{
    if (length(end - start) <= 1.0e-9) {
        return;
    }

    AcGePoint3d line[2] = {
        sectionPoint(origin, xAxis, yAxis, start.x, start.y),
        sectionPoint(origin, xAxis, yAxis, end.x, end.y)};
    worldDraw->geometry().polyline(2, line);
}

bool pointInPolygon(const SectionPoint2d& point, const std::vector<SectionPoint2d>& polygon)
{
    bool inside = false;
    for (std::size_t i = 0, j = polygon.empty() ? 0 : polygon.size() - 1; i < polygon.size(); j = i++) {
        const auto& current = polygon[i];
        const auto& previous = polygon[j];
        const bool crosses = (current.y > point.y) != (previous.y > point.y);
        if (crosses) {
            const auto x = (previous.x - current.x) * (point.y - current.y) / (previous.y - current.y) + current.x;
            if (point.x < x) {
                inside = !inside;
            }
        }
    }
    return inside;
}

void drawClippedHatchLine(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const std::vector<SectionPoint2d>& polygon,
    const SectionPoint2d& direction,
    const SectionPoint2d& normal,
    double offset)
{
    std::vector<SectionPoint2d> intersections;
    for (std::size_t i = 0; i < polygon.size(); ++i) {
        const auto& first = polygon[i];
        const auto& second = polygon[(i + 1) % polygon.size()];
        const auto firstDistance = dot(first, normal) - offset;
        const auto secondDistance = dot(second, normal) - offset;
        if (std::fabs(firstDistance) <= 1.0e-9) {
            intersections.push_back(first);
        }
        if (firstDistance * secondDistance < 0.0) {
            const auto t = firstDistance / (firstDistance - secondDistance);
            intersections.push_back(first + (second - first) * t);
        }
    }
    if (intersections.size() < 2) {
        return;
    }

    std::sort(intersections.begin(), intersections.end(), [&](const auto& first, const auto& second) {
        return dot(first, direction) < dot(second, direction);
    });
    drawSectionLine(worldDraw, origin, xAxis, yAxis, intersections.front(), intersections.back());
}

void drawHatchFamily(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const std::vector<SectionPoint2d>& polygon,
    SectionPoint2d direction,
    double spacing)
{
    direction = normalized(direction);
    const auto normal = perpendicular(direction);
    auto minDistance = dot(polygon.front(), normal);
    auto maxDistance = minDistance;
    for (const auto& point : polygon) {
        minDistance = std::min(minDistance, dot(point, normal));
        maxDistance = std::max(maxDistance, dot(point, normal));
    }
    for (auto offset = minDistance - spacing; offset <= maxDistance + spacing; offset += spacing) {
        drawClippedHatchLine(worldDraw, origin, xAxis, yAxis, polygon, direction, normal, offset);
    }
}

double safeHatchScale(double hatchScale)
{
    return std::isfinite(hatchScale) && hatchScale > 0.0 ? hatchScale : 1.0;
}

SectionPoint2d hatchDirectionFromAngle(double hatchAngle)
{
    const auto angleRadians = (std::isfinite(hatchAngle) ? hatchAngle : 0.0) * kPi / 180.0;
    return normalized(makeSectionPoint(std::cos(angleRadians), std::sin(angleRadians)));
}

void drawLayerPreviewFill(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const PavementLayerSectionLayer& layer,
    double scale,
    PavementLayerTemplateDisplayMode displayMode)
{
    if (displayMode == PavementLayerTemplateDisplayMode::Hatch) {
        return;
    }

    worldDraw->subEntityTraits().setTrueColor(pavementLayerFillColor(layer.color));
    worldDraw->subEntityTraits().setTransparency(AcCmTransparency(kOpaqueTransparencyAlpha));
    worldDraw->subEntityTraits().setFillType(kAcGiFillAlways);
    AcGePoint3d fillPoints[4] = {
        sectionPoint(origin, xAxis, yAxis, layer.topInner.offset * scale, layer.topInner.elevation * scale),
        sectionPoint(origin, xAxis, yAxis, layer.topOuter.offset * scale, layer.topOuter.elevation * scale),
        sectionPoint(origin, xAxis, yAxis, layer.bottomOuter.offset * scale, layer.bottomOuter.elevation * scale),
        sectionPoint(origin, xAxis, yAxis, layer.bottomInner.offset * scale, layer.bottomInner.elevation * scale)};
    worldDraw->geometry().polygon(4, fillPoints);
    worldDraw->subEntityTraits().setFillType(kAcGiFillNever);
}

void drawLayerHatchPattern(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const PavementLayerSectionLayer& sectionLayer,
    const PavementLayerTemplateLayer& layer,
    double scale,
    PavementLayerTemplateDisplayMode displayMode)
{
    if (displayMode == PavementLayerTemplateDisplayMode::Color || layer.hatchPattern == L"SOLID") {
        return;
    }

    const auto polygon = std::vector<SectionPoint2d>{
        scaledSectionPoint(sectionLayer.topInner, scale),
        scaledSectionPoint(sectionLayer.topOuter, scale),
        scaledSectionPoint(sectionLayer.bottomOuter, scale),
        scaledSectionPoint(sectionLayer.bottomInner, scale)};
    const auto color = displayMode == PavementLayerTemplateDisplayMode::HatchAndColor
        ? pavementLayerStrokeColor(sectionLayer.color)
        : colorFromRgb(205, 213, 222);
    worldDraw->subEntityTraits().setTrueColor(color);
    worldDraw->subEntityTraits().setTransparency(AcCmTransparency(kOpaqueTransparencyAlpha));
    worldDraw->subEntityTraits().setFillType(kAcGiFillNever);

    const auto spacing = std::max(0.35, 0.12 * scale * safeHatchScale(layer.hatchScale));
    if (layer.hatchPattern == L"DOTS") {
        auto minX = polygon.front().x;
        auto maxX = polygon.front().x;
        auto minY = polygon.front().y;
        auto maxY = polygon.front().y;
        for (const auto& point : polygon) {
            minX = std::min(minX, point.x);
            maxX = std::max(maxX, point.x);
            minY = std::min(minY, point.y);
            maxY = std::max(maxY, point.y);
        }
        const auto dotSize = std::max(0.08, 0.015 * scale);
        for (auto x = minX; x <= maxX; x += spacing) {
            for (auto y = minY; y <= maxY; y += spacing) {
                const auto point = makeSectionPoint(x, y);
                if (pointInPolygon(point, polygon)) {
                    drawSectionLine(
                        worldDraw,
                        origin,
                        xAxis,
                        yAxis,
                        makeSectionPoint(x - dotSize, y),
                        makeSectionPoint(x + dotSize, y));
                }
            }
        }
        return;
    }

    const auto primaryDirection = hatchDirectionFromAngle(layer.hatchAngle);
    drawHatchFamily(worldDraw, origin, xAxis, yAxis, polygon, primaryDirection, spacing);
    if (layer.hatchPattern == L"CROSS"
        || layer.hatchPattern == L"ANSI32"
        || layer.hatchPattern == L"ANSI37"
        || layer.hatchPattern == L"ANSI38"
        || layer.hatchPattern == L"GRAVEL"
        || layer.hatchPattern == L"EARTH"
        || layer.hatchPattern == L"AR-CONC") {
        drawHatchFamily(worldDraw, origin, xAxis, yAxis, polygon, perpendicular(primaryDirection), spacing);
    }
    if (layer.hatchPattern == L"GRAVEL" || layer.hatchPattern == L"EARTH" || layer.hatchPattern == L"AR-CONC") {
        drawHatchFamily(worldDraw, origin, xAxis, yAxis, polygon, hatchDirectionFromAngle(layer.hatchAngle + 45.0), spacing * 1.4);
    }
}

void drawLayerEdge(AcGiWorldDraw* worldDraw, const AcGePoint3d& start, const AcGePoint3d& end)
{
    if ((end - start).length() <= 1.0e-9) {
        return;
    }

    AcGePoint3d line[2] = {start, end};
    worldDraw->geometry().polyline(2, line);
}

void drawLayerEdges(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const PavementLayerSectionLayer& layer,
    double scale,
    bool drawTopEdge)
{
    worldDraw->subEntityTraits().setTrueColor(pavementLayerStrokeColor(layer.color));
    worldDraw->subEntityTraits().setTransparency(AcCmTransparency(kOpaqueTransparencyAlpha));
    worldDraw->subEntityTraits().setFillType(kAcGiFillNever);
    const auto topInner =
        sectionPoint(origin, xAxis, yAxis, layer.topInner.offset * scale, layer.topInner.elevation * scale);
    const auto topOuter =
        sectionPoint(origin, xAxis, yAxis, layer.topOuter.offset * scale, layer.topOuter.elevation * scale);
    const auto bottomOuter =
        sectionPoint(origin, xAxis, yAxis, layer.bottomOuter.offset * scale, layer.bottomOuter.elevation * scale);
    const auto bottomInner =
        sectionPoint(origin, xAxis, yAxis, layer.bottomInner.offset * scale, layer.bottomInner.elevation * scale);

    if (drawTopEdge) {
        drawLayerEdge(worldDraw, topInner, topOuter);
    }
    drawLayerEdge(worldDraw, topOuter, bottomOuter);
    drawLayerEdge(worldDraw, bottomOuter, bottomInner);
    drawLayerEdge(worldDraw, bottomInner, topInner);
}

AcGeVector3d textDirection(const AcGeVector3d& xAxis)
{
    auto direction = xAxis;
    if (direction.isZeroLength()) {
        return AcGeVector3d::kXAxis;
    }
    direction.normalize();
    return direction;
}

AcGeVector3d textNormal(const AcGeVector3d& xAxis, const AcGeVector3d& yAxis)
{
    auto normal = xAxis.crossProduct(yAxis);
    if (normal.isZeroLength()) {
        return AcGeVector3d::kZAxis;
    }
    normal.normalize();
    return normal;
}

void drawText(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    double x,
    double y,
    const std::wstring& text,
    double height)
{
    if (text.empty()) {
        return;
    }
    worldDraw->geometry().text(
        sectionPoint(origin, xAxis, yAxis, x, y),
        textNormal(xAxis, yAxis),
        textDirection(xAxis),
        height,
        1.0,
        0.0,
        text.c_str());
}

void drawLine(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    double x1,
    double y1,
    double x2,
    double y2)
{
    AcGePoint3d points[2] = {
        sectionPoint(origin, xAxis, yAxis, x1, y1),
        sectionPoint(origin, xAxis, yAxis, x2, y2)};
    worldDraw->geometry().polyline(2, points);
}

double drawingScale(const FullRoadPavementTemplateData& data)
{
    return SubgradeTemplateRules::isSupportedDisplayScale(data.properties.displayScale)
        ? data.properties.displayScale
        : 10.0;
}

double componentWidth(const FullRoadPavementComponentSnapshot& component)
{
    return std::max(0.05, std::fabs(component.subgrade.width));
}

double displaySlope(const FullRoadPavementComponentSnapshot& component)
{
    if (component.subgrade.slopeMode == SubgradeSlopeMode::Fixed) {
        return std::isfinite(component.subgrade.fixedSlope) ? component.subgrade.fixedSlope : 0.0;
    }
    return component.subgrade.variableSlopeTable.empty()
        ? 0.0
        : component.subgrade.variableSlopeTable.front().value;
}

bool isFlatMedianComponent(const FullRoadPavementComponentSnapshot& component)
{
    return component.subgrade.type == SubgradeComponentType::Median
        || component.subgrade.type == SubgradeComponentType::SideMedian;
}

double curbDepth(const FullRoadPavementComponentSnapshot& component, bool inner, double scale)
{
    const auto enabled = inner ? component.subgrade.hasInnerCurb : component.subgrade.hasOuterCurb;
    if (!enabled) {
        return 0.0;
    }
    const auto height = inner ? component.subgrade.innerCurbHeight : component.subgrade.outerCurbHeight;
    const auto embedDepth = inner ? component.subgrade.innerCurbEmbedDepth : component.subgrade.outerCurbEmbedDepth;
    return (std::max(0.0, height) + std::max(0.0, embedDepth)) * scale;
}

double totalWidth(const FullRoadPavementTemplateData& data)
{
    double width = 0.0;
    for (const auto& component : data.components) {
        width += componentWidth(component);
    }
    return width;
}

void drawCurb(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const FullRoadPavementComponentSnapshot& component,
    double edgeX,
    double insideX,
    double edgeSurfaceY,
    double insideSurfaceY,
    double height,
    double embedDepth)
{
    if (std::fabs(edgeX - insideX) <= 1.0e-9 || (height <= 0.0 && embedDepth <= 0.0)) {
        return;
    }

    const auto curbTopStartY = edgeSurfaceY;
    const auto curbTopInsideY = insideSurfaceY;
    const auto edgeBottom = curbTopStartY - height - embedDepth;
    const auto insideBottom = curbTopInsideY - height - embedDepth;

    worldDraw->subEntityTraits().setTrueColor(
        colorFromRgb(component.subgrade.color.r, component.subgrade.color.g, component.subgrade.color.b));
    worldDraw->subEntityTraits().setFillType(kAcGiFillAlways);
    AcGePoint3d polygon[4] = {
        sectionPoint(origin, xAxis, yAxis, edgeX, curbTopStartY),
        sectionPoint(origin, xAxis, yAxis, insideX, curbTopInsideY),
        sectionPoint(origin, xAxis, yAxis, insideX, insideBottom),
        sectionPoint(origin, xAxis, yAxis, edgeX, edgeBottom)};
    worldDraw->geometry().polygon(4, polygon);

    worldDraw->subEntityTraits().setTrueColor(colorFromRgb(255, 255, 255));
    worldDraw->subEntityTraits().setFillType(kAcGiFillNever);
    drawLine(worldDraw, origin, xAxis, yAxis, edgeX, edgeBottom, edgeX, curbTopStartY);
    drawLine(worldDraw, origin, xAxis, yAxis, edgeX, curbTopStartY, insideX, curbTopInsideY);
    drawLine(worldDraw, origin, xAxis, yAxis, insideX, curbTopInsideY, insideX, insideBottom);
    drawLine(worldDraw, origin, xAxis, yAxis, insideX, insideBottom, edgeX, edgeBottom);
}

void drawSubgradeComponent(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const FullRoadPavementComponentSnapshot& component,
    double x1,
    double x2,
    double topStartY,
    double topEndY,
    double scale)
{
    const auto width = std::fabs(x2 - x1);
    const auto sign = component.subgrade.side == SubgradeSide::Left ? -1.0 : 1.0;
    worldDraw->subEntityTraits().setTrueColor(
        colorFromRgb(component.subgrade.color.r, component.subgrade.color.g, component.subgrade.color.b));
    worldDraw->subEntityTraits().setFillType(kAcGiFillNever);
    drawLine(worldDraw, origin, xAxis, yAxis, x1, topStartY, x2, topEndY);

    if (component.subgrade.hasInnerCurb) {
        const auto curbWidth = std::min(std::max(0.0, component.subgrade.innerCurbWidth) * scale, width);
        const auto curbHeight = std::max(0.0, component.subgrade.innerCurbHeight) * scale;
        const auto curbEmbedDepth = std::max(0.0, component.subgrade.innerCurbEmbedDepth) * scale;
        const auto xInside = x1 + sign * curbWidth;
        const auto yInside = width > 1.0e-9 ? topStartY + (topEndY - topStartY) * (curbWidth / width) : topStartY;
        drawCurb(worldDraw, origin, xAxis, yAxis, component, x1, xInside, topStartY, yInside, curbHeight, curbEmbedDepth);
    }

    if (component.subgrade.hasOuterCurb) {
        const auto curbWidth = std::min(std::max(0.0, component.subgrade.outerCurbWidth) * scale, width);
        const auto curbHeight = std::max(0.0, component.subgrade.outerCurbHeight) * scale;
        const auto curbEmbedDepth = std::max(0.0, component.subgrade.outerCurbEmbedDepth) * scale;
        const auto xInside = x2 - sign * curbWidth;
        const auto yInside = width > 1.0e-9 ? topEndY - (topEndY - topStartY) * (curbWidth / width) : topEndY;
        drawCurb(worldDraw, origin, xAxis, yAxis, component, x2, xInside, topEndY, yInside, curbHeight, curbEmbedDepth);
    }
}

PavementLayerSectionPoint fullRoadSectionPoint(
    const PavementLayerSectionPoint& point,
    double innerOffset,
    double sign)
{
    return PavementLayerSectionPoint{
        innerOffset + sign * point.offset,
        point.elevation};
}

PavementLayerSectionLayer fullRoadSectionLayer(
    const PavementLayerSectionLayer& layer,
    double innerOffset,
    double sign)
{
    auto result = layer;
    result.topInner = fullRoadSectionPoint(layer.topInner, innerOffset, sign);
    result.topOuter = fullRoadSectionPoint(layer.topOuter, innerOffset, sign);
    result.bottomInner = fullRoadSectionPoint(layer.bottomInner, innerOffset, sign);
    result.bottomOuter = fullRoadSectionPoint(layer.bottomOuter, innerOffset, sign);
    return result;
}

void drawPavementLayers(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const FullRoadPavementComponentSnapshot& component,
    double x1,
    double x2,
    double topStartY,
    double topEndY,
    double scale)
{
    (void)x2;
    if (component.pavement.layers.empty()) {
        return;
    }

    const auto section = PavementLayerTemplateRules::buildSection(
        component.pavement,
        componentWidth(component),
        component.subgrade.side,
        topStartY / scale,
        topEndY / scale);
    if (!section.succeeded) {
        return;
    }

    const auto innerOffset = x1 / scale;
    const auto sign = component.subgrade.side == SubgradeSide::Left ? -1.0 : 1.0;
    const auto displayMode = component.pavement.properties.displayMode;
    std::vector<PavementLayerSectionLayer> layers;
    layers.reserve(section.layers.size());
    for (const auto& layer : section.layers) {
        layers.push_back(fullRoadSectionLayer(layer, innerOffset, sign));
    }

    for (const auto& layer : layers) {
        drawLayerPreviewFill(worldDraw, origin, xAxis, yAxis, layer, scale, displayMode);
    }
    for (std::size_t layerIndex = 0; layerIndex < layers.size() && layerIndex < component.pavement.layers.size(); ++layerIndex) {
        drawLayerHatchPattern(
            worldDraw,
            origin,
            xAxis,
            yAxis,
            layers[layerIndex],
            component.pavement.layers[layerIndex],
            scale,
            displayMode);
    }
    for (std::size_t layerIndex = 0; layerIndex < layers.size(); ++layerIndex) {
        const auto& layer = layers[layerIndex];
        const bool drawTopEdge = layerIndex == 0
            || !sameSectionPoint(layer.topInner, layers[layerIndex - 1].bottomInner)
            || !sameSectionPoint(layer.topOuter, layers[layerIndex - 1].bottomOuter);
        drawLayerEdges(worldDraw, origin, xAxis, yAxis, layer, scale, drawTopEdge);
    }
}

void drawCenterline(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const FullRoadPavementTemplateData& data,
    double scale)
{
    const auto width = totalWidth(data) * scale;
    worldDraw->subEntityTraits().setColor(1);
    drawLine(worldDraw, origin, xAxis, yAxis, 0.0, scale, 0.0, -std::max(width * 0.08, scale));
    drawText(worldDraw, origin, xAxis, yAxis, 0.25 * scale, 0.45 * scale, L"中线", std::max(2.0, 0.28 * scale));
}

void drawTemplateTitle(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const FullRoadPavementTemplateData& data,
    double minX,
    double scale)
{
    const auto reference = data.properties.referenceSubgradeTemplateName.empty()
        ? L"未选择参考路基模板"
        : data.properties.referenceSubgradeTemplateName;
    drawText(
        worldDraw,
        origin,
        xAxis,
        yAxis,
        minX,
        1.2 * scale,
        data.properties.name + L" / " + reference,
        std::max(2.5, 0.32 * scale));
}

void drawSide(
    AcGiWorldDraw* worldDraw,
    const AcGePoint3d& origin,
    const AcGeVector3d& xAxis,
    const AcGeVector3d& yAxis,
    const FullRoadPavementTemplateData& data,
    SubgradeSide side,
    double scale)
{
    const auto sign = side == SubgradeSide::Left ? -1.0 : 1.0;
    double x = 0.0;
    double y = 0.0;
    for (const auto& component : data.components) {
        if (component.subgrade.side != side) {
            continue;
        }

        const auto width = componentWidth(component) * scale;
        auto yStart = y + SubgradeTemplateRules::innerCurbHeightDelta(component.subgrade) * scale;
        auto yEnd = yStart + displaySlope(component) * width * sign;
        if (isFlatMedianComponent(component)) {
            yEnd = yStart;
        }
        const auto xEnd = x + sign * width;
        drawPavementLayers(worldDraw, origin, xAxis, yAxis, component, x, xEnd, yStart, yEnd, scale);
        drawSubgradeComponent(worldDraw, origin, xAxis, yAxis, component, x, xEnd, yStart, yEnd, scale);

        x = xEnd;
        y = yEnd + SubgradeTemplateRules::outerCurbHeightDelta(component.subgrade) * scale;
    }
}

struct PreviewBounds {
    double minX = 0.0;
    double maxX = 0.0;
    double minY = 0.0;
    double maxY = 0.0;
};

void addPreviewBounds(PreviewBounds& bounds, double x, double y)
{
    bounds.minX = std::min(bounds.minX, x);
    bounds.maxX = std::max(bounds.maxX, x);
    bounds.minY = std::min(bounds.minY, y);
    bounds.maxY = std::max(bounds.maxY, y);
}

void addPavementLayerBounds(PreviewBounds& bounds, const PavementLayerSectionLayer& layer, double scale)
{
    addPreviewBounds(bounds, layer.topInner.offset * scale, layer.topInner.elevation * scale);
    addPreviewBounds(bounds, layer.topOuter.offset * scale, layer.topOuter.elevation * scale);
    addPreviewBounds(bounds, layer.bottomOuter.offset * scale, layer.bottomOuter.elevation * scale);
    addPreviewBounds(bounds, layer.bottomInner.offset * scale, layer.bottomInner.elevation * scale);
}

void addPavementSectionBounds(
    PreviewBounds& bounds,
    const FullRoadPavementComponentSnapshot& component,
    double x,
    double yStart,
    double yEnd,
    double scale,
    double sign)
{
    if (component.pavement.layers.empty()) {
        return;
    }

    const auto section = PavementLayerTemplateRules::buildSection(
        component.pavement,
        componentWidth(component),
        component.subgrade.side,
        yStart / scale,
        yEnd / scale);
    if (!section.succeeded) {
        return;
    }

    const auto innerOffset = x / scale;
    for (const auto& layer : section.layers) {
        addPavementLayerBounds(bounds, fullRoadSectionLayer(layer, innerOffset, sign), scale);
    }
}

void walkSideForBounds(
    const FullRoadPavementTemplateData& data,
    SubgradeSide side,
    double scale,
    PreviewBounds& bounds)
{
    const auto sign = side == SubgradeSide::Left ? -1.0 : 1.0;
    double x = 0.0;
    double y = 0.0;
    for (const auto& component : data.components) {
        if (component.subgrade.side != side) {
            continue;
        }

        const auto width = componentWidth(component) * scale;
        auto yStart = y + SubgradeTemplateRules::innerCurbHeightDelta(component.subgrade) * scale;
        auto yEnd = yStart + displaySlope(component) * width * sign;
        if (isFlatMedianComponent(component)) {
            yEnd = yStart;
        }
        const auto xEnd = x + sign * width;

        addPreviewBounds(bounds, x, yStart);
        addPreviewBounds(bounds, xEnd, yEnd);
        addPavementSectionBounds(bounds, component, x, yStart, yEnd, scale, sign);

        if (component.subgrade.hasInnerCurb) {
            const auto curbWidth = std::min(std::max(0.0, component.subgrade.innerCurbWidth) * scale, width);
            const auto curbDepthValue = curbDepth(component, true, scale);
            const auto xInside = x + sign * curbWidth;
            const auto yInside = width > 1.0e-9 ? yStart + (yEnd - yStart) * (curbWidth / width) : yStart;
            addPreviewBounds(bounds, x, yStart - curbDepthValue);
            addPreviewBounds(bounds, xInside, yInside - curbDepthValue);
        }
        if (component.subgrade.hasOuterCurb) {
            const auto curbWidth = std::min(std::max(0.0, component.subgrade.outerCurbWidth) * scale, width);
            const auto curbDepthValue = curbDepth(component, false, scale);
            const auto xInside = xEnd - sign * curbWidth;
            const auto yInside = width > 1.0e-9 ? yEnd - (yEnd - yStart) * (curbWidth / width) : yEnd;
            addPreviewBounds(bounds, xEnd, yEnd - curbDepthValue);
            addPreviewBounds(bounds, xInside, yInside - curbDepthValue);
        }

        x = xEnd;
        y = yEnd + SubgradeTemplateRules::outerCurbHeightDelta(component.subgrade) * scale;
    }
}

PreviewBounds calculateBounds(const FullRoadPavementTemplateData& data)
{
    const auto scale = drawingScale(data);
    PreviewBounds bounds;
    addPreviewBounds(bounds, 0.0, -scale);
    addPreviewBounds(bounds, 0.0, 2.0 * scale);
    walkSideForBounds(data, SubgradeSide::Left, scale, bounds);
    walkSideForBounds(data, SubgradeSide::Right, scale, bounds);
    bounds.minX -= kExtentsPadding;
    bounds.maxX += kExtentsPadding;
    bounds.minY -= kExtentsPadding;
    bounds.maxY += kExtentsPadding;
    return bounds;
}

void markGraphicsModifiedIfResident(AcDbEntity& entity)
{
    if (!entity.objectId().isNull()) {
        entity.recordGraphicsModified(true);
    }
}

} // namespace

DnFullRoadPavementTemplateEntity::DnFullRoadPavementTemplateEntity()
    : templateData_(FullRoadPavementTemplateDefaults::create())
    , insertionPoint_(AcGePoint3d::kOrigin)
    , xAxis_(AcGeVector3d::kXAxis)
    , yAxis_(AcGeVector3d::kYAxis)
{
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::setTemplateData(const FullRoadPavementTemplateData& data)
{
    assertWriteEnabled();
    auto normalized = data;
    std::wstring errorMessage;
    if (!FullRoadPavementTemplateRules::normalize(normalized, errorMessage)) {
        return Acad::eInvalidInput;
    }
    templateData_ = std::move(normalized);
    markGraphicsModifiedIfResident(*this);
    return Acad::eOk;
}

const FullRoadPavementTemplateData& DnFullRoadPavementTemplateEntity::templateData() const
{
    assertReadEnabled();
    return templateData_;
}

void DnFullRoadPavementTemplateEntity::setInsertionPoint(const AcGePoint3d& point)
{
    assertWriteEnabled();
    insertionPoint_ = point;
    markGraphicsModifiedIfResident(*this);
}

AcGePoint3d DnFullRoadPavementTemplateEntity::insertionPoint() const
{
    assertReadEnabled();
    return insertionPoint_;
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::dwgInFields(AcDbDwgFiler* filer)
{
    assertWriteEnabled();
    auto status = AcDbEntity::dwgInFields(filer);
    if (status != Acad::eOk) {
        return status;
    }

    Adesk::Int16 version = 0;
    filer->readInt16(&version);
    if (version > kEntityVersion) {
        return Acad::eMakeMeProxy;
    }
    if (version < 1) {
        return Acad::eInvalidInput;
    }

    filer->readPoint3d(&insertionPoint_);
    filer->readVector3d(&xAxis_);
    filer->readVector3d(&yAxis_);
    if (!isFinitePoint(insertionPoint_) || !areAxesUsable(xAxis_, yAxis_)) {
        resetDefaultAxes(xAxis_, yAxis_);
    }

    FullRoadPavementTemplateData data;
    data.properties.name = readWideString(filer);
    filer->readDouble(&data.properties.displayScale);
    data.properties.referenceSubgradeTemplateHandle = readWideString(filer);
    data.properties.referenceSubgradeTemplateName = readWideString(filer);
    Adesk::Int32 referenceRoadGrade = 0;
    filer->readInt32(&referenceRoadGrade);
    if (!isValidRoadGradeValue(referenceRoadGrade)) {
        return Acad::eInvalidInput;
    }
    data.properties.referenceRoadGrade = static_cast<RoadGrade>(referenceRoadGrade);

    Adesk::Int32 componentCount = 0;
    filer->readInt32(&componentCount);
    if (componentCount < 0 || componentCount > kMaxComponents) {
        return Acad::eInvalidInput;
    }

    data.components.reserve(static_cast<std::size_t>(componentCount));
    for (Adesk::Int32 i = 0; i < componentCount; ++i) {
        FullRoadPavementComponentSnapshot component;
        Adesk::Int32 side = 0;
        Adesk::Int32 type = 0;
        filer->readInt32(&side);
        filer->readInt32(&type);
        filer->readInt32(&component.key.sameSideTypeOrdinal);
        if (!isValidSubgradeSideValue(side) || !isValidSubgradeComponentTypeValue(type)) {
            return Acad::eInvalidInput;
        }
        component.key.side = static_cast<SubgradeSide>(side);
        component.key.type = static_cast<SubgradeComponentType>(type);
        component.subgrade.side = component.key.side;
        component.subgrade.type = component.key.type;
        filer->readDouble(&component.subgrade.width);
        filer->readDouble(&component.subgrade.height);
        filer->readDouble(&component.subgrade.fixedSlope);
        Adesk::Int32 slopeMode = 0;
        filer->readInt32(&slopeMode);
        if (!isValidSubgradeSlopeModeValue(slopeMode)) {
            return Acad::eInvalidInput;
        }
        component.subgrade.slopeMode = static_cast<SubgradeSlopeMode>(slopeMode);
        filer->readInt32(&component.subgrade.color.r);
        filer->readInt32(&component.subgrade.color.g);
        filer->readInt32(&component.subgrade.color.b);
        component.subgrade.wideningTable = readStationRows(filer);
        component.subgrade.variableSlopeTable = readStationRows(filer);
        component.subgrade.hasInnerCurb = readBool(filer);
        filer->readDouble(&component.subgrade.innerCurbWidth);
        filer->readDouble(&component.subgrade.innerCurbHeight);
        filer->readDouble(&component.subgrade.innerCurbEmbedDepth);
        component.subgrade.hasOuterCurb = readBool(filer);
        filer->readDouble(&component.subgrade.outerCurbWidth);
        filer->readDouble(&component.subgrade.outerCurbHeight);
        filer->readDouble(&component.subgrade.outerCurbEmbedDepth);
        status = readPavementData(filer, component.pavement);
        if (status != Acad::eOk) {
            return status;
        }
        data.components.push_back(std::move(component));
    }

    status = checkFilerStatus(filer);
    if (status != Acad::eOk) {
        return status;
    }
    std::wstring errorMessage;
    if (!FullRoadPavementTemplateRules::normalize(data, errorMessage)) {
        return Acad::eInvalidInput;
    }
    templateData_ = std::move(data);
    return Acad::eOk;
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::dwgOutFields(AcDbDwgFiler* filer) const
{
    assertReadEnabled();
    auto status = AcDbEntity::dwgOutFields(filer);
    if (status != Acad::eOk) {
        return status;
    }
    if (!isFinitePoint(insertionPoint_) || !areAxesUsable(xAxis_, yAxis_)
        || templateData_.components.size() > static_cast<std::size_t>(kMaxComponents)) {
        return Acad::eInvalidInput;
    }

    filer->writeInt16(kEntityVersion);
    filer->writePoint3d(insertionPoint_);
    filer->writeVector3d(xAxis_);
    filer->writeVector3d(yAxis_);
    writeWideString(filer, templateData_.properties.name);
    filer->writeDouble(templateData_.properties.displayScale);
    writeWideString(filer, templateData_.properties.referenceSubgradeTemplateHandle);
    writeWideString(filer, templateData_.properties.referenceSubgradeTemplateName);
    filer->writeInt32(static_cast<Adesk::Int32>(templateData_.properties.referenceRoadGrade));

    const auto componentCount = static_cast<Adesk::Int32>(std::min<std::size_t>(templateData_.components.size(), kMaxComponents));
    filer->writeInt32(componentCount);
    for (std::size_t i = 0; i < templateData_.components.size() && i < static_cast<std::size_t>(kMaxComponents); ++i) {
        const auto& component = templateData_.components[i];
        filer->writeInt32(static_cast<Adesk::Int32>(component.key.side));
        filer->writeInt32(static_cast<Adesk::Int32>(component.key.type));
        filer->writeInt32(component.key.sameSideTypeOrdinal);
        filer->writeDouble(component.subgrade.width);
        filer->writeDouble(component.subgrade.height);
        filer->writeDouble(component.subgrade.fixedSlope);
        filer->writeInt32(static_cast<Adesk::Int32>(component.subgrade.slopeMode));
        filer->writeInt32(component.subgrade.color.r);
        filer->writeInt32(component.subgrade.color.g);
        filer->writeInt32(component.subgrade.color.b);
        writeStationRows(filer, component.subgrade.wideningTable);
        writeStationRows(filer, component.subgrade.variableSlopeTable);
        writeBool(filer, component.subgrade.hasInnerCurb);
        filer->writeDouble(component.subgrade.innerCurbWidth);
        filer->writeDouble(component.subgrade.innerCurbHeight);
        filer->writeDouble(component.subgrade.innerCurbEmbedDepth);
        writeBool(filer, component.subgrade.hasOuterCurb);
        filer->writeDouble(component.subgrade.outerCurbWidth);
        filer->writeDouble(component.subgrade.outerCurbHeight);
        filer->writeDouble(component.subgrade.outerCurbEmbedDepth);
        writePavementData(filer, component.pavement);
    }
    return filer->filerStatus();
}

Adesk::Boolean DnFullRoadPavementTemplateEntity::subWorldDraw(AcGiWorldDraw* worldDraw)
{
    assertReadEnabled();
    if (worldDraw == nullptr || !areAxesUsable(xAxis_, yAxis_)) {
        return Adesk::kTrue;
    }

    const auto scale = drawingScale(templateData_);
    drawSide(worldDraw, insertionPoint_, xAxis_, yAxis_, templateData_, SubgradeSide::Left, scale);
    drawSide(worldDraw, insertionPoint_, xAxis_, yAxis_, templateData_, SubgradeSide::Right, scale);

    drawCenterline(worldDraw, insertionPoint_, xAxis_, yAxis_, templateData_, scale);
    const auto bounds = calculateBounds(templateData_);
    drawTemplateTitle(worldDraw, insertionPoint_, xAxis_, yAxis_, templateData_, bounds.minX + kExtentsPadding, scale);
    return Adesk::kTrue;
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::subGetGeomExtents(AcDbExtents& extents) const
{
    assertReadEnabled();
    if (!areAxesUsable(xAxis_, yAxis_)) {
        return Acad::eInvalidExtents;
    }
    const auto bounds = calculateBounds(templateData_);
    extents.addPoint(sectionPoint(insertionPoint_, xAxis_, yAxis_, bounds.minX, bounds.minY));
    extents.addPoint(sectionPoint(insertionPoint_, xAxis_, yAxis_, bounds.minX, bounds.maxY));
    extents.addPoint(sectionPoint(insertionPoint_, xAxis_, yAxis_, bounds.maxX, bounds.minY));
    extents.addPoint(sectionPoint(insertionPoint_, xAxis_, yAxis_, bounds.maxX, bounds.maxY));
    return Acad::eOk;
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::subTransformBy(const AcGeMatrix3d& transform)
{
    assertWriteEnabled();
    auto transformedInsertionPoint = insertionPoint_;
    auto transformedXAxis = xAxis_;
    auto transformedYAxis = yAxis_;
    transformedInsertionPoint.transformBy(transform);
    transformedXAxis.transformBy(transform);
    transformedYAxis.transformBy(transform);
    if (!isFinitePoint(transformedInsertionPoint) || !areAxesUsable(transformedXAxis, transformedYAxis)) {
        return Acad::eInvalidInput;
    }
    insertionPoint_ = transformedInsertionPoint;
    xAxis_ = transformedXAxis;
    yAxis_ = transformedYAxis;
    markGraphicsModifiedIfResident(*this);
    return Acad::eOk;
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::subGetGripPoints(
    AcGePoint3dArray& gripPoints,
    AcDbIntArray&,
    AcDbIntArray&) const
{
    assertReadEnabled();
    gripPoints.append(insertionPoint_);
    return Acad::eOk;
}

Acad::ErrorStatus DnFullRoadPavementTemplateEntity::subMoveGripPointsAt(
    const AcDbIntArray& indices,
    const AcGeVector3d& offset)
{
    assertWriteEnabled();
    if (indices.isEmpty()) {
        return Acad::eOk;
    }
    if (!isFiniteVector(offset)) {
        return Acad::eInvalidInput;
    }
    insertionPoint_ += offset;
    recordGraphicsModified(true);
    return Acad::eOk;
}

namespace roadproto::cad_adapter::objectarx {

void initializeFullRoadPavementTemplateEntityClass()
{
    DnFullRoadPavementTemplateEntity::rxInit();
    acrxBuildClassHierarchy();
}

void uninitializeFullRoadPavementTemplateEntityClass()
{
    deleteAcRxClass(DnFullRoadPavementTemplateEntity::desc());
}

} // namespace roadproto::cad_adapter::objectarx
