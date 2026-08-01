using System;
using System.IO;

namespace VsDebugBridge.Contracts
{
    public static class BridgeConstants
    {
        public const string ProductName = "VsDebugBridge";
        public const string SnapshotSchemaVersion = "1.1";

        public static string GetDefaultDiscoveryRoot()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, ProductName);
        }

        public static string CreatePipeName(int processId)
        {
            return ProductName + "." + processId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
