// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Flow.Plugin.KiroWorkspace.KiroHelper;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;

namespace Flow.Plugin.KiroWorkspace.WorkspacesHelper
{
    public class KiroWorkspacesApi
    {
        private const string RecentlyOpenedKey = "history.recentlyOpenedPathsList";

        private static readonly Regex WorkspaceLabelParser = new Regex("(.+?)(\\[.+\\])", RegexOptions.Compiled);

        [CanBeNull]
        public static RecentWorkspace ParseUri(string uri, KiroInstance kiroInstance)
        {
            if (string.IsNullOrEmpty(uri))
                return null;

            var unescapedUri = Uri.UnescapeDataString(uri);
            var typeWorkspace = KiroUriParser.GetTypeWorkspace(unescapedUri);
            if (!typeWorkspace.workspaceLocation.HasValue)
                return null;

            return new RecentWorkspace
            {
                Path = unescapedUri,
                OriginalUri = uri,
                RelativePath = typeWorkspace.Path,
                FolderName = GetFolderName(typeWorkspace.Path),
                ExtraInfo = typeWorkspace.MachineName,
                WorkspaceLocation = typeWorkspace.workspaceLocation.Value,
                KiroInstance = kiroInstance,
            };
        }

        internal static string GetFolderName(string path)
        {
            var trimmed = path.TrimEnd('/', '\\');
            var folderName = Path.GetFileName(trimmed);

            // Drive roots such as "d:/" have no file name; show the drive letter instead.
            return string.IsNullOrEmpty(folderName) ? trimmed.TrimEnd(':') : folderName;
        }

        public List<RecentWorkspace> Workspaces
        {
            get
            {
                var results = new List<RecentWorkspace>();

                foreach (var kiroInstance in KiroInstances.Instances)
                {
                    // Newer Kiro builds keep the list in the shared storage database; older ones in the
                    // per-user global storage database.
                    var storageDbPaths = new[]
                        {
                            Path.Combine(kiroInstance.AppData, "User", "globalStorage", "state.vscdb"),
                            kiroInstance.SharedStorageDbPath,
                        }
                        .Where(filePath => !string.IsNullOrEmpty(filePath))
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                    foreach (var storageDbPath in storageDbPaths)
                    {
                        results.AddRange(ReadRecentlyOpened(storageDbPath, kiroInstance));
                    }
                }

                return results
                    .GroupBy(workspace =>
                            $"{workspace.KiroInstance?.ExecutablePath ?? ""}|{workspace.WorkspaceType}|{workspace.Path}",
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
            }
        }

        /// <summary>
        /// Reads Kiro's <c>history.recentlyOpenedPathsList</c> entry from a <c>state.vscdb</c> SQLite database.
        /// Returns an empty list (and logs) when the database is missing, locked or in an unexpected format.
        /// </summary>
        internal static List<RecentWorkspace> ReadRecentlyOpened(string storageDbPath, KiroInstance kiroInstance)
        {
            var results = new List<RecentWorkspace>();

            if (!File.Exists(storageDbPath))
                return results;

            try
            {
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = storageDbPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Shared,
                    Pooling = false,
                }.ToString();

                string json;
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT value FROM ItemTable WHERE key = $key";
                    command.Parameters.AddWithValue("$key", RecentlyOpenedKey);
                    json = command.ExecuteScalar()?.ToString();
                }

                if (string.IsNullOrEmpty(json))
                    return results;

                using var historyDoc = JsonDocument.Parse(json);
                var root = historyDoc.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Array)
                {
                    return results;
                }

                foreach (var entry in entries.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                        continue;

                    RecentWorkspace workspace = null;
                    if (TryGetString(entry, "folderUri", out var folderUri))
                    {
                        workspace = ParseUri(folderUri, kiroInstance);
                    }
                    else if (entry.TryGetProperty("workspace", out var workspaceInfo) &&
                             workspaceInfo.ValueKind == JsonValueKind.Object &&
                             TryGetString(workspaceInfo, "configPath", out var configPath))
                    {
                        workspace = ParseUri(configPath, kiroInstance);
                        if (workspace != null)
                            workspace = workspace with { WorkspaceType = WorkspaceType.Workspace };
                    }

                    if (workspace == null)
                        continue;

                    if (TryGetString(entry, "label", out var label))
                        workspace = workspace with { Label = FormatLabel(label) };

                    results.Add(workspace);
                }
            }
            catch (Exception ex)
            {
                // The database belongs to Kiro and may be locked, missing tables, or change format.
                // A failure here must not break the whole query.
                Main.Context?.API.LogException(nameof(KiroWorkspacesApi), $"Failed to read recent workspaces from {storageDbPath}", ex);
            }

            return results;
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = null;
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return !string.IsNullOrEmpty(value);
            }

            return false;
        }

        private static string FormatLabel(string label)
        {
            var match = WorkspaceLabelParser.Match(label);
            return match.Success ? $"{match.Groups[2]} {match.Groups[1]}".Trim() : label;
        }
    }
}
