# Chapter 13 — final publication review

**Reviewed:** September 29, 2026. **Result:** a corrected manuscript suitable for the chapter's educational, simulated examples—not a claim of production authorization or verified robot navigation.

## Deliverables

- [Final manuscript](ch13_costea_AF_final.docx)
- [Evidence archive](ch13_costea_AF_final_evidence.zip): raw and normalized runtime logs, source hashes, build logs, exact before/after change manifest, listing extracts, document validation, and the Word-rendered PDF preview.
- Original `ch13_costea_AF.docx` preserved byte-for-byte. No repository C# or project files were changed, and no commit was created by this review.

You confirmed the scope as the **three** projects actually present under `13.GroupChat`; no fourth project exists there. The conceptual code-backed participant in Figure 13.5 is not another runnable example.

## Verification at a glance

| Check | Result |
|---|---|
| Repository projects | 3 builds; 0 warnings, 0 errors; no restore needed |
| Live execution | 7 fresh runs; all exit 0, no timeout or workflow/executor errors |
| Meaningful completion | 6 terminal execution/decision paths; 1 basic negotiation stopped at its 10-turn ceiling without execution |
| Printed listings | All 12 matched source-derived extracts and compiled as 3 reconstructed applications; 0 warnings/errors |
| Source references | 12 code paths verified on disk; companion files and shared project references explained |
| Output blocks | 7 blocks traced to fresh logs; 1 manager-state illustration explicitly labeled as non-console context |
| Word/PDF | 29 pages; all 22 listing/figure/table captions complete on one page; no off-page text/images or replacement characters detected |
| Structure | All 15 original heading/title entries preserved, 7 figures, 3 tables, 10 summary takeaways |
| Framework baseline | MAF Doctor 1.15.0: grade B, 0 errors, 5 warnings, 78 uncapped-cost findings; 142 files scanned, not truncated |

The MAF baseline is repository-wide, not a clean bill of health for every example. Its top findings concern other chapters; this review did not make unrelated changes. Current MAF constraints were read, and MAF Doctor reported its package and workspace steering current.

**Runtime source commit:** `2269851b7cd480b39666906256344952e4c322f8`. **SDK:** .NET SDK 10.0.401. **Target:** `net10.0`. Each project uses `Microsoft.Agents.AI`, `.OpenAI`, and `.Workflows` **1.23.0**, plus `Microsoft.Extensions.Configuration.UserSecrets` **10.0.12**. Package versions were preserved, not upgraded.

## Section-by-section review

| Location | What changed and why |
|---|---|
| Opening and chapter coverage | Preserved Robby and the problem-first narrative; clarified that the loop ends on reported execution or the turn ceiling, not merely on approval. |
| 13.1 Understanding Group Chat Orchestration | Separated the host's delivery/transcript duties from manager policy. Corrected hook order: append canonical history, check termination, shape/broadcast the new batch, select, increment, schedule. Distinguished turns from rounds and participant sessions from canonical history. |
| 13.1.1 Group Chat Orchestration in Action | Removed unsupported claims that the preferred turn angles prove shortest-path or collision-free navigation. The mission lacks obstacle width and clearance information. |
| First practical example | Corrected `ApprovalState` to `ExecutionDecision`; restored exact manager code, namespaces, setup dependencies, and logging cases. Explained that `nameof` does not validate hardcoded prompt words and substring checks do not prove permission or execution. |
| First run and exercise | Replaced the previous example transcript with a fresh proposal/revision/tool trace; documented a separate 10-turn non-converging run. Removed the reference to a nonexistent hardening section and made the geometry exercise specify an end position and clearance. |
| 13.2 Group Chat with Human in the Loop | Distinguished a meaningful human decision point from merely mixing participant types. Removed universal/high-stakes overclaims and separated plan participation from tool-level authorization. |
| HITL example and HumanAgent | Restored the exact source enum references, serializer placeholder, already-executed message, and logging. Documented blocking console input and the absence of demonstrated durable serialization. The reused manager does not authenticate which agent approved. |
| HITL run and exercise | Captured actual `n` then `y` behavior: revision without movement, then execution after human approval despite advisory denial. Explained the geometry concern observed in the other HITL run. Retained the concrete asynchronous-approval exercise. |
| 13.3 Next Participant with Custom Manager | Corrected “four participants” to three agents plus a manager. Explained state-driven routing, the interpretation of CALIBRATION, hook inputs, and the separation between broadcast annotations and canonical history. |
| Custom-manager practical example | Synchronized all prompts/classes/wiring with source. Corrected listing 13.12 cross-references, separated the enum from the manager listing, and explained counting/tool-result assumptions and missing durable-state hooks. |
| Custom-manager output and exercise | Used two safe observed pairs followed by real simulated calibration and approval; used a separate actual 91°C second-reading denial. Kept unobserved numeric/failure cases as evaluation work, not implied coverage. |
| 13.4 Conclusions | Renumbered comparison tables in order; added practical choice rules and corrected the next-chapter transition: custom-manager routing is already dynamic, while handoff changes who owns routing. |
| Summary | Replaced overclaims about robust substring checks, prompt enforcement, cost bounds, and output filtering with ten accurate takeaways, including conditional selection. |

## Listing-to-source traceability

Line numbers refer to the source files at the runtime commit above. The entire executable content is source-derived. Allowed adaptations are plain console logging, removed XML documentation comments, callout comments, and the explicitly omitted repeated setup. Word soft wrapping is formatting, not edited code.

| Listing | Repository source | Source lines | Verification / correction |
|---|---|---:|---|
| 13.1 | [13.GroupChat/GroupChatOrchestration/ExecutionDecision.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestration/ExecutionDecision.cs#L1-L8) | 1–8 | Renamed stale ApprovalState references; actual enum and namespace. |
| 13.2 | [13.GroupChat/GroupChatOrchestration/ApprovedTerminationManager.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestration/ApprovedTerminationManager.cs#L1-L21) | 1–21 | Actual Contains/nameof condition; no invented author validation. |
| 13.3 | [13.GroupChat/GroupChatOrchestration/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestration/Program.cs#L14-L48) | 14–48 | Exact participant instructions; repeated usings/UserSecrets setup omitted explicitly. |
| 13.4 | [13.GroupChat/GroupChatOrchestration/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestration/Program.cs#L50-L102) | 50–102 | Restored actual workflow export and event switch; removed extra AgentResponseEvent case; plain Console. |
| 13.5 | [13.GroupChat/GroupChatOrchestrationWithHitl/HumanAgent.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithHitl/HumanAgent.cs#L1-L80) | 1–80 | Exact HumanAgent, including default JsonElement and HUMAN APPROVED shortcut. |
| 13.6 | [13.GroupChat/GroupChatOrchestrationWithHitl/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithHitl/Program.cs#L15-L52) | 15–52 | Exact advisory Navigator and human-authoritative Motors prompts. |
| 13.7 | [13.GroupChat/GroupChatOrchestrationWithHitl/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithHitl/Program.cs#L54-L97) | 54–97 | Actual event switch; no extra full-response printing; default host transcript retained. |
| 13.8 | [13.GroupChat/GroupChatOrchestrationWithCustomManager/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithCustomManager/Program.cs#L1-L43) | 1–43 | Complete provider setup and existing shared sensor/calibration tools. |
| 13.9 | [13.GroupChat/GroupChatOrchestrationWithCustomManager/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithCustomManager/Program.cs#L45-L76) | 45–76 | All ordered safety/evidence rules retained verbatim. |
| 13.10 | [13.GroupChat/GroupChatOrchestrationWithCustomManager/SafetyDecision.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithCustomManager/SafetyDecision.cs#L1-L8) | 1–8 | Correct standalone SafetyDecision enum; removed unrelated usings. |
| 13.11 | [13.GroupChat/GroupChatOrchestrationWithCustomManager/InvestigationManager.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithCustomManager/InvestigationManager.cs#L1-L66) | 1–66 | Exact manager state, hook implementations, and leading-token checks. |
| 13.12 | [13.GroupChat/GroupChatOrchestrationWithCustomManager/Program.cs](https://github.com/dcostea/AgentFrameworkBook/blob/2269851b7cd480b39666906256344952e4c322f8/13.GroupChat/GroupChatOrchestrationWithCustomManager/Program.cs#L78-L115) | 78–115 | Exact workflow and streamed responses; removed remaining ColorHelper calls. |

**Assembly of the listings:** 13.3 + 13.4 form the basic `Program.cs`; 13.6 + 13.7 form the HITL `Program.cs`; 13.8 + 13.9 + 13.12 form the custom-manager `Program.cs`. The companion enum/manager/HumanAgent files are included. Only the explicitly omitted original using directives and UserSecrets setup were restored for the first two builds. The shared AITools/Helpers project references were retained. Compilation was performed on code extracted from the generated DOCX—not just on the original repository.

The first builder-shape snippet is explicitly schematic, not an additional runnable listing. The `InvestigationManager state` block is an illustrative broadcast message, not an observed console log.

## Figure and table review

| Item | Disposition |
|---|---|
| Figure 13.1 | Fixed “selects and run” to “and runs”; caption/prose distinguish aggregate coordination from actual host/manager responsibilities. |
| Figure 13.2 | Retained diagram; clarified per-participant accumulated context, scheduled-turn counting, and default transcript output. |
| Figure 13.3 | Replaced misleading approval-only termination labels with execution-after-approval or turn-limit labels; caption explicitly identifies simulated actions. |
| Figure 13.4 | Retained simplified inheritance diagram; described CustomAgent as an illustrative branch and noted omitted serialization members. |
| Figure 13.5 | Retained participant-types diagram; explained that the conventional-code branch is conceptual and not a fourth project. |
| Figure 13.6 | Fixed “3nd turn” to “3rd turn” and approval-only termination labels; caption distinguishes intended human authority from code enforcement. |
| Figure 13.7 | Replaced the maintenance move-proposal label with calibration work; added the not-calibrated condition and calibrated-approval condition; corrected the omitted-turn-limit note. Prose also identifies the post-calibration nonterminal fallback omitted by the simplified diagram. |
| Tables | Renumbered in appearance order: 13.1 participant strategies, 13.2 human participant/tool approval, 13.3 orchestration comparison; updated every cross-reference. |

All seven original embedded images were byte-identical to the earlier extracted figures. Therefore the existing coordinate-based corrections were reused only after verifying those identities. Both PNG and SVG representations were updated where present so Word cannot display a stale vector alternative. No diagram was replaced with a different visual design.

## Fresh execution results

| Run | Seconds | Observed outcome | Assessment |
|---|---:|---|---|
| `normal-01` | 22.401 | Five proposals, five denials, ten turns, no tool calls or EXECUTED | Bounded non-convergence; process success is not mission success. |
| `normal-02` | 25.765 | DENIED → revised 45° plan → APPROVED → seven matching tool calls → EXECUTED | Intended ordering observed; no default workflow-output line, as expected. |
| `hitl-y-01` | 30.711 | Navigator denied; human approved; eight tool calls and EXECUTED followed | Human authority ordering observed; geometric safety not established. |
| `hitl-n-y-02` | 29.882 | Human n → revision without movement → human y → eight tool calls → EXECUTED | Intended denial/approval path observed; default transcript output emitted. |
| `custom-01` | 18.453 | (31°C, 63%), (−7°C, 57%) → calibration → APPROVED | Pairs match tool readings; both under lower denial limits; calibration precedes approval. |
| `custom-02` | 14.966 | (46°C, 12%), (34°C, 35%) → calibration → APPROVED | Same intended path with different sampled readings. |
| `custom-03` | 12.037 | (51°C, 49%), (91°C, 39%) → DENIED | Actual 91°C exceeds 90°C; no unnecessary calibration after denial. |

The two basic runs and the two HITL runs are not deterministic route-planning tests. For example, the approved basic revision contains 78 meters of forward motion versus 70 in its original proposal; a blanket claim that the model proved a shorter path would be false. The y-first HITL rectangle returns to the nominal tree position under a simple planar interpretation. These concerns are now visible in the prose rather than being hidden behind successful EXECUTED messages.

### Output provenance

| Printed block | Log in evidence archive |
|---|---|
| After listing 13.4 | `runtime/normal-02/stdout.normalized.log` |
| After listing 13.5 | `runtime/hitl-n-y-02/stdout.normalized.log`, first human prompt/denial |
| After listing 13.7 | `runtime/hitl-n-y-02/stdout.normalized.log` |
| After listings 13.8 and 13.9 | `runtime/custom-01/stdout.normalized.log`, first observation and review |
| After listing 13.12, approval | `runtime/custom-01/stdout.normalized.log` |
| After listing 13.12, denial | `runtime/custom-03/stdout.normalized.log` |

ANSI color escapes, timestamps, and repetitive executor notifications were removed for presentation. Display line breaks were inserted where streaming joined messages; model words, values, decisions, and tool results were not rewritten. Two omitted HITL plan lists are marked with explicit ellipses. Piped human input is described in the lead-in, not fabricated as echoed console output. Each printed line was verified as an ordered substring of its source log after these declared transformations.

## Limitations deliberately documented, not silently “fixed”

1. **Approval is prompt-level cooperation here.** The tools remain exposed; neither the text-based termination manager nor a successful test run enforces authorization at the tool boundary.
2. **Substring matching is not proof.** The first manager does not authenticate authors, bind approval to a proposal, or validate tool execution.
3. **Navigation is simulated.** MotorTools logs commands and delays. It does not validate obstacle geometry or actuate a real car.
4. **Numeric review remains model behavior.** InvestigationManager counts continuing environment turns, not parsed sensor pairs; it does not recalculate the safety decision or explicitly require two observations in its approval branch.
5. **Calibration evidence is sample-specific.** A non-null, exception-free function result is meaningful because MaintenanceAgent has only the one calibration tool. Multiple tools or non-exception failure payloads need a more precise check.
6. **Persistence is not demonstrated.** HumanAgent returns a default JsonElement; custom manager fields lack custom checkpoint serialization. Blocking Console.ReadLine also has no wall-clock deadline.
7. **Coverage is bounded.** No fresh run exercised humidity >95, lower-limit-only violations (60/80), first-reading extreme rejection, equality at 60/80/90/95, an earlier violation masked by a later safe reading, or tool failure. These are explicitly identified as evaluation cases.
8. **Output designation is not summary generation.** Absence of the default WorkflowOutputEvent with WithOutputFrom was expected, not treated as a bug, and no source change was made to force it.

## Exact change trace and preservation

The evidence archive contains **163 individually identified change entries**, `C001` onward, in `change-manifest.json`. Each contains the original zero-based paragraph index or range and exact before/after text; listing entries also record source lines and hashes. Formatting changes are listed separately. This is a clean final manuscript, not a Word Track Changes document.

- Original DOCX SHA-256: `37ec8ec10c277dc583e74ce2461597a860025530ca3bbafe67b32c04d7752a90`
- Final DOCX SHA-256: `480af620eb9e35e75048fd5d55f4cb3eb154875f33c5666dc21bef0bb5ba04f6`
- All Chapter 13 source hashes were rechecked after reconstruction and rendering. The runtime agent also verified 25 chapter/shared source and project files unchanged.
- The original normal-project workflow.md was restored byte-for-byte. Generated HITL/custom workflow.md files were removed because they were originally absent; generated copies remain in the evidence archive.
- Build products and temporary validation projects remain outside the repository. Only the final DOCX, this report, and the evidence ZIP are new deliverables.

### How to inspect the evidence

Extract the ZIP. Start with `runtime/runtime-review.txt` and `runtime/run-summary.json` for execution details; `change-manifest.json` for exact edits; `document-validation.json`, `listing-builds.json`, and `layout-validation.json` for verification. `final-preview.pdf` is the Microsoft Word rendering used to check pagination and captions. `evidence-files.json` lists included files and their SHA-256 hashes.

## API references used

- [Official Agent Framework group-chat guide](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/group-chat?pivots=programming-language-csharp)
- [UpdateHistoryAsync contract and broadcast semantics](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.workflows.groupchatmanager.updatehistoryasync?view=agent-framework-dotnet-latest)
- The resolved **1.23.0** assemblies were inspected for GroupChatManager, RoundRobinGroupChatManager, GroupChatWorkflowBuilder, GroupChatHost, and AIAgentHostExecutor; these exact-version behaviors take precedence over older example snippets surfaced by documentation search.

No package upgrade, infrastructure deployment, external publication, or production-safety certification was performed.
