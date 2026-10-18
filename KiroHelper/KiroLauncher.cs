using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Flow.Plugin.KiroWorkspace.WorkspacesHelper;

namespace Flow.Plugin.KiroWorkspace.KiroHelper
{
    internal enum LaunchOutcome
    {
        Success,
        ExecutableMissing,
        PathMissing,
        Failed,
    }

    /// <summary>
    /// Opens a workspace in Kiro by running <c>Kiro.exe --folder-uri|--file-uri &lt;uri&gt;</c>.
    /// </summary>
    internal static class KiroLauncher
    {
        internal static ProcessStartInfo BuildStartInfo(RecentWorkspace workspace)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = workspace.KiroInstance.ExecutablePath,
                UseShellExecute = false,
            };

            startInfo.ArgumentList.Add(workspace.WorkspaceType == WorkspaceType.Workspace
                ? "--file-uri"
                : "--folder-uri");

            // Hand Kiro back the exact URI it stored; fall back to the unescaped one for hand-entered workspaces.
            startInfo.ArgumentList.Add(string.IsNullOrEmpty(workspace.OriginalUri)
                ? workspace.Path
                : workspace.OriginalUri);

            return startInfo;
        }

        /// <param name="workspace">The workspace to open.</param>
        /// <param name="start">Process launcher; replaceable so the logic can be tested without starting Kiro.</param>
        internal static LaunchOutcome Launch(RecentWorkspace workspace, Action<ProcessStartInfo> start = null)
        {
            var executable = workspace.KiroInstance?.ExecutablePath;
            if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
                return LaunchOutcome.ExecutableMissing;

            // Only local paths can be checked from here; remote ones are validated by Kiro itself.
            if (workspace.WorkspaceLocation == WorkspaceLocation.Local && !LocalPathExists(workspace))
                return LaunchOutcome.PathMissing;

            try
            {
                var startInfo = BuildStartInfo(workspace);
                if (start != null)
                {
                    start(startInfo);
                }
                else
                {
                    Process.Start(startInfo)?.Dispose();
                }

                return LaunchOutcome.Success;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
            {
                Main.Context?.API.LogException(nameof(KiroLauncher), $"Failed to start {executable}", ex);
                return LaunchOutcome.Failed;
            }
        }

        internal static bool LocalPathExists(RecentWorkspace workspace)
        {
            var path = SystemPath.RealPath(workspace.RelativePath);
            return workspace.WorkspaceType == WorkspaceType.Workspace
                ? File.Exists(path)
                : Directory.Exists(path);
        }
    }
}
