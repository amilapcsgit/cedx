using System.Diagnostics;

namespace CedxNative.Services;

public sealed class ExternalActionService
{
    public void OpenFolder(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"") { UseShellExecute = true });
    }

    public void LaunchAnyDesk(string anyDeskId)
    {
        if (string.IsNullOrWhiteSpace(anyDeskId) || anyDeskId.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var uri = "anydesk:" + anyDeskId;
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
