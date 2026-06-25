using RoadProto.Terrain.UI.Agent;
using RoadProto.Terrain.UI.Bridge;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string NewTempFile()
{
    return Path.Combine(Path.GetTempPath(), $"RoadProtoManagedBridgeTests_{Guid.NewGuid():N}.response");
}

static string FindRepoRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RoadProto.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    throw new InvalidOperationException("Cannot find RoadProto repository root.");
}

static void ExpectThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static System.Reflection.PropertyInfo RequiredProperty(Type type, string propertyName)
{
    var property = type.GetProperty(propertyName);
    Check(property != null, $"{type.Name} should expose {propertyName}");
    return property!;
}

static object RequiredEnumValue(string enumTypeName, string valueName)
{
    var enumType = typeof(SubgradeTemplateDialogResponse).Assembly.GetType(enumTypeName);
    Check(enumType != null, $"{enumTypeName} should exist");
    return Enum.Parse(enumType!, valueName);
}

static void WithCulture(string cultureName, Action action)
{
    var originalCulture = Thread.CurrentThread.CurrentCulture;
    var originalUICulture = Thread.CurrentThread.CurrentUICulture;
    try
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        action();
    }
    finally
    {
        Thread.CurrentThread.CurrentCulture = originalCulture;
        Thread.CurrentThread.CurrentUICulture = originalUICulture;
    }
}

static void ResponseWritesPickTerrainAction()
{
    var path = NewTempFile();
    try
    {
        AlignmentDialogFile.WriteResponse(path, new AlignmentDialogResponse
        {
            Accepted = true,
            Action = AlignmentDialogAction.PickTerrain,
            Mode = AlignmentDialogMode.Full,
            Handle = "1A2",
            DeleteOnCancel = true,
            RoadName = "K1",
            RoadGradeIndex = 9,
            StationLabelInterval = 100,
            Radius = 80,
            Ls1 = 20,
            Ls2 = 20,
        });

        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("action=pickTerrain"), "response file should request terrain picking");
        Check(content.Contains("handle=1A2"), "response should keep target centerline handle");
        Check(content.Contains("deleteOnCancel=1"), "response should keep create-cancel cleanup flag");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void SubgradeRequestReadsPersistedEntityComponents()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=2B",
            "responsePath=C:/temp/subgrade.response",
            "insertionX=10.5",
            "insertionY=20.25",
            "insertionZ=0",
            "templateName=edited-template",
            "displayScale=20",
            "roadGrade=FirstClass",
            "roadCenterlineHandle=1A",
            "componentCount=2",
            "component.0.side=Left",
            "component.0.type=TravelLane",
            "component.0.width=3.75",
            "component.0.height=0.1",
            "component.0.fixedSlope=0.02",
            "component.0.slopeMode=VariableByStation",
            "component.0.colorR=10",
            "component.0.colorG=20",
            "component.0.colorB=30",
            "component.0.wideningCount=1",
            "component.0.widening.0.station=100",
            "component.0.widening.0.value=0.5",
            "component.0.slopeTableCount=1",
            "component.0.slopeTable.0.station=100",
            "component.0.slopeTable.0.value=-0.025",
            "component.0.hasInnerCurb=1",
            "component.0.innerCurbWidth=0.2",
            "component.0.innerCurbHeight=0.18",
            "component.0.innerCurbEmbedDepth=0.12",
            "component.0.hasOuterCurb=1",
            "component.0.outerCurbWidth=0.25",
            "component.0.outerCurbHeight=0.2",
            "component.0.outerCurbEmbedDepth=0.1",
            "component.0.pavementLayerLinked=1",
            "component.0.pavementLayerHandle=44",
            "component.0.pavementLayerName=主线%25%0A结构层",
            "component.0.pavementLayerThickness=0.28",
            "component.1.side=Right",
            "component.1.type=HardShoulder",
            "component.1.width=2.5",
            "component.1.height=0",
            "component.1.fixedSlope=0.015",
            "component.1.slopeMode=Fixed",
            "component.1.colorR=0",
            "component.1.colorG=90",
            "component.1.colorB=180",
            "component.1.wideningCount=0",
            "component.1.slopeTableCount=0",
            "component.1.pavementLayerLinked=0",
            "component.1.pavementLayerHandle=",
            "component.1.pavementLayerName=",
            "component.1.pavementLayerThickness=0",
        }, Encoding.UTF8);

        var request = SubgradeTemplateDialogFile.ReadRequest(path);
        Check(request.Handle == "2B", "edit request should keep entity handle");
        Check(request.RoadGrade == SubgradeRoadGrade.FirstClass, "edit request should keep road grade");
        Check(request.Components.Count == 2, "edit request should keep persisted components");
        Check(request.Components[0].Side == SubgradeSide.Left, "first component side should round-trip");
        Check(request.Components[0].Type == SubgradeComponentType.TravelLane, "first component type should round-trip");
        Check(Math.Abs(request.Components[0].Width - 3.75) < 1.0e-9, "first component width should round-trip");
        Check(request.Components[0].SlopeMode == SubgradeSlopeMode.VariableByStation, "slope mode should round-trip");
        Check(request.Components[0].VariableSlopeTable.Count == 1, "variable slope table should round-trip");
        Check(Math.Abs(request.Components[0].VariableSlopeTable[0].Value + 0.025) < 1.0e-9, "slope value should round-trip");
        Check(request.Components[0].HasInnerCurb, "inner curb flag should round-trip");
        Check(Math.Abs(request.Components[0].InnerCurbWidth - 0.2) < 1.0e-9, "inner curb width should round-trip");
        Check(Math.Abs(request.Components[0].InnerCurbHeight - 0.18) < 1.0e-9, "inner curb height should round-trip");
        Check(Math.Abs(request.Components[0].InnerCurbEmbedDepth - 0.12) < 1.0e-9, "inner curb embed depth should round-trip");
        Check(request.Components[0].HasOuterCurb, "outer curb flag should round-trip");
        Check(Math.Abs(request.Components[0].OuterCurbWidth - 0.25) < 1.0e-9, "outer curb width should round-trip");
        Check(Math.Abs(request.Components[0].OuterCurbHeight - 0.2) < 1.0e-9, "outer curb height should round-trip");
        Check(Math.Abs(request.Components[0].OuterCurbEmbedDepth - 0.1) < 1.0e-9, "outer curb embed depth should round-trip");
        Check(request.Components[0].PavementLayerLinked, "pavement link should round-trip");
        Check((string)RequiredProperty(typeof(SubgradeComponentDto), "PavementLayerName").GetValue(request.Components[0])! == "主线%\n结构层", "pavement layer name should round-trip");
        Check(request.Components[1].Side == SubgradeSide.Right, "second component side should round-trip");
        Check(request.Components[1].Type == SubgradeComponentType.HardShoulder, "second component type should round-trip");
        Check(Math.Abs(request.Components[1].Width - 2.5) < 1.0e-9, "second component width should round-trip");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void SubgradeResponseWritesPavementTemplatePickActionAndPreservesRows()
{
    var path = NewTempFile();
    try
    {
        var response = new SubgradeTemplateDialogResponse
        {
            Accepted = false,
            Handle = "SG-1",
            InsertionX = 1.5,
            InsertionY = 2.5,
            InsertionZ = 0,
            TemplateName = "路基模板",
            DisplayScale = 10,
            RoadGrade = SubgradeRoadGrade.SecondClass,
            RoadCenterlineHandle = "CL-1",
        };
        RequiredProperty(typeof(SubgradeTemplateDialogResponse), "Action").SetValue(
            response,
            RequiredEnumValue("RoadProto.Terrain.UI.Bridge.SubgradeTemplateDialogAction", "PickPavementLayerTemplate"));
        RequiredProperty(typeof(SubgradeTemplateDialogResponse), "PickComponentIndex").SetValue(response, 1);

        var first = new SubgradeComponentDto
        {
            Side = SubgradeSide.Left,
            Type = SubgradeComponentType.TravelLane,
            Width = 3.75,
            HasInnerCurb = true,
            InnerCurbWidth = 0.2,
            InnerCurbHeight = 0.18,
            InnerCurbEmbedDepth = 0.12,
            HasOuterCurb = true,
            OuterCurbWidth = 0.25,
            OuterCurbHeight = 0.2,
            OuterCurbEmbedDepth = 0.1,
            PavementLayerLinked = true,
            PavementLayerHandle = "PV%1",
            PavementLayerThickness = 0.28,
        };
        RequiredProperty(typeof(SubgradeComponentDto), "PavementLayerName").SetValue(first, "主线\n结构层");
        response.Components.Add(first);
        response.Components.Add(new SubgradeComponentDto
        {
            Side = SubgradeSide.Right,
            Type = SubgradeComponentType.HardShoulder,
            Width = 2.5,
        });

        SubgradeTemplateDialogFile.WriteResponse(path, response);
        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("action=pickPavementLayerTemplate"), "subgrade response should request pavement template picking");
        Check(content.Contains("accepted=0"), "picking should close the WPF dialog without accepting final changes");
        Check(content.Contains("pickComponentIndex=1"), "subgrade response should keep selected component index");
        Check(content.Contains("componentCount=2"), "subgrade pick response should preserve current component rows");
        Check(content.Contains("component.0.hasInnerCurb=1"), "subgrade response should write inner curb flag");
        Check(content.Contains("component.0.innerCurbWidth=0.2"), "subgrade response should write inner curb width");
        Check(content.Contains("component.0.innerCurbHeight=0.18"), "subgrade response should write inner curb height");
        Check(content.Contains("component.0.innerCurbEmbedDepth=0.12"), "subgrade response should write inner curb embed depth");
        Check(content.Contains("component.0.hasOuterCurb=1"), "subgrade response should write outer curb flag");
        Check(content.Contains("component.0.outerCurbWidth=0.25"), "subgrade response should write outer curb width");
        Check(content.Contains("component.0.outerCurbHeight=0.2"), "subgrade response should write outer curb height");
        Check(content.Contains("component.0.outerCurbEmbedDepth=0.1"), "subgrade response should write outer curb embed depth");
        Check(content.Contains("component.0.pavementLayerHandle=PV%251"), "subgrade response should escape pavement template handle");
        Check(content.Contains("component.0.pavementLayerName=主线%0A结构层"), "subgrade response should write pavement template name");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelRequestReadsAssignmentsUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=AA11",
            "responsePath=C:/temp/road-model.response",
            "roadCenterlineHandle=CL%251",
            "profileVerticalCurveHandle=VC%0A2",
            "sampleInterval=12.5",
            "assignmentCount=2",
            "assignment.0.startStation=0.5",
            "assignment.0.endStation=100.25",
            "assignment.0.templateHandle=TPL%250",
            "assignment.0.templateName=主线%25%0A模板",
            "assignment.1.startStation=100.25",
            "assignment.1.endStation=180.75",
            "assignment.1.templateHandle=TPL-2",
            "assignment.1.templateName=secondary",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = RoadModelDialogFile.ReadRequest(path);
            Check(request.Handle == "AA11", "road model request should keep handle");
            Check(request.ResponsePath == "C:/temp/road-model.response", "road model request should require response path");
            Check(request.RoadCenterlineHandle == "CL%1", "road centerline handle should unescape percent");
            Check(request.ProfileVerticalCurveHandle == "VC\n2", "vertical curve handle should unescape newline");
            Check(Math.Abs(request.SampleInterval - 12.5) < 1.0e-9, "sample interval should parse with invariant culture");
            Check(request.Assignments.Count == 2, "assignmentCount should control assignment rows");
            Check(Math.Abs(request.Assignments[0].StartStation - 0.5) < 1.0e-9, "assignment start station should parse with invariant culture");
            Check(Math.Abs(request.Assignments[0].EndStation - 100.25) < 1.0e-9, "assignment end station should parse with invariant culture");
            Check(request.Assignments[0].TemplateHandle == "TPL%0", "template handle should unescape percent");
            Check(request.Assignments[0].TemplateName == "主线%\n模板", "template name should unescape UTF-8, percent, and newline");
        });
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void SlopeTemplateDialogFileRoundTripsComponents()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=SL-1",
            "responsePath=C:/temp/slope-template.response",
            "insertionX=1.5",
            "insertionY=2.25",
            "insertionZ=3.75",
            "templateName=边坡%0A模板",
            "displayScale=10",
            "kind=Cut",
            "stopAtGround=1",
            "repeatLastGroupUntilGround=true",
            "componentCount=1",
            "component.0.type=Berm",
            "component.0.constraintMode=SlopeAndWidth",
            "component.0.slope=0.03",
            "component.0.height=0.5",
            "component.0.width=1.25",
            "component.0.groundSearchHeightIncrement=2.5",
            "component.0.colorR=10",
            "component.0.colorG=20",
            "component.0.colorB=30",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = SlopeTemplateDialogFile.ReadRequest(path);
            Check(request.Handle == "SL-1", "slope template request should keep handle");
            Check(request.TemplateName == "边坡\n模板", "slope template name should unescape newline");
            Check(request.Kind == SlopeTemplateKind.Cut, "slope template kind should parse");
            Check(request.StopAtGround, "slope template stop-at-ground should parse");
            Check(request.RepeatLastGroupUntilGround, "slope template repeat-last-group should parse");
            Check(request.Components.Count == 1, "slope template component count should parse");
            Check(request.Components[0].Type == SlopeComponentType.Berm, "slope component type should parse");
            Check(request.Components[0].ConstraintMode == SlopeGeometryConstraintMode.SlopeAndWidth, "slope component mode should parse");
            Check(Math.Abs(request.Components[0].GroundSearchHeightIncrement - 2.5) < 1.0e-9, "slope component search increment should parse invariant decimal");
        });

        var response = new SlopeTemplateDialogResponse
        {
            Accepted = true,
            Handle = "SL%1",
            InsertionX = 1.5,
            InsertionY = 2.25,
            InsertionZ = 3.75,
            TemplateName = "边坡\n模板",
            DisplayScale = 10,
            Kind = SlopeTemplateKind.Fill,
            StopAtGround = true,
            RepeatLastGroupUntilGround = false,
        };
        response.Components.Add(new SlopeComponentDto
        {
            Type = SlopeComponentType.FillSlope,
            ConstraintMode = SlopeGeometryConstraintMode.SlopeAndHeight,
            Slope = -0.6666666666666666,
            Height = 4,
            Width = 6,
            GroundSearchHeightIncrement = 2,
            ColorR = 30,
            ColorG = 132,
            ColorB = 88,
        });

        SlopeTemplateDialogFile.WriteResponse(path, response);
        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("accepted=1"), "slope template response should record accepted state");
        Check(content.Contains("handle=SL%251"), "slope template response should escape percent in handle");
        Check(content.Contains("templateName=边坡%0A模板"), "slope template response should escape newline in name");
        Check(content.Contains("componentCount=1"), "slope template response should write component count");
        Check(content.Contains("component.0.groundSearchHeightIncrement=2"), "slope template response should write search increment");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelResponseWritesAssignmentsUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        WithCulture("de-DE", () =>
        {
            var response = new RoadModelDialogResponse
            {
                Accepted = true,
                Handle = "RM-1",
                RoadCenterlineHandle = "CL\n1",
                ProfileVerticalCurveHandle = "VC%2",
                SampleInterval = 7.5,
            };
            response.Assignments.Add(new RoadModelTemplateAssignmentDto
            {
                StartStation = 0.25,
                EndStation = 50.75,
                TemplateHandle = "TPL%1",
                TemplateName = "左幅\n模板",
            });
            response.Assignments.Add(new RoadModelTemplateAssignmentDto
            {
                StartStation = 50.75,
                EndStation = 120.5,
                TemplateHandle = "TPL-2",
                TemplateName = "right",
            });

            RoadModelDialogFile.WriteResponse(path, response);
        });

        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("accepted=1"), "road model response should record accepted state");
        Check(content.Contains("sampleInterval=7.5"), "road model response should write invariant decimal separator");
        Check(!content.Contains("sampleInterval=7,5"), "road model response should not use current culture decimal separator");
        Check(content.Contains("assignmentCount=2"), "road model response should write assignmentCount");
        Check(content.Contains("assignment.0.startStation=0.25"), "road model response should write assignment start station");
        Check(content.Contains("assignment.1.endStation=120.5"), "road model response should write assignment end station");
        Check(content.Contains("roadCenterlineHandle=CL%0A1"), "road centerline handle should escape newline");
        Check(content.Contains("profileVerticalCurveHandle=VC%252"), "vertical curve handle should escape percent");
        Check(content.Contains("assignment.0.templateName=左幅%0A模板"), "template name should escape newline");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelResponseWritesPickTemplateActionAndRowIndex()
{
    var path = NewTempFile();
    try
    {
        var response = new RoadModelDialogResponse
        {
            Action = RoadModelDialogAction.PickTemplate,
            Accepted = false,
            PickAssignmentIndex = 1,
            Handle = "RM-1",
            RoadCenterlineHandle = "CL-1",
            ProfileVerticalCurveHandle = "VC-1",
            SampleInterval = 10.0,
        };
        response.Assignments.Add(new RoadModelTemplateAssignmentDto
        {
            StartStation = 0,
            EndStation = 100,
            TemplateHandle = "TPL-A",
            TemplateName = "模板A",
        });
        response.Assignments.Add(new RoadModelTemplateAssignmentDto
        {
            StartStation = 100,
            EndStation = 200,
            TemplateHandle = string.Empty,
            TemplateName = string.Empty,
        });

        RoadModelDialogFile.WriteResponse(path, response);
        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("action=pickTemplate"), "road model response should request template picking");
        Check(content.Contains("pickAssignmentIndex=1"), "road model response should keep selected assignment index");
        Check(content.Contains("assignmentCount=2"), "road model response should keep current rows when picking a template");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelSlopeGroupsRoundTripUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=RM-1",
            "responsePath=C:/temp/road-model.response",
            "roadCenterlineHandle=CL-1",
            "profileVerticalCurveHandle=VC-1",
            "sampleInterval=10",
            "leftSlopeSearchWidth=45.5",
            "rightSlopeSearchWidth=55.25",
            "selectedLeftSlopeGroupIndex=1",
            "selectedRightSlopeGroupIndex=0",
            "assignmentCount=0",
            "leftSlopeGroupCount=1",
            "leftSlopeGroup.0.startStation=0.5",
            "leftSlopeGroup.0.endStation=100.25",
            "leftSlopeGroup.0.templateCount=2",
            "leftSlopeGroup.0.template.0.templateHandle=SL%251",
            "leftSlopeGroup.0.template.0.templateName=填方%0A模板",
            "leftSlopeGroup.0.template.1.templateHandle=SL-2",
            "leftSlopeGroup.0.template.1.templateName=备用",
            "rightSlopeGroupCount=0",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = RoadModelDialogFile.ReadRequest(path);
            Check(Math.Abs(request.LeftSlopeSearchWidth - 45.5) < 1.0e-9, "left slope search width should parse with invariant culture");
            Check(Math.Abs(request.RightSlopeSearchWidth - 55.25) < 1.0e-9, "right slope search width should parse with invariant culture");
            Check(request.SelectedLeftSlopeGroupIndex == 1, "request should keep selected left slope group");
            Check(request.SelectedRightSlopeGroupIndex == 0, "request should keep selected right slope group");
            Check(request.LeftSlopeGroups.Count == 1, "request should read left slope group count");
            Check(request.LeftSlopeGroups[0].Templates.Count == 2, "request should read slope templates in group");
            Check(request.LeftSlopeGroups[0].Templates[0].TemplateHandle == "SL%1", "slope template handle should unescape percent");
            Check(request.LeftSlopeGroups[0].Templates[0].TemplateName == "填方\n模板", "slope template name should unescape newline");
        });

        var response = new RoadModelDialogResponse
        {
            Action = RoadModelDialogAction.PickLeftSlopeTemplate,
            Accepted = false,
            PickSlopeGroupIndex = 0,
            Handle = "RM-1",
            RoadCenterlineHandle = "CL-1",
            ProfileVerticalCurveHandle = "VC-1",
            SampleInterval = 10,
            LeftSlopeSearchWidth = 45.5,
            RightSlopeSearchWidth = 55.25,
        };
        response.LeftSlopeGroups.Add(new RoadModelSlopeTemplateGroupDto
        {
            StartStation = 0.5,
            EndStation = 100.25,
            Templates = new List<RoadModelSlopeTemplateReferenceDto>
            {
                new() { TemplateHandle = "SL%1", TemplateName = "填方\n模板" },
            },
        });

        RoadModelDialogFile.WriteResponse(path, response);
        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("action=pickLeftSlopeTemplate"), "road model response should request left slope template picking");
        Check(content.Contains("pickSlopeGroupIndex=0"), "road model response should keep selected slope group index");
        Check(content.Contains("leftSlopeSearchWidth=45.5"), "response should write left slope search width using invariant culture");
        Check(content.Contains("leftSlopeGroupCount=1"), "response should write left slope group count");
        Check(content.Contains("leftSlopeGroup.0.templateCount=1"), "response should write template count in slope group");
        Check(content.Contains("leftSlopeGroup.0.template.0.templateHandle=SL%251"), "response should escape slope template handle percent");
        Check(content.Contains("leftSlopeGroup.0.template.0.templateName=填方%0A模板"), "response should escape slope template name newline");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelStructuresRoundTripUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=RM-1",
            "responsePath=C:/temp/road-model.response",
            "roadCenterlineHandle=CL-1",
            "profileVerticalCurveHandle=VC-1",
            "sampleInterval=10",
            "assignmentCount=0",
            "leftSlopeGroupCount=0",
            "rightSlopeGroupCount=0",
            "structureCount=2",
            "structure.0.startStation=12.5",
            "structure.0.endStation=34.75",
            "structure.0.type=Bridge",
            "structure.0.sideRange=Left",
            "structure.1.startStation=40",
            "structure.1.endStation=60",
            "structure.1.type=Tunnel",
            "structure.1.sideRange=Both",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = RoadModelDialogFile.ReadRequest(path);
            Check(request.Structures.Count == 2, "road model request should read structure rows");
            Check(Math.Abs(request.Structures[0].StartStation - 12.5) < 1.0e-9, "structure start station should parse invariant decimal");
            Check(Math.Abs(request.Structures[0].EndStation - 34.75) < 1.0e-9, "structure end station should parse invariant decimal");
            Check(request.Structures[0].Type == RoadModelStructureType.Bridge, "structure type should parse bridge");
            Check(request.Structures[0].SideRange == RoadModelStructureSideRange.Left, "structure side range should parse left");
            Check(request.Structures[1].Type == RoadModelStructureType.Tunnel, "structure type should parse tunnel");
            Check(request.Structures[1].SideRange == RoadModelStructureSideRange.Both, "structure side range should parse both");
        });

        var response = new RoadModelDialogResponse
        {
            Accepted = true,
            Handle = "RM-1",
            RoadCenterlineHandle = "CL-1",
            ProfileVerticalCurveHandle = "VC-1",
            SampleInterval = 10,
        };
        response.Structures.Add(new RoadModelStructureRangeDto
        {
            StartStation = 12.5,
            EndStation = 34.75,
            Type = RoadModelStructureType.Bridge,
            SideRange = RoadModelStructureSideRange.Left,
        });
        response.Structures.Add(new RoadModelStructureRangeDto
        {
            StartStation = 40,
            EndStation = 60,
            Type = RoadModelStructureType.Tunnel,
            SideRange = RoadModelStructureSideRange.Both,
        });

        RoadModelDialogFile.WriteResponse(path, response);
        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("structureCount=2"), "road model response should write structure count");
        Check(content.Contains("structure.0.startStation=12.5"), "road model response should write structure start station");
        Check(content.Contains("structure.0.type=Bridge"), "road model response should write bridge type");
        Check(content.Contains("structure.0.sideRange=Left"), "road model response should write left side range");
        Check(content.Contains("structure.1.type=Tunnel"), "road model response should write tunnel type");
        Check(content.Contains("structure.1.sideRange=Both"), "road model response should write both side range");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelRequestReadsSelectedAssignmentIndex()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=RM-1",
            "responsePath=C:/temp/road-model.response",
            "roadCenterlineHandle=CL-1",
            "profileVerticalCurveHandle=VC-1",
            "sampleInterval=10",
            "selectedAssignmentIndex=2",
            "assignmentCount=0",
        }, Encoding.UTF8);

        var request = RoadModelDialogFile.ReadRequest(path);
        Check(request.SelectedAssignmentIndex == 2, "road model request should keep selected assignment index after template pick");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelRequestRejectsMissingOrEmptyResponsePath()
{
    var missingPath = NewTempFile();
    var emptyPath = NewTempFile();
    try
    {
        File.WriteAllLines(missingPath, new[]
        {
            "handle=RM-1",
            "sampleInterval=10",
            "assignmentCount=0",
        }, Encoding.UTF8);
        File.WriteAllLines(emptyPath, new[]
        {
            "handle=RM-1",
            "responsePath=   ",
            "sampleInterval=10",
            "assignmentCount=0",
        }, Encoding.UTF8);

        ExpectThrows<InvalidDataException>(
            () => RoadModelDialogFile.ReadRequest(missingPath),
            "road model request should reject missing responsePath");
        ExpectThrows<InvalidDataException>(
            () => RoadModelDialogFile.ReadRequest(emptyPath),
            "road model request should reject empty responsePath");
    }
    finally
    {
        if (File.Exists(missingPath))
        {
            File.Delete(missingPath);
        }
        if (File.Exists(emptyPath))
        {
            File.Delete(emptyPath);
        }
    }
}

static void RoadModelSectionViewerRequestReadsPreviewsUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=RM%251",
            "roadCenterlineHandle=CL%0A1",
            "responsePath=C:/Temp/road-model-section-viewer.response",
            "previewCount=1",
            "preview.0.station=10.5",
            "preview.0.stationLabel=K0+010.5",
            "preview.0.statusMessage=已生成%0A预览",
            "preview.0.hasGroundLine=1",
            "preview.0.segmentCount=3",
            "preview.0.segment.0.kind=Subgrade",
            "preview.0.segment.0.label=路基模板",
            "preview.0.segment.0.colorR=1",
            "preview.0.segment.0.colorG=2",
            "preview.0.segment.0.colorB=3",
            "preview.0.segment.0.pointCount=2",
            "preview.0.segment.0.point.0.offset=0",
            "preview.0.segment.0.point.0.elevation=101.25",
            "preview.0.segment.0.point.1.offset=-3.5",
            "preview.0.segment.0.point.1.elevation=101.18",
            "preview.0.segment.1.kind=Ground",
            "preview.0.segment.1.label=地面线",
            "preview.0.segment.1.colorR=132",
            "preview.0.segment.1.colorG=96",
            "preview.0.segment.1.colorB=56",
            "preview.0.segment.1.pointCount=2",
            "preview.0.segment.1.point.0.offset=-10",
            "preview.0.segment.1.point.0.elevation=98.5",
            "preview.0.segment.1.point.1.offset=10",
            "preview.0.segment.1.point.1.elevation=103.75",
            "preview.0.segment.2.kind=PavementLayer",
            "preview.0.segment.2.label=结构层",
            "preview.0.segment.2.colorR=196",
            "preview.0.segment.2.colorG=86",
            "preview.0.segment.2.colorB=28",
            "preview.0.segment.2.pointCount=2",
            "preview.0.segment.2.point.0.offset=-3.5",
            "preview.0.segment.2.point.0.elevation=101.12",
            "preview.0.segment.2.point.1.offset=3.5",
            "preview.0.segment.2.point.1.elevation=101.12",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = RoadModelSectionViewerFile.ReadRequest(path);
            Check(request.Handle == "RM%1", "section viewer request should unescape handle percent");
            Check(request.RoadCenterlineHandle == "CL\n1", "section viewer request should unescape centerline newline");
            Check(request.ResponsePath == "C:/Temp/road-model-section-viewer.response", "section viewer request should read response path");
            Check(request.Previews.Count == 1, "section viewer request should read preview count");
            Check(Math.Abs(request.Previews[0].Station - 10.5) < 1.0e-9, "section viewer station should parse invariant decimal");
            Check(request.Previews[0].StatusMessage == "已生成\n预览", "section viewer status should unescape newline");
            Check(request.Previews[0].HasGroundLine, "section viewer should keep ground line flag");
            Check(request.Previews[0].Segments.Count == 3, "section viewer should read segments");
            Check(request.Previews[0].Segments[0].Kind == RoadModelSectionViewerSegmentKind.Subgrade, "section viewer should parse segment kind");
            var pavementLayerKind = RequiredEnumValue("RoadProto.Terrain.UI.Bridge.RoadModelSectionViewerSegmentKind", "PavementLayer");
            Check(request.Previews[0].Segments[2].Kind.Equals(pavementLayerKind), "section viewer should parse pavement layer segment kind");
            Check(request.Previews[0].Segments[2].Label == "结构层", "section viewer should read pavement layer label");
            Check(request.Previews[0].Segments[0].Points.Count == 2, "section viewer should read segment points");
            Check(Math.Abs(request.Previews[0].Segments[0].Points[1].Offset + 3.5) < 1.0e-9, "section viewer point offset should parse invariant decimal");
        });
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelSectionViewerResponseWritesDrawSectionsAction()
{
    var path = NewTempFile();
    try
    {
        RoadModelSectionViewerFile.WriteResponse(path, new RoadModelSectionViewerResponse
        {
            Action = RoadModelSectionViewerAction.DrawSections,
            Accepted = true,
            Handle = "RM-1",
        });

        var text = File.ReadAllText(path, Encoding.UTF8);
        Check(text.Contains("action=drawSections"), "section viewer response should write drawSections action");
        Check(text.Contains("accepted=1"), "section viewer response should write accepted flag");
        Check(text.Contains("handle=RM-1"), "section viewer response should write road model handle");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RoadModelSectionViewerWindowContainsStationListPreviewAndLegend()
{
    var xamlPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "RoadModelSectionViewerWindow.xaml");
    var xaml = File.ReadAllText(xamlPath, Encoding.UTF8);
    Check(xaml.Contains("Title=\"查看横断面\""), "section viewer window title should be 查看横断面");
    Check(xaml.Contains("x:Name=\"StationListBox\""), "section viewer should include station selector");
    Check(xaml.Contains("x:Name=\"PreviewCanvas\""), "section viewer should include preview canvas");
    Check(xaml.Contains("绘制横断面"), "section viewer should expose draw sections action");
    Check(xaml.Contains("MouseWheel=\"PreviewCanvas_MouseWheel\""), "section viewer preview should handle mouse wheel zoom");
    Check(xaml.Contains("MouseMove=\"PreviewCanvas_MouseMove\""), "section viewer preview should handle drag pan");
    Check(xaml.Contains("ClipToBounds=\"True\""), "section viewer preview should clip panned content");
    Check(xaml.Contains("路基模板") && xaml.Contains("边坡模板") && xaml.Contains("地面线") && xaml.Contains("结构层"), "section viewer should show layer legend");

    var sourcePath = Path.Combine(
        FindRepoRoot(),
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "RoadModelSectionViewerWindow.xaml.cs");
    var source = File.ReadAllText(sourcePath, Encoding.UTF8);
    Check(source.Contains("RoadModelSectionViewerSegmentKind.PavementLayer"), "section viewer draw path should handle pavement layer segments");
    Check(source.Contains("_previewZoom") && source.Contains("_previewPan"), "section viewer should keep zoom and pan state");
    Check(source.Contains("DrawPavementLayerFills"), "section viewer should draw pavement layer fill before linework");
    Check(source.Contains("RoadModelSectionViewerAction.DrawSections"), "section viewer should set draw sections response action");

    var commandPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "AutoCad",
        "RoadModelSectionViewerCommands.cs");
    var command = File.ReadAllText(commandPath, Encoding.UTF8);
    Check(command.Contains("RD_SECTION_ROAD_MODEL_VIEW_SECTION_APPLY_DIALOG_FILE"), "section viewer managed command should forward draw action to native apply command");
}

static void RoadModelWindowReadOnlyHandleBindingIsOneWay()
{
    var xamlPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "RoadModelWindow.xaml");
    var xaml = File.ReadAllText(xamlPath, Encoding.UTF8);
    var dtoPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "Bridge",
        "RoadModelDialogDtos.cs");
    var dtoSource = File.ReadAllText(dtoPath, Encoding.UTF8);
    Check(
        xaml.Contains("Text=\"{Binding RoadCenterlineHandle, Mode=OneWay}\""),
        "road model window should bind read-only road centerline handle with Mode=OneWay");
    Check(
        xaml.Contains("Header=\"点选模板\""),
        "road model window should offer per-row template picking");
    Check(
        xaml.Contains("Click=\"OnPickTemplate\""),
        "road model window should wire template picking button");
    Check(
        xaml.Contains("Header=\"边坡模板\""),
        "road model window should include slope template tab");
    Check(
        xaml.Contains("Click=\"OnPickLeftSlopeTemplate\"") && xaml.Contains("Click=\"OnPickRightSlopeTemplate\""),
        "road model window should wire left and right slope template picking buttons");
    Check(
        xaml.Contains("Header=\"管理模板组\""),
        "road model window should expose per-row slope template group management");
    Check(
        xaml.Contains("Click=\"OnManageLeftSlopeGroup\"") && xaml.Contains("Click=\"OnManageRightSlopeGroup\""),
        "road model window should wire left and right slope template group management buttons");
    Check(
        xaml.Contains("当前模板组管理") && xaml.Contains("组内模板"),
        "road model window should show selected group management controls");
    Check(
        xaml.Contains("Click=\"OnDeleteLeftSlopeTemplate\"") && xaml.Contains("Click=\"OnMoveLeftSlopeTemplateUp\""),
        "road model window should allow editing templates inside a group");
    Check(
        xaml.Contains("Header=\"构造物\""),
        "road model window should include structure tab");
    Check(
        xaml.Contains("ItemsSource=\"{Binding Structures}\""),
        "road model window should bind structure rows");
    Check(
        xaml.Contains("RoadModelStructureOptions.Types") && xaml.Contains("RoadModelStructureOptions.SideRanges"),
        "road model window should expose structure type and side range dropdowns");
    Check(
        dtoSource.Contains("桥梁") && dtoSource.Contains("隧道") && dtoSource.Contains("两侧"),
        "road model window should expose bridge, tunnel, and both-side choices");
}

static PavementLayerTemplateLayerDto MakePavementLayer(
    PavementLayerType type,
    string name,
    bool uniformThickness,
    double thickness,
    double innerThickness,
    double outerThickness)
    => new()
    {
        Type = type,
        Name = name,
        UniformThickness = uniformThickness,
        Thickness = thickness,
        InnerThickness = innerThickness,
        OuterThickness = outerThickness,
        InnerWidening = 0.15,
        OuterWidening = 0.25,
        InnerSlope = -0.02,
        OuterSlope = 0.03,
        ColorR = type == PavementLayerType.UpperSurface ? 228 : 20,
        ColorG = type == PavementLayerType.UpperSurface ? 187 : 180,
        ColorB = type == PavementLayerType.UpperSurface ? 236 : 230,
        HatchPattern = type == PavementLayerType.UpperSurface ? "ANSI31" : "GRAVEL",
        HatchAngle = type == PavementLayerType.UpperSurface ? 30.0 : 90.0,
        HatchScale = type == PavementLayerType.UpperSurface ? 1.25 : 0.5,
    };

static void PavementLayerTemplateDialogFileReadsRequestUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        File.WriteAllLines(path, new[]
        {
            "handle=PV%251",
            "responsePath=C:/temp/pavement.response",
            "insertionX=10.5",
            "insertionY=20.25",
            "insertionZ=0.75",
            "templateName=主线%25%0A路面结构层",
            "displayScale=20.5",
            "previewWidth=3.75",
            "displayMode=HatchAndColor",
            "showAllGeneralParameters=1",
            "showCreateWizard=1",
            "structureCode=I-1",
            "subgradeMoistureTypes=Dry;Wet",
            "pavementType=Concrete",
            "subgradeSoilGroups=Bedrock;SoftSoil;Other",
            "designDeflection=23.5%0A0.01mm",
            "cumulativeAxleLoads=1200%25万次",
            "layerCount=2",
            "layer.0.type=UpperSurface",
            "layer.0.name=上面层%0AAC-13",
            "layer.0.uniformThickness=1",
            "layer.0.thickness=0.04",
            "layer.0.innerThickness=0.04",
            "layer.0.outerThickness=0.04",
            "layer.0.innerWidening=0.1",
            "layer.0.outerWidening=0.2",
            "layer.0.innerSlope=-0.02",
            "layer.0.outerSlope=0.03",
            "layer.0.colorR=228",
            "layer.0.colorG=187",
            "layer.0.colorB=236",
            "layer.0.hatchPattern=ANSI31",
            "layer.0.hatchAngle=30",
            "layer.0.hatchScale=1.25",
            "layer.1.type=Base",
            "layer.1.name=基层%25水稳",
            "layer.1.uniformThickness=false",
            "layer.1.thickness=0.18",
            "layer.1.innerThickness=0.16",
            "layer.1.outerThickness=0.2",
            "layer.1.innerWidening=0.3",
            "layer.1.outerWidening=0.4",
            "layer.1.innerSlope=0",
            "layer.1.outerSlope=0.01",
            "layer.1.colorR=20",
            "layer.1.colorG=180",
            "layer.1.colorB=230",
            "layer.1.hatchPattern=GRAVEL",
            "layer.1.hatchAngle=90",
            "layer.1.hatchScale=0.5",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = PavementLayerTemplateDialogFile.ReadRequest(path);
            Check(request.Handle == "PV%1", "pavement request should unescape percent in handle");
            Check(request.ResponsePath == "C:/temp/pavement.response", "pavement request should keep response path");
            Check(Math.Abs(request.InsertionX - 10.5) < 1.0e-9, "pavement insertion X should parse invariant decimal");
            Check(Math.Abs(request.InsertionY - 20.25) < 1.0e-9, "pavement insertion Y should parse invariant decimal");
            Check(Math.Abs(request.InsertionZ - 0.75) < 1.0e-9, "pavement insertion Z should parse invariant decimal");
            Check(request.TemplateName == "主线%\n路面结构层", "pavement template name should unescape unicode, percent, and newline");
            Check(Math.Abs(request.DisplayScale - 20.5) < 1.0e-9, "pavement display scale should parse invariant decimal");
            Check(Math.Abs(request.PreviewWidth - 3.75) < 1.0e-9, "pavement preview width should parse invariant decimal");
            Check(request.DisplayMode == PavementLayerTemplateDisplayMode.HatchAndColor, "pavement display mode should parse");
            Check(request.ShowAllGeneralParameters, "pavement request should parse show-all-general-parameters flag");
            Check(request.ShowCreateWizard, "pavement create request should parse create-wizard flag");
            Check(request.StructureCode == "I-1", "pavement request should parse structure code");
            Check(request.SubgradeMoistureTypes.SequenceEqual(new[] { PavementSubgradeMoistureType.Dry, PavementSubgradeMoistureType.Wet }), "pavement request should parse subgrade moisture multi-select values");
            Check(request.PavementType == PavementSurfaceType.Concrete, "pavement request should parse pavement type");
            Check(request.SubgradeSoilGroups.SequenceEqual(new[] { PavementSubgradeSoilGroup.Bedrock, PavementSubgradeSoilGroup.SoftSoil, PavementSubgradeSoilGroup.Other }), "pavement request should parse subgrade soil group multi-select values");
            Check(request.DesignDeflection == "23.5\n0.01mm", "pavement request should unescape design deflection");
            Check(request.CumulativeAxleLoads == "1200%万次", "pavement request should unescape cumulative axle loads");
            Check(request.Layers.Count == 2, "pavement layerCount should control layers");
            Check(request.Layers[0].Type == PavementLayerType.UpperSurface, "pavement layer type should parse");
            Check(request.Layers[0].Name == "上面层\nAC-13", "pavement layer name should unescape newline");
            Check(request.Layers[0].UniformThickness, "pavement layer uniform thickness should parse true");
            Check(Math.Abs(request.Layers[0].InnerWidening - 0.1) < 1.0e-9, "pavement inner widening should parse invariant decimal");
            Check(request.Layers[0].ColorR == 228 && request.Layers[0].ColorG == 187 && request.Layers[0].ColorB == 236, "pavement first layer RGB should parse");
            Check(request.Layers[0].HatchPattern == "ANSI31", "pavement first layer hatch pattern should parse");
            Check(Math.Abs(request.Layers[0].HatchAngle - 30.0) < 1.0e-9, "pavement first layer hatch angle should parse");
            Check(Math.Abs(request.Layers[0].HatchScale - 1.25) < 1.0e-9, "pavement first layer hatch scale should parse");
            Check(request.Layers[1].Type == PavementLayerType.Base, "second pavement layer type should parse");
            Check(request.Layers[1].Name == "基层%水稳", "pavement layer name should unescape percent");
            Check(!request.Layers[1].UniformThickness, "pavement layer uniform thickness should parse false");
            Check(Math.Abs(request.Layers[1].InnerThickness - 0.16) < 1.0e-9, "pavement inner thickness should parse invariant decimal");
            Check(Math.Abs(request.Layers[1].OuterThickness - 0.2) < 1.0e-9, "pavement outer thickness should parse invariant decimal");
            Check(request.Layers[1].ColorR == 20 && request.Layers[1].ColorG == 180 && request.Layers[1].ColorB == 230, "pavement second layer RGB should parse");
            Check(request.Layers[1].HatchPattern == "GRAVEL", "pavement second layer hatch pattern should parse");
            Check(Math.Abs(request.Layers[1].HatchAngle - 90.0) < 1.0e-9, "pavement second layer hatch angle should parse");
            Check(Math.Abs(request.Layers[1].HatchScale - 0.5) < 1.0e-9, "pavement second layer hatch scale should parse");
        });
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void PavementLayerTemplateDialogFileWritesAcceptedResponseUsingInvariantCultureAndEscaping()
{
    var path = NewTempFile();
    try
    {
        WithCulture("de-DE", () =>
        {
            var response = new PavementLayerTemplateDialogResponse
            {
                Accepted = true,
                Handle = "PV%1",
                InsertionX = 10.5,
                InsertionY = 20.25,
                InsertionZ = 0.75,
                TemplateName = "主线%\n路面结构层",
                DisplayScale = 20.5,
                PreviewWidth = 3.75,
                DisplayMode = PavementLayerTemplateDisplayMode.HatchAndColor,
                ShowAllGeneralParameters = true,
                StructureCode = "I-1",
                PavementType = PavementSurfaceType.Concrete,
                DesignDeflection = "23.5\n0.01mm",
                CumulativeAxleLoads = "1200%万次",
            };
            response.SubgradeMoistureTypes.Add(PavementSubgradeMoistureType.Dry);
            response.SubgradeMoistureTypes.Add(PavementSubgradeMoistureType.Wet);
            response.SubgradeSoilGroups.Add(PavementSubgradeSoilGroup.Bedrock);
            response.SubgradeSoilGroups.Add(PavementSubgradeSoilGroup.SoftSoil);
            response.SubgradeSoilGroups.Add(PavementSubgradeSoilGroup.Other);
            response.Layers.Add(MakePavementLayer(PavementLayerType.UpperSurface, "上面层\nAC-13", true, 0.04, 0.04, 0.04));
            response.Layers.Add(MakePavementLayer(PavementLayerType.Base, "基层%水稳", false, 0.18, 0.16, 0.2));

            PavementLayerTemplateDialogFile.WriteResponse(path, response);
        });

        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("accepted=1"), "pavement response should write accepted flag");
        Check(content.Contains("handle=PV%251"), "pavement response should escape percent in handle");
        Check(content.Contains("insertionX=10.5"), "pavement response should write insertion X using invariant decimal");
        Check(content.Contains("insertionY=20.25"), "pavement response should write insertion Y using invariant decimal");
        Check(content.Contains("insertionZ=0.75"), "pavement response should write insertion Z using invariant decimal");
        Check(content.Contains("templateName=主线%25%0A路面结构层"), "pavement response should escape percent and newline in template name");
        Check(content.Contains("displayScale=20.5"), "pavement response should write display scale using invariant decimal");
        Check(content.Contains("previewWidth=3.75"), "pavement response should write preview width using invariant decimal");
        Check(content.Contains("displayMode=HatchAndColor"), "pavement response should write display mode");
        Check(content.Contains("showAllGeneralParameters=1"), "pavement response should write show-all-general-parameters flag");
        Check(content.Contains("structureCode=I-1"), "pavement response should write structure code");
        Check(content.Contains("subgradeMoistureTypes=Dry;Wet"), "pavement response should write moisture type codes");
        Check(content.Contains("pavementType=Concrete"), "pavement response should write pavement type code");
        Check(content.Contains("subgradeSoilGroups=Bedrock;SoftSoil;Other"), "pavement response should write soil group codes");
        Check(content.Contains("designDeflection=23.5%0A0.01mm"), "pavement response should escape design deflection");
        Check(content.Contains("cumulativeAxleLoads=1200%25万次"), "pavement response should escape cumulative axle loads");
        Check(content.Contains("layerCount=2"), "pavement response should write layer count");
        Check(content.Contains("layer.0.type=UpperSurface"), "pavement response should write first layer type");
        Check(content.Contains("layer.0.name=上面层%0AAC-13"), "pavement response should escape newline in layer name");
        Check(content.Contains("layer.0.uniformThickness=1"), "pavement response should write uniform thickness bool");
        Check(content.Contains("layer.0.thickness=0.04"), "pavement response should write thickness");
        Check(content.Contains("layer.0.innerThickness=0.04"), "pavement response should write inner thickness");
        Check(content.Contains("layer.0.outerThickness=0.04"), "pavement response should write outer thickness");
        Check(content.Contains("layer.0.innerWidening=0.15"), "pavement response should write inner widening");
        Check(content.Contains("layer.0.outerWidening=0.25"), "pavement response should write outer widening");
        Check(content.Contains("layer.0.innerSlope=-0.02"), "pavement response should write inner slope");
        Check(content.Contains("layer.0.outerSlope=0.03"), "pavement response should write outer slope");
        Check(content.Contains("layer.0.colorR=228"), "pavement response should write color R");
        Check(content.Contains("layer.0.colorG=187"), "pavement response should write color G");
        Check(content.Contains("layer.0.colorB=236"), "pavement response should write color B");
        Check(content.Contains("layer.0.hatchPattern=ANSI31"), "pavement response should write first layer hatch pattern");
        Check(content.Contains("layer.0.hatchAngle=30"), "pavement response should write first layer hatch angle");
        Check(content.Contains("layer.0.hatchScale=1.25"), "pavement response should write first layer hatch scale");
        Check(content.Contains("layer.1.type=Base"), "pavement response should write second layer type");
        Check(content.Contains("layer.1.name=基层%25水稳"), "pavement response should escape percent in layer name");
        Check(content.Contains("layer.1.uniformThickness=0"), "pavement response should write non-uniform thickness bool");
        Check(content.Contains("layer.1.innerThickness=0.16"), "pavement response should write non-uniform inner thickness");
        Check(content.Contains("layer.1.outerThickness=0.2"), "pavement response should write non-uniform outer thickness");
        Check(content.Contains("layer.1.colorR=20"), "pavement response should write second layer color R");
        Check(content.Contains("layer.1.hatchPattern=GRAVEL"), "pavement response should write second layer hatch pattern");
        Check(content.Contains("layer.1.hatchAngle=90"), "pavement response should write second layer hatch angle");
        Check(content.Contains("layer.1.hatchScale=0.5"), "pavement response should write second layer hatch scale");
        Check(!content.Contains("displayScale=20,5"), "pavement response should not use current culture decimal separator");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void FullRoadPavementTemplateDialogFileRoundTripsComponentsAndAction()
{
    var requestPath = NewTempFile();
    var responsePath = NewTempFile();
    try
    {
        File.WriteAllLines(requestPath, new[]
        {
            "handle=FR%251",
            "responsePath=C:/temp/full-road.response",
            "templateName=整幅路%0A结构层",
            "displayScale=20",
            "referenceSubgradeTemplateHandle=SG%251",
            "referenceSubgradeTemplateName=高速路基%0A模板",
            "referenceRoadGrade=Expressway",
            "currentComponentIndex=0",
            "applyDefaultPresets=1",
            "componentCount=1",
            "component.0.side=Left",
            "component.0.type=TravelLane",
            "component.0.sameSideTypeOrdinal=1",
            "component.0.width=7.5",
            "component.0.height=0",
            "component.0.fixedSlope=0.02",
            "component.0.slopeMode=Fixed",
            "component.0.colorR=204",
            "component.0.colorG=102",
            "component.0.colorB=0",
            "component.0.wideningCount=1",
            "component.0.widening.0.station=100.5",
            "component.0.widening.0.value=0.25",
            "component.0.variableSlopeCount=1",
            "component.0.variableSlope.0.station=100.5",
            "component.0.variableSlope.0.value=0.03",
            "component.0.hasInnerCurb=1",
            "component.0.innerCurbWidth=0.15",
            "component.0.innerCurbHeight=0.12",
            "component.0.innerCurbEmbedDepth=0.08",
            "component.0.hasOuterCurb=1",
            "component.0.outerCurbWidth=0.18",
            "component.0.outerCurbHeight=0.14",
            "component.0.outerCurbEmbedDepth=0.09",
            "component.0.pavement.templateName=左幅行车道",
            "component.0.pavement.displayScale=10",
            "component.0.pavement.previewWidth=7.5",
            "component.0.pavement.displayMode=HatchAndColor",
            "component.0.pavement.layerCount=1",
            "component.0.pavement.layer.0.type=UpperSurface",
            "component.0.pavement.layer.0.name=SMA%2513",
            "component.0.pavement.layer.0.uniformThickness=1",
            "component.0.pavement.layer.0.thickness=0.04",
            "component.0.pavement.layer.0.innerThickness=0.04",
            "component.0.pavement.layer.0.outerThickness=0.04",
            "component.0.pavement.layer.0.colorR=228",
            "component.0.pavement.layer.0.colorG=187",
            "component.0.pavement.layer.0.colorB=236",
        }, Encoding.UTF8);

        WithCulture("fr-FR", () =>
        {
            var request = FullRoadPavementTemplateDialogFile.ReadRequest(requestPath);
            Check(request.Handle == "FR%1", "full-road request should unescape handle");
            Check(request.ResponsePath == "C:/temp/full-road.response", "full-road request should read response path");
            Check(request.TemplateName == "整幅路\n结构层", "full-road request should unescape template name");
            Check(request.ReferenceSubgradeTemplateHandle == "SG%1", "full-road request should read reference handle");
            Check(request.ReferenceSubgradeTemplateName == "高速路基\n模板", "full-road request should read reference name");
            Check(request.ReferenceRoadGrade == SubgradeRoadGrade.Expressway, "full-road request should parse road grade");
            Check(request.ApplyDefaultPresets, "full-road request should read default preset flag");
            Check(request.Components.Count == 1, "full-road request should read component count");
            Check(request.Components[0].Side == SubgradeSide.Left, "full-road component side should parse");
            Check(request.Components[0].Type == SubgradeComponentType.TravelLane, "full-road component type should parse");
            Check(request.Components[0].SameSideTypeOrdinal == 1, "full-road component ordinal should parse");
            Check(Math.Abs(request.Components[0].Width - 7.5) < 1.0e-9, "full-road component width should parse invariant decimal");
            Check(request.Components[0].WideningTable.Count == 1, "full-road component widening table should parse");
            Check(request.Components[0].VariableSlopeTable.Count == 1, "full-road component slope table should parse");
            Check(request.Components[0].HasInnerCurb && request.Components[0].HasOuterCurb, "full-road component curbs should parse");
            Check(request.Components[0].Pavement.Layers.Count == 1, "full-road component pavement layers should parse");
            Check(request.Components[0].Pavement.Layers[0].Name == "SMA%13", "full-road pavement layer name should unescape percent");
        });

        var response = new FullRoadPavementTemplateDialogResponse
        {
            Accepted = false,
            Action = FullRoadPavementTemplateDialogAction.PickReferenceSubgradeTemplate,
            Handle = "FR%1",
            TemplateName = "整幅路\n结构层",
            DisplayScale = 20.0,
            ReferenceSubgradeTemplateHandle = "SG%1",
            ReferenceSubgradeTemplateName = "高速路基\n模板",
            ReferenceRoadGrade = SubgradeRoadGrade.Expressway,
            CurrentComponentIndex = 0,
        };
        response.Components.Add(new FullRoadPavementComponentDto
        {
            Side = SubgradeSide.Left,
            Type = SubgradeComponentType.TravelLane,
            SameSideTypeOrdinal = 1,
            Width = 7.5,
            FixedSlope = 0.02,
            ColorR = 204,
            ColorG = 102,
            ColorB = 0,
            HasInnerCurb = true,
            InnerCurbWidth = 0.15,
            InnerCurbHeight = 0.12,
            InnerCurbEmbedDepth = 0.08,
            Pavement = new PavementLayerTemplateDto
            {
                TemplateName = "左幅行车道",
                DisplayScale = 10,
                PreviewWidth = 7.5,
                DisplayMode = PavementLayerTemplateDisplayMode.HatchAndColor,
                Layers = { MakePavementLayer(PavementLayerType.UpperSurface, "SMA%13", true, 0.04, 0.04, 0.04) },
            },
        });

        WithCulture("de-DE", () => FullRoadPavementTemplateDialogFile.WriteResponse(responsePath, response));
        var content = File.ReadAllText(responsePath, Encoding.UTF8);
        Check(content.Contains("action=pickReferenceSubgradeTemplate"), "full-road response should write pick reference action");
        Check(content.Contains("handle=FR%251"), "full-road response should escape handle");
        Check(content.Contains("templateName=整幅路%0A结构层"), "full-road response should escape template name");
        Check(content.Contains("referenceSubgradeTemplateHandle=SG%251"), "full-road response should escape reference handle");
        Check(content.Contains("componentCount=1"), "full-road response should write component count");
        Check(content.Contains("component.0.side=Left"), "full-road response should write component side");
        Check(content.Contains("component.0.sameSideTypeOrdinal=1"), "full-road response should write component ordinal");
        Check(content.Contains("component.0.pavement.layer.0.name=SMA%2513"), "full-road response should write nested pavement layer");
    }
    finally
    {
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
        }
        if (File.Exists(responsePath))
        {
            File.Delete(responsePath);
        }
    }
}

static void PavementLayerTemplateXmlFileRoundTripsPavementTemplate()
{
    var path = Path.Combine(Path.GetTempPath(), $"RoadProtoManagedBridgeTests_{Guid.NewGuid():N}.rpavement.xml");
    try
    {
        var template = new PavementLayerTemplateDto
        {
            TemplateName = "主线路面结构层",
            DisplayScale = 25,
            PreviewWidth = 4.25,
            DisplayMode = PavementLayerTemplateDisplayMode.HatchAndColor,
            ShowAllGeneralParameters = true,
            StructureCode = "I-1",
            PavementType = PavementSurfaceType.Concrete,
            DesignDeflection = "23.5",
            CumulativeAxleLoads = "1200万次",
        };
        template.SubgradeMoistureTypes.Add(PavementSubgradeMoistureType.Dry);
        template.SubgradeMoistureTypes.Add(PavementSubgradeMoistureType.Wet);
        template.SubgradeSoilGroups.Add(PavementSubgradeSoilGroup.Bedrock);
        template.SubgradeSoilGroups.Add(PavementSubgradeSoilGroup.SoftSoil);
        template.Layers.Add(MakePavementLayer(PavementLayerType.UpperSurface, "上面层", true, 0.04, 0.04, 0.04));
        template.Layers.Add(MakePavementLayer(PavementLayerType.Base, "基层", false, 0.18, 0.16, 0.2));
        template.Layers[1].InnerWidening = -0.15;

        PavementLayerTemplateXmlFile.Write(path, template);
        var content = File.ReadAllText(path, Encoding.UTF8);
        Check(content.Contains("RoadProtoPavementLayerTemplate"), "pavement XML should write expected root element");
        Check(content.Contains("version=\"1\""), "pavement XML should write version");
        Check(content.Contains("uniformThickness=\"true\""), "pavement XML should write true uniform thickness");
        Check(content.Contains("uniformThickness=\"false\""), "pavement XML should write false uniform thickness");
        Check(content.Contains("colorR=\"228\"") && content.Contains("colorG=\"187\"") && content.Contains("colorB=\"236\""), "pavement XML should write layer RGB attributes");
        Check(content.Contains("displayMode=\"HatchAndColor\""), "pavement XML should write display mode");
        Check(content.Contains("showAllGeneralParameters=\"true\""), "pavement XML should write show-all-general-parameters flag");
        Check(content.Contains("structureCode=\"I-1\""), "pavement XML should write structure code");
        Check(content.Contains("subgradeMoistureTypes=\"Dry;Wet\""), "pavement XML should write subgrade moisture type list");
        Check(content.Contains("pavementType=\"Concrete\""), "pavement XML should write pavement type");
        Check(content.Contains("subgradeSoilGroups=\"Bedrock;SoftSoil\""), "pavement XML should write subgrade soil group list");
        Check(content.Contains("designDeflection=\"23.5\""), "pavement XML should write design deflection");
        Check(content.Contains("cumulativeAxleLoads=\"1200万次\""), "pavement XML should write cumulative axle loads");
        Check(content.Contains("hatchPattern=\"ANSI31\""), "pavement XML should write first layer hatch pattern");
        Check(content.Contains("hatchAngle=\"30\""), "pavement XML should write first layer hatch angle");
        Check(content.Contains("hatchScale=\"1.25\""), "pavement XML should write first layer hatch scale");

        var roundTrip = PavementLayerTemplateXmlFile.Read(path);
        Check(roundTrip.TemplateName == "主线路面结构层", "pavement XML should round-trip template name");
        Check(Math.Abs(roundTrip.DisplayScale - 25) < 1.0e-9, "pavement XML should round-trip display scale");
        Check(Math.Abs(roundTrip.PreviewWidth - 4.25) < 1.0e-9, "pavement XML should round-trip preview width");
        Check(roundTrip.DisplayMode == PavementLayerTemplateDisplayMode.HatchAndColor, "pavement XML should round-trip display mode");
        Check(roundTrip.ShowAllGeneralParameters, "pavement XML should round-trip show-all-general-parameters flag");
        Check(roundTrip.StructureCode == "I-1", "pavement XML should round-trip structure code");
        Check(roundTrip.SubgradeMoistureTypes.SequenceEqual(new[] { PavementSubgradeMoistureType.Dry, PavementSubgradeMoistureType.Wet }), "pavement XML should round-trip moisture types");
        Check(roundTrip.PavementType == PavementSurfaceType.Concrete, "pavement XML should round-trip pavement type");
        Check(roundTrip.SubgradeSoilGroups.SequenceEqual(new[] { PavementSubgradeSoilGroup.Bedrock, PavementSubgradeSoilGroup.SoftSoil }), "pavement XML should round-trip soil groups");
        Check(roundTrip.DesignDeflection == "23.5", "pavement XML should round-trip design deflection");
        Check(roundTrip.CumulativeAxleLoads == "1200万次", "pavement XML should round-trip cumulative axle loads");
        Check(roundTrip.Layers.Count == 2, "pavement XML should round-trip layer count");
        Check(roundTrip.Layers[0].Type == PavementLayerType.UpperSurface, "pavement XML should round-trip layer type");
        Check(roundTrip.Layers[0].UniformThickness, "pavement XML should round-trip uniform thickness true");
        Check(Math.Abs(roundTrip.Layers[0].Thickness - 0.04) < 1.0e-9, "pavement XML should round-trip uniform thickness value");
        Check(roundTrip.Layers[0].ColorR == 228 && roundTrip.Layers[0].ColorG == 187 && roundTrip.Layers[0].ColorB == 236, "pavement XML should round-trip first layer RGB");
        Check(roundTrip.Layers[0].HatchPattern == "ANSI31", "pavement XML should round-trip first layer hatch pattern");
        Check(Math.Abs(roundTrip.Layers[0].HatchAngle - 30.0) < 1.0e-9, "pavement XML should round-trip first layer hatch angle");
        Check(Math.Abs(roundTrip.Layers[0].HatchScale - 1.25) < 1.0e-9, "pavement XML should round-trip first layer hatch scale");
        Check(roundTrip.Layers[1].Type == PavementLayerType.Base, "pavement XML should round-trip second layer type");
        Check(!roundTrip.Layers[1].UniformThickness, "pavement XML should round-trip uniform thickness false");
        Check(Math.Abs(roundTrip.Layers[1].InnerThickness - 0.16) < 1.0e-9, "pavement XML should round-trip inner thickness");
        Check(Math.Abs(roundTrip.Layers[1].OuterThickness - 0.2) < 1.0e-9, "pavement XML should round-trip outer thickness");
        Check(Math.Abs(roundTrip.Layers[1].InnerWidening - -0.15) < 1.0e-9, "pavement XML should round-trip negative inner widening");
        Check(Math.Abs(roundTrip.Layers[1].OuterSlope - 0.03) < 1.0e-9, "pavement XML should round-trip outer slope");
        Check(roundTrip.Layers[1].ColorR == 20 && roundTrip.Layers[1].ColorG == 180 && roundTrip.Layers[1].ColorB == 230, "pavement XML should round-trip second layer RGB");
        Check(roundTrip.Layers[1].HatchPattern == "GRAVEL", "pavement XML should round-trip second layer hatch pattern");
        Check(Math.Abs(roundTrip.Layers[1].HatchAngle - 90.0) < 1.0e-9, "pavement XML should round-trip second layer hatch angle");
        Check(Math.Abs(roundTrip.Layers[1].HatchScale - 0.5) < 1.0e-9, "pavement XML should round-trip second layer hatch scale");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void PavementLayerTemplateXmlFileRejectsMalformedXml()
{
    var path = Path.Combine(Path.GetTempPath(), $"RoadProtoManagedBridgeTests_{Guid.NewGuid():N}.rpavement.xml");
    try
    {
        void WriteXml(string propertiesAttributes, string layerAttributes)
        {
            File.WriteAllText(
                path,
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <RoadProtoPavementLayerTemplate version="1">
                  <Properties {propertiesAttributes} />
                  <Layer {layerAttributes} />
                </RoadProtoPavementLayerTemplate>
                """,
                Encoding.UTF8);
        }

        const string validProperties = "name=\"主线路面结构层\" displayScale=\"25\" previewWidth=\"4.25\"";
        const string validLayer = "type=\"UpperSurface\" name=\"上面层\" uniformThickness=\"true\" thickness=\"0.04\" innerThickness=\"0.04\" outerThickness=\"0.04\" innerWidening=\"0\" outerWidening=\"0\" innerSlope=\"0\" outerSlope=\"0\" colorR=\"65\" colorG=\"174\" colorB=\"221\"";

        WriteXml(validProperties, validLayer.Replace("UpperSurface", "BadType"));
        ExpectThrows<InvalidDataException>(
            () => PavementLayerTemplateXmlFile.Read(path),
            "pavement XML should reject invalid layer type");

        WriteXml(validProperties, validLayer.Replace("uniformThickness=\"true\"", "uniformThickness=\"maybe\""));
        ExpectThrows<InvalidDataException>(
            () => PavementLayerTemplateXmlFile.Read(path),
            "pavement XML should reject invalid bool attributes");

        WriteXml(validProperties.Replace("displayScale=\"25\"", "displayScale=\"abc\""), validLayer);
        ExpectThrows<InvalidDataException>(
            () => PavementLayerTemplateXmlFile.Read(path),
            "pavement XML should reject invalid numeric attributes");

        WriteXml(validProperties, validLayer.Replace(" thickness=\"0.04\"", string.Empty));
        ExpectThrows<InvalidDataException>(
            () => PavementLayerTemplateXmlFile.Read(path),
            "pavement XML should reject missing mandatory layer attributes");

        WriteXml(validProperties, validLayer.Replace("innerWidening=\"0\"", "innerWidening=\"-0.1\""));
        var negativeWidening = PavementLayerTemplateXmlFile.Read(path);
        Check(Math.Abs(negativeWidening.Layers[0].InnerWidening - -0.1) < 1.0e-9, "pavement XML should accept negative widening values");

        WriteXml(validProperties.Replace("previewWidth=\"4.25\"", "previewWidth=\"0\""), validLayer);
        ExpectThrows<InvalidDataException>(
            () => PavementLayerTemplateXmlFile.Read(path),
            "pavement XML should reject non-positive preview width");

        WriteXml(validProperties, validLayer.Replace("colorR=\"65\"", "colorR=\"bad\""));
        ExpectThrows<InvalidDataException>(
            () => PavementLayerTemplateXmlFile.Read(path),
            "pavement XML should reject invalid color attributes");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void PavementLayerTemplateEnumsExposeWizardLayerTypes()
{
    var asphaltSeal = (PavementLayerType)RequiredEnumValue(
        "RoadProto.Terrain.UI.Bridge.PavementLayerType",
        "AsphaltSeal");
    var approachSlab = (PavementLayerType)RequiredEnumValue(
        "RoadProto.Terrain.UI.Bridge.PavementLayerType",
        "ApproachSlab");

    Check(PavementLayerTemplateLabels.LayerTypeLabel(asphaltSeal) == "沥青封层", "pavement layer labels should include asphalt seal");
    Check(PavementLayerTemplateLabels.LayerTypeLabel(approachSlab) == "搭板", "pavement layer labels should display approach slab as 搭板");
    Check(PavementLayerTemplateLabels.DefaultLayers().Any(layer => layer.Type == asphaltSeal), "default layer options should include asphalt seal");
}

static void PavementLayerTemplatePresetFactoryBuildsDocumentDefaults()
{
    foreach (var hatchPattern in new[] { "NET", "AR-HBONE", "GRAVEL", "SACNCR", "TRIANG", "AR-SAND", "HEX" })
    {
        Check(PavementLayerTemplateLabels.HatchPatternOptions.Contains(hatchPattern), $"hatch pattern options should include document pattern {hatchPattern}");
    }

    var mainline = PavementLayerTemplatePresetFactory.Create(
        PavementSurfaceType.Asphalt,
        PavementLayerTemplateRoadSegmentType.MainlineLane);
    var asphaltSegmentLabels = PavementLayerTemplatePresetFactory
        .RoadSegmentOptions(PavementSurfaceType.Asphalt)
        .Select(option => option.Label)
        .ToList();
    Check(!asphaltSegmentLabels.Contains("主线路缘带"), "asphalt wizard segment options should remove mainline edge strip");

    Check(mainline.TemplateName == "沥青路面-主线行车道", "mainline preset should name the selected pavement and road segment");
    Check(mainline.PavementType == PavementSurfaceType.Asphalt, "mainline preset should use asphalt pavement type");
    Check(mainline.DisplayMode == PavementLayerTemplateDisplayMode.HatchAndColor, "mainline preset should use hatch and color display mode");
    Check(mainline.ShowAllGeneralParameters, "mainline preset should expose populated general parameters in the editor");
    Check(Math.Abs(mainline.PreviewWidth - 3.0) < 1.0e-9, "mainline preset should use the unified preview width");
    Check(mainline.StructureCode == "Ⅰ-1", "mainline preset should use document structure code");
    Check(mainline.Layers.Count == 6, "mainline preset should omit empty or zero document layers");
    Check(mainline.Layers[0].Type == PavementLayerType.UpperSurface, "mainline first layer should be upper surface");
    Check(mainline.Layers[0].Name == "4cm沥青马蹄脂碎石混合料（SMA-13s）", "mainline upper surface should use document material name");
    Check(Math.Abs(mainline.Layers[0].Thickness - 0.04) < 1.0e-9, "mainline upper surface should use document thickness");
    Check(mainline.Layers[0].HatchPattern == "NET", "mainline upper surface should use document hatch pattern image");
    Check(Math.Abs(mainline.Layers[0].HatchScale - 0.1) < 1.0e-9, "mainline upper surface should use document hatch scale");
    var middleSurface = mainline.Layers.Single(layer => layer.Type == PavementLayerType.MiddleSurface);
    Check(middleSurface.HatchPattern == "AR-HBONE", "mainline middle surface should use document hatch pattern image");
    Check(Math.Abs(middleSurface.HatchScale - 0.003) < 1.0e-9, "mainline middle surface should use document hatch scale");
    var lowerSurface = mainline.Layers.Single(layer => layer.Type == PavementLayerType.LowerSurface);
    Check(lowerSurface.HatchPattern == "AR-HBONE", "mainline lower surface should use document hatch pattern image");
    Check(Math.Abs(lowerSurface.HatchScale - 0.005) < 1.0e-9, "mainline lower surface should use document hatch scale");
    var asphaltSeal = mainline.Layers.Single(layer => layer.Type == PavementLayerType.AsphaltSeal);
    Check(asphaltSeal.Name == "沥青封层", "mainline should include asphalt seal layer");
    Check(asphaltSeal.HatchPattern == "SOLID", "asphalt seal should use document hatch pattern");
    Check(Math.Abs(asphaltSeal.Thickness - 0.01) < 1.0e-9, "asphalt seal should use document thickness");
    Check(Math.Abs(asphaltSeal.HatchScale - 1.0) < 1.0e-9, "asphalt seal should use document hatch scale");
    var baseLayer = mainline.Layers.Single(layer => layer.Type == PavementLayerType.Base);
    Check(baseLayer.Name == "36cm水泥稳定碎石", "mainline base should use document material name");
    Check(
        Math.Abs(baseLayer.InnerWidening - 0.1) < 1.0e-9 &&
        Math.Abs(baseLayer.OuterWidening) < 1.0e-9 &&
        Math.Abs(baseLayer.InnerSlope - 1.0) < 1.0e-9 &&
        Math.Abs(baseLayer.OuterSlope) < 1.0e-9,
        "mainline lane base should use asymmetric inner-side widening and slope");
    Check(baseLayer.HatchPattern == "GRAVEL", "mainline base should use document hatch pattern image");
    Check(Math.Abs(baseLayer.HatchScale - 0.04) < 1.0e-9, "mainline base should use document hatch scale");
    var subbaseLayer = mainline.Layers.Single(layer => layer.Type == PavementLayerType.Subbase);
    Check(
        Math.Abs(subbaseLayer.InnerWidening - 0.1) < 1.0e-9 &&
        Math.Abs(subbaseLayer.OuterWidening) < 1.0e-9 &&
        Math.Abs(subbaseLayer.InnerSlope - 1.0) < 1.0e-9 &&
        Math.Abs(subbaseLayer.OuterSlope) < 1.0e-9,
        "mainline lane subbase should use asymmetric inner-side widening and slope");
    Check(subbaseLayer.HatchPattern == "SACNCR", "mainline subbase should use document hatch pattern image");
    Check(Math.Abs(subbaseLayer.HatchScale - 0.2) < 1.0e-9, "mainline subbase should use document hatch scale");

    var shoulder = PavementLayerTemplatePresetFactory.Create(
        PavementSurfaceType.Asphalt,
        PavementLayerTemplateRoadSegmentType.MainlineShoulder);
    var shoulderBase = shoulder.Layers.Single(layer => layer.Type == PavementLayerType.Base);
    Check(
        Math.Abs(shoulderBase.InnerWidening) < 1.0e-9 &&
        Math.Abs(shoulderBase.OuterWidening - 0.1) < 1.0e-9 &&
        Math.Abs(shoulderBase.InnerSlope) < 1.0e-9 &&
        Math.Abs(shoulderBase.OuterSlope - 1.0) < 1.0e-9,
        "mainline shoulder base should use asymmetric outer-side widening and slope");
    var shoulderSubbase = shoulder.Layers.Single(layer => layer.Type == PavementLayerType.Subbase);
    Check(
        Math.Abs(shoulderSubbase.InnerWidening) < 1.0e-9 &&
        Math.Abs(shoulderSubbase.OuterWidening - 0.1) < 1.0e-9 &&
        Math.Abs(shoulderSubbase.InnerSlope) < 1.0e-9 &&
        Math.Abs(shoulderSubbase.OuterSlope - 1.0) < 1.0e-9,
        "mainline shoulder subbase should use asymmetric outer-side widening and slope");

    var wizardMainline = PavementLayerTemplateLabels.CloneTemplate(mainline);
    Check(wizardMainline.Layers[0].HatchPattern == "NET", "wizard clone should preserve mainline upper surface hatch pattern");
    Check(wizardMainline.Layers.Single(layer => layer.Type == PavementLayerType.MiddleSurface).HatchPattern == "AR-HBONE", "wizard clone should preserve mainline middle surface hatch pattern");
    Check(wizardMainline.Layers.Single(layer => layer.Type == PavementLayerType.Base).HatchPattern == "GRAVEL", "wizard clone should preserve mainline base hatch pattern");
    Check(wizardMainline.Layers.Single(layer => layer.Type == PavementLayerType.Subbase).HatchPattern == "SACNCR", "wizard clone should preserve mainline subbase hatch pattern");

    var bridgeTransition = PavementLayerTemplatePresetFactory.Create(
        PavementSurfaceType.Asphalt,
        PavementLayerTemplateRoadSegmentType.BridgeTransition);
    Check(Math.Abs(bridgeTransition.PreviewWidth - 3.0) < 1.0e-9, "bridge transition preset should use the unified preview width");
    Check(bridgeTransition.StructureCode == "Ⅱ-1", "bridge transition preset should use document structure code");
    Check(!bridgeTransition.Layers.Any(layer => layer.Type == PavementLayerType.LowerSurface), "bridge transition should omit blank lower surface");
    var approachSlab = bridgeTransition.Layers.Single(layer => layer.Type == PavementLayerType.ApproachSlab);
    Check(approachSlab.Name == "水泥混凝土", "bridge transition should include approach slab material");
    Check(Math.Abs(approachSlab.Thickness - 0.35) < 1.0e-9, "approach slab should use document thickness");
    Check(approachSlab.HatchPattern == "TRIANG", "approach slab should use document hatch pattern image");
    Check(Math.Abs(approachSlab.HatchScale - 0.04) < 1.0e-9, "approach slab should use document hatch scale");
    var cushion = bridgeTransition.Layers.Single(layer => layer.Type == PavementLayerType.Cushion);
    Check(cushion.HatchPattern == "AR-SAND", "bridge transition cushion should use document hatch pattern image");
    Check(Math.Abs(cushion.HatchScale - 0.07) < 1.0e-9, "bridge transition cushion should use document hatch scale");
    var gradedSubbase = bridgeTransition.Layers.Single(layer => layer.Type == PavementLayerType.Subbase);
    Check(gradedSubbase.HatchPattern == "HEX", "bridge transition graded aggregate should use document hatch pattern image");
    Check(Math.Abs(gradedSubbase.HatchScale - 0.5) < 1.0e-9, "bridge transition graded aggregate should use document hatch scale");

    var tollPlaza = PavementLayerTemplatePresetFactory.Create(
        PavementSurfaceType.Concrete,
        PavementLayerTemplateRoadSegmentType.TollPlaza);
    Check(tollPlaza.PavementType == PavementSurfaceType.Concrete, "toll plaza preset should use concrete pavement type");
    Check(tollPlaza.StructureCode == "Ⅲ-1", "toll plaza preset should use document structure code");
    Check(tollPlaza.Layers.Count == 3, "toll plaza preset should include only populated concrete pavement layers");
    Check(tollPlaza.Layers[0].HatchPattern == "TRIANG", "toll plaza concrete layer should use document hatch pattern image");
    Check(Math.Abs(tollPlaza.Layers[0].HatchScale - 0.04) < 1.0e-9, "toll plaza concrete layer should use document hatch scale");
    Check(tollPlaza.Layers[1].HatchPattern == "GRAVEL", "toll plaza base should use document hatch pattern image");
    Check(Math.Abs(tollPlaza.Layers[1].HatchScale - 0.04) < 1.0e-9, "toll plaza base should use document hatch scale");
    Check(tollPlaza.Layers[2].HatchPattern == "HEX", "toll plaza graded aggregate should use document hatch pattern image");
    Check(Math.Abs(tollPlaza.Layers[2].HatchScale - 0.5) < 1.0e-9, "toll plaza graded aggregate should use document hatch scale");
    foreach (var pavementType in PavementLayerTemplatePresetFactory.PavementTypeOptions.Select(option => option.Value))
    {
        foreach (var roadSegmentType in PavementLayerTemplatePresetFactory.RoadSegmentOptions(pavementType).Select(option => option.Value))
        {
            var preset = PavementLayerTemplatePresetFactory.Create(pavementType, roadSegmentType);
            Check(Math.Abs(preset.PreviewWidth - 3.0) < 1.0e-9, "all pavement layer wizard presets should default preview width to 3");
        }
    }
}

static void PavementLayerMaterialOptionsFollowLayerTypeAndAllowCustomNames()
{
    var upperSurfaceOptions = PavementLayerTemplateLabels.MaterialOptionsForLayerType(PavementLayerType.UpperSurface);
    var baseOptions = PavementLayerTemplateLabels.MaterialOptionsForLayerType(PavementLayerType.Base);
    var approachSlabOptions = PavementLayerTemplateLabels.MaterialOptionsForLayerType(PavementLayerType.ApproachSlab);

    Check(upperSurfaceOptions.Contains("细粒式沥青混凝土 AC-13C"), "upper surface material options should include common asphalt materials");
    Check(upperSurfaceOptions.Contains("沥青玛蹄脂碎石混合料 SMA-13"), "upper surface material options should include SMA recommendations");
    Check(baseOptions.Contains("水泥稳定碎石"), "base material options should include cement stabilized crushed stone");
    Check(baseOptions.Contains("贫混凝土基层"), "base material options should include lean concrete base");
    Check(approachSlabOptions.Contains("钢筋混凝土搭板"), "approach slab material options should include reinforced concrete slab");
    Check(!object.ReferenceEquals(upperSurfaceOptions, baseOptions), "material option lists should be type-specific");

    var beforeCount = upperSurfaceOptions.Count;
    upperSurfaceOptions.Add("用户自定义材料");
    Check(
        PavementLayerTemplateLabels.MaterialOptionsForLayerType(PavementLayerType.UpperSurface).Count == beforeCount,
        "material option callers should receive a copy so custom text never mutates shared recommendations");
}

static void PavementLayerTemplateApplyUsesUniqueResponsePathContract()
{
    var root = FindRepoRoot();
    var commandPath = Path.Combine(
        root,
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "AutoCad",
        "PavementLayerTemplateDialogCommands.cs");
    var windowPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "ui",
        "wpf",
        "RoadProto.Terrain.UI",
        "PavementLayerTemplateWindow.xaml.cs");
    var source = File.ReadAllText(commandPath, Encoding.UTF8);
    var windowSource = File.ReadAllText(windowPath, Encoding.UTF8);
    var applyHandlerStart = source.IndexOf("window.ApplyRequested += (_, response) =>", StringComparison.Ordinal);
    var dialogStart = source.IndexOf("var dialogResult = window.ShowDialog();", StringComparison.Ordinal);
    Check(applyHandlerStart >= 0 && dialogStart > applyHandlerStart, "pavement command should contain ApplyRequested handler before ShowDialog");

    var applyHandler = source.Substring(applyHandlerStart, dialogStart - applyHandlerStart);
    Check(applyHandler.Contains("CreateApplyResponsePath("), "pavement Apply should create a unique response path");
    Check(applyHandler.Contains("PavementLayerTemplateDialogFile.WriteResponse(applyResponsePath, response)"), "pavement Apply should write to the unique response path");
    Check(applyHandler.Contains("SendApplyCommand(document, applyResponsePath)"), "pavement Apply should queue native apply with the unique response path");
    Check(!applyHandler.Contains("request.ResponsePath"), "pavement Apply should not write or queue the original request response path");
    Check(source.Contains("PavementLayerTemplateDialogFile.WriteResponse(request.ResponsePath, response)"), "pavement final OK/Cancel should keep original response path");

    var applyClickStart = windowSource.IndexOf("private void Apply_Click", StringComparison.Ordinal);
    var okClickStart = windowSource.IndexOf("private void Ok_Click", StringComparison.Ordinal);
    Check(applyClickStart >= 0 && okClickStart > applyClickStart, "pavement window should contain Apply_Click before Ok_Click");
    var applyClick = windowSource.Substring(applyClickStart, okClickStart - applyClickStart);
    Check(applyClick.Contains("ApplyRequested?.Invoke(this, response)"), "pavement Apply button should raise apply event");
    Check(!applyClick.Contains("Response ="), "pavement Apply button should not overwrite the final dialog response");
}

static void SubgradeTemplateWindowContainsPavementTemplateBindingControls()
{
    var root = FindRepoRoot();
    var xaml = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "SubgradeTemplateWindow.xaml"), Encoding.UTF8);
    var source = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "SubgradeTemplateWindow.xaml.cs"), Encoding.UTF8);
    var combined = xaml + "\n" + source;

    Check(combined.Contains("启用路面结构层模板") || combined.Contains("启用结构层模板"), "subgrade window should expose pavement template enable status");
    Check(combined.Contains("选择结构层模板"), "subgrade window should expose pavement template pick button");
    Check(combined.Contains("清除结构层模板"), "subgrade window should expose pavement template clear button");
    Check(combined.Contains("PavementLayerName"), "subgrade window should display pavement template name");
    Check(combined.Contains("PavementLayerHandle"), "subgrade window should display pavement template handle");
    Check(!xaml.Contains("结构层厚度"), "subgrade window should not expose legacy pavement thickness label");
    Check(!xaml.Contains("PavementLayerThicknessBox"), "subgrade window should not expose legacy pavement thickness input");
    Check(xaml.Contains("IsHitTestVisible=\"False\""), "subgrade pavement template checkbox should be status-only, not a user toggle");
    Check(!source.Contains("component.PavementLayerLinked = PavementLayerCheckBox.IsChecked == true"), "subgrade window should not derive linked state directly from the checkbox");
    Check(source.Contains("!string.IsNullOrWhiteSpace(component.PavementLayerHandle)"), "subgrade window should derive linked state from a non-empty pavement template handle");
    Check(source.Contains("PickPavementLayerTemplate"), "subgrade window should request native pavement template picking");
    Check(source.Contains("PickComponentIndex"), "subgrade window should preserve selected component index for picking");
}

static void SubgradeTemplateManagedCommandPreservesPickAction()
{
    var root = FindRepoRoot();
    var source = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "AutoCad", "SubgradeTemplateDialogCommands.cs"), Encoding.UTF8);

    Check(source.Contains("Action = request.Action"), "subgrade fallback response should preserve request action");
    Check(source.Contains("PickComponentIndex = request.PickComponentIndex"), "subgrade fallback response should preserve request pick component index");
    Check(source.Contains("response.Action"), "subgrade command should preserve the window response action");
    Check(source.Contains("RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE"), "subgrade command should send native apply command");
}

static void PavementLayerTemplateWindowContainsRequiredEditorContracts()
{
    var root = FindRepoRoot();
    var xaml = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "PavementLayerTemplateWindow.xaml"), Encoding.UTF8);
    var source = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "PavementLayerTemplateWindow.xaml.cs"), Encoding.UTF8);
    var dtoSource = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Bridge", "PavementLayerTemplateDialogDtos.cs"), Encoding.UTF8);
    var dialogFileSource = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Bridge", "PavementLayerTemplateDialogFile.cs"), Encoding.UTF8);
    var combined = xaml + "\n" + source + "\n" + dtoSource + "\n" + dialogFileSource;

    Check(combined.Contains("PreviewCanvas"), "pavement window should contain PreviewCanvas");
    Check(combined.Contains("LayerCountBox"), "pavement window should contain LayerCountBox");
    Check(combined.Contains("CurrentLayerBox"), "pavement editor should expose current layer input");
    Check(combined.Contains("PreviousLayerButton"), "pavement editor should expose previous layer icon button");
    Check(combined.Contains("NextLayerButton"), "pavement editor should expose next layer icon button");
    Check(combined.Contains("ShowAllGeneralParametersBox"), "pavement editor should expose show-all-general-parameters checkbox");
    Check(combined.Contains("AdvancedGeneralParametersPanel"), "pavement editor should keep advanced general parameters in a collapsible panel");
    Check(combined.Contains("StructureCodeBox"), "pavement editor should expose structure code input");
    Check(combined.Contains("SubgradeMoistureTypesBox"), "pavement editor should expose subgrade moisture multi-select dropdown");
    Check(combined.Contains("PavementTypeBox"), "pavement editor should expose pavement type dropdown");
    Check(combined.Contains("SubgradeSoilGroupsBox"), "pavement editor should expose subgrade soil group multi-select dropdown");
    Check(combined.Contains("DesignDeflectionBox"), "pavement editor should expose design deflection input");
    Check(combined.Contains("CumulativeAxleLoadsBox"), "pavement editor should expose cumulative axle loads input");
    Check(combined.Contains("干燥") && combined.Contains("中湿") && combined.Contains("潮湿") && combined.Contains("过湿"), "pavement editor should expose all moisture labels");
    Check(combined.Contains("沥青路面") && combined.Contains("混凝土路面"), "pavement editor should expose pavement type labels");
    Check(combined.Contains("基岩") && combined.Contains("碎石土") && combined.Contains("低液限黏土") && combined.Contains("其他"), "pavement editor should expose soil group labels");
    Check(combined.Contains("DisplayModeBox"), "pavement editor should expose display mode dropdown");
    Check(combined.Contains("HatchPatternBox"), "pavement editor should expose hatch pattern dropdown");
    Check(combined.Contains("HatchAngleBox"), "pavement editor should expose hatch angle input");
    Check(combined.Contains("HatchScaleBox"), "pavement editor should expose hatch scale input");
    Check(combined.Contains("AR-CONC") && combined.Contains("STEEL") && combined.Contains("BRICK"), "pavement editor should expose a richer CAD hatch pattern list");
    Check(combined.Contains("SaveXml"), "pavement window should expose SaveXml action");
    Check(combined.Contains("ImportXml"), "pavement window should expose ImportXml action");
    Check(combined.Contains("MouseWheel"), "pavement preview should handle mouse wheel zoom");
    Check(combined.Contains("PreviewCanvas_MouseLeftButtonDown"), "pavement preview should support click-to-select layer");
    Check(combined.Contains("MouseMiddleButtonDown"), "pavement preview should handle middle-button pan start");
    Check(combined.Contains("MouseMiddleButtonUp"), "pavement preview should handle middle-button pan end");
    Check(combined.Contains("内外加宽是否一致"), "pavement editor should expose uniform widening checkbox");
    Check(combined.Contains("内外坡度是否一致"), "pavement editor should expose uniform slope checkbox");
    Check(source.Contains("ScreenToWorld"), "pavement preview zoom should convert the mouse position to world coordinates");
    Check(source.Contains("WorldToScreen"), "pavement preview zoom should keep the same world point under the mouse");
    Check(source.Contains("CreatePreviewTransform"), "pavement preview should use a reusable centered transform");
    Check(source.Contains("ReadSlope"), "pavement preview should accept 1:n slope input syntax");
    Check(!source.Contains("denominator >= 0.0"), "pavement preview should allow negative 1:n slope input");
    Check(source.Contains("SlopeInset"), "pavement preview should convert slope and thickness into bottom-edge inset");
    Check(source.Contains("-thickness * slope"), "pavement preview should invert slope so positive 1:n slopes move the bottom edge outward");
    Check(!source.Contains("Math.Sqrt(slope)"), "pavement preview should not use the old square-root slope conversion");
    Check(source.Contains("inheritedTopGrade = (topOuterY - topInnerY) / inheritedTopWidth"), "pavement preview should extend widening along the inherited top edge line");
    Check(source.Contains("topInnerX - layer.InnerWidening") && source.Contains("topInnerY - layer.InnerWidening * inheritedTopGrade"), "pavement preview should apply inner widening to the current layer top edge");
    Check(source.Contains("topOuterX + layer.OuterWidening") && source.Contains("topOuterY + layer.OuterWidening * inheritedTopGrade"), "pavement preview should apply outer widening to the current layer top edge");
    Check(!source.Contains("Math.Max(0.0, ReadDouble(controls.WideningBox.Text"), "pavement editor should allow negative uniform widening");
    Check(!source.Contains("Math.Max(0.0, ReadDouble(controls.InnerWideningBox.Text"), "pavement editor should allow negative inner widening");
    Check(source.Contains("bottomInnerX = currentTopInner.X + innerInset"), "pavement preview should inset the bottom inner edge from the widened top edge");
    Check(source.Contains("bottomOuterX = currentTopOuter.X - outerInset"), "pavement preview should inset the bottom outer edge from the widened top edge");
    Check(source.Contains("topInner, topOuter, bottomOuter, bottomInner"), "pavement preview polygon should always stay a quadrilateral");
    Check(source.Contains("DrawHatchPattern"), "pavement preview should draw hatch pattern overlays");
    Check(source.Contains("geometry.Layer.HatchAngle"), "pavement preview hatch pattern should use per-layer hatch angle");
    Check(source.Contains("geometry.Layer.HatchScale"), "pavement preview hatch pattern should use per-layer hatch scale");
    Check(source.Contains("const double DefaultPreviewWidth = 3.0"), "pavement editor should use the unified preview width fallback");
    Check(dtoSource.Contains("PreviewWidth { get; set; } = 3.0"), "pavement dialog DTO should default preview width to 3");
    Check(dialogFileSource.Contains("GetDouble(values, \"previewWidth\", 3.0)"), "pavement dialog file parser should default preview width to 3");
    Check(source.Contains("DrawLayerCalloutLabels(geometry, transform)"), "pavement preview should draw one callout label block after layer edges");
    Check(source.Contains("WorldTextHeightToScreen"), "pavement preview text should convert fixed model text heights through the current preview scale");
    Check(source.Contains("WorldDistanceToScreen"), "pavement preview annotation offsets and lengths should convert fixed model distances through the current preview scale");
    Check(source.Contains("fontSize = WorldTextHeightToScreen") && source.Contains("transform.Scale"), "pavement preview labels should scale with preview zoom instead of staying fixed to screen pixels");
    Check(source.Contains("const double calloutTextHeight = 0.075"), "pavement preview callout labels should use a smaller fixed model text height");
    Check(source.Contains("const double slopeTextHeight = 0.052"), "pavement preview slope labels should use a smaller fixed model text height");
    Check(source.Contains("const double wideningTextHeight = 0.052"), "pavement preview widening labels should use a smaller fixed model text height");
    Check(source.Contains("const double calloutUnderlineLength = 1.85"), "pavement preview callout underlines should be slightly shorter");
    Check(source.Contains("const double wideningDimensionOffset = 0.085"), "pavement preview widening dimension extension lines should be shorter");
    Check(source.Contains("const double wideningArrowLength = 0.026"), "pavement preview widening arrowheads should be smaller");
    Check(source.Contains("const double wideningArrowHalfWidth = 0.011"), "pavement preview widening arrowheads should be narrower");
    Check(source.Contains("(a.Y + b.Y) * 0.5 - fontSize * 1.25"), "pavement preview widening label should stay just above the dimension arrow center");
    Check(source.Contains("LayerCalloutLabel"), "pavement preview should format layer callout text separately from geometry dimensions");
    Check(source.Contains("DrawCalloutUnderline"), "pavement preview should draw an underline for every layer callout row");
    Check(source.Contains("AddEdge(leaderStart"), "pavement preview should draw a vertical leader line for the label block");
    Check(source.Contains("leaderStart = new Point(leaderX, TopLineYAtX(topSurfaceStart, topSurfaceEnd, leaderX))") && !source.Contains("minTopY - 8.0"), "pavement preview callout leader should start exactly at the pavement top");
    Check(source.Contains("SlopeLabelPosition"), "pavement preview slope label should be positioned from the side-edge midpoint");
    Check(!source.Contains("-dimensionLabelFontSize * 3.5"), "pavement preview slope label should not be pushed far away from the side edge");
    Check(!source.Contains("var offset = new Vector(0.0, -18.0)"), "pavement preview widening dimension offset should not be fixed in screen pixels");
    Check(!source.Contains("direction * 8.0") && !source.Contains("normal * 3.5"), "pavement preview arrowheads should not be fixed in screen pixels");
    Check(!source.Contains("Math.Max(72.0, Math.Min(260.0"), "pavement preview callout underline width should not be fixed in screen pixels");
    Check(!source.Contains("layerHeight * 0.22"), "pavement preview layer labels should not scale with layer thickness");
    Check(source.Contains("DrawWideningDimension"), "pavement preview should use CAD-style widening dimensions");
    Check(source.Contains("FormatSlopeLabel"), "pavement preview should format slope labels as 1:n");
    Check(source.Contains("ColorIndexDialog"), "pavement editor should expose an index color dialog");
    Check(source.Contains("drawTopEdge") && source.Contains("index == 0"), "pavement preview should draw shared layer boundaries once to avoid false intersections");
    Check(source.Contains("颜色 RGB"), "pavement editor should expose per-layer RGB color editing");
    Check(source.Contains("ColorRBox") && source.Contains("ColorGBox") && source.Contains("ColorBBox"), "pavement editor should keep RGB text boxes in layer controls");
    Check(source.Contains("var nameBox = new ComboBox"), "pavement editor should use an editable material ComboBox for layer names");
    Check(source.Contains("ItemsSource = PavementLayerTemplateLabels.MaterialOptionsForLayerType(layer.Type)"), "pavement material ComboBox should initialize from the selected layer type");
    Check(source.Contains("IsEditable = true") && source.Contains("IsTextSearchEnabled = true") && source.Contains("StaysOpenOnEdit = true"), "pavement material ComboBox should allow searchable custom text entry");
    Check(source.Contains("UpdateMaterialOptions(controls)") && source.Contains("controls.NameBox.Text"), "pavement material options should refresh by layer type while preserving typed text");
    Check(source.Contains("SyncSelectedMaterialText(sender)") &&
        source.Contains("e is SelectionChangedEventArgs") &&
        source.Contains("comboBox.SelectedItem is string selectedMaterial") &&
        source.Contains("comboBox.Text = selectedMaterial"),
        "pavement material ComboBox should copy selected recommendation text into editable Text only for selection changes, leaving typed edits deletable");
    Check(source.Contains("LayerColor(PavementLayerTemplateLayerDto layer, int index)"), "pavement preview should derive color from layer RGB");
    Check(source.Contains("DefaultColorForLayerIndex(index)"), "pavement preview should fall back to the shared default palette");
    Check(source.Contains("Color.FromRgb(ToByte(layer.ColorR), ToByte(layer.ColorG), ToByte(layer.ColorB))"), "pavement preview should draw layer edges with user RGB");
    Check(!source.Contains("Color.FromRgb(65, 174, 221)") && !source.Contains("Color.FromRgb(142, 164, 180)"), "pavement preview should not hard-code layer colors in the window");
    Check(!source.Contains("topInnerY - innerThickness - layer.InnerWidening * layer.InnerSlope"), "pavement preview should not fold widening slope into the main thickness edge");
    Check(!source.Contains("topOuterY - outerThickness + layer.OuterWidening * layer.OuterSlope"), "pavement preview should not fold widening slope into the main thickness edge");
    Check(!source.Contains("BodyBottomInner") && !source.Contains("BodyBottomOuter"), "pavement preview should not create six-point body outline steps");
    Check(!combined.Contains("左侧") && !combined.Contains("右侧"), "pavement editor labels should use 内侧/外侧 wording, not 左侧/右侧");
}

static void PavementLayerTemplateCreateWizardWindowContainsRequiredContracts()
{
    var root = FindRepoRoot();
    var xaml = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "PavementLayerTemplateCreateWizardWindow.xaml"), Encoding.UTF8);
    var source = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "PavementLayerTemplateCreateWizardWindow.xaml.cs"), Encoding.UTF8);
    var command = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "AutoCad", "PavementLayerTemplateDialogCommands.cs"), Encoding.UTF8);
    var editorXaml = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "PavementLayerTemplateWindow.xaml"), Encoding.UTF8);
    var editorSource = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "PavementLayerTemplateWindow.xaml.cs"), Encoding.UTF8);
    var combined = xaml + "\n" + source + "\n" + command + "\n" + editorSource;

    Check(combined.Contains("路面结构层创建向导"), "pavement create wizard should use the requested title");
    Check(combined.Contains("PavementTypeBox"), "pavement create wizard should expose pavement type selection");
    Check(combined.Contains("RoadSegmentTypeBox"), "pavement create wizard should expose road segment type selection");
    Check(combined.Contains("WizardLayersPanel"), "pavement create wizard should expose editable preset layer rows");
    Check(combined.Contains("内侧厚度") && combined.Contains("外侧厚度"), "pavement create wizard should expose inner and outer thickness fields");
    Check(combined.Contains("内侧加宽") && combined.Contains("外侧加宽"), "pavement create wizard should expose inner and outer widening fields");
    Check(combined.Contains("内侧坡度") && combined.Contains("外侧坡度"), "pavement create wizard should expose inner and outer slope fields");
    Check(source.Contains("InnerThicknessBox") && source.Contains("OuterThicknessBox"), "pavement create wizard should read inner and outer thickness text boxes");
    Check(source.Contains("InnerWideningBox") && source.Contains("OuterWideningBox"), "pavement create wizard should read inner and outer widening text boxes");
    Check(source.Contains("InnerSlopeBox") && source.Contains("OuterSlopeBox"), "pavement create wizard should read inner and outer slope text boxes");
    Check(combined.Contains("PavementLayerTemplatePresetFactory.Create"), "pavement create wizard should build initial values from the preset factory");
    Check(combined.Contains("request.ShowCreateWizard"), "pavement managed command should branch on create-wizard requests");
    Check(combined.Contains("new PavementLayerTemplateWindow(request)"), "pavement managed command should still open the existing editor window after the wizard");
    Check(combined.Contains("ApplyButton.IsEnabled = !string.IsNullOrWhiteSpace(request.Handle)"), "pavement create editor should disable Apply before the new entity has a handle");
    Check(editorXaml.Contains("Content=\"新增部件\""), "pavement editor should expose add-component button");
    Check(editorXaml.Contains("Content=\"删除部件\""), "pavement editor should expose delete-component button");
    Check(editorSource.Contains("ShowInsertLayerDialog"), "pavement editor should ask whether to insert above or below the selected layer");
    Check(editorSource.Contains("\"上方\"") && editorSource.Contains("\"下方\"") && editorSource.Contains("\"取消\""), "pavement add-component dialog should offer above, below, and cancel");
    Check(editorSource.Contains("MessageBox.Show(this, \"是否删除选中部件？\""), "pavement editor should confirm deleting the selected component");
}

static void PavementLayerTemplateCreateRequestUsesMainlinePresetWithoutShowingWizard()
{
    var root = FindRepoRoot();
    var command = File.ReadAllText(
        Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "AutoCad", "PavementLayerTemplateDialogCommands.cs"),
        Encoding.UTF8);

    Check(
        command.Contains("PavementLayerTemplatePresetFactory.Create(") &&
        command.Contains("PavementSurfaceType.Asphalt") &&
        command.Contains("PavementLayerTemplateRoadSegmentType.MainlineLane"),
        "pavement create request should initialize the editor with the asphalt mainline lane preset");
    Check(
        !command.Contains("new PavementLayerTemplateCreateWizardWindow(request)"),
        "pavement create request should bypass the wizard window while keeping the wizard source available");
}

static void PavementLayerTemplateRibbonAndCommandSourceContractsExist()
{
    var root = FindRepoRoot();
    var ribbon = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "AutoCad", "RoadProtoRibbonExtension.cs"), Encoding.UTF8);
    var command = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "AutoCad", "PavementLayerTemplateDialogCommands.cs"), Encoding.UTF8);
    var combined = ribbon + "\n" + command;

    Check(combined.Contains("CommandClass(typeof(RoadProto.Terrain.UI.AutoCad.PavementLayerTemplateDialogCommands))"), "pavement command class should be registered");
    Check(combined.Contains("RD_SECTION_PAVEMENT_LAYER_TEMPLATE_CREATE"), "ribbon should contain pavement create command");
    Check(combined.Contains("RD_SECTION_PAVEMENT_LAYER_TEMPLATE_SHOW_WPF_DIALOG"), "managed command source should contain pavement show WPF command");
    Check(combined.Contains("DNPAVEMENTLAYERTEMPLATEENTITY"), "double-click source should contain pavement DXF constant");
    Check(combined.Contains("RD_SECTION_PAVEMENT_LAYER_TEMPLATE_EDIT_HANDLE"), "double-click source should queue pavement edit handle command");
}

static void AgentConsoleRibbonAndCommandSourceContractsExist()
{
    var root = FindRepoRoot();
    var ribbon = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "AutoCad", "RoadProtoRibbonExtension.cs"), Encoding.UTF8);
    var commandPath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "AgentConsoleCommands.cs");
    var xamlPath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "AgentConsolePalette.xaml");
    var paletteCodePath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "AgentConsolePalette.xaml.cs");
    var viewModelPath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "AgentConsoleViewModel.cs");
    var clientPath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "Backend", "AgentBackendClient.cs");
    var dtosPath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "Models", "AgentDtos.cs");
    var bridgePath = Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "Bridge", "AgentLocalToolBridge.cs");
    var nativeAgentConsolePath = Path.Combine(root, "src", "cad_adapter", "objectarx", "agent", "ObjectArxAgentConsoleCommand.cpp");
    Check(File.Exists(commandPath), "agent console command source should exist");
    Check(File.Exists(xamlPath), "agent console palette XAML should exist");
    Check(File.Exists(bridgePath), "agent local tool bridge should exist");
    Check(File.Exists(nativeAgentConsolePath), "native agent console command source should exist");

    var command = File.ReadAllText(commandPath, Encoding.UTF8);
    var xaml = File.ReadAllText(xamlPath, Encoding.UTF8);
    var paletteCode = File.ReadAllText(paletteCodePath, Encoding.UTF8);
    var viewModel = File.ReadAllText(viewModelPath, Encoding.UTF8);
    var client = File.ReadAllText(clientPath, Encoding.UTF8);
    var dtos = File.ReadAllText(dtosPath, Encoding.UTF8);
    var bridge = File.ReadAllText(bridgePath, Encoding.UTF8);
    var nativeAgentConsole = File.ReadAllText(nativeAgentConsolePath, Encoding.UTF8);
    var combined = ribbon + "\n" + command + "\n" + xaml + "\n" + paletteCode + "\n" + viewModel + "\n" + client + "\n" + dtos + "\n" + bridge;

    Check(combined.Contains("CommandClass(typeof(RoadProto.Terrain.UI.Agent.AgentConsoleCommands))"), "agent command class should be registered");
    Check(combined.Contains("AgentPanelId"), "ribbon should define an agent panel id");
    Check(combined.Contains("AgentConsoleButtonId"), "ribbon should define an agent console button id");
    Check(ribbon.Contains("OpenAgentConsoleCommandHandler"), "agent ribbon button should open the palette directly without AutoCAD command queue forwarding");
    Check(!ribbon.Contains("AgentConsoleButtonId,\r\n                \"Agent 控制台\",\r\n                \"打开可停靠的可控工程 Agent 控制台\",\r\n                \"RD_AGENT_CONSOLE_UI "), "agent ribbon button should not send the managed palette command through SendStringToExecute");
    Check(nativeAgentConsole.Contains("RD_AGENT_CONSOLE_UI "), "native agent console command should still expose the command-line forwarding fallback");
    Check(
        nativeAgentConsole.Contains("RoadProto.Terrain.UI.dll") && nativeAgentConsole.Contains("_.NETLOAD"),
        "native agent console command should ensure the managed palette assembly is loaded before forwarding");
    Check(command.Contains("PaletteSet"), "agent command should create an AutoCAD PaletteSet");
    Check(command.Contains("WpfControls.Grid") && command.Contains("_palette.AddVisual(\"Agent\", _wpfHost, false)"), "agent palette should register an empty WPF host with AddVisual before adding the full Agent control");
    Check(command.Contains("_wpfHost.Children.Add(paletteContent)"), "agent command should attach the full Agent WPF control after the WPF host is registered");
    Check(!command.Contains("ElementHost"), "agent production palette path should not use ElementHost because AutoCAD 2021 can crash while parenting it");
    Check(!command.Contains("PaletteGuid"), "agent palette should not use a persisted guid until AutoCAD 2021 palette state corruption is ruled out");
    Check(command.Contains("Size = new Size(560, 720)") && command.Contains("MinimumSize = new Size(480, 560)"), "agent palette should open wider by default");
    Check(!command.Contains("Dock = DockSides.Right,"), "agent palette should not set Dock in the PaletteSet initializer because AutoCAD 2021 can hang while adding hosted controls");
    Check(command.Contains("DockEnabled = DockSides.Right") && command.Contains("Dock = DockSides.Right"), "agent palette should dock on the right by default");
    Check(command.Contains("PaletteSetStyles.Snappable") && command.Contains("PaletteSetStyles.SingleColDock"), "agent palette should use AutoCAD docking styles");
    Check(!command.Contains("KeepFocus"), "agent palette should not set KeepFocus because it can destabilize AutoCAD host focus");
    Check(!command.Contains("throw;"), "agent palette command should not rethrow UI startup exceptions into AutoCAD host");
    Check(xaml.Contains("ProviderBox"), "agent palette should expose provider settings");
    Check(xaml.Contains("TraceId"), "agent palette should show TraceId context");
    Check(xaml.Contains("LogList"), "agent palette should expose flow logs");
    Check(xaml.Contains("TextWrapping=\"Wrap\""), "agent chat and flow logs should wrap long lines");
    Check(xaml.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\""), "agent chat and flow logs should adapt to palette width");
    Check(xaml.Contains("Text=\"{Binding MessagesText}\"") && xaml.Contains("Text=\"{Binding LogText}\""), "agent chat and flow logs should use stable selectable text surfaces");
    Check(!xaml.Contains("ListBox.ItemTemplate"), "agent palette should not nest focusable text boxes inside list items");
    Check(xaml.Contains("OnInputPreviewKeyDown"), "agent input should send on Enter");
    Check(xaml.Contains("Content=\"确认执行\"") && xaml.Contains("Content=\"取消\""), "agent confirmation actions should live inside the conversation area");
    Check(xaml.Contains("BaseUrlBox"), "agent palette should expose provider BaseUrl input");
    Check(xaml.Contains("ModelBox"), "agent palette should expose provider model input");
    Check(xaml.Contains("ApiKeyBox"), "agent palette should expose provider API key input");
    Check(xaml.Contains("SaveProviderButton"), "agent palette should expose save provider button");
    Check(xaml.Contains("OpenRoadProtoLogsButton"), "agent palette should expose local log directory action");
    Check(paletteCode.Contains("InputBox.Clear()"), "agent input should clear after sending");
    Check(paletteCode.Contains("RestoreInputFocusAsync"), "agent input should restore keyboard focus asynchronously to reduce AutoCAD command-line focus jumps");
    Check(paletteCode.Contains("BeginInitializeAsync"), "agent palette should delay backend initialization until after AutoCAD hosts the palette");
    Check(!paletteCode.Contains("OnInputGotKeyboardFocus"), "agent palette should not recursively restore focus from GotKeyboardFocus");
    Check(!paletteCode.Contains("FocusInputBox();"), "agent palette should not synchronously force focus during palette load");
    Check(client.Contains("GetModelProvidersAsync") && client.Contains("api/settings/models"), "agent backend client should load saved model provider settings");
    Check(client.Contains("CancelAsync") && client.Contains("/cancel"), "agent backend client should cancel awaiting confirmation runs");
    Check(client.Contains("SaveModelProviderAsync") && client.Contains("api/settings/models/{provider}"), "agent backend client should save model provider settings");
    Check(client.Contains("PostToolResultAsync") && client.Contains("/tool-result"), "agent backend client should post local tool results");
    Check(viewModel.Contains("SaveProviderAsync"), "agent view model should save provider settings through backend");
    Check(viewModel.Contains("LoadProviderSettingsAsync") && viewModel.Contains("ApplyProviderView"), "agent view model should restore last saved provider settings on open");
    Check(viewModel.Contains("MessagesText") && viewModel.Contains("LogText"), "agent view model should expose selectable text projections for chat and logs");
    Check(viewModel.Contains("_loggedEventKeys") && viewModel.Contains("run.Events"), "agent view model should render backend flow events without duplicate visible logs");
    Check(viewModel.Contains("CancelAsync"), "agent view model should support cancelling a pending confirmation from the conversation area");
    Check(viewModel.Contains("AgentLocalToolBridge"), "agent view model should dispatch confirmed tools through local bridge");
    Check(dtos.Contains("ModelProviderUpdateDto"), "agent DTOs should include provider update payload");
    Check(dtos.Contains("AgentRunEventDto") && dtos.Contains("Name = \"events\""), "agent run DTO should expose backend flow events");
    Check(dtos.Contains("Name = \"updatedAt\""), "agent provider DTO should expose updated time for last-used provider selection");
    Check(dtos.Contains("AgentToolResultDto"), "agent DTOs should include tool result payload");
    Check(dtos.Contains("SubgradeTemplateCreateArgumentsDto"), "agent DTOs should include subgrade tool arguments");
    Check(dtos.Contains("AgentId") && dtos.Contains("SkillId") && dtos.Contains("IntentId"), "agent plan DTO should expose AgentId, SkillId, and IntentId");
    Check(dtos.Contains("RiskLevel") && dtos.Contains("FollowUpMessage") && dtos.Contains("ResultMessage"), "agent plan DTO should expose risk, follow-up, and result messages");
    Check(dtos.Contains("AgentRunDto") && dtos.Contains("Name = \"followUpMessage\""), "agent run DTO should expose follow-up message for awaiting user input state");
    Check(dtos.Contains("AgentEntryRouteDto") && dtos.Contains("Name = \"entryRoute\""), "agent run DTO should expose entry route result");
    Check(dtos.Contains("AgentRunUserInputRequestDto"), "agent DTOs should include user input continuation payload");
    Check(client.Contains("PostUserInputAsync") && client.Contains("/user-input"), "agent backend client should post user input continuation");
    Check(viewModel.Contains("AwaitingUserInput"), "agent view model should handle follow-up state");
    Check(viewModel.Contains("run.FollowUpMessage") && viewModel.Contains("请补充必要信息"), "agent view model should prefer run-level follow-up message before fallback text");
    Check(viewModel.Contains("EntryRoute") && viewModel.Contains("run.ToolResult.Message"), "agent view model should display routed chat/help results without a plan");
    Check(viewModel.Contains("DescribePlan(run.Plan)"), "agent view model should display readable plan context");
    Check(viewModel.Contains("TranslateRiskLevel") && viewModel.Contains("TranslateToolName"), "agent view model should translate risk and tool codes");
    Check(!viewModel.Contains("当前 Skill {run.Plan.SkillId}"), "agent view model should not show raw Skill code in chat");
    Check(bridge.Contains("SubgradeTemplateDialogFile.WriteResponse"), "agent local bridge should reuse existing subgrade template dialog response writer");
    Check(bridge.Contains("RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE"), "agent local bridge should queue native subgrade apply command");
    Check(bridge.Contains("SubgradeTemplate.Create"), "agent local bridge should support subgrade create tool");
    Check(bridge.Contains("SubgradeTemplate.Modify"), "agent local bridge should support subgrade modify tool");
    Check(bridge.Contains("SubgradeTemplate.Delete"), "agent local bridge should support subgrade delete tool");
    Check(bridge.Contains("SubgradeTemplate.Query"), "agent local bridge should support subgrade query tool");
    Check(bridge.Contains("RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE"), "agent local bridge should queue native subgrade CRUD tool command");
    Check(dtos.Contains("double? LaneWidth"), "agent DTOs should not define local lane width defaults");
    Check(dtos.Contains("double? HardShoulderWidth"), "agent DTOs should not define local hard shoulder defaults");
    Check(dtos.Contains("double? EarthShoulderWidth"), "agent DTOs should not define local earth shoulder defaults");
    Check(dtos.Contains("double? MedianWidth"), "agent DTOs should expose median width from Agent rules");
    Check(dtos.Contains("double? DisplayScale"), "agent DTOs should expose display scale from Agent rules");
    Check(dtos.Contains("SubgradeTemplateComponentArgumentDto"), "agent DTOs should include full subgrade component arguments");
    Check(dtos.Contains("List<SubgradeTemplateComponentArgumentDto> Components"), "agent subgrade create arguments should carry a full component list");
    Check(bridge.Contains("ValidateCreateArguments"), "agent local bridge should validate create arguments before dispatch");
    Check(bridge.Contains("默认值应由 Agent 规则文件提供"), "agent local bridge validation should state that defaults come from Agent rules");
    Check(bridge.Contains("缺少路基模板部件列表"), "agent local bridge should require rule-provided component defaults");
    Check(bridge.Contains("DataContractJsonSerializer"), "agent local bridge should deserialize tool arguments as structured JSON");
    Check(!bridge.Contains("1.0 / arguments.SlopeRatio"), "agent local bridge should not derive template slopes from slopeRatio");
    Check(!bridge.Contains("Width = width > 0.0 ? width : 0.75"), "agent local bridge should not fall back to a hardcoded width");
    Check(viewModel.Contains("AgentLogFormatter.Format"), "agent view model should render readable flow log lines");
}

static void AgentLogFormatterMapsInternalStagesToReadableChinese()
{
    var succeeded = AgentLogFormatter.Format("RunUpdated", "Task task_123 Succeeded");
    var routed = AgentLogFormatter.Format("EntryRouted", "WorkflowCandidate: 用户表达包含动作，但未明确工程对象。");
    var unsupported = AgentLogFormatter.Format("EntryRouted", "UnsupportedWorkflow: 用户提到道路模型。道路模型是独立工程对象。");
    var awaiting = AgentLogFormatter.Format("AwaitingUserInput", "Task task_456 AwaitingUserInput");
    var rerouted = AgentLogFormatter.Format("ContinuationRerouted", "ChatOnly: 识别为闲聊输入，不进入工程工作流。");
    var facts = AgentLogFormatter.Format("RuntimeFactsAnswered", "ChatOnly controlled conversation source: runtime-facts.");
    var model = AgentLogFormatter.Format("ConversationModelRequested", "ChatOnly controlled conversation source: model.");
    var planOutput = AgentLogFormatter.Format("PlanOutput", "Intent=subgrade_template.create; Tool=SubgradeTemplate.Create; Risk=medium");
    var toolArgs = AgentLogFormatter.Format("ToolArgumentsPrepared", "RoadGrade=Expressway; Components=8; Left TravelLane width=7.5");

    Check(succeeded.Contains("任务已成功完成", StringComparison.Ordinal), "agent log formatter should explain succeeded task state");
    Check(succeeded.Contains("task_123", StringComparison.Ordinal), "agent log formatter should keep task id for troubleshooting");
    Check(!succeeded.StartsWith("RunUpdated:", StringComparison.Ordinal), "agent log formatter should hide raw stage prefixes in the visible log");
    Check(routed.Contains("入口路由", StringComparison.Ordinal) && routed.Contains("工作流候选", StringComparison.Ordinal), "agent log formatter should explain route type");
    Check(unsupported.Contains("未接入的工程对象", StringComparison.Ordinal), "agent log formatter should explain unsupported workflow route type");
    Check(awaiting.Contains("等待用户补充信息", StringComparison.Ordinal) && awaiting.Contains("task_456", StringComparison.Ordinal), "agent log formatter should explain awaiting user input task state");
    Check(!awaiting.StartsWith("AwaitingUserInput:", StringComparison.Ordinal), "agent log formatter should hide raw awaiting stage prefixes");
    Check(rerouted.Contains("补充输入已重新路由", StringComparison.Ordinal) && rerouted.Contains("闲聊", StringComparison.Ordinal), "agent log formatter should explain rerouted continuation input");
    Check(facts.Contains("运行时事实层", StringComparison.Ordinal), "agent log formatter should explain runtime facts answers");
    Check(model.Contains("调用大模型", StringComparison.Ordinal), "agent log formatter should explain model-backed chat answers");
    Check(planOutput.Contains("计划输出", StringComparison.Ordinal) && planOutput.Contains("SubgradeTemplate.Create", StringComparison.Ordinal), "agent log formatter should show readable plan output");
    Check(toolArgs.Contains("工具参数", StringComparison.Ordinal) && toolArgs.Contains("Components=8", StringComparison.Ordinal), "agent log formatter should show readable tool argument output");
}

ResponseWritesPickTerrainAction();
SubgradeRequestReadsPersistedEntityComponents();
SubgradeResponseWritesPavementTemplatePickActionAndPreservesRows();
SlopeTemplateDialogFileRoundTripsComponents();
RoadModelRequestReadsAssignmentsUsingInvariantCultureAndEscaping();
RoadModelResponseWritesAssignmentsUsingInvariantCultureAndEscaping();
RoadModelResponseWritesPickTemplateActionAndRowIndex();
RoadModelSlopeGroupsRoundTripUsingInvariantCultureAndEscaping();
RoadModelStructuresRoundTripUsingInvariantCultureAndEscaping();
RoadModelRequestReadsSelectedAssignmentIndex();
RoadModelRequestRejectsMissingOrEmptyResponsePath();
RoadModelSectionViewerRequestReadsPreviewsUsingInvariantCultureAndEscaping();
RoadModelSectionViewerResponseWritesDrawSectionsAction();
RoadModelSectionViewerWindowContainsStationListPreviewAndLegend();
RoadModelWindowReadOnlyHandleBindingIsOneWay();
PavementLayerTemplateDialogFileReadsRequestUsingInvariantCultureAndEscaping();
PavementLayerTemplateDialogFileWritesAcceptedResponseUsingInvariantCultureAndEscaping();
FullRoadPavementTemplateDialogFileRoundTripsComponentsAndAction();
PavementLayerTemplateXmlFileRoundTripsPavementTemplate();
PavementLayerTemplateXmlFileRejectsMalformedXml();
PavementLayerTemplateApplyUsesUniqueResponsePathContract();
PavementLayerTemplateEnumsExposeWizardLayerTypes();
PavementLayerTemplatePresetFactoryBuildsDocumentDefaults();
PavementLayerMaterialOptionsFollowLayerTypeAndAllowCustomNames();
SubgradeTemplateWindowContainsPavementTemplateBindingControls();
SubgradeTemplateManagedCommandPreservesPickAction();
PavementLayerTemplateWindowContainsRequiredEditorContracts();
PavementLayerTemplateCreateWizardWindowContainsRequiredContracts();
PavementLayerTemplateCreateRequestUsesMainlinePresetWithoutShowingWizard();
PavementLayerTemplateRibbonAndCommandSourceContractsExist();
AgentConsoleRibbonAndCommandSourceContractsExist();
AgentLogFormatterMapsInternalStagesToReadableChinese();
Console.WriteLine("All RoadProto managed bridge tests passed.");
