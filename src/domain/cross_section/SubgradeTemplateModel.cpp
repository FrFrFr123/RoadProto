#include "domain/cross_section/SubgradeTemplateModel.h"

#include <algorithm>
#include <cmath>

namespace roadproto::domain::cross_section {
namespace {

constexpr double kStationTolerance = 1.0e-6;
constexpr double kMedianOuterCurbDefaultSize = 0.15;

void applyDefaultCurbs(SubgradeTemplateComponent& component)
{
    if (component.type != SubgradeComponentType::Median) {
        return;
    }

    component.hasOuterCurb = true;
    component.outerCurbWidth = kMedianOuterCurbDefaultSize;
    component.outerCurbHeight = kMedianOuterCurbDefaultSize;
    component.outerCurbEmbedDepth = kMedianOuterCurbDefaultSize;
}

SubgradeTemplateComponent makeComponent(
    SubgradeSide side,
    SubgradeComponentType type,
    double width)
{
    SubgradeTemplateComponent component;
    component.side = side;
    component.type = type;
    component.width = width;
    component.fixedSlope = SubgradeTemplateDefaults::defaultSlopeFor(side, type);
    component.color = SubgradeTemplateDefaults::defaultColorFor(side, type);
    applyDefaultCurbs(component);
    return component;
}

void appendSymmetricComponents(
    SubgradeTemplateData& data,
    const std::vector<std::pair<SubgradeComponentType, double>>& leftToOutside)
{
    for (const auto& item : leftToOutside) {
        data.components.push_back(makeComponent(SubgradeSide::Left, item.first, item.second));
    }

    for (const auto& item : leftToOutside) {
        data.components.push_back(makeComponent(SubgradeSide::Right, item.first, item.second));
    }
}

bool stationMatches(double lhs, double rhs)
{
    return std::fabs(lhs - rhs) <= kStationTolerance;
}

bool isFiniteNonNegative(double value)
{
    return std::isfinite(value) && value >= 0.0;
}

bool isFiniteValue(double value)
{
    return std::isfinite(value);
}

SubgradeTemplateRgbColor aciColorToRgb(int colorIndex)
{
    switch (colorIndex) {
    case 7:
        return {255, 255, 255};
    case 12:
        return {204, 0, 0};
    case 22:
        return {204, 51, 0};
    case 32:
        return {204, 102, 0};
    case 42:
        return {204, 153, 0};
    case 43:
        return {204, 178, 102};
    case 52:
        return {204, 204, 0};
    case 61:
        return {223, 255, 127};
    case 62:
        return {153, 204, 0};
    case 72:
        return {102, 204, 0};
    case 82:
        return {51, 204, 0};
    case 92:
        return {0, 204, 0};
    case 102:
        return {0, 204, 51};
    default:
        return {120, 120, 120};
    }
}

std::vector<SubgradeSide> sidesForOperation(SubgradeComponentSideScope scope)
{
    switch (scope) {
    case SubgradeComponentSideScope::Left:
        return {SubgradeSide::Left};
    case SubgradeComponentSideScope::Right:
        return {SubgradeSide::Right};
    case SubgradeComponentSideScope::Both:
    default:
        return {SubgradeSide::Left, SubgradeSide::Right};
    }
}

bool sideMatches(SubgradeSide side, SubgradeComponentSideScope scope)
{
    return scope == SubgradeComponentSideScope::Both
        || (scope == SubgradeComponentSideScope::Left && side == SubgradeSide::Left)
        || (scope == SubgradeComponentSideScope::Right && side == SubgradeSide::Right);
}

bool occurrenceMatches(int occurrence, const SubgradeComponentOperation& operation)
{
    switch (operation.occurrence) {
    case SubgradeComponentOccurrence::All:
        return true;
    case SubgradeComponentOccurrence::First:
        return occurrence == 0;
    case SubgradeComponentOccurrence::Second:
        return occurrence == 1;
    case SubgradeComponentOccurrence::Index:
        return operation.occurrenceIndex >= 0 && occurrence == operation.occurrenceIndex;
    default:
        return true;
    }
}

int& occurrenceCounterForSide(SubgradeSide side, int& leftCount, int& rightCount)
{
    return side == SubgradeSide::Left ? leftCount : rightCount;
}

void applyPatch(SubgradeTemplateComponent& component, const SubgradeComponentPatch& patch)
{
    if (patch.type.has_value()) {
        component.type = *patch.type;
    }
    if (patch.width.has_value()) {
        component.width = *patch.width;
    }
    if (patch.widthDelta.has_value()) {
        component.width += *patch.widthDelta;
    }
    if (patch.height.has_value()) {
        component.height = *patch.height;
    }
    if (patch.fixedSlope.has_value()) {
        component.fixedSlope = *patch.fixedSlope;
    }
    if (patch.slopeMode.has_value()) {
        component.slopeMode = *patch.slopeMode;
    }
    if (patch.color.has_value()) {
        component.color = *patch.color;
    }
    if (patch.wideningTable.has_value()) {
        component.wideningTable = *patch.wideningTable;
    }
    if (patch.variableSlopeTable.has_value()) {
        component.variableSlopeTable = *patch.variableSlopeTable;
    }
    if (patch.hasInnerCurb.has_value()) {
        component.hasInnerCurb = *patch.hasInnerCurb;
    }
    if (patch.innerCurbWidth.has_value()) {
        component.innerCurbWidth = *patch.innerCurbWidth;
    }
    if (patch.innerCurbHeight.has_value()) {
        component.innerCurbHeight = *patch.innerCurbHeight;
    }
    if (patch.innerCurbEmbedDepth.has_value()) {
        component.innerCurbEmbedDepth = *patch.innerCurbEmbedDepth;
    }
    if (patch.hasOuterCurb.has_value()) {
        component.hasOuterCurb = *patch.hasOuterCurb;
    }
    if (patch.outerCurbWidth.has_value()) {
        component.outerCurbWidth = *patch.outerCurbWidth;
    }
    if (patch.outerCurbHeight.has_value()) {
        component.outerCurbHeight = *patch.outerCurbHeight;
    }
    if (patch.outerCurbEmbedDepth.has_value()) {
        component.outerCurbEmbedDepth = *patch.outerCurbEmbedDepth;
    }
    if (patch.pavementLayerLinked.has_value()) {
        component.pavementLayerLinked = *patch.pavementLayerLinked;
    }
    if (patch.pavementLayerHandle.has_value()) {
        component.pavementLayerHandle = *patch.pavementLayerHandle;
    }
    if (patch.pavementLayerName.has_value()) {
        component.pavementLayerName = *patch.pavementLayerName;
    }
    if (patch.pavementLayerThickness.has_value()) {
        component.pavementLayerThickness = *patch.pavementLayerThickness;
    }
}

bool operationTargetsComponent(
    const SubgradeTemplateComponent& component,
    const SubgradeComponentOperation& operation)
{
    return sideMatches(component.side, operation.sideScope)
        && operation.componentType.has_value()
        && component.type == *operation.componentType;
}

SubgradeTemplateComponent createComponentFromOperation(
    SubgradeSide side,
    SubgradeComponentType type,
    const SubgradeComponentPatch& patch)
{
    SubgradeTemplateComponent component;
    component.side = side;
    component.type = type;
    component.width = patch.width.value_or(0.0);
    component.fixedSlope = SubgradeTemplateDefaults::defaultSlopeFor(side, type);
    component.color = SubgradeTemplateDefaults::defaultColorFor(side, type);
    applyPatch(component, patch);
    component.side = side;
    if (!patch.type.has_value()) {
        component.type = type;
    }
    return component;
}

std::vector<SubgradeTemplateComponent>::iterator insertionPointForOperation(
    std::vector<SubgradeTemplateComponent>& components,
    SubgradeSide side,
    const SubgradeComponentOperation& operation)
{
    if (!operation.anchorType.has_value()) {
        return components.end();
    }

    auto anchor = std::find_if(
        components.begin(),
        components.end(),
        [&](const auto& component) {
            return component.side == side && component.type == *operation.anchorType;
        });
    if (anchor == components.end()) {
        return components.end();
    }

    const auto mode = operation.positionMode.value_or(SubgradeComponentPositionMode::OutsideOf);
    if (mode == SubgradeComponentPositionMode::OutsideOf || mode == SubgradeComponentPositionMode::After) {
        return std::next(anchor);
    }

    return anchor;
}

} // namespace

SubgradeTemplateRgbColor SubgradeTemplateDefaults::defaultColorFor(SubgradeComponentType type)
{
    return defaultColorFor(SubgradeSide::Right, type);
}

SubgradeTemplateRgbColor SubgradeTemplateDefaults::defaultColorFor(
    SubgradeSide side,
    SubgradeComponentType type)
{
    return aciColorToRgb(defaultColorIndexFor(side, type));
}

int SubgradeTemplateDefaults::defaultColorIndexFor(
    SubgradeSide side,
    SubgradeComponentType type)
{
    const auto isLeft = side == SubgradeSide::Left;
    switch (type) {
    case SubgradeComponentType::Median:
        return isLeft ? 42 : 52;
    case SubgradeComponentType::SideMedian:
        return isLeft ? 92 : 102;
    case SubgradeComponentType::TravelLane:
        return isLeft ? 32 : 62;
    case SubgradeComponentType::HardShoulder:
    case SubgradeComponentType::BikeLane:
        return isLeft ? 22 : 72;
    case SubgradeComponentType::EarthShoulder:
    case SubgradeComponentType::Sidewalk:
        return isLeft ? 12 : 82;
    case SubgradeComponentType::CurbStrip:
        return isLeft ? 43 : 61;
    default:
        return 7;
    }
}

double SubgradeTemplateDefaults::defaultSlopeFor(SubgradeSide side, SubgradeComponentType type)
{
    const auto sign = side == SubgradeSide::Left ? 1.0 : -1.0;
    switch (type) {
    case SubgradeComponentType::TravelLane:
    case SubgradeComponentType::HardShoulder:
    case SubgradeComponentType::CurbStrip:
        return sign * 0.02;
    case SubgradeComponentType::EarthShoulder:
        return sign * 0.03;
    default:
        return 0.0;
    }
}

SubgradeTemplateData SubgradeTemplateDefaults::create(RoadGrade grade)
{
    SubgradeTemplateData data;
    data.properties.roadGrade = grade;
    data.properties.displayScale = 10.0;
    data.properties.name = L"\u9ed8\u8ba4\u8def\u57fa\u6a21\u677f";

    switch (grade) {
    case RoadGrade::Expressway:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::Median, 1.5},
                {SubgradeComponentType::TravelLane, 7.5},
                {SubgradeComponentType::HardShoulder, 3.0},
                {SubgradeComponentType::EarthShoulder, 0.75},
            });
        break;
    case RoadGrade::FirstClass:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::Median, 1.0},
                {SubgradeComponentType::TravelLane, 3.75},
                {SubgradeComponentType::TravelLane, 3.75},
                {SubgradeComponentType::HardShoulder, 2.5},
                {SubgradeComponentType::EarthShoulder, 0.75},
            });
        break;
    case RoadGrade::SecondClass:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::TravelLane, 3.75},
                {SubgradeComponentType::HardShoulder, 1.5},
                {SubgradeComponentType::EarthShoulder, 0.75},
            });
        break;
    case RoadGrade::ThirdClass:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::TravelLane, 3.5},
                {SubgradeComponentType::HardShoulder, 0.75},
                {SubgradeComponentType::EarthShoulder, 0.75},
            });
        break;
    case RoadGrade::FourthClass:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::TravelLane, 3.0},
                {SubgradeComponentType::HardShoulder, 0.25},
                {SubgradeComponentType::EarthShoulder, 0.5},
            });
        break;
    case RoadGrade::UrbanExpressway:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::Median, 1.0},
                {SubgradeComponentType::TravelLane, 7.5},
                {SubgradeComponentType::SideMedian, 1.0},
                {SubgradeComponentType::BikeLane, 3.0},
                {SubgradeComponentType::Sidewalk, 4.0},
            });
        break;
    case RoadGrade::UrbanArterial:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::Median, 1.5},
                {SubgradeComponentType::TravelLane, 3.5},
                {SubgradeComponentType::TravelLane, 3.5},
                {SubgradeComponentType::SideMedian, 1.5},
                {SubgradeComponentType::BikeLane, 2.5},
                {SubgradeComponentType::Sidewalk, 3.0},
            });
        break;
    case RoadGrade::UrbanSubArterial:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::TravelLane, 3.5},
                {SubgradeComponentType::TravelLane, 3.5},
                {SubgradeComponentType::BikeLane, 2.5},
                {SubgradeComponentType::Sidewalk, 3.0},
            });
        break;
    case RoadGrade::UrbanBranch:
        appendSymmetricComponents(
            data,
            {
                {SubgradeComponentType::TravelLane, 3.25},
                {SubgradeComponentType::Sidewalk, 2.0},
            });
        break;
    default:
        break;
    }

    return data;
}

bool SubgradeTemplateRules::isSupportedDisplayScale(double displayScale)
{
    return std::isfinite(displayScale)
        && (std::fabs(displayScale - 1.0) < 1.0e-9
            || std::fabs(displayScale - 10.0) < 1.0e-9
            || std::fabs(displayScale - 20.0) < 1.0e-9
            || std::fabs(displayScale - 50.0) < 1.0e-9
            || std::fabs(displayScale - 100.0) < 1.0e-9);
}

double SubgradeTemplateRules::widthAtStation(const SubgradeTemplateComponent& component, double station)
{
    if (!isFiniteValue(component.width) || component.width < 0.0 || !std::isfinite(station)) {
        return 0.0;
    }

    for (const auto& row : component.wideningTable) {
        if (stationMatches(row.station, station) && isFiniteValue(row.value)) {
            return std::max(0.0, component.width + row.value);
        }
    }

    return component.width;
}

double SubgradeTemplateRules::slopeAtStation(const SubgradeTemplateComponent& component, double station)
{
    if (component.slopeMode != SubgradeSlopeMode::VariableByStation) {
        return std::isfinite(component.fixedSlope) ? component.fixedSlope : 0.0;
    }
    if (!std::isfinite(station)) {
        return 0.0;
    }

    for (const auto& row : component.variableSlopeTable) {
        if (stationMatches(row.station, station) && isFiniteValue(row.value)) {
            return row.value;
        }
    }

    return 0.0;
}

double SubgradeTemplateRules::slopeElevationDeltaAtStation(
    const SubgradeTemplateComponent& component,
    double width,
    double station)
{
    if (!isFiniteValue(width)) {
        return 0.0;
    }

    const auto sideSign = component.side == SubgradeSide::Right ? 1.0 : -1.0;
    return sideSign * std::fabs(width) * slopeAtStation(component, station);
}

double SubgradeTemplateRules::innerCurbHeightDelta(const SubgradeTemplateComponent& component)
{
    return component.hasInnerCurb && isFiniteNonNegative(component.innerCurbHeight)
        ? component.innerCurbHeight
        : 0.0;
}

double SubgradeTemplateRules::outerCurbHeightDelta(const SubgradeTemplateComponent& component)
{
    return component.hasOuterCurb && isFiniteNonNegative(component.outerCurbHeight)
        ? -component.outerCurbHeight
        : 0.0;
}

double SubgradeTemplateRules::effectivePavementThickness(const SubgradeTemplateComponent& component)
{
    if (!component.pavementLayerLinked || !isFiniteNonNegative(component.pavementLayerThickness)) {
        return 0.0;
    }

    return component.pavementLayerThickness;
}

int SubgradeTemplateRules::applyComponentOperation(
    SubgradeTemplateData& data,
    const SubgradeComponentOperation& operation,
    std::wstring& errorMessage)
{
    if (!operation.componentType.has_value()
        && operation.kind != SubgradeComponentOperationKind::ModifyComponent) {
        errorMessage = L"Subgrade component operation is missing component type.";
        return 0;
    }

    if (operation.kind == SubgradeComponentOperationKind::AddComponent) {
        const auto type = operation.patch.type.value_or(*operation.componentType);
        if (!operation.patch.width.has_value() || !isFiniteNonNegative(*operation.patch.width) || *operation.patch.width <= 0.0) {
            errorMessage = L"Subgrade add component operation requires a positive width.";
            return 0;
        }

        int changed = 0;
        for (const auto side : sidesForOperation(operation.sideScope)) {
            auto component = createComponentFromOperation(side, type, operation.patch);
            auto insertion = insertionPointForOperation(data.components, side, operation);
            data.components.insert(insertion, component);
            ++changed;
        }

        return changed;
    }

    int leftCount = 0;
    int rightCount = 0;

    if (operation.kind == SubgradeComponentOperationKind::DeleteComponent) {
        const auto before = data.components.size();
        std::vector<SubgradeTemplateComponent> kept;
        kept.reserve(data.components.size());

        for (const auto& component : data.components) {
            bool remove = false;
            if (operationTargetsComponent(component, operation)) {
                auto& count = occurrenceCounterForSide(component.side, leftCount, rightCount);
                remove = occurrenceMatches(count, operation);
                ++count;
            }

            if (!remove) {
                kept.push_back(component);
            }
        }

        data.components = std::move(kept);
        return static_cast<int>(before - data.components.size());
    }

    int changed = 0;
    for (auto& component : data.components) {
        if (!operationTargetsComponent(component, operation)) {
            continue;
        }

        auto& count = occurrenceCounterForSide(component.side, leftCount, rightCount);
        const auto shouldModify = occurrenceMatches(count, operation);
        ++count;
        if (!shouldModify) {
            continue;
        }

        applyPatch(component, operation.patch);
        ++changed;
    }

    if (changed == 0) {
        errorMessage = L"Subgrade component operation did not match any component.";
    }
    return changed;
}

bool SubgradeTemplateRules::normalize(SubgradeTemplateData& data, std::wstring& errorMessage)
{
    if (data.properties.name.empty()) {
        data.properties.name = L"\u9ed8\u8ba4\u8def\u57fa\u6a21\u677f";
    }

    if (!isSupportedDisplayScale(data.properties.displayScale)) {
        errorMessage = L"Subgrade template display scale must be 1, 10, 20, 50, or 100.";
        return false;
    }

    for (auto& component : data.components) {
        if (!isFiniteNonNegative(component.width)) {
            errorMessage = L"Subgrade template component width must be finite and non-negative.";
            return false;
        }
        if (!isFiniteValue(component.height)) {
            errorMessage = L"Subgrade template component height must be finite.";
            return false;
        }
        component.height = 0.0;
        if (!isFiniteValue(component.fixedSlope)) {
            errorMessage = L"Subgrade template component slope must be finite.";
            return false;
        }
        if (component.slopeMode == SubgradeSlopeMode::VariableByStation) {
            component.fixedSlope = 0.0;
        }
        if (component.hasInnerCurb) {
            if (!isFiniteNonNegative(component.innerCurbWidth) ||
                !isFiniteNonNegative(component.innerCurbHeight) ||
                !isFiniteNonNegative(component.innerCurbEmbedDepth)) {
                errorMessage = L"Subgrade template inner curb dimensions must be finite and non-negative.";
                return false;
            }
        } else {
            component.innerCurbWidth = 0.0;
            component.innerCurbHeight = 0.0;
            component.innerCurbEmbedDepth = 0.0;
        }
        if (component.hasOuterCurb) {
            if (!isFiniteNonNegative(component.outerCurbWidth) ||
                !isFiniteNonNegative(component.outerCurbHeight) ||
                !isFiniteNonNegative(component.outerCurbEmbedDepth)) {
                errorMessage = L"Subgrade template outer curb dimensions must be finite and non-negative.";
                return false;
            }
        } else {
            component.outerCurbWidth = 0.0;
            component.outerCurbHeight = 0.0;
            component.outerCurbEmbedDepth = 0.0;
        }
        if (component.pavementLayerHandle.empty()) {
            component.pavementLayerLinked = false;
        }
        if (!component.pavementLayerLinked) {
            component.pavementLayerThickness = 0.0;
            component.pavementLayerHandle.clear();
            component.pavementLayerName.clear();
        } else if (!isFiniteNonNegative(component.pavementLayerThickness)) {
            errorMessage = L"Subgrade template pavement layer thickness must be finite and non-negative.";
            return false;
        }

        component.color.r = std::clamp(component.color.r, 0, 255);
        component.color.g = std::clamp(component.color.g, 0, 255);
        component.color.b = std::clamp(component.color.b, 0, 255);

        for (const auto& row : component.wideningTable) {
            if (!isFiniteValue(row.station) || !isFiniteValue(row.value)) {
                errorMessage = L"Subgrade template widening table contains non-finite values.";
                return false;
            }
        }
        for (const auto& row : component.variableSlopeTable) {
            if (!isFiniteValue(row.station) || !isFiniteValue(row.value)) {
                errorMessage = L"Subgrade template variable slope table contains non-finite values.";
                return false;
            }
        }
    }

    return true;
}

const wchar_t* roadGradeCode(RoadGrade grade)
{
    switch (grade) {
    case RoadGrade::Expressway:
        return L"Expressway";
    case RoadGrade::FirstClass:
        return L"FirstClass";
    case RoadGrade::SecondClass:
        return L"SecondClass";
    case RoadGrade::ThirdClass:
        return L"ThirdClass";
    case RoadGrade::FourthClass:
        return L"FourthClass";
    case RoadGrade::UrbanExpressway:
        return L"UrbanExpressway";
    case RoadGrade::UrbanArterial:
        return L"UrbanArterial";
    case RoadGrade::UrbanSubArterial:
        return L"UrbanSubArterial";
    case RoadGrade::UrbanBranch:
        return L"UrbanBranch";
    default:
        return L"Expressway";
    }
}

const wchar_t* subgradeSideCode(SubgradeSide side)
{
    return side == SubgradeSide::Left ? L"Left" : L"Right";
}

const wchar_t* subgradeComponentTypeCode(SubgradeComponentType type)
{
    switch (type) {
    case SubgradeComponentType::Median:
        return L"Median";
    case SubgradeComponentType::TravelLane:
        return L"TravelLane";
    case SubgradeComponentType::HardShoulder:
        return L"HardShoulder";
    case SubgradeComponentType::EarthShoulder:
        return L"EarthShoulder";
    case SubgradeComponentType::SideMedian:
        return L"SideMedian";
    case SubgradeComponentType::Sidewalk:
        return L"Sidewalk";
    case SubgradeComponentType::BikeLane:
        return L"BikeLane";
    case SubgradeComponentType::CurbStrip:
        return L"CurbStrip";
    default:
        return L"TravelLane";
    }
}

const wchar_t* subgradeComponentTypeDisplayName(SubgradeComponentType type)
{
    switch (type) {
    case SubgradeComponentType::Median:
        return L"中分带";
    case SubgradeComponentType::TravelLane:
        return L"行车道";
    case SubgradeComponentType::HardShoulder:
        return L"硬路肩";
    case SubgradeComponentType::EarthShoulder:
        return L"土路肩";
    case SubgradeComponentType::SideMedian:
        return L"侧分带";
    case SubgradeComponentType::Sidewalk:
        return L"人行道";
    case SubgradeComponentType::BikeLane:
        return L"慢车道";
    case SubgradeComponentType::CurbStrip:
        return L"路缘带";
    default:
        return L"行车道";
    }
}

const wchar_t* subgradeSlopeModeCode(SubgradeSlopeMode mode)
{
    return mode == SubgradeSlopeMode::VariableByStation ? L"VariableByStation" : L"Fixed";
}

RoadGrade roadGradeFromCode(const std::wstring& code, RoadGrade fallback)
{
    if (code == L"Expressway") {
        return RoadGrade::Expressway;
    }
    if (code == L"FirstClass") {
        return RoadGrade::FirstClass;
    }
    if (code == L"SecondClass") {
        return RoadGrade::SecondClass;
    }
    if (code == L"ThirdClass") {
        return RoadGrade::ThirdClass;
    }
    if (code == L"FourthClass") {
        return RoadGrade::FourthClass;
    }
    if (code == L"UrbanExpressway") {
        return RoadGrade::UrbanExpressway;
    }
    if (code == L"UrbanArterial") {
        return RoadGrade::UrbanArterial;
    }
    if (code == L"UrbanSubArterial") {
        return RoadGrade::UrbanSubArterial;
    }
    if (code == L"UrbanBranch") {
        return RoadGrade::UrbanBranch;
    }
    return fallback;
}

SubgradeSide subgradeSideFromCode(const std::wstring& code, SubgradeSide fallback)
{
    if (code == L"Left") {
        return SubgradeSide::Left;
    }
    if (code == L"Right") {
        return SubgradeSide::Right;
    }
    return fallback;
}

SubgradeComponentType subgradeComponentTypeFromCode(
    const std::wstring& code,
    SubgradeComponentType fallback)
{
    if (code == L"Median") {
        return SubgradeComponentType::Median;
    }
    if (code == L"TravelLane") {
        return SubgradeComponentType::TravelLane;
    }
    if (code == L"HardShoulder") {
        return SubgradeComponentType::HardShoulder;
    }
    if (code == L"EarthShoulder") {
        return SubgradeComponentType::EarthShoulder;
    }
    if (code == L"SideMedian") {
        return SubgradeComponentType::SideMedian;
    }
    if (code == L"Sidewalk") {
        return SubgradeComponentType::Sidewalk;
    }
    if (code == L"BikeLane") {
        return SubgradeComponentType::BikeLane;
    }
    if (code == L"CurbStrip") {
        return SubgradeComponentType::CurbStrip;
    }
    return fallback;
}

SubgradeSlopeMode subgradeSlopeModeFromCode(
    const std::wstring& code,
    SubgradeSlopeMode fallback)
{
    if (code == L"Fixed") {
        return SubgradeSlopeMode::Fixed;
    }
    if (code == L"VariableByStation") {
        return SubgradeSlopeMode::VariableByStation;
    }
    return fallback;
}

} // namespace roadproto::domain::cross_section
