#pragma once

#include <string>
#include <optional>
#include <vector>

namespace roadproto::domain::cross_section {

enum class RoadGrade {
    Expressway,
    FirstClass,
    SecondClass,
    ThirdClass,
    FourthClass,
    UrbanExpressway,
    UrbanArterial,
    UrbanSubArterial,
    UrbanBranch
};

enum class SubgradeSide {
    Left,
    Right
};

enum class SubgradeComponentType {
    Median,
    TravelLane,
    HardShoulder,
    EarthShoulder,
    SideMedian,
    Sidewalk,
    BikeLane,
    CurbStrip
};

enum class SubgradeSlopeMode {
    Fixed,
    VariableByStation
};

struct SubgradeTemplateRgbColor {
    int r = 120;
    int g = 120;
    int b = 120;
};

struct SubgradeStationValue {
    double station = 0.0;
    double value = 0.0;
};

struct SubgradeTemplateComponent {
    SubgradeSide side = SubgradeSide::Right;
    SubgradeComponentType type = SubgradeComponentType::TravelLane;
    double width = 0.0;
    double height = 0.0;
    double fixedSlope = 0.0;
    SubgradeSlopeMode slopeMode = SubgradeSlopeMode::Fixed;
    SubgradeTemplateRgbColor color;
    std::vector<SubgradeStationValue> wideningTable;
    std::vector<SubgradeStationValue> variableSlopeTable;
    bool hasInnerCurb = false;
    double innerCurbWidth = 0.0;
    double innerCurbHeight = 0.0;
    double innerCurbEmbedDepth = 0.0;
    bool hasOuterCurb = false;
    double outerCurbWidth = 0.0;
    double outerCurbHeight = 0.0;
    double outerCurbEmbedDepth = 0.0;
    bool pavementLayerLinked = false;
    std::wstring pavementLayerHandle;
    std::wstring pavementLayerName;
    double pavementLayerThickness = 0.0;
};

struct SubgradeTemplateProperties {
    std::wstring name = L"\u9ed8\u8ba4\u8def\u57fa\u6a21\u677f";
    double displayScale = 10.0;
    RoadGrade roadGrade = RoadGrade::Expressway;
};

struct SubgradeTemplateData {
    SubgradeTemplateProperties properties;
    std::vector<SubgradeTemplateComponent> components;
    std::wstring roadCenterlineHandle;
};

enum class SubgradeComponentOperationKind {
    ModifyComponent,
    AddComponent,
    DeleteComponent
};

enum class SubgradeComponentSideScope {
    Left,
    Right,
    Both
};

enum class SubgradeComponentOccurrence {
    All,
    First,
    Second,
    Index
};

enum class SubgradeComponentPositionMode {
    OutsideOf,
    InsideOf,
    Before,
    After
};

struct SubgradeComponentPatch {
    std::optional<SubgradeComponentType> type;
    std::optional<double> width;
    std::optional<double> widthDelta;
    std::optional<double> height;
    std::optional<double> fixedSlope;
    std::optional<SubgradeSlopeMode> slopeMode;
    std::optional<SubgradeTemplateRgbColor> color;
    std::optional<std::vector<SubgradeStationValue>> wideningTable;
    std::optional<std::vector<SubgradeStationValue>> variableSlopeTable;
    std::optional<bool> hasInnerCurb;
    std::optional<double> innerCurbWidth;
    std::optional<double> innerCurbHeight;
    std::optional<double> innerCurbEmbedDepth;
    std::optional<bool> hasOuterCurb;
    std::optional<double> outerCurbWidth;
    std::optional<double> outerCurbHeight;
    std::optional<double> outerCurbEmbedDepth;
    std::optional<bool> pavementLayerLinked;
    std::optional<std::wstring> pavementLayerHandle;
    std::optional<std::wstring> pavementLayerName;
    std::optional<double> pavementLayerThickness;
};

struct SubgradeComponentOperation {
    SubgradeComponentOperationKind kind = SubgradeComponentOperationKind::ModifyComponent;
    SubgradeComponentSideScope sideScope = SubgradeComponentSideScope::Both;
    std::optional<SubgradeComponentType> componentType;
    SubgradeComponentOccurrence occurrence = SubgradeComponentOccurrence::All;
    int occurrenceIndex = -1;
    std::optional<SubgradeComponentPositionMode> positionMode;
    std::optional<SubgradeComponentType> anchorType;
    SubgradeComponentPatch patch;
};

class SubgradeTemplateDefaults {
public:
    static SubgradeTemplateData create(RoadGrade grade);
    static SubgradeTemplateRgbColor defaultColorFor(SubgradeComponentType type);
    static SubgradeTemplateRgbColor defaultColorFor(SubgradeSide side, SubgradeComponentType type);
    static int defaultColorIndexFor(SubgradeSide side, SubgradeComponentType type);
    static double defaultSlopeFor(SubgradeSide side, SubgradeComponentType type);
};

class SubgradeTemplateRules {
public:
    static bool isSupportedDisplayScale(double displayScale);
    static double widthAtStation(const SubgradeTemplateComponent& component, double station);
    static double slopeAtStation(const SubgradeTemplateComponent& component, double station);
    static double slopeElevationDeltaAtStation(
        const SubgradeTemplateComponent& component,
        double width,
        double station);
    static double innerCurbHeightDelta(const SubgradeTemplateComponent& component);
    static double outerCurbHeightDelta(const SubgradeTemplateComponent& component);
    static double effectivePavementThickness(const SubgradeTemplateComponent& component);
    static int applyComponentOperation(
        SubgradeTemplateData& data,
        const SubgradeComponentOperation& operation,
        std::wstring& errorMessage);
    static bool normalize(SubgradeTemplateData& data, std::wstring& errorMessage);
};

const wchar_t* roadGradeCode(RoadGrade grade);
const wchar_t* subgradeSideCode(SubgradeSide side);
const wchar_t* subgradeComponentTypeCode(SubgradeComponentType type);
const wchar_t* subgradeComponentTypeDisplayName(SubgradeComponentType type);
const wchar_t* subgradeSlopeModeCode(SubgradeSlopeMode mode);
RoadGrade roadGradeFromCode(const std::wstring& code, RoadGrade fallback = RoadGrade::Expressway);
SubgradeSide subgradeSideFromCode(const std::wstring& code, SubgradeSide fallback = SubgradeSide::Right);
SubgradeComponentType subgradeComponentTypeFromCode(
    const std::wstring& code,
    SubgradeComponentType fallback = SubgradeComponentType::TravelLane);
SubgradeSlopeMode subgradeSlopeModeFromCode(
    const std::wstring& code,
    SubgradeSlopeMode fallback = SubgradeSlopeMode::Fixed);

} // namespace roadproto::domain::cross_section
