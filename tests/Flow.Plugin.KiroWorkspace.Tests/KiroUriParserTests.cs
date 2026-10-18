using Flow.Plugin.KiroWorkspace.WorkspacesHelper;
using Xunit;

namespace Flow.Plugin.KiroWorkspace.Tests
{
    public class KiroUriParserTests
    {
        [Fact]
        public void LocalFileUri_IsLocal()
        {
            var result = KiroUriParser.GetTypeWorkspace("file:///d:/Projects/app");

            Assert.Equal(WorkspaceLocation.Local, result.workspaceLocation);
            Assert.Null(result.MachineName);
            Assert.Equal("d:/Projects/app", result.Path);
        }

        [Fact]
        public void SshRemoteUri_ExposesHostAndPath()
        {
            var result = KiroUriParser.GetTypeWorkspace("vscode-remote://ssh-remote+my-server/home/me/app");

            Assert.Equal(WorkspaceLocation.RemoteSSH, result.workspaceLocation);
            Assert.Equal("my-server", result.MachineName);
            Assert.Equal("/home/me/app", result.Path);
        }

        [Fact]
        public void WslRemoteUri_ExposesDistroAndPath()
        {
            var result = KiroUriParser.GetTypeWorkspace("vscode-remote://wsl+Ubuntu/home/me/app");

            Assert.Equal(WorkspaceLocation.RemoteWSL, result.workspaceLocation);
            Assert.Equal("Ubuntu", result.MachineName);
            Assert.Equal("/home/me/app", result.Path);
        }

        [Fact]
        public void DevContainerUri_IsDevContainer()
        {
            var result = KiroUriParser.GetTypeWorkspace("vscode-remote://dev-container+abc123/workspaces/app");

            Assert.Equal(WorkspaceLocation.DevContainer, result.workspaceLocation);
            Assert.Equal("/workspaces/app", result.Path);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("https://example.com/not-a-workspace")]
        [InlineData("vscode-remote://vsonline+codespace/workspaces/app")]
        public void UnsupportedOrEmptyUri_IsRejected(string uri)
        {
            var result = KiroUriParser.GetTypeWorkspace(uri);

            Assert.Null(result.workspaceLocation);
        }
    }
}
