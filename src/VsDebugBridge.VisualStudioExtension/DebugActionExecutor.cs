using System;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using VsDebugBridge.Contracts;

namespace VsDebugBridge.VisualStudioExtension
{
    internal sealed class DebugActionExecutor
    {
        private readonly DTE2 _dte;
        private readonly DebugSnapshotProvider _snapshotProvider;

        public DebugActionExecutor(DTE2 dte, DebugSnapshotProvider snapshotProvider)
        {
            _dte = dte;
            _snapshotProvider = snapshotProvider;
        }

        public DebugActionResult Execute(VisualStudioInstanceInfo instance, DebugActionRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!request.Approved)
            {
                return new DebugActionResult
                {
                    Action = request.Action,
                    Executed = false,
                    Message = "Debug action was not executed because approved=true was not provided."
                };
            }

            var message = ExecuteApprovedAction(request);
            var snapshotRequest = request.SnapshotRequest ?? new DebugSnapshotRequest();
            var snapshot = _snapshotProvider.Capture(instance, snapshotRequest);

            return new DebugActionResult
            {
                Action = request.Action,
                Executed = true,
                Message = message,
                Snapshot = snapshot
            };
        }

        private string ExecuteApprovedAction(DebugActionRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            switch (request.Action)
            {
                case DebugActionKinds.StepOver:
                    RequireBreakMode(request.Action);
                    _dte.ExecuteCommand("Debug.StepOver");
                    return "Step over executed.";
                case DebugActionKinds.StepInto:
                    RequireBreakMode(request.Action);
                    _dte.ExecuteCommand("Debug.StepInto");
                    return "Step into executed.";
                case DebugActionKinds.StepOut:
                    RequireBreakMode(request.Action);
                    _dte.ExecuteCommand("Debug.StepOut");
                    return "Step out executed.";
                case DebugActionKinds.Continue:
                    RequireBreakMode(request.Action);
                    _dte.ExecuteCommand("Debug.Start");
                    return "Continue executed.";
                case DebugActionKinds.Pause:
                    _dte.ExecuteCommand("Debug.BreakAll");
                    return "Pause requested.";
                case DebugActionKinds.SetBreakpoint:
                    SetBreakpoint(request);
                    return "Breakpoint set.";
                case DebugActionKinds.RemoveBreakpoint:
                    RemoveBreakpoint(request);
                    return "Breakpoint removed if a matching breakpoint existed.";
                default:
                    throw new InvalidOperationException("Unknown debug action: " + request.Action);
            }
        }

        private void RequireBreakMode(string action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            {
                throw new InvalidOperationException(action + " requires Visual Studio to be in break mode.");
            }
        }

        private void SetBreakpoint(DebugActionRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var filePath = RequireFilePath(request);
            var line = RequireLine(request);
            _dte.Debugger.Breakpoints.Add(string.Empty, filePath, line, request.Column ?? 1);
        }

        private void RemoveBreakpoint(DebugActionRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var filePath = RequireFilePath(request);
            var line = RequireLine(request);
            foreach (Breakpoint breakpoint in _dte.Debugger.Breakpoints)
            {
                if (string.Equals(breakpoint.File, filePath, StringComparison.OrdinalIgnoreCase) &&
                    breakpoint.FileLine == line)
                {
                    breakpoint.Delete();
                    return;
                }
            }
        }

        private static string RequireFilePath(DebugActionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                throw new InvalidOperationException(request.Action + " requires filePath.");
            }

            return request.FilePath!;
        }

        private static int RequireLine(DebugActionRequest request)
        {
            if (request.Line == null || request.Line <= 0)
            {
                throw new InvalidOperationException(request.Action + " requires a positive line number.");
            }

            return request.Line.Value;
        }
    }
}
