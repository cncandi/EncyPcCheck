using CAMAPI.Application;
using CAMAPI.Extensions;
using CAMAPI.ResultStatus;

namespace EncyPcCheck;

/// <summary>
/// Utility: prueft die Leistung des Rechners und zeigt einen Leistungsindex fuer ENCY.
/// Es werden keine COM-Objekte benutzt; das Fenster laeuft modal in einem eigenen STA-Thread.
/// </summary>
public class UtilityExtension : IExtension, IExtensionUtility
{
    /// <inheritdoc />
    public IExtensionInfo? Info { get; set; }

    /// <inheritdoc />
    public void Run(IExtensionUtilityContext context, out TResultStatus resultStatus)
    {
        resultStatus = default;
        try
        {
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    using var form = new PcCheckForm();
                    form.ShowDialog();
                }
                catch (Exception e)
                {
                    error = e;
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw error;
        }
        catch (Exception e)
        {
            resultStatus.Code = TResultStatusCode.rsError;
            resultStatus.Description = e.Message;
        }
    }
}
