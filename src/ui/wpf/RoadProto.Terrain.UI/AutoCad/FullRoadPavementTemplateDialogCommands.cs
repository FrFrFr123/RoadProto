using System.Diagnostics;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.Runtime;
using RoadProto.Terrain.UI.Bridge;
using CoreApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace RoadProto.Terrain.UI.AutoCad;

public sealed class FullRoadPavementTemplateDialogCommands
{
    private static string PendingRequestPath
        => Path.Combine(Path.GetTempPath(), $"RoadProtoFullRoadPavementTemplateDialog_{Process.GetCurrentProcess().Id}.pending");

    [CommandMethod("RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_SHOW_WPF_DIALOG", CommandFlags.Session)]
    public void ShowFullRoadPavementTemplateDialog()
    {
        var document = CoreApplication.DocumentManager.MdiActiveDocument;
        var editor = document?.Editor;
        if (document == null || editor == null)
        {
            return;
        }

        if (!File.Exists(PendingRequestPath))
        {
            editor.WriteMessage("\nRoadProto full road pavement template dialog pending request path is missing.");
            return;
        }

        try
        {
            var requestPath = File.ReadAllText(PendingRequestPath, Encoding.UTF8).Trim().Trim('"');
            File.Delete(PendingRequestPath);
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                editor.WriteMessage("\nRoadProto full road pavement template dialog request path is empty.");
                return;
            }

            var request = FullRoadPavementTemplateDialogFile.ReadRequest(requestPath);
            if (string.IsNullOrWhiteSpace(request.ResponsePath))
            {
                editor.WriteMessage("\nRoadProto full road pavement template dialog response path is empty.");
                return;
            }

            var window = new FullRoadPavementTemplateWindow(request);
            window.ApplyRequested += (_, response) =>
            {
                var applyResponsePath = CreateApplyResponsePath();
                FullRoadPavementTemplateDialogFile.WriteResponse(applyResponsePath, response);
                SendApplyCommand(document, applyResponsePath);
            };

            var dialogResult = window.ShowDialog();
            var response = window.Response ?? CreateCancelledResponse(request, dialogResult == true);
            FullRoadPavementTemplateDialogFile.WriteResponse(request.ResponsePath, response);
            SendApplyCommand(document, request.ResponsePath);
        }
        catch (System.Exception error)
        {
            editor.WriteMessage($"\nRoadProto full road pavement template WPF dialog failed: {error.Message}");
        }
    }

    private static FullRoadPavementTemplateDialogResponse CreateCancelledResponse(
        FullRoadPavementTemplateDialogRequest request,
        bool accepted)
        => new()
        {
            Accepted = accepted,
            Action = FullRoadPavementTemplateDialogAction.None,
            Handle = request.Handle,
            InsertionX = request.InsertionX,
            InsertionY = request.InsertionY,
            InsertionZ = request.InsertionZ,
            TemplateName = request.TemplateName,
            DisplayScale = request.DisplayScale,
            ReferenceSubgradeTemplateHandle = request.ReferenceSubgradeTemplateHandle,
            ReferenceSubgradeTemplateName = request.ReferenceSubgradeTemplateName,
            ReferenceRoadGrade = request.ReferenceRoadGrade,
            CurrentComponentIndex = request.CurrentComponentIndex,
            Components = request.Components,
        };

    private static void SendApplyCommand(Autodesk.AutoCAD.ApplicationServices.Document document, string responsePath)
    {
        var responseCommandPath = responsePath.Replace('\\', '/');
        document.SendStringToExecute(
            $"RD_SECTION_FULL_ROAD_PAVEMENT_TEMPLATE_APPLY_DIALOG_FILE \"{responseCommandPath}\"\n",
            true,
            false,
            true);
    }

    private static string CreateApplyResponsePath()
        => Path.Combine(
            Path.GetTempPath(),
            $"RoadProtoFullRoadPavementTemplateApply_{Process.GetCurrentProcess().Id}_{System.Guid.NewGuid():N}.response");
}
