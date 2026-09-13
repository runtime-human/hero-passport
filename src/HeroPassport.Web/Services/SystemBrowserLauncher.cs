using System.ComponentModel;
using System.Diagnostics;

namespace HeroPassport.Web.Services;

internal interface ISystemBrowserLauncher
{
    bool TryOpen(Uri uri);
}

internal sealed class SystemBrowserLauncher : ISystemBrowserLauncher
{
    public bool TryOpen(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            return false;
        }
    }
}
