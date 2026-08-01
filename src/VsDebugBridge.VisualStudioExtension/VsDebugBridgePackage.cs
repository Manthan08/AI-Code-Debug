using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace VsDebugBridge.VisualStudioExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("AI Debug Lens", "Shows Visual Studio debugger context inside Codex.", "0.5.2")]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class VsDebugBridgePackage : AsyncPackage
    {
        public const string PackageGuidString = "345d37b8-3d76-4dc8-9c1f-bc5db9fbbf1a";

        private VisualStudioBridgeService? _bridgeService;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if (dte == null)
            {
                return;
            }

            var snapshotProvider = new DebugSnapshotProvider(dte);
            _bridgeService = new VisualStudioBridgeService(dte, snapshotProvider, JoinableTaskFactory);
            _bridgeService.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bridgeService?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
