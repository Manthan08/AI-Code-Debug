using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using EnvDTE90a;
using Microsoft.VisualStudio.Shell;
using VsDebugBridge.Contracts;

namespace VsDebugBridge.VisualStudioExtension
{
    internal sealed class DebugSnapshotProvider
    {
        private static readonly Regex ErrorCodeRegex = new Regex(@"\b[A-Z]{2,}[A-Z0-9]*\d{3,}\b", RegexOptions.Compiled);

        private readonly DTE2 _dte;

        public DebugSnapshotProvider(DTE2 dte)
        {
            _dte = dte;
        }

        public DebugSnapshot Capture(VisualStudioInstanceInfo instance, DebugSnapshotRequest request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var debuggerState = MapDebuggerState(_dte.Debugger.CurrentMode);
            var currentLocation = CaptureCurrentLocation(debuggerState);
            var snapshot = new DebugSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                VisualStudio = instance,
                Solution = CaptureSolution(),
                CurrentLocation = currentLocation,
                DebuggerState = debuggerState,
                BreakReason = SafeString(() => _dte.Debugger.LastBreakReason.ToString())
            };

            CaptureSourceContext(snapshot, request.SourceContextLinesBefore, request.SourceContextLinesAfter, request.MaxValueLength);
            CaptureCurrentThread(snapshot);
            CaptureException(snapshot, request.MaxValueLength);
            CaptureCallStack(snapshot, request.MaxStackFrames);
            CaptureSelectedStackFrame(snapshot);
            CaptureLocals(snapshot, request.MaxLocals, request.MaxValueLength);
            CaptureBuildErrors(snapshot, request.MaxBuildErrors);
            CaptureOutputPanes(snapshot, request.MaxOutputPanes, request.MaxOutputLines, request.MaxValueLength);

            return snapshot;
        }

        private SolutionInfo CaptureSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var solution = _dte.Solution;
            var solutionFullName = solution?.FullName;
            var info = new SolutionInfo
            {
                IsOpen = solution != null && solution.IsOpen,
                FullName = solutionFullName,
                Name = string.IsNullOrWhiteSpace(solutionFullName) ? null : Path.GetFileNameWithoutExtension(solutionFullName)
            };

            var activeProjects = _dte.ActiveSolutionProjects as Array;
            if (activeProjects != null)
            {
                foreach (var item in activeProjects)
                {
                    if (item is Project project)
                    {
                        info.ActiveProjects.Add(new ProjectInfo
                        {
                            Name = project.Name,
                            FullName = SafeString(() => project.FullName)
                        });
                    }
                }
            }

            return info;
        }

        private DocumentLocation? CaptureCurrentLocation(DebuggerState debuggerState)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var stackFrameLocation = debuggerState == DebuggerState.Break
                ? CaptureCurrentStackFrameLocation()
                : null;
            var activeDocumentLocation = CaptureActiveDocumentLocation();
            return DebugLocationSelector.SelectCurrentLocation(debuggerState, activeDocumentLocation, stackFrameLocation);
        }

        private DocumentLocation? CaptureActiveDocumentLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var document = _dte.ActiveDocument;
            if (document == null)
            {
                return null;
            }

            var location = new DocumentLocation
            {
                FilePath = SafeString(() => document.FullName)
            };

            if (document.Selection is TextSelection selection)
            {
                location.Line = selection.ActivePoint?.Line;
                location.Column = selection.ActivePoint?.LineCharOffset;
            }

            return location;
        }

        private DocumentLocation? CaptureCurrentStackFrameLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var frame = _dte.Debugger.CurrentStackFrame;
                if (frame == null)
                {
                    return null;
                }

                var filePath = ReadStackFrameFileName(frame);
                var line = ReadStackFrameLineNumber(frame);
                if (string.IsNullOrWhiteSpace(filePath) || line == null)
                {
                    return null;
                }

                return new DocumentLocation
                {
                    FilePath = filePath,
                    Line = line
                };
            }
            catch
            {
                return null;
            }
        }

        private void CaptureSourceContext(DebugSnapshot snapshot, int linesBefore, int linesAfter, int maxLineLength)
        {
            var location = snapshot.CurrentLocation;
            var filePath = location?.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) || location?.Line == null)
            {
                return;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                var focusLineValue = location!.Line!.Value;
                var lines = File.ReadAllLines(filePath);
                if (lines.Length == 0)
                {
                    return;
                }

                var focusLine = Math.Max(1, Math.Min(focusLineValue, lines.Length));
                var startLine = Math.Max(1, focusLine - Math.Max(0, linesBefore));
                var endLine = Math.Min(lines.Length, focusLine + Math.Max(0, linesAfter));
                var context = new SourceContextInfo
                {
                    FilePath = filePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    FocusLine = focusLine
                };

                for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
                {
                    context.Lines.Add(new SourceLineInfo
                    {
                        LineNumber = lineNumber,
                        Text = Truncate(lines[lineNumber - 1], maxLineLength) ?? string.Empty,
                        IsCurrent = lineNumber == focusLine
                    });
                }

                snapshot.SourceContext = context;
            }
            catch
            {
            }
        }

        private void CaptureCurrentThread(DebugSnapshot snapshot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (snapshot.DebuggerState != DebuggerState.Break)
            {
                return;
            }

            try
            {
                var thread = _dte.Debugger.CurrentThread;
                if (thread == null)
                {
                    return;
                }

                snapshot.CurrentThread = new ThreadInfo
                {
                    Id = SafeComInt(thread, "ID"),
                    Name = SafeComString(thread, "Name"),
                    Location = SafeComString(thread, "Location")
                };
            }
            catch
            {
            }
        }

        private void CaptureException(DebugSnapshot snapshot, int maxValueLength)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var exception = _dte.Debugger.GetExpression("$exception", false, 1000);
                if (exception == null || !exception.IsValidValue)
                {
                    return;
                }

                snapshot.CurrentException = new ExceptionInfo
                {
                    Name = exception.Name,
                    Type = exception.Type,
                    Value = Truncate(exception.Value, maxValueLength),
                    Message = ReadExpressionValue("$exception.Message", maxValueLength),
                    HResult = ReadExpressionValue("$exception.HResult", maxValueLength),
                    StackTrace = ReadExpressionValue("$exception.StackTrace", maxValueLength),
                    InnerException = ReadExpressionValue("$exception.InnerException", maxValueLength)
                };
            }
            catch
            {
            }
        }

        private void CaptureCallStack(DebugSnapshot snapshot, int maxStackFrames)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (snapshot.DebuggerState != DebuggerState.Break)
            {
                return;
            }

            try
            {
                var frames = _dte.Debugger.CurrentThread?.StackFrames;
                if (frames == null)
                {
                    return;
                }

                var count = Math.Min(frames.Count, Math.Max(0, maxStackFrames));
                for (var i = 1; i <= count; i++)
                {
                    var frame = frames.Item(i);
                    snapshot.CallStack.Add(CreateStackFrameInfo(frame, i - 1));
                }
            }
            catch
            {
            }
        }

        private void CaptureSelectedStackFrame(DebugSnapshot snapshot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (snapshot.DebuggerState != DebuggerState.Break)
            {
                return;
            }

            try
            {
                var frame = _dte.Debugger.CurrentStackFrame;
                if (frame != null)
                {
                    snapshot.SelectedStackFrame = CreateStackFrameInfo(frame, null);
                }
            }
            catch
            {
            }
        }

        private void CaptureLocals(DebugSnapshot snapshot, int maxLocals, int maxValueLength)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (snapshot.DebuggerState != DebuggerState.Break)
            {
                return;
            }

            try
            {
                var frame = _dte.Debugger.CurrentStackFrame;
                var locals = frame?.Locals;
                if (locals == null)
                {
                    return;
                }

                var count = Math.Min(locals.Count, Math.Max(0, maxLocals));
                for (var i = 1; i <= count; i++)
                {
                    var expression = locals.Item(i);
                    snapshot.Locals.Add(new VariableInfo
                    {
                        Name = expression.Name,
                        Type = expression.Type,
                        Value = Truncate(expression.Value, maxValueLength),
                        IsValid = expression.IsValidValue
                    });
                }
            }
            catch
            {
            }
        }

        private void CaptureBuildErrors(DebugSnapshot snapshot, int maxBuildErrors)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var items = _dte.ToolWindows.ErrorList.ErrorItems;
                var count = Math.Min(items.Count, Math.Max(0, maxBuildErrors));
                for (var i = 1; i <= count; i++)
                {
                    var item = items.Item(i);
                    snapshot.BuildErrors.Add(new BuildErrorInfo
                    {
                        Severity = SafeString(() => item.ErrorLevel.ToString()),
                        Description = item.Description,
                        Code = ExtractErrorCode(item.Description),
                        FilePath = item.FileName,
                        Line = item.Line <= 0 ? (int?)null : item.Line,
                        Column = item.Column <= 0 ? (int?)null : item.Column,
                        ProjectName = item.Project
                    });
                }
            }
            catch
            {
            }
        }

        private void CaptureOutputPanes(DebugSnapshot snapshot, int maxOutputPanes, int maxOutputLines, int maxValueLength)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var panes = _dte.ToolWindows.OutputWindow.OutputWindowPanes;
                var count = Math.Min(panes.Count, Math.Max(0, maxOutputPanes));
                for (var i = 1; i <= count; i++)
                {
                    var pane = panes.Item(i);
                    var text = SafeString(() =>
                    {
                        var document = pane.TextDocument;
                        var editPoint = document.StartPoint.CreateEditPoint();
                        return editPoint.GetText(document.EndPoint);
                    });

                    snapshot.OutputPanes.Add(new OutputPaneInfo
                    {
                        Name = SafeString(() => pane.Name),
                        Lines = TakeLastLines(text, maxOutputLines, maxValueLength)
                    });
                }
            }
            catch
            {
            }
        }

        private StackFrameInfo CreateStackFrameInfo(StackFrame frame, int? index)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return new StackFrameInfo
            {
                Index = index ?? -1,
                FunctionName = SafeString(() => frame.FunctionName),
                FilePath = ReadStackFrameFileName(frame),
                Line = ReadStackFrameLineNumber(frame),
                Language = SafeString(() => frame.Language)
            };
        }

        private static DebuggerState MapDebuggerState(dbgDebugMode mode)
        {
            switch (mode)
            {
                case dbgDebugMode.dbgDesignMode:
                    return DebuggerState.Design;
                case dbgDebugMode.dbgRunMode:
                    return DebuggerState.Run;
                case dbgDebugMode.dbgBreakMode:
                    return DebuggerState.Break;
                default:
                    return DebuggerState.Unknown;
            }
        }

        private string? ReadExpressionValue(string expressionText, int maxValueLength)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var expression = _dte.Debugger.GetExpression(expressionText, false, 1000);
                if (expression == null || !expression.IsValidValue)
                {
                    return null;
                }

                return Truncate(expression.Value, maxValueLength);
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractErrorCode(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var match = ErrorCodeRegex.Match(description!);
            return match.Success ? match.Value : null;
        }

        private static List<string> TakeLastLines(string? text, int maxLines, int maxLineLength)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text) || maxLines <= 0)
            {
                return result;
            }

            var lines = text!.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.None);
            var start = Math.Max(0, lines.Length - maxLines);
            for (var i = start; i < lines.Length; i++)
            {
                result.Add(Truncate(lines[i], maxLineLength) ?? string.Empty);
            }

            return result;
        }

        private static string? SafeComString(object target, string propertyName)
        {
            try
            {
                return target.GetType()
                    .InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null)
                    ?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static int? SafeComInt(object target, string propertyName)
        {
            try
            {
                var value = target.GetType().InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null);
                if (value == null)
                {
                    return null;
                }

                var converted = Convert.ToInt32(value);
                return converted <= 0 ? (int?)null : converted;
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadStackFrameFileName(StackFrame frame)
        {
            if (frame is StackFrame2 frame2)
            {
                return SafeString(() => frame2.FileName);
            }

            return SafeComString(frame, "FileName");
        }

        private static int? ReadStackFrameLineNumber(StackFrame frame)
        {
            if (frame is StackFrame2 frame2)
            {
                return SafeUIntToInt(() => frame2.LineNumber);
            }

            return SafeComInt(frame, "LineNumber");
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (value == null || maxLength <= 0 || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static string? SafeString(Func<string?> getValue)
        {
            try
            {
                return getValue();
            }
            catch
            {
                return null;
            }
        }

        private static int? SafeInt(Func<int> getValue)
        {
            try
            {
                var value = getValue();
                return value <= 0 ? (int?)null : value;
            }
            catch
            {
                return null;
            }
        }

        private static int? SafeUIntToInt(Func<uint> getValue)
        {
            try
            {
                var value = getValue();
                return value == 0 || value > int.MaxValue ? (int?)null : (int)value;
            }
            catch
            {
                return null;
            }
        }
    }
}
