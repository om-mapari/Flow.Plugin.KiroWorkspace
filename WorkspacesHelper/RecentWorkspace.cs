// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Flow.Plugin.KiroWorkspace.KiroHelper;
using Flow.Plugin.KiroWorkspace.Properties;

namespace Flow.Plugin.KiroWorkspace.WorkspacesHelper
{
    public record RecentWorkspace
    {
        /// <summary>
        /// Unescaped URI, used for display and de-duplication.
        /// </summary>
        public PathString Path { get; init; }

        /// <summary>
        /// The URI exactly as Kiro stored it. This is what is handed back to Kiro when opening the workspace.
        /// </summary>
        public string OriginalUri { get; init; }

        public PathString RelativePath { get; init; }

        public PathString FolderName { get; init; }

        public string Label { get; init; }

        public string ExtraInfo { get; init; }

        public WorkspaceLocation WorkspaceLocation { get; init; }

        public WorkspaceType WorkspaceType { get; init; } = WorkspaceType.Folder;

        public KiroInstance KiroInstance { get; init; }

        public string WorkspaceTypeToString()
        {
            return WorkspaceLocation switch
            {
                WorkspaceLocation.Local => Resources.TypeWorkspaceLocal,
                WorkspaceLocation.RemoteSSH => "SSH",
                WorkspaceLocation.RemoteWSL => "WSL",
                WorkspaceLocation.DevContainer => Resources.TypeWorkspaceDevContainer,
                _ => string.Empty
            };
        }
    }

    public enum WorkspaceLocation
    {
        Local = 1,
        RemoteWSL = 3,
        RemoteSSH = 4,
        DevContainer = 6,
    }

    public enum WorkspaceType
    {
        Folder = 1,
        Workspace = 2,
    }
}
