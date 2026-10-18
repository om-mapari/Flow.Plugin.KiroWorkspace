// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace Flow.Plugin.KiroWorkspace
{
    using Flow.Launcher.Plugin;
    using KiroHelper;
    using Properties;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Windows.Controls;
    using WorkspacesHelper;

    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu
    {
        private const string FallbackIconPath = "Images\\folder.png";

        internal static PluginInitContext Context { get; private set; }

        private static Settings _settings;

        public string Name => GetTranslatedPluginTitle();

        public string Description => GetTranslatedPluginDescription();

        private KiroInstance _defaultInstance;

        private readonly KiroWorkspacesApi _workspacesApi = new();

        public List<Result> Query(Query query)
        {
            // Kiro may have been installed after Flow Launcher started.
            if (KiroInstances.Instances.Count == 0)
            {
                KiroInstances.LoadKiroInstances();
                _defaultInstance = KiroInstances.Instances.FirstOrDefault();
            }

            if (KiroInstances.Instances.Count == 0)
            {
                return new List<Result> { CreateKiroNotFoundResult() };
            }

            var workspaces = new List<RecentWorkspace>();

            // User defined extra workspaces
            if (_defaultInstance != null)
            {
                workspaces.AddRange(_settings.CustomWorkspaces
                    .Select(uri => KiroWorkspacesApi.ParseUri(uri, _defaultInstance))
                    .Where(ws => ws != null));
            }

            // Search opened workspaces
            if (_settings.DiscoverWorkspaces)
            {
                workspaces.AddRange(_workspacesApi.Workspaces);
            }

            // Simple de-duplication
            var results = workspaces.Distinct().Select(CreateWorkspaceResult).ToList();

            if (query.ActionKeyword == string.Empty ||
                (query.ActionKeyword != string.Empty && query.Search != string.Empty))
            {
                results = results.Where(r =>
                {
                    r.Score = Context.API.FuzzySearch(query.Search, r.Title).Score;
                    return r.Score > 0;
                }).ToList();
            }

            return results;
        }

        private static Result CreateKiroNotFoundResult()
        {
            return new Result
            {
                Title = Resources.KiroNotFound,
                SubTitle = Resources.KiroNotFoundSubTitle,
                IcoPath = FallbackIconPath,
                Action = _ => false,
            };
        }

        private static Result CreateWorkspaceResult(RecentWorkspace ws)
        {
            var title = $"{ws.FolderName}";
            var typeWorkspace = ws.WorkspaceTypeToString();
            if (ws.WorkspaceLocation != WorkspaceLocation.Local)
            {
                title = ws.Label != null
                    ? $"{ws.Label}"
                    : $"{title}{(ws.ExtraInfo != null ? $" - {ws.ExtraInfo}" : string.Empty)} ({typeWorkspace})";
            }

            var tooltip =
                $"{Resources.Workspace}{(ws.WorkspaceLocation != WorkspaceLocation.Local ? $" {Resources.In} {typeWorkspace}" : string.Empty)}: {SystemPath.RealPath(ws.RelativePath)}";

            var result = new Result
            {
                Title = title,
                SubTitle = tooltip,
                TitleToolTip = tooltip,
                Action = c =>
                {
                    // Ctrl+Enter opens the folder in the file manager (local folders only).
                    if (ws.WorkspaceLocation == WorkspaceLocation.Local &&
                        c.SpecialKeyState.ToModifierKeys() == System.Windows.Input.ModifierKeys.Control)
                    {
                        return OpenInFileManager(ws);
                    }

                    var outcome = KiroLauncher.Launch(ws);
                    if (outcome == LaunchOutcome.Success)
                    {
                        return true;
                    }

                    ShowLaunchFailure(outcome, ws);
                    return false;
                },
                ContextData = ws,
            };

            ApplyIcon(result, ws.KiroInstance);
            return result;
        }

        private static void ApplyIcon(Result result, KiroInstance instance)
        {
            if (instance?.WorkspaceIconBitMap != null)
            {
                result.Icon = instance.WorkspaceIcon;
            }
            else
            {
                result.IcoPath = FallbackIconPath;
            }
        }

        private static bool OpenInFileManager(RecentWorkspace ws)
        {
            if (!KiroLauncher.LocalPathExists(ws))
            {
                ShowLaunchFailure(LaunchOutcome.PathMissing, ws);
                return false;
            }

            var path = SystemPath.RealPath(ws.RelativePath);

            // A .code-workspace entry points at a file; show the folder that contains it.
            if (ws.WorkspaceType == WorkspaceType.Workspace)
            {
                path = System.IO.Path.GetDirectoryName(path) ?? path;
            }

            Context.API.OpenDirectory(path);
            return true;
        }

        private static void ShowLaunchFailure(LaunchOutcome outcome, RecentWorkspace ws)
        {
            var message = outcome switch
            {
                LaunchOutcome.ExecutableMissing => Resources.KiroExecutableMissing,
                LaunchOutcome.PathMissing => $"{Resources.PathNotFound} {SystemPath.RealPath(ws.RelativePath)}",
                _ => Resources.OpenFail,
            };

            Context.API.ShowMsg(Context.CurrentPluginMetadata.Name, message, string.Empty);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();

            KiroInstances.LoadKiroInstances();

            _defaultInstance = KiroInstances.Instances.FirstOrDefault();
        }

        public Control CreateSettingPanel() => new SettingsView(Context, _settings);

        public void OnCultureInfoChanged(CultureInfo newCulture)
        {
            Resources.Culture = newCulture;
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.PluginTitle;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.PluginDescription;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            List<Result> results = new();

            if (selectedResult.ContextData is RecentWorkspace ws && ws.WorkspaceLocation == WorkspaceLocation.Local)
            {
                var menuItem = new Result
                {
                    Title = Resources.OpenFolder,
                    SubTitle = Resources.OpenFolderSubTitle,
                    TitleToolTip = Resources.OpenFolderSubTitle,
                    Action = c => OpenInFileManager(ws),
                    ContextData = ws,
                };

                ApplyIcon(menuItem, ws.KiroInstance);
                results.Add(menuItem);
            }

            return results;
        }
    }
}
