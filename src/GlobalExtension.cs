using CAMAPI.Extensions;
using CAMAPI.ResultStatus;

namespace EncyPcCheck;

/// <summary>Startet beim Start von ENCY das kleine Auslastungs-Widget und beendet es mit ENCY.</summary>
public class GlobalExtension : IExtension, IExtensionGlobal
{
    /// <inheritdoc />
    public IExtensionInfo? Info { get; set; }

    /// <inheritdoc />
    public TResultStatus OnSCInitializing()
    {
        TResultStatus status = default;
        try { WidgetHost.EnsureStarted(); } // eigener Thread, blockiert den Start von ENCY nicht
        catch (Exception e) { status.Code = TResultStatusCode.rsError; status.Description = e.Message; }
        return status;
    }

    /// <inheritdoc />
    public TResultStatus OnSCFinalizing()
    {
        TResultStatus status = default;
        try { WidgetHost.Stop(); }
        catch (Exception e) { status.Code = TResultStatusCode.rsError; status.Description = e.Message; }
        return status;
    }
}
