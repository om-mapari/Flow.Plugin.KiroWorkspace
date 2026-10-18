// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Flow.Plugin.KiroWorkspace.KiroHelper
{
    public static class KiroInstances
    {
        internal const string ExecutableName = "Kiro.exe";

        private static string _systemPath = string.Empty;

        public static List<KiroInstance> Instances { get; set; } = new();

        /// <summary>
        /// Finds Kiro installations and loads their icons. Safe to call repeatedly: it only re-scans when PATH changed.
        /// </summary>
        public static void LoadKiroInstances()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (_systemPath == pathVariable && Instances.Count > 0)
                return;

            _systemPath = pathVariable;
            Instances = Discover(
                pathVariable,
                Environment.GetEnvironmentVariable("LOCALAPPDATA"),
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("AppData"),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                File.Exists,
                Directory.Exists);

            foreach (var instance in Instances)
            {
                instance.WorkspaceIconBitMap = TryCreateWorkspaceIcon(instance.ExecutablePath);
            }
        }

        /// <summary>
        /// Locates Kiro installations without touching the file system directly, so it can be unit tested.
        /// </summary>
        /// <remarks>
        /// Order: directories on PATH (the Kiro installer adds <c>...\Kiro\bin</c>), then the default per-user
        /// install location, then the default per-machine install location.
        /// </remarks>
        internal static List<KiroInstance> Discover(
            string pathVariable,
            string localAppData,
            string programFiles,
            string roamingAppData,
            string userProfile,
            Func<string, bool> fileExists,
            Func<string, bool> directoryExists)
        {
            var installDirectories = new List<string>();

            foreach (var rawEntry in (pathVariable ?? string.Empty).Split(';'))
            {
                var entry = rawEntry.Trim().Trim('"');
                if (entry.Length == 0)
                    continue;

                entry = entry.TrimEnd('\\', '/');
                if (Path.GetFileName(entry).Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetDirectoryName(entry);
                    if (!string.IsNullOrEmpty(parent))
                        installDirectories.Add(parent);
                }
                else
                {
                    installDirectories.Add(entry);
                }
            }

            if (!string.IsNullOrEmpty(localAppData))
                installDirectories.Add(Path.Combine(localAppData, "Programs", "Kiro"));

            if (!string.IsNullOrEmpty(programFiles))
                installDirectories.Add(Path.Combine(programFiles, "Kiro"));

            var instances = new List<KiroInstance>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var installDirectory in installDirectories)
            {
                var executable = Path.Combine(installDirectory, ExecutableName);
                if (!seen.Add(executable) || !fileExists(executable))
                    continue;

                instances.Add(CreateInstance(installDirectory, executable, roamingAppData, userProfile, directoryExists));
            }

            return instances;
        }

        private static KiroInstance CreateInstance(
            string installDirectory,
            string executable,
            string roamingAppData,
            string userProfile,
            Func<string, bool> directoryExists)
        {
            // Portable layout inherited from Code OSS: a "data" folder next to the executable.
            // The regular (installed) layout was verified against a real Kiro install; the portable one was not.
            var portableData = Path.Combine(installDirectory, "data");
            if (directoryExists(portableData))
            {
                return new KiroInstance
                {
                    ExecutablePath = executable,
                    AppData = Path.Combine(portableData, "user-data"),
                    SharedStorageDbPath = Path.Combine(installDirectory, "data-shared", "sharedStorage", "state.vscdb"),
                };
            }

            return new KiroInstance
            {
                ExecutablePath = executable,
                AppData = Path.Combine(roamingAppData ?? string.Empty, "Kiro"),
                SharedStorageDbPath = string.IsNullOrEmpty(userProfile)
                    ? string.Empty
                    : Path.Combine(userProfile, ".kiro-shared", "sharedStorage", "state.vscdb"),
            };
        }

        private static BitmapImage TryCreateWorkspaceIcon(string executablePath)
        {
            try
            {
                var imagesDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Images");
                using var folderIcon = (Bitmap)Image.FromFile(Path.Combine(imagesDirectory, "folder.png"));

                using var appIcon = Icon.ExtractAssociatedIcon(executablePath);
                if (appIcon == null)
                    return Bitmap2BitmapImage(folderIcon);

                using var appBitmap = appIcon.ToBitmap();
                using var combined = BitmapOverlayToCenter(folderIcon, appBitmap);
                return Bitmap2BitmapImage(combined);
            }
            catch (Exception ex)
            {
                // A missing icon must never prevent the plugin from loading.
                Main.Context?.API.LogException(nameof(KiroInstances), $"Failed to build workspace icon for {executablePath}", ex);
                return null;
            }
        }

        private static BitmapImage Bitmap2BitmapImage(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        private static Bitmap BitmapOverlayToCenter(Bitmap bitmap1, Bitmap overlayBitmap)
        {
            int bitmap1Width = bitmap1.Width;
            int bitmap1Height = bitmap1.Height;

            using var overlayBitmapResized = new Bitmap(overlayBitmap, new System.Drawing.Size(bitmap1Width / 2, bitmap1Height / 2));

            float marginLeft = (float)((bitmap1Width * 0.7) - (overlayBitmapResized.Width * 0.5));
            float marginTop = (float)((bitmap1Height * 0.7) - (overlayBitmapResized.Height * 0.5));

            var finalBitmap = new Bitmap(bitmap1Width, bitmap1Height);
            using (Graphics g = Graphics.FromImage(finalBitmap))
            {
                g.DrawImage(bitmap1, System.Drawing.Point.Empty);
                g.DrawImage(overlayBitmapResized, marginLeft, marginTop);
            }

            return finalBitmap;
        }
    }
}
