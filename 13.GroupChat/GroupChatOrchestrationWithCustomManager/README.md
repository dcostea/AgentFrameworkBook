# Group Chat orchestration with a custom manager

This sample shows one idea: a custom `GroupChatManager` can choose the next agent from the conversation instead of using a fixed round-robin order.

Robby uses three agents:

| Agent | Responsibility | Tools |
| --- | --- | --- |
| `EnvironmentAgent` | Reads temperature and humidity | Shared `SensorTools` |
| `MaintenanceAgent` | Calibrates the sensors | Shared `MaintenanceTools.CalibrateSensorsAsync` |
| `SafetyAgent` | Reviews evidence and returns `APPROVED`, `DENIED`, or `CALIBRATION` | None |

The custom manager follows these routing rules:

1. `EnvironmentAgent` starts by collecting readings.
2. After Environment responds, `SafetyAgent` reviews the readings.
3. Safety immediately denies only extreme first readings above 90°C or 95% humidity. Otherwise, it responds with `CALIBRATION` and the manager asks Environment for an independent second observation.
4. Safety applies the normal 60°C and 80% limits to both observations. If both are safe, Safety responds with `CALIBRATION` so Maintenance can check the sensors before approval.
5. After Maintenance confirms calibration, Safety makes the final decision.
6. The conversation ends when Safety responds with `APPROVED` or `DENIED`, or when the iteration limit is reached.

Extreme readings are denied immediately. Most investigations collect two observations; safe evidence takes six turns: Environment → Safety → Environment → Safety → Maintenance → Safety.

One observation is a temperature/humidity pair collected during one Environment turn. The manager's `UpdateHistoryAsync` hook appends the completed observation count and calibration status to each broadcast. Safety uses this explicit state rather than counting tool messages or treating its earlier `CALIBRATION` requests as completed maintenance. Calibration is marked complete only when Maintenance returns a successful tool result; Maintenance has only the calibration tool. Termination checks the leading decision keyword, not keywords mentioned in the reason, and `CALIBRATION` remains a request rather than a terminal decision.

The sensor readings are random because this sample reuses the same tools as the other book examples. Calibration is simulated and does not alter the random readings. Numeric comparisons still belong to SafetyAgent, so clearer prompts and explicit state reduce ambiguity but do not guarantee correct model arithmetic. The thresholds are teaching rules that make conditional speaker selection visible, not production safety guidance.

## Run the sample

Configure the shared user secrets, then run the project:

```powershell
dotnet user-secrets set "OpenAI:ModelId" "<your-model>"
dotnet user-secrets set "OpenAI:ApiKey" "<your-api-key>"
dotnet run
```

The workflow diagram is written to `workflow.md`, and streamed agent responses are printed to the console.