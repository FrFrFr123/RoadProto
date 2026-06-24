#pragma once

#include <string>
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
