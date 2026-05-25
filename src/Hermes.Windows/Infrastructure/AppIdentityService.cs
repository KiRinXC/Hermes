using System.Runtime.InteropServices;

namespace Hermes.Windows.Infrastructure;

public static class AppIdentityService
{
    public const string AppUserModelId = "Hermes.Windows";

    public static void EnsureRegistered(AppLogger logger)
    {
        try
        {
            var result = NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            if (result < 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }
        }
        catch (Exception ex)
        {
            logger.Error("Failed to set process AppUserModelID.", ex);
        }
    }
}
