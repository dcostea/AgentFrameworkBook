# Three ways to call MCP tools

This client runs three demonstrations in order against `MCPServerWithStdio`:

| Demo | Client API | What happens |
|---|---|---|
| **1. Simple call** | `CallToolAsync("turn_left", ...)` | Send arguments and await the ordinary tool result. |
| **2. Call with progress** | `CallToolAsync("run_diagnostics_with_progress", progress: ...)` | One request stays open while `notifications/progress` reports motors 1–4. |
| **3. Call with polling** | `CallToolWithPollingAsync(new CallToolRequestParams { Name = "run_diagnostics" }, ...)` | Opt into an MCP background task; the SDK polls `tasks/get` and returns the final tool result. |

Progress notifications are not polling. A long-running ordinary call can report progress without becoming a background task. Conversely, the polling example reuses `run_diagnostics`, which does not report progress.

## Run the demonstrations

From the repository root, with no application arguments:

```powershell
dotnet run --project .\08.MCP\MCPClientWithStdio\MCPClientWithStdio.csproj
```

The client starts the stdio server automatically. It lists tools, prompts and resources, runs all three tool-call demos, and reads the example prompts/resources. The demonstrations take roughly ten seconds, plus startup/build time, and require no OpenAI key or model invocation.

Afterward, the client asks:

```text
Run the AI agent? [y/N]
```

- Press **Enter** or answer **n** to finish without loading OpenAI secrets or invoking a model. End-of-input also skips the agent.
- Answer **y** (case-insensitive) to run the existing agent example. This requires the existing `OpenAI:ModelId` and `OpenAI:ApiKey` user secrets and invokes the configured model.

No command-line switch or code edit is needed to choose between the two paths.

Expected tool-call output includes:

```text
1. SIMPLE CALL: turn_left
SIMPLE CALL RESULT: turned anticlockwise 99°.
2. CALL WITH PROGRESS: run_diagnostics_with_progress
  PROGRESS: 1/4 motors checked
  PROGRESS: 2/4 motors checked
  PROGRESS: 3/4 motors checked
  PROGRESS: 4/4 motors checked
PROGRESS CALL RESULT: Diagnostics complete. All 4 motors passed.
3. CALL WITH POLLING: run_diagnostics
POLLING CALL RESULT: Diagnostics complete. All 4 motors passed.
```

## Server setup

Both projects reference `ModelContextProtocol.Extensions.Tasks` 2.2.0. The server registers `MotorTools` for movement and `MaintenanceTools` for diagnostics, then enables Tasks:

```csharp
.WithTasks(new InMemoryMcpTaskStore { DefaultPollIntervalMs = 250 });
```

This advertises the Tasks extension and offloads tool calls **only when the client opts in**. It does not convert the simple or progress demonstrations into task-backed calls. `CallToolWithPollingAsync` supplies that per-request opt-in and handles polling automatically; there is no polling loop to maintain in this sample.

The in-memory task store is for this demonstration: task state does not survive a server restart. The polling wait has a 30-second cancellation timeout. Both diagnostic methods accept the SDK-injected cancellation token, allowing cooperative cancellation. Server logs go to **stderr**, leaving stdout exclusively for MCP messages.

Tasks requires MCP protocol version **2026-07-28 or later**, supported by the SDK version used here. See the [official Tasks documentation](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tasks/tasks.html) and [v2.2.0 Tasks extension example](https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.2.0/samples/TasksExtension).
