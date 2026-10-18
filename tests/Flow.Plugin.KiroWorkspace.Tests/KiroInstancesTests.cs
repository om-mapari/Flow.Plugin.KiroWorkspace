using System;
using System.Collections.Generic;
using Flow.Plugin.KiroWorkspace.KiroHelper;
using Xunit;

namespace Flow.Plugin.KiroWorkspace.Tests
{
    public class KiroInstancesTests
    {
        private const string Local = @"C:\Users\me\AppData\Local";
        private const string Roaming = @"C:\Users\me\AppData\Roaming";
        private const string Profile = @"C:\Users\me";
        private const string ProgramFiles = @"C:\Program Files";

        private static List<KiroInstance> Discover(string path, string[] existingFiles, string[] existingDirectories = null)
        {
            var files = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
            var directories = new HashSet<string>(existingDirectories ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            return KiroInstances.Discover(path, Local, ProgramFiles, Roaming, Profile, files.Contains, directories.Contains);
        }

        [Fact]
        public void NothingInstalled_ReturnsEmpty()
        {
            var result = Discover(@"C:\Windows;C:\Tools\bin", Array.Empty<string>());

            Assert.Empty(result);
        }

        [Fact]
        public void BinFolderOnPath_ResolvesKiroExeInParent()
        {
            var result = Discover(@"C:\Windows;D:\Apps\Kiro\bin", new[] { @"D:\Apps\Kiro\Kiro.exe" });

            var instance = Assert.Single(result);
            Assert.Equal(@"D:\Apps\Kiro\Kiro.exe", instance.ExecutablePath);
        }

        [Fact]
        public void TrailingSeparatorsAndQuotesOnPath_AreTolerated()
        {
            var result = Discover("\"D:\\Apps\\Kiro\\bin\\\";;", new[] { @"D:\Apps\Kiro\Kiro.exe" });

            Assert.Single(result);
        }

        [Fact]
        public void InstallFolderOnPath_ResolvesKiroExeDirectly()
        {
            var result = Discover(@"D:\Apps\Kiro", new[] { @"D:\Apps\Kiro\Kiro.exe" });

            Assert.Single(result);
        }

        [Fact]
        public void DefaultPerUserInstall_IsFoundWithoutPathEntry()
        {
            var result = Discover(string.Empty, new[] { Local + @"\Programs\Kiro\Kiro.exe" });

            var instance = Assert.Single(result);
            Assert.Equal(Local + @"\Programs\Kiro\Kiro.exe", instance.ExecutablePath);
        }

        [Fact]
        public void DefaultPerMachineInstall_IsFoundWithoutPathEntry()
        {
            var result = Discover(string.Empty, new[] { ProgramFiles + @"\Kiro\Kiro.exe" });

            Assert.Single(result);
        }

        [Fact]
        public void SameInstallOnPathAndDefaultLocation_IsReportedOnce()
        {
            var exe = Local + @"\Programs\Kiro\Kiro.exe";

            var result = Discover(Local + @"\Programs\Kiro\bin;" + Local.ToLowerInvariant() + @"\Programs\Kiro\bin", new[] { exe });

            Assert.Single(result);
        }

        [Fact]
        public void RegularInstall_UsesRoamingAppDataAndKiroSharedStorage()
        {
            var result = Discover(string.Empty, new[] { Local + @"\Programs\Kiro\Kiro.exe" });

            var instance = Assert.Single(result);
            Assert.Equal(Roaming + @"\Kiro", instance.AppData);
            Assert.Equal(Profile + @"\.kiro-shared\sharedStorage\state.vscdb", instance.SharedStorageDbPath);
        }

        [Fact]
        public void PortableInstall_UsesDataFolderNextToExecutable()
        {
            var result = Discover(
                @"D:\Portable\Kiro",
                new[] { @"D:\Portable\Kiro\Kiro.exe" },
                new[] { @"D:\Portable\Kiro\data" });

            var instance = Assert.Single(result);
            Assert.Equal(@"D:\Portable\Kiro\data\user-data", instance.AppData);
            Assert.Equal(@"D:\Portable\Kiro\data-shared\sharedStorage\state.vscdb", instance.SharedStorageDbPath);
        }

        [Fact]
        public void KiroInstances_WithSamePaths_AreEqual()
        {
            var a = new KiroInstance { ExecutablePath = @"C:\K\Kiro.exe", AppData = @"C:\D" };
            var b = new KiroInstance { ExecutablePath = @"c:\k\kiro.exe", AppData = @"c:\d" };

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }
    }
}
