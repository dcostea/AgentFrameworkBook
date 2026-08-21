---
name: agent-framework-book-author
description: Write new chapters for "Microsoft Agent Framework in .NET" by Daniel Costea so they match the voice, structure, and pedagogy of Chapters 1-11. Encodes the book's narrative universe (Robby the robot car), chapter skeleton, prose style, terminology, code conventions, and pedagogical progression pattern extracted directly from the manuscript. Use when drafting, editing, or reviewing Chapters 12-16 (multi-agent orchestration) or any future chapter of this book.
---

# Agent Framework Book — Author Consistency Skill

**Purpose:** Write new chapters (12–16) for *Microsoft Agent Framework in .NET* by Daniel Costea, so they are indistinguishable in voice, structure, and pedagogy from Chapters 1–11. This is not a generic "technical writing" skill — it encodes Daniel's specific authorial fingerprint extracted directly from the manuscript.

---

## 1. Author Identity and Narrative Contract

- Author persona: .NET software engineer, AI specialist, Microsoft MVP for AI and .NET. Voice is that of a senior peer explaining hard-won lessons, not an academic or a marketer.
- The book has ONE continuous fictional narrative thread: **Robby, the fire-detecting robot car**, born from the author's real low-budget (<$100) IoT/Raspberry Pi project. Every new chapter must extend Robby's story — do not introduce a disconnected example universe (no generic "customer support bot" or "todo app") unless Robby's domain genuinely cannot illustrate the concept.
- Robby has named specialized agents introduced organically as complexity grows: `MotorsAgent` (movement planning), `AuditorAgent` (safety/audit trail). New chapters on orchestration should introduce further named, single-responsibility agents (e.g., a `RouterAgent`, `PlannerAgent`, `ReviewerAgent`) rather than anonymous "Agent A / Agent B" — always give them domain-flavored names tied to Robby's world (motors, maintenance, navigation, diagnostics, safety).
- The author explicitly narrates his OWN past failures before introducing the framework's solution ("I lacked an elegant solution... had to abandon my efforts"). This confessional, problem-first structure is a signature device — never open a new concept by just stating the solution.
- First-person singular ("I") is used for anecdotes and design decisions; first-person plural ("we") is used for the pedagogical walkthrough ("Let's see...", "We'll explore..."). Never use "you" as the primary narrator voice except in direct instructions ("Open the Program.cs file...") or NOTE/TIP/IMPORTANT boxes.

## 2. Chapter Skeleton (mandatory structure)

Every chapter follows this exact skeleton. Do not skip or reorder sections.

1. **Chapter number + title** — short, verb-first or gerund-first, plain language (e.g., "Building your first agent step by step", "Standardizing tools using MCP (Model Context Protocol)"). Avoid buzzword titles.
2. **"This chapter covers" box** — 3-5 bullet points, each a short noun phrase or gerund phrase, in the exact order topics appear in the chapter. This is the chapter's table of contents in miniature.
3. **Opening narrative paragraph(s)** — 1-3 short paragraphs connecting the chapter to Robby's ongoing story or to a production pain point ("Robby worked well in development... In production, the same agent became dangerous"). Establish the problem before the solution.
4. **Numbered sections (X.1, X.2...)** and **sub-numbered subsections (X.1.1, X.1.2...)** using Title Case for section headers and Title Case ALL-CAPS-STYLE for some sub-sub-headers when marking a distinct concern (e.g., "ENVIRONMENT VARIABLES", "PRACTICAL EXAMPLE", "EXERCISE"). Use this all-caps convention specifically for: `PRACTICAL EXAMPLE`, `EXERCISE`, named technical sub-topics inside a subsection (e.g., "THE NON-DETERMINISTIC NATURE OF LLMS", "SOLVING PRODUCTION PROBLEMS WITH MIDDLEWARE").
5. **Diagrams** — every architecturally significant concept gets a labeled figure (numbered `Figure X.Y`) with a one-sentence caption stating what the figure shows and why it matters. Diagrams use simple box-and-arrow or pipeline shapes, never decorative. Caption format: "Figure X.Y [Diagram Name] [shows/depicts what]." Always followed immediately by 1-2 paragraphs walking through the diagram step by step, numbered or bulleted to match diagram callouts.
6. **Code listings** — every non-trivial concept gets a numbered `Listing X.Y` with a descriptive title, preceded by:
   - "Requirements (NuGet packages):" block listing exact `dotnet add package` commands.
   - "Code path:" line pointing to the GitHub repo folder/file (e.g., `Code path: /AgentWithStreaming/Program.cs`).
   - A short step-by-step "Step-by-step explanation" numbered list BEFORE the listing when the code introduces several new concepts at once (see Ch.2 pattern), OR go straight to the listing for simpler, incremental code changes.
7. **Callout numbering in code** — use circled/numbered markers (❶❷❸...) inline in code comments, with a matching bulleted legend immediately after the code block explaining each marker in one line. Never leave a numbered marker unexplained.
8. **"Code output:" block** — nearly every listing is followed by an actual (or realistic simulated) console output block, in a monospace block, showing what running the code produces. This is a non-negotiable convention — readers must see proof the code works.
9. **Explanatory paragraph after each listing** — 1-2 paragraphs explaining WHY the code works, not just what it does. Always tie back to a production concern (reliability, cost, determinism, security) when relevant.
10. **EXERCISE** box — nearly every practical example ends with an "EXERCISE" heading containing 1-3 sentences describing a specific, concrete extension task for the reader (not open-ended "explore more"). Sometimes includes a "Bonus:" sub-task that raises the difficulty.
11. **NOTE / IMPORTANT / TIP boxes** — horizontal-rule-bounded call-out boxes used for:
    - `NOTE`: clarifying terminology, scope limitations, or an aside that doesn't interrupt flow.
    - `IMPORTANT`: safety, security, or "don't do this in production" warnings.
    - `TIP`: a practical shortcut or best practice.
    Keep these to 1-3 sentences. Bold the label word only.
12. **Comparison tables** — used whenever two or more approaches, technologies, or trade-offs are being weighed (e.g., "MCP AI Tools vs. AI Tools", "Table 2.1 Troubleshooting the Application"). Format: `Aspect | Option A | Option B` rows, terse phrases not full sentences in cells.
13. **"Choose X when:" / "Otherwise, start with Y" decision bullets** — after a comparison table, give the reader an explicit decision rule as a short bullet list. Never leave the reader without a concrete recommendation.
14. **Analogy paragraph** — at least one central analogy per major new concept, stated explicitly and then mapped point-by-point (e.g., "kitchen for a chef" → orchestration=recipes, standards=appliance hookups, governance=food inspectors, observability=cameras; "car safety systems" → seatbelts=validation, airbags=error handling, speed limiters=rate limiting, dashcam=audit logging, collision warning=human approval). Analogies must be mapped as a bulleted list, each bullet in the form "[Real-world thing] ([technical concept]) [does what]."
15. **Chapter-ending "Summary" section** — italicized/styled header "Summary", followed by 6-10 bullet points, each a single dense sentence summarizing one major takeaway, written as a statement of fact the reader now knows (not "we learned that..."). Mirrors and expands the "This chapter covers" opening bullets.

## 3. Prose Style Rules

- Sentence length: mostly medium (15-25 words), varied with occasional short punchy sentences for emphasis ("Let's run it again!", "Almost there!", "This is a standard flow.").
- Contractions are allowed and used naturally ("doesn't", "we'll", "isn't", "let's").
- Rhetorical direct address to build momentum: "Let's...", "Ready to...?", "Are you getting a response like this?", "Try running it a few times to see how similar they are."
- Avoid marketing superlatives ("revolutionary", "game-changing", "cutting-edge"). Confidence is conveyed through precision and working code, not adjectives.
- Technical terms are always introduced with an inline informal definition before or immediately after first use, often in parentheses: "middleware, the layer that sits between our chat calls and the LLM to enforce guardrails, is not optional."
- When introducing an official/precise definition, prefer a **NOTE** box over inline prose if the definition is more than one sentence.
- Use second person imperative only for literal reader actions ("Open the Program.cs file...", "Run the following command..."), never for conceptual explanation.
- Em dashes and parenthetical asides are used moderately to add color without breaking flow ("Robby's AI agent worked flawlessly in development. Then production happened, and that's when we learned that middleware... is not optional. It is essential.").
- Avoid hedging language ("might", "could potentially", "in some cases may"). State things directly, then qualify with a NOTE/IMPORTANT box if a caveat matters.
- British vs. American spelling: use American English consistently (matches manuscript: "organize", "behavior", "color").

## 4. Terminology and Naming Conventions (STRICT — consistency-critical)

- After first full mention with a `NOTE`, use **"Agent Framework"** (never "the Agent Framework", never "Microsoft Agent Framework" again except in formal/legal contexts). Abbreviation "AF" may be used sparingly in prose once established, but spelled out in headers.
- Never mention **Semantic Kernel** or **AutoGen** as usable/current tools — only as historical lineage ("Agent Framework combines strengths from both, so you do not have to choose"). Do not suggest code patterns, APIs, or idioms from either.
- Refer to **Microsoft.Extensions.AI** by its short form **MEAI** after first introduction, consistent with Ch.5 usage ("This low-level approach, inherited from MEAI").
- Class/type names always in code font: `AIAgent`, `ChatClientAgent`, `AgentSession`, `ChatClientAgentSession`, `AgentResponse`, `ChatOptions`, `ChatMessage`, `McpClient`, `McpServerTool`.
- The robot car's basic move vocabulary is fixed: `forward`, `backward`, `turn left`, `turn right`, `stop`. Do not invent new base moves without narrative justification; extended movement (angles, distances) is expressed as parameters, not new verbs.
- Robby is always referred to as "Robby" in narrative prose, "the robot car" in technical/descriptive prose, and "RobotCarAgent" (or similarly suffixed `*Agent`) in code.
- Use **PACT (Persona-Action-Context-Template)** as the canonical prompt-engineering framework taught in the book — this is the author's own coined framework and must remain the primary teaching lens for any new prompt-engineering content in later chapters (do not introduce a competing framework without explicitly relating it back to PACT).
- File paths in "Code path:" lines follow `/{ExampleProjectName}/Program.cs` or `/{ExampleProjectName}/{ClassName}.cs` convention, PascalCase, matching the public GitHub repo (`github.com/dcostea/AgentFrameworkBook`) structure.

## 5. Code Conventions

- Target the **latest stable Agent Framework release** available at time of writing; verify against `github.com/microsoft/agent-framework` and `learn.microsoft.com/en-us/agent-framework` before finalizing any API shown (per project instructions — never use Semantic Kernel/AutoGen-era APIs).
- C# style: top-level statements (no `Main` method wrapper shown), `var` for local variables, raw string literals (`"""..."""`) for prompts and instructions, target-typed `new()` where natural, nullable reference types respected (`string?`).
- Secrets handling: **always** use `Microsoft.Extensions.Configuration.UserSecrets` + `ConfigurationBuilder` in examples; production alternatives (Azure Key Vault, AWS Secrets Manager, env vars) are mentioned in prose/boxes but never used as the primary example pattern, to keep examples runnable by readers immediately.
- Every listing must be a **complete, runnable increment** — comment out or omit only genuinely repetitive boilerplate with `// 'usings' and API key fetching omitted for brevity`, never omit the actual new concept being taught.
- Console output style: `Console.WriteLine($"USER: {prompt}")` / `Console.WriteLine($"ASSISTANT: {response.Text}")` labeling convention for conversational demos; plain `Console.WriteLine(response.Text)` for single-shot demos.
- JSON is the default structured-output format taught; always show both the "loose" prompt-engineered JSON approach AND the type-safe `ResponseFormat`/`ChatResponseFormat.ForJsonSchema<T>()` approach when the topic allows, in that pedagogical order (loose first, strict second) — mirrors the progression in Ch.2 → Ch.5.
- Exceptions/errors are documented via a **troubleshooting table** (`ClientResultException` style: error code → description) rather than prose, whenever a chapter introduces a new failure-prone integration point (API keys, transports, tool calls).

## 6. Pedagogical Progression Pattern (applies within every chapter and across the book)

Follow this repeatable teaching arc for each new concept:

1. State the limitation of the current/previous approach (motivate why something new is needed).
2. Introduce the new concept in plain language with an analogy.
3. Show a diagram of the concept's architecture/flow.
4. Walk through a minimal/naive code example.
5. Run it, show output, highlight the shortcoming or non-determinism if relevant.
6. Refine the example incrementally (do not jump straight to the polished final version — show 2-3 iterations when teaching prompt/config refinement, matching the PACT JSON-refinement sequence in Ch.2).
7. Present the final, production-appropriate listing.
8. Explain trade-offs with a comparison table if alternatives exist.
9. Give a decision rule ("Choose X when... / Otherwise start with Y").
10. Close with an EXERCISE.

## 7. Voice Consistency Checklist (apply before finalizing any new chapter)

- [ ] Opens with a narrative hook tied to Robby or a stated production pain point — not a dry definition.
- [ ] "This chapter covers" bullets present and match final section order.
- [ ] At least one original analogy, explicitly mapped point-by-point.
- [ ] All new types/APIs verified against the current Agent Framework GitHub repo/docs (no stale or SK/AutoGen-era syntax).
- [ ] Every code listing has: NuGet requirements, code path, numbered callouts with legend, code output block.
- [ ] Every practical example ends in an EXERCISE (with Bonus where appropriate).
- [ ] At least one NOTE, IMPORTANT, or TIP box used correctly per major section.
- [ ] Any comparative technology discussion ends in an explicit table + decision-rule bullets.
- [ ] Terminology matches Section 4 exactly (Agent Framework, MEAI, PACT, Robby's move vocabulary).
- [ ] Chapter closes with a "Summary" bullet list, 6-10 items, dense single-sentence takeaways.
- [ ] No marketing adjectives; confidence conveyed via working code and directness.
- [ ] American English spelling throughout.

## 8. Chapter-Specific Notes for Remaining Chapters (12–16)

Given the established narrative arc, apply these continuity constraints:

- **Ch.12 (Sequential/Concurrent patterns):** Should open by referencing the existing `MotorsAgent`/`AuditorAgent` pair from Ch.5 §5.3.4, since that section explicitly foreshadows: "In the multi-Agents chapters, we will move to a more capable approach, where orchestration is not limited to hard-coded agent calls." New chapter MUST deliver on this exact promise — do not introduce unrelated agents as the primary example.
- **Ch.13 (Group chat):** Should extend the audit/safety theme — group chat is a natural fit for multiple Robby specialists debating a risky maneuver (e.g., MotorsAgent proposes a path, AuditorAgent objects, a third agent breaks the tie).
- **Ch.14 (Handoff):** Frame around routing robot car requests to specialist agents (e.g., maintenance vs. navigation vs. emergency-stop agent) — consistent with the MaintenanceTools/MotorTools split already seeded in Ch.8.
- **Ch.15 (AI skills / dynamic orchestration):** This chapter's very name overlaps with "skills" as a concept — be careful to define Agent Framework's notion of "AI skills" (if that is the official current terminology; verify against the latest docs, as framework terminology may have evolved) distinctly from generic "capabilities," and tie back to the MCP standardization work from Ch.8.
- **Ch.16 (Custom orchestration/workflows):** Should reuse the checkpointing/durability concepts already flagged in Ch.1's Enterprise-Ready section ("Checkpointing is like creating saving points in a video game") — bring that analogy back explicitly rather than inventing a new one.
- All five chapters are PART 4: MULTI-AGENTS — introduce this part with a short part-opening framing (not seen needed for earlier parts individually, but check if Parts 1-3 have part-intro pages; if so, match that format for Part 4).

## 9. What NOT to Do

- Do not introduce a new fictional example universe unrelated to Robby without strong justification.
- Do not use Semantic Kernel or AutoGen code, terminology, or idioms as if current.
- Do not skip the EXERCISE, Code output, or Summary sections for the sake of brevity — these are structural signatures of the book, not filler.
- Do not use hedgy, uncertain prose ("this might work", "in theory this should"). If genuinely uncertain about a bleeding-edge API, say so explicitly in an IMPORTANT box rather than hedging in prose.
- Do not let chapters exceed the established pacing — each chapter in the manuscript covers 1 cohesive topic area across roughly 15-25 pages with 5-10 numbered listings; do not compress multiple orchestration patterns into a single oversized chapter beyond what's already planned per the table of contents.
