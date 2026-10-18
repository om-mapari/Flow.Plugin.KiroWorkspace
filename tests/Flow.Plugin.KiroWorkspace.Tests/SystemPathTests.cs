using Xunit;

namespace Flow.Plugin.KiroWorkspace.Tests
{
    public class SystemPathTests
    {
        [Theory]
        [InlineData("d:/Projects/app", @"D:\Projects\app")]
        [InlineData("C:/", @"C:\")]
        [InlineData("/home/me/app", "/home/me/app")]
        public void RealPath_ConvertsWindowsPathsAndLeavesOthersAlone(string input, string expected)
        {
            Assert.Equal(expected, SystemPath.RealPath(input));
        }
    }
}
