using System;
using System.Collections.Generic;

namespace VsDebugBridge.Contracts
{
    public sealed class DebugSnapshot
    {
        public string SchemaVersion { get; set; } = BridgeConstants.SnapshotSchemaVersion;

        public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public VisualStudioInstanceInfo? VisualStudio { get; set; }

        public SolutionInfo? Solution { get; set; }

        public DocumentLocation? CurrentLocation { get; set; }

        public SourceContextInfo? SourceContext { get; set; }

        public DebuggerState DebuggerState { get; set; } = DebuggerState.Unknown;

        public string? BreakReason { get; set; }

        public ThreadInfo? CurrentThread { get; set; }

        public StackFrameInfo? SelectedStackFrame { get; set; }

        public ExceptionInfo? CurrentException { get; set; }

        public List<StackFrameInfo> CallStack { get; set; } = new List<StackFrameInfo>();

        public List<VariableInfo> Locals { get; set; } = new List<VariableInfo>();

        public List<BuildErrorInfo> BuildErrors { get; set; } = new List<BuildErrorInfo>();

        public List<OutputPaneInfo> OutputPanes { get; set; } = new List<OutputPaneInfo>();
    }

    public enum DebuggerState
    {
        Unknown = 0,
        Design = 1,
        Run = 2,
        Break = 3
    }

    public sealed class DebugSnapshotRequest
    {
        public string? InstanceId { get; set; }

        public int MaxStackFrames { get; set; } = 30;

        public int MaxLocals { get; set; } = 200;

        public int MaxBuildErrors { get; set; } = 200;

        public int MaxValueLength { get; set; } = 500;

        public int SourceContextLinesBefore { get; set; } = 20;

        public int SourceContextLinesAfter { get; set; } = 20;

        public int MaxOutputPanes { get; set; } = 5;

        public int MaxOutputLines { get; set; } = 200;
    }

    public sealed class SolutionInfo
    {
        public bool IsOpen { get; set; }

        public string? FullName { get; set; }

        public string? Name { get; set; }

        public List<ProjectInfo> ActiveProjects { get; set; } = new List<ProjectInfo>();
    }

    public sealed class ProjectInfo
    {
        public string? Name { get; set; }

        public string? FullName { get; set; }
    }

    public sealed class DocumentLocation
    {
        public string? FilePath { get; set; }

        public int? Line { get; set; }

        public int? Column { get; set; }
    }

    public sealed class SourceContextInfo
    {
        public string? FilePath { get; set; }

        public int StartLine { get; set; }

        public int EndLine { get; set; }

        public int? FocusLine { get; set; }

        public List<SourceLineInfo> Lines { get; set; } = new List<SourceLineInfo>();
    }

    public sealed class SourceLineInfo
    {
        public int LineNumber { get; set; }

        public string Text { get; set; } = string.Empty;

        public bool IsCurrent { get; set; }
    }

    public sealed class ThreadInfo
    {
        public int? Id { get; set; }

        public string? Name { get; set; }

        public string? Location { get; set; }
    }

    public sealed class ExceptionInfo
    {
        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Value { get; set; }

        public string? Message { get; set; }

        public string? HResult { get; set; }

        public string? StackTrace { get; set; }

        public string? InnerException { get; set; }
    }

    public sealed class StackFrameInfo
    {
        public int Index { get; set; }

        public string? FunctionName { get; set; }

        public string? FilePath { get; set; }

        public int? Line { get; set; }

        public string? Language { get; set; }
    }

    public sealed class VariableInfo
    {
        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Value { get; set; }

        public bool IsValid { get; set; }
    }

    public sealed class BuildErrorInfo
    {
        public string? Severity { get; set; }

        public string? Description { get; set; }

        public string? Code { get; set; }

        public string? FilePath { get; set; }

        public int? Line { get; set; }

        public int? Column { get; set; }

        public string? ProjectName { get; set; }
    }

    public sealed class OutputPaneInfo
    {
        public string? Name { get; set; }

        public List<string> Lines { get; set; } = new List<string>();
    }
}
