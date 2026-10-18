// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Plugin.KiroWorkspace.KiroHelper
{
    /// <summary>
    /// A discovered Kiro installation and the location of its user data.
    /// </summary>
    public class KiroInstance : IEquatable<KiroInstance>
    {
        /// <summary>
        /// Full path of <c>Kiro.exe</c>.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Kiro user data directory (<c>%APPDATA%\Kiro</c> for a regular install).
        /// </summary>
        public string AppData { get; set; } = string.Empty;

        /// <summary>
        /// Path of the shared storage database (<c>~\.kiro-shared\sharedStorage\state.vscdb</c> for a regular install).
        /// </summary>
        public string SharedStorageDbPath { get; set; } = string.Empty;

        public ImageSource WorkspaceIcon() => WorkspaceIconBitMap;

        public BitmapImage WorkspaceIconBitMap { get; set; }

        public bool Equals(KiroInstance other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(ExecutablePath, other.ExecutablePath, StringComparison.InvariantCultureIgnoreCase)
                   && string.Equals(AppData, other.AppData, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj is KiroInstance instance && Equals(instance);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ExecutablePath.GetHashCode(StringComparison.InvariantCultureIgnoreCase),
                AppData.GetHashCode(StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
