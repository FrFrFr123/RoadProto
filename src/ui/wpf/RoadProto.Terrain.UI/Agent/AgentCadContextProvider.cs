using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Linq;

namespace RoadProto.Terrain.UI.Agent;

internal sealed class AgentCadContextProvider
{
    private const string SubgradeTemplateDxfName = "DNSUBGRADETEMPLATEENTITY";
    private static readonly string[] CurrentTemplateReferenceWords =
    {
        "这个模板",
        "这个路基模板",
        "当前模板",
        "当前路基模板",
        "当前选中的模板",
        "当前选中模板",
        "当前选中的路基模板",
        "当前选中路基模板",
        "选中的模板",
        "选中模板",
        "选中的路基模板",
        "选中路基模板",
        "this template",
        "current template",
        "selected template",
    };

    public AgentCadContext CaptureForMessage(string message)
    {
        if (!MentionsCurrentTemplate(message))
        {
            return AgentCadContext.Empty;
        }

        try
        {
            var document = AcadApplication.DocumentManager.MdiActiveDocument;
            var implied = document?.Editor.SelectImplied();
            if (implied?.Status != PromptStatus.OK || implied.Value == null)
            {
                return AgentCadContext.Empty;
            }

            var ids = implied.Value.GetObjectIds()
                .Where(IsSubgradeTemplateEntity)
                .Take(2)
                .ToArray();
            if (ids.Length != 1)
            {
                return AgentCadContext.Empty;
            }

            return new AgentCadContext(ids[0].Handle.ToString(), null);
        }
        catch
        {
            return AgentCadContext.Empty;
        }
    }

    public AgentCadContext PickSubgradeTemplate()
    {
        try
        {
            var document = AcadApplication.DocumentManager.MdiActiveDocument;
            var editor = document?.Editor;
            if (editor == null)
            {
                return AgentCadContext.Empty;
            }

            var options = new PromptEntityOptions("\n请选择要操作的路基模板实体: ");
            var result = editor.GetEntity(options);
            if (result.Status != PromptStatus.OK || result.ObjectId.IsNull)
            {
                return AgentCadContext.Empty;
            }

            if (!IsSubgradeTemplateEntity(result.ObjectId))
            {
                editor.WriteMessage("\n选择对象不是 RoadProto 路基模板实体。");
                return AgentCadContext.Empty;
            }

            return new AgentCadContext(result.ObjectId.Handle.ToString(), null);
        }
        catch
        {
            return AgentCadContext.Empty;
        }
    }

    private static bool MentionsCurrentTemplate(string message)
    {
        return CurrentTemplateReferenceWords.Any(word =>
            message.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsSubgradeTemplateEntity(ObjectId objectId)
    {
        if (objectId.IsNull)
        {
            return false;
        }

        var objectClass = objectId.ObjectClass;
        return string.Equals(objectClass?.DxfName, SubgradeTemplateDxfName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectClass?.Name, SubgradeTemplateDxfName, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class AgentCadContext
{
    public AgentCadContext(string? currentTemplateHandle, string? currentTemplateName)
    {
        CurrentTemplateHandle = currentTemplateHandle;
        CurrentTemplateName = currentTemplateName;
    }

    public static AgentCadContext Empty { get; } = new AgentCadContext(null, null);

    public string? CurrentTemplateHandle { get; }

    public string? CurrentTemplateName { get; }

    public bool HasCurrentTemplate => !string.IsNullOrWhiteSpace(CurrentTemplateHandle)
        || !string.IsNullOrWhiteSpace(CurrentTemplateName);
}
