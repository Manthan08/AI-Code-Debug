# AI Debug Lens Product Explainer Video

## Format

- Length: 75 to 90 seconds
- Tone: practical, developer-focused, demo-first
- Audience: software engineers, tech leads, engineering managers, AI-tool evaluators

## Core Message

AI Debug Lens connects Visual Studio debugging context to Codex through a local MCP bridge, so an AI assistant can understand the live debug state instead of guessing from source code alone.

## 90-Second Script

### 0:00-0:08 - Problem

When we debug with an AI assistant, the assistant usually sees only the code we paste into chat. It does not know where Visual Studio is paused, which variable is wrong, what exception was thrown, or what the Output window says.

### 0:08-0:20 - Product

This product is AI Debug Lens. It is a Visual Studio extension and local MCP server that lets Codex read the current Visual Studio debug context directly from your machine.

### 0:20-0:38 - How It Works

The Visual Studio extension runs inside Visual Studio and reads debugger and build context. A separate local MCP server connects Codex to that extension through a named pipe. Codex can then request a snapshot of the current state.

On screen, I can show the current solution, project, file, line, call stack, locals, exception details, build errors, and recent Output window lines.

### 0:38-0:52 - Safety Boundary

The important design point is control. Snapshot tools are read-only. Debug actions like step over, continue, pause, or breakpoint changes are separate tools and require explicit approval before they execute.

### 0:52-1:15 - Where It Is Useful

This is useful in real debugging sessions where the issue depends on runtime state, not just source code.

It helps with C# and .NET bug fixing, legacy-code understanding, migration work, build-error investigation, onboarding new developers, QA bug reproduction, and AI pair-programming inside Visual Studio.

It is especially helpful when you need evidence: the exact file and line, the selected stack frame, live variable values, the exception, and the output that was actually produced.

### 1:15-1:30 - Close

In short, AI Debug Lens gives Codex eyes into Visual Studio debugging while keeping the bridge local and controlled. Instead of explaining the entire debug state manually, I can ask Codex to inspect the live snapshot and help me reason from real debugger evidence.

## Visual Storyboard

| Time | Visual | Voice-over Focus |
| --- | --- | --- |
| 0:00 | Developer debugging in Visual Studio and switching to Codex | AI cannot help well when it only sees pasted code |
| 0:08 | AI Debug Lens name and simple architecture diagram | VSIX plus local MCP server |
| 0:20 | Visual Studio paused at a breakpoint | Current file, line, stack frame, locals |
| 0:32 | Codex showing a debug snapshot | Codex gets structured context |
| 0:38 | Approval prompt or safety slide | Read-only snapshots; actions require approval |
| 0:52 | Use-case grid | Bug fixing, migration, onboarding, QA, support |
| 1:15 | Final product screen | Local, controlled AI debugging context |

## Areas Where It Will Be Useful

- Live C#/.NET debugging in Visual Studio.
- Investigating runtime bugs where variable values and call stack matter.
- Understanding legacy code without manually explaining every stack frame.
- Migration and refactoring work where behavior must be verified at runtime.
- Build-error and Error List investigation from inside the IDE context.
- QA or support reproduction, where evidence from the actual paused state matters.
- Developer onboarding and mentoring, because the assistant can explain the real execution path.
- AI pair-programming workflows where Codex needs debugger context before suggesting a fix.

## Short Pitch

AI Debug Lens is a local Visual Studio debugging bridge for Codex. It lets Codex inspect the live debugger snapshot, including file, line, call stack, locals, exceptions, build errors, and output, while keeping debug-control actions gated behind explicit approval.

## What Not To Claim

- Do not say it replaces Visual Studio debugging.
- Do not say it automatically fixes bugs.
- Do not say it sends debugger data to a remote server by itself.
- Do not say all languages are supported; the MVP is focused on C#/.NET projects.
