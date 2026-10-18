using System;
using System.IO;
using System.Linq;
using Flow.Plugin.KiroWorkspace.KiroHelper;
using Flow.Plugin.KiroWorkspace.WorkspacesHelper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Flow.Plugin.KiroWorkspace.Tests
{
    public sealed class KiroWorkspacesApiTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "kiro-workspace-tests-" + Guid.NewGuid().ToString("N"));
        private readonly KiroInstance _instance = new() { ExecutablePath = @"C:\Kiro\Kiro.exe", AppData = @"C:\Kiro\data" };

        public KiroWorkspacesApiTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort cleanup of the temp folder.
            }
        }

        // Creates a database shaped like Kiro's state.vscdb (a key/value ItemTable).
        private string CreateStateDb(string value, bool createTable = true)
        {
            var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".vscdb");
            var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            if (createTable)
            {
                using var create = connection.CreateCommand();
                create.CommandText = "CREATE TABLE ItemTable (key TEXT UNIQUE ON CONFLICT REPLACE, value BLOB)";
                create.ExecuteNonQuery();

                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO ItemTable (key, value) VALUES ('history.recentlyOpenedPathsList', $value)";
                insert.Parameters.AddWithValue("$value", value);
                insert.ExecuteNonQuery();
            }

            return path;
        }

        [Fact]
        public void ParseUri_DecodesDriveLetterAndKeepsOriginalUri()
        {
            var workspace = KiroWorkspacesApi.ParseUri("file:///d%3A/Projects/My%20App", _instance);

            Assert.NotNull(workspace);
            Assert.Equal("file:///d:/Projects/My App", workspace.Path.Value);
            Assert.Equal("file:///d%3A/Projects/My%20App", workspace.OriginalUri);
            Assert.Equal("d:/Projects/My App", workspace.RelativePath.Value);
            Assert.Equal("My App", workspace.FolderName.Value);
            Assert.Equal(WorkspaceLocation.Local, workspace.WorkspaceLocation);
            Assert.Same(_instance, workspace.KiroInstance);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a uri")]
        [InlineData("https://example.com")]
        public void ParseUri_RejectsUnsupportedValues(string uri)
        {
            Assert.Null(KiroWorkspacesApi.ParseUri(uri, _instance));
        }

        [Theory]
        [InlineData("d:/", "d")]
        [InlineData("d:/Projects/app/", "app")]
        [InlineData("/home/me/app", "app")]
        public void GetFolderName_HandlesRootsAndTrailingSeparators(string path, string expected)
        {
            Assert.Equal(expected, KiroWorkspacesApi.GetFolderName(path));
        }

        [Fact]
        public void ReadRecentlyOpened_ReadsFoldersWorkspacesAndRemoteEntries()
        {
            var db = CreateStateDb("""
                {"entries":[
                  {"folderUri":"file:///d%3A/Projects/app"},
                  {"workspace":{"id":"abc","configPath":"file:///d%3A/Projects/team.code-workspace"}},
                  {"folderUri":"vscode-remote://wsl+Ubuntu/home/me/app","label":"app [WSL: Ubuntu]"}
                ]}
                """);

            var workspaces = KiroWorkspacesApi.ReadRecentlyOpened(db, _instance);

            Assert.Equal(3, workspaces.Count);

            var folder = workspaces[0];
            Assert.Equal(WorkspaceType.Folder, folder.WorkspaceType);
            Assert.Equal("app", folder.FolderName.Value);

            var workspaceFile = workspaces[1];
            Assert.Equal(WorkspaceType.Workspace, workspaceFile.WorkspaceType);
            Assert.Equal("team.code-workspace", workspaceFile.FolderName.Value);

            var wsl = workspaces[2];
            Assert.Equal(WorkspaceLocation.RemoteWSL, wsl.WorkspaceLocation);
            Assert.Equal("Ubuntu", wsl.ExtraInfo);
            Assert.Equal("[WSL: Ubuntu] app", wsl.Label);
        }

        [Fact]
        public void ReadRecentlyOpened_SkipsUnrecognisedEntries()
        {
            var db = CreateStateDb("""
                {"entries":[
                  42,
                  {"folderUri":123},
                  {"folderUri":"https://example.com"},
                  {"workspace":"nope"},
                  {"folderUri":"file:///c%3A/ok"}
                ]}
                """);

            var workspaces = KiroWorkspacesApi.ReadRecentlyOpened(db, _instance);

            var only = Assert.Single(workspaces);
            Assert.Equal("ok", only.FolderName.Value);
        }

        [Fact]
        public void ReadRecentlyOpened_MissingFile_ReturnsEmpty()
        {
            var workspaces = KiroWorkspacesApi.ReadRecentlyOpened(Path.Combine(_directory, "missing.vscdb"), _instance);

            Assert.Empty(workspaces);
        }

        [Fact]
        public void ReadRecentlyOpened_DatabaseWithoutItemTable_ReturnsEmpty()
        {
            var db = CreateStateDb(string.Empty, createTable: false);

            Assert.Empty(KiroWorkspacesApi.ReadRecentlyOpened(db, _instance));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("{\"entries\":\"nope\"}")]
        [InlineData("{}")]
        public void ReadRecentlyOpened_UnexpectedContent_ReturnsEmpty(string content)
        {
            var db = CreateStateDb(content);

            Assert.Empty(KiroWorkspacesApi.ReadRecentlyOpened(db, _instance));
        }

        [Fact]
        public void ReadRecentlyOpened_NotASqliteFile_ReturnsEmpty()
        {
            var path = Path.Combine(_directory, "garbage.vscdb");
            File.WriteAllText(path, "this is not a database");

            Assert.Empty(KiroWorkspacesApi.ReadRecentlyOpened(path, _instance).ToList());
        }
    }
}
