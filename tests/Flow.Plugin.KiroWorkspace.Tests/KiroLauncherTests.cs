using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Flow.Plugin.KiroWorkspace.KiroHelper;
using Flow.Plugin.KiroWorkspace.WorkspacesHelper;
using Xunit;

namespace Flow.Plugin.KiroWorkspace.Tests
{
    public sealed class KiroLauncherTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "kiro-launcher-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _fakeExe;
        private readonly string _projectFolder;

        public KiroLauncherTests()
        {
            Directory.CreateDirectory(_directory);
            _fakeExe = Path.Combine(_directory, "Kiro.exe");
            File.WriteAllText(_fakeExe, string.Empty);
            _projectFolder = Path.Combine(_directory, "my project");
            Directory.CreateDirectory(_projectFolder);
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

        private RecentWorkspace LocalWorkspace(string folder, string exe = null, WorkspaceType type = WorkspaceType.Folder)
        {
            var uri = new Uri(folder).AbsoluteUri;
            var workspace = KiroWorkspacesApi.ParseUri(uri, new KiroInstance { ExecutablePath = exe ?? _fakeExe });
            return workspace with { WorkspaceType = type };
        }

        [Fact]
        public void BuildStartInfo_Folder_UsesFolderUriWithOriginalUri()
        {
            var workspace = LocalWorkspace(_projectFolder);

            var startInfo = KiroLauncher.BuildStartInfo(workspace);

            Assert.Equal(_fakeExe, startInfo.FileName);
            Assert.False(startInfo.UseShellExecute);
            Assert.Equal(new[] { "--folder-uri", workspace.OriginalUri }, startInfo.ArgumentList);
        }

        [Fact]
        public void BuildStartInfo_WorkspaceFile_UsesFileUri()
        {
            var workspace = LocalWorkspace(_projectFolder, type: WorkspaceType.Workspace);

            var startInfo = KiroLauncher.BuildStartInfo(workspace);

            Assert.Equal("--file-uri", startInfo.ArgumentList[0]);
        }

        [Fact]
        public void BuildStartInfo_WithoutOriginalUri_FallsBackToUnescapedPath()
        {
            var workspace = LocalWorkspace(_projectFolder) with { OriginalUri = null };

            var startInfo = KiroLauncher.BuildStartInfo(workspace);

            Assert.Equal(workspace.Path.Value, startInfo.ArgumentList[1]);
        }

        [Fact]
        public void Launch_ExistingFolder_StartsKiroOnce()
        {
            var workspace = LocalWorkspace(_projectFolder);
            ProcessStartInfo started = null;
            var calls = 0;

            var outcome = KiroLauncher.Launch(workspace, info =>
            {
                started = info;
                calls++;
            });

            Assert.Equal(LaunchOutcome.Success, outcome);
            Assert.Equal(1, calls);
            Assert.Equal(_fakeExe, started.FileName);
        }

        [Fact]
        public void Launch_MissingExecutable_ReportsExecutableMissingWithoutStarting()
        {
            var workspace = LocalWorkspace(_projectFolder, exe: Path.Combine(_directory, "gone", "Kiro.exe"));
            var started = false;

            var outcome = KiroLauncher.Launch(workspace, _ => started = true);

            Assert.Equal(LaunchOutcome.ExecutableMissing, outcome);
            Assert.False(started);
        }

        [Fact]
        public void Launch_WithoutKiroInstance_ReportsExecutableMissing()
        {
            var workspace = LocalWorkspace(_projectFolder) with { KiroInstance = null };

            Assert.Equal(LaunchOutcome.ExecutableMissing, KiroLauncher.Launch(workspace, _ => { }));
        }

        [Fact]
        public void Launch_LocalFolderThatNoLongerExists_ReportsPathMissingWithoutStarting()
        {
            var workspace = LocalWorkspace(Path.Combine(_directory, "deleted folder"));
            var started = false;

            var outcome = KiroLauncher.Launch(workspace, _ => started = true);

            Assert.Equal(LaunchOutcome.PathMissing, outcome);
            Assert.False(started);
        }

        [Fact]
        public void Launch_RemoteWorkspace_SkipsLocalPathCheck()
        {
            var workspace = KiroWorkspacesApi.ParseUri(
                "vscode-remote://wsl+Ubuntu/home/me/app",
                new KiroInstance { ExecutablePath = _fakeExe });
            var started = false;

            var outcome = KiroLauncher.Launch(workspace, _ => started = true);

            Assert.Equal(LaunchOutcome.Success, outcome);
            Assert.True(started);
        }

        [Fact]
        public void Launch_ProcessStartFails_ReportsFailedInsteadOfThrowing()
        {
            var workspace = LocalWorkspace(_projectFolder);

            var outcome = KiroLauncher.Launch(workspace, _ => throw new Win32Exception("access denied"));

            Assert.Equal(LaunchOutcome.Failed, outcome);
        }
    }
}
