#pragma once

#include "domain/cross_section/FullRoadPavementTemplateModel.h"

#include "dbents.h"
#include "dbmain.h"

class DnFullRoadPavementTemplateEntity : public AcDbEntity {
public:
    ACRX_DECLARE_MEMBERS(DnFullRoadPavementTemplateEntity);

    DnFullRoadPavementTemplateEntity();

    Acad::ErrorStatus setTemplateData(
        const roadproto::domain::cross_section::FullRoadPavementTemplateData& data);
    const roadproto::domain::cross_section::FullRoadPavementTemplateData& templateData() const;
    void setInsertionPoint(const AcGePoint3d& point);
    AcGePoint3d insertionPoint() const;

    Acad::ErrorStatus dwgInFields(AcDbDwgFiler* filer) override;
    Acad::ErrorStatus dwgOutFields(AcDbDwgFiler* filer) const override;

protected:
    Adesk::Boolean subWorldDraw(AcGiWorldDraw* worldDraw) override;
    Acad::ErrorStatus subGetGeomExtents(AcDbExtents& extents) const override;
    Acad::ErrorStatus subTransformBy(const AcGeMatrix3d& transform) override;
    Acad::ErrorStatus subGetGripPoints(
        AcGePoint3dArray& gripPoints,
        AcDbIntArray& osnapModes,
        AcDbIntArray& geomIds) const override;
    Acad::ErrorStatus subMoveGripPointsAt(const AcDbIntArray& indices, const AcGeVector3d& offset) override;

private:
    roadproto::domain::cross_section::FullRoadPavementTemplateData templateData_;
    AcGePoint3d insertionPoint_;
    AcGeVector3d xAxis_;
    AcGeVector3d yAxis_;
};

namespace roadproto::cad_adapter::objectarx {

void initializeFullRoadPavementTemplateEntityClass();
void uninitializeFullRoadPavementTemplateEntityClass();

} // namespace roadproto::cad_adapter::objectarx
