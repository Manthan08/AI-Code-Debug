using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using VsDebugBridge.Contracts;

namespace VsDebugBridge.Ipc
{
    public sealed class DiscoveryStore
    {
        private readonly string _instancesDirectory;
        private readonly TimeSpan _staleAfter;

        public DiscoveryStore()
            : this(BridgeConstants.GetDefaultDiscoveryRoot(), TimeSpan.FromMinutes(2))
        {
        }

        public DiscoveryStore(string rootDirectory, TimeSpan staleAfter)
        {
            _instancesDirectory = Path.Combine(rootDirectory, "instances");
            _staleAfter = staleAfter;
        }

        public string InstancesDirectory => _instancesDirectory;

        public void WriteInstance(VisualStudioInstanceInfo instance)
        {
            Directory.CreateDirectory(_instancesDirectory);
            instance.LastSeenUtc = DateTimeOffset.UtcNow;
            var path = GetInstancePath(instance.InstanceId);
            File.WriteAllText(path, BridgeJson.Serialize(instance));
        }

        public void DeleteInstance(string instanceId)
        {
            var path = GetInstancePath(instanceId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public IReadOnlyList<VisualStudioInstanceInfo> ListInstances()
        {
            if (!Directory.Exists(_instancesDirectory))
            {
                return new List<VisualStudioInstanceInfo>();
            }

            var now = DateTimeOffset.UtcNow;
            var instances = new List<VisualStudioInstanceInfo>();

            foreach (var path in Directory.GetFiles(_instancesDirectory, "*.json"))
            {
                var instance = ReadInstance(path);
                if (instance == null)
                {
                    continue;
                }

                if (now - instance.LastSeenUtc > _staleAfter || !IsProcessAlive(instance.ProcessId))
                {
                    TryDelete(path);
                    continue;
                }

                instances.Add(instance);
            }

            return instances
                .OrderByDescending(instance => instance.LastSeenUtc)
                .ThenBy(instance => instance.ProcessId)
                .ToList();
        }

        public IReadOnlyList<VisualStudioBridgeInstanceHealth> InspectInstances()
        {
            if (!Directory.Exists(_instancesDirectory))
            {
                return new List<VisualStudioBridgeInstanceHealth>();
            }

            var now = DateTimeOffset.UtcNow;
            var instances = new List<VisualStudioBridgeInstanceHealth>();

            foreach (var path in Directory.GetFiles(_instancesDirectory, "*.json"))
            {
                var health = InspectInstance(path, now);
                instances.Add(health);
            }

            return instances
                .OrderByDescending(instance => instance.Instance?.LastSeenUtc ?? DateTimeOffset.MinValue)
                .ThenBy(instance => instance.Instance?.ProcessId ?? 0)
                .ToList();
        }

        public VisualStudioInstanceInfo ResolveInstance(string? instanceId)
        {
            var instances = ListInstances();
            if (instances.Count == 0)
            {
                throw new InvalidOperationException("No Visual Studio debug bridge instances are registered. Start Visual Studio with the bridge extension installed.");
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return instances[0];
            }

            var requested = instanceId!.Trim();
            var match = instances.FirstOrDefault(instance => MatchesInstance(instance, requested));

            if (match == null)
            {
                throw new InvalidOperationException("Visual Studio bridge instance '" + requested + "' was not found.");
            }

            return match;
        }

        public static bool MatchesInstance(VisualStudioInstanceInfo instance, string instanceId)
        {
            var requested = instanceId.Trim();
            return string.Equals(instance.InstanceId, requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(instance.ProcessId.ToString(CultureInfo.InvariantCulture), requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(instance.SolutionName, requested, StringComparison.OrdinalIgnoreCase);
        }

        private VisualStudioBridgeInstanceHealth InspectInstance(string path, DateTimeOffset now)
        {
            var health = new VisualStudioBridgeInstanceHealth
            {
                InstanceFilePath = path
            };

            try
            {
                health.Instance = BridgeJson.Deserialize<VisualStudioInstanceInfo>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Error = "Instance registration file could not be parsed: " + ex.Message;
                return health;
            }

            if (health.Instance == null)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Error = "Instance registration file was empty or invalid.";
                return health;
            }

            health.LastSeenAgeSeconds = Math.Max(0, (now - health.Instance.LastSeenUtc).TotalSeconds);
            health.IsStale = now - health.Instance.LastSeenUtc > _staleAfter;
            health.ProcessAlive = IsProcessAlive(health.Instance.ProcessId);

            if (health.IsStale)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Error = "Instance registration is stale.";
            }
            else if (!health.ProcessAlive)
            {
                health.Status = BridgeHealthStatus.Unavailable;
                health.Error = "Visual Studio process is no longer running.";
            }
            else
            {
                health.Status = BridgeHealthStatus.NotChecked;
            }

            return health;
        }

        private string GetInstancePath(string instanceId)
        {
            return Path.Combine(_instancesDirectory, instanceId + ".json");
        }

        private static VisualStudioInstanceInfo? ReadInstance(string path)
        {
            try
            {
                return BridgeJson.Deserialize<VisualStudioInstanceInfo>(File.ReadAllText(path));
            }
            catch
            {
                TryDelete(path);
                return null;
            }
        }

        private static bool IsProcessAlive(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
