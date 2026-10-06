# One agent with AI skills

This console sample reuses the scenario from `14.Handoff\HandoffOrchestrationAndHandoffReasons` without building a workflow or creating specialist agents.

The original example has **four agents**: an environment router and three specialists. Here, **one `RobotCarAgent`** reads the sensors and selects skills with the same specialist responsibilities.

| Handoff example | Skills example | Reused implementation |
| --- | --- | --- |
| `EnvironmentAgent` | The single agent's base instructions and sensor tools | `SensorTools.AsAITools()` |
| `FireDetectorAgent` | `fire-response` skill | `FireDetectorTools` methods |
| `RainDetectorAgent` | `rain-response` skill | `RainDetectorTools` methods |
| `MotorsAgent` | `movement` skill and `movement-rules` resource | `MotorTools` methods |

No shared tool implementation or existing handoff project is changed.

## How skills are wired

`AgentSkillsProvider` **is an `AIContextProvider`**. Register it through `ChatClientAgentOptions.AIContextProviders`, just like the context providers in the memory examples:

```csharp
using var skillsProvider = RobotCarSkills.CreateProvider();

var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
  ChatOptions = new ChatOptions
  {
    Instructions = "Read the sensors, then choose the appropriate skill.",
    Tools = [.. SensorTools.AsAITools()]
  },
  AIContextProviders = [skillsProvider]
});
```

`RobotCarSkills.cs` defines three `AgentInlineSkill` instances. Each has a discovery description (the equivalent of a handoff reason), detailed instructions, and executable operations. The movement skill also has an on-demand reference resource.

The provider supplies three tools:

1. **`load_skill`** returns the selected skill's instructions and its resource/script descriptions and parameter schemas. Only skill names and descriptions are advertised initially.
2. **`read_skill_resource`** reads supplementary information, such as movement units and rules.
3. **`run_skill_script`** invokes a selected operation.

For example, `.AddScript("StartWipers", RainDetectorTools.StartWipersAsync, ...)` binds the **existing C# method** to a skill script. Agent Framework wraps that delegate using `AIFunctionFactory`, including its parameter schema. These scripts are in-process functions, **not shell commands or generated code**. The remaining fire, rain and movement operations are preserved in the same way, including sprinkler/wiper shutdown and backward movement.

The sensor functions remain ordinary `AITool` entries because the agent needs them before choosing a skill. The specialist functions are not all added to the agent's initial `ChatOptions.Tools`; they are addressed through `run_skill_script` after their instructions are loaded. Skill-local operation names are explicit; `backward` matches the shared tool's naming attribute.

## What the sample does

- Read temperature and droplet level once for the mission.
- Above **60 degrees Celsius**, use the fire skill, then the movement skill to stop.
- Otherwise, when droplets are **High**, use the rain skill, then the movement skill to stop.
- Otherwise, use the movement skill to execute the tree-avoidance mission and stop.
- Prefer fire when both hazards are present. This resolves the overlapping reasons in the original example explicitly.

These are **instructions to the model, not deterministic C# branches**. The handoff example constrains possible transfers with graph edges but also relies on the model to interpret the reasons. Skills share one agent and conversation; there is no transfer to another agent or isolated specialist session. Random mock sensor readings and model decisions mean different runs can take different paths.

Progressive disclosure is **not authorization**: the generic script tool can address a configured skill even if the model skips `load_skill`. Loading first, stopping on hazards, and selecting fire before rain are prompt-level procedures, not runtime-enforced safety guarantees. Keep deterministic application checks and an appropriate approval policy if moving beyond this educational simulation.

## Run

Requires .NET 10 and a tool-capable OpenAI model. The sample uses Agent Framework **1.23.0**, matching the repository baseline. It does not need the Workflows package.

From the repository root:

```powershell
dotnet user-secrets set "OpenAI:ModelId" "<your-model>" --project .\16.AISkills\AgentWithAISkills
dotnet user-secrets set "OpenAI:ApiKey" "<your-api-key>" --project .\16.AISkills\AgentWithAISkills
dotnet run --project .\16.AISkills\AgentWithAISkills
```

The project deliberately shares the book's existing `UserSecretsId`; if those values are already configured, only the run command is needed. **No application command-line switches are required.** Run incurs model API usage; the shared tools only print mock operations and return simulated values.

The console prints sensor calls, skill loading, resource reads, script calls and a final summary. An illustrative fire-path excerpt is:

```text
[TOOL] load_skill {"skillName":"fire-response"}
[TOOL] run_skill_script {"skillName":"fire-response","scriptName":"SoundAlarm"}
[RESULT] Fire alarm sounded.
...
[TOOL] load_skill {"skillName":"movement"}
[TOOL] read_skill_resource {"skillName":"movement","resourceName":"movement-rules"}
[TOOL] run_skill_script {"skillName":"movement","scriptName":"Stop"}
[RESULT] stopped.
ASSISTANT: ...
```

The excerpt is illustrative, not a recorded model response. Argument formatting and call order can vary. Full loaded instructions/resources are not echoed to keep the trace readable.

> **Important:** Skill-tool approval is explicitly disabled only because these three skills are trusted, local, in-process **mock** operations. Do not carry this configuration over to real motors, external scripts, or untrusted skill sources. Loading skills does not establish a sandbox.

## Offline validation

```powershell
dotnet run --project .\16.AISkills\AgentWithAISkills.Tests
```

The xUnit tests use scripted local chat responses and the real skills provider. They verify progressive disclosure, resource reads, typed script arguments, operation bindings and error responses without contacting a model or using credentials. They do **not** establish that a real model will always obey the skill-selection or safety instructions.

## Official references

- [Agent Skills documentation](https://learn.microsoft.com/agent-framework/agents/skills?pivots=programming-language-csharp)
- [Released code-defined skills sample](https://github.com/microsoft/agent-framework/blob/dotnet-1.23.0/dotnet/samples/02-agents/AgentSkills/Agent_Step02_CodeDefinedSkills/Program.cs)
- [Released AgentInlineSkill implementation](https://github.com/microsoft/agent-framework/blob/dotnet-1.23.0/dotnet/src/Microsoft.Agents.AI/Skills/Programmatic/AgentInlineSkill.cs)
- [Released skills provider and approval options](https://github.com/microsoft/agent-framework/blob/dotnet-1.23.0/dotnet/src/Microsoft.Agents.AI/Skills/AgentSkillsProviderOptions.cs)
