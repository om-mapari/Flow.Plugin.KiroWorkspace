// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Text.RegularExpressions;

namespace Flow.Plugin.KiroWorkspace.WorkspacesHelper
{
    /// <summary>
    /// Classifies the URIs found in Kiro's recently opened list.
    /// Kiro is built on Code OSS, so it uses the same <c>file://</c> and <c>vscode-remote://</c> URI schemes.
    /// </summary>
    public static class KiroUriParser
    {
        private static readonly Regex LocalWorkspace = new Regex("^file:///(.+)$", RegexOptions.Compiled);
        private static readonly Regex RemoteSSHWorkspace = new Regex(@"^vscode-remote://ssh-remote\+(.+?(?=\/))(.+)$", RegexOptions.Compiled);
        private static readonly Regex RemoteWSLWorkspace = new Regex(@"^vscode-remote://wsl\+(.+?(?=\/))(.+)$", RegexOptions.Compiled);
        private static readonly Regex DevContainerWorkspace = new Regex(@"^vscode-remote://dev-container\+(.+?(?=\/))(.+)$", RegexOptions.Compiled);

        public static (WorkspaceLocation? workspaceLocation, string MachineName, string Path) GetTypeWorkspace(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return (null, null, null);
            }

            var local = LocalWorkspace.Match(uri);
            if (local.Success)
            {
                return (WorkspaceLocation.Local, null, local.Groups[1].Value);
            }

            var ssh = RemoteSSHWorkspace.Match(uri);
            if (ssh.Success)
            {
                return (WorkspaceLocation.RemoteSSH, ssh.Groups[1].Value, ssh.Groups[2].Value);
            }

            var wsl = RemoteWSLWorkspace.Match(uri);
            if (wsl.Success)
            {
                return (WorkspaceLocation.RemoteWSL, wsl.Groups[1].Value, wsl.Groups[2].Value);
            }

            var devContainer = DevContainerWorkspace.Match(uri);
            if (devContainer.Success)
            {
                return (WorkspaceLocation.DevContainer, null, devContainer.Groups[2].Value);
            }

            return (null, null, null);
        }
    }
}
