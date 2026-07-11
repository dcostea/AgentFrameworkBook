# Evaluator JSONL Data

This folder contains one JSONL file per evaluator scenario. Each line is a complete test record: the test loads the line, constructs the chat history and response, runs the relevant evaluator, and compares the evaluator result with the expected outcome in the record.

## Common fields

- `id`: Stable record identifier used by xUnit `[InlineData]`.
- `source`: Metadata that records where the sample came from. Tests do not assert on it.
- `scenario`: Human-readable scenario name.
- `model`: Metadata about the model family/name associated with the sample.
- `agent`: Agent name, instructions, and available tools used to reconstruct the test input.
- `userInput`: User message sent to the evaluator as part of the chat history.
- `toolCalls`: Tool calls represented as `FunctionCallContent` when the evaluator needs tool-call context.
- `finalResponse`: Assistant response evaluated by text-based evaluators.
- `expectedBehavior`: Human-readable explanation of why the record should pass or fail.
- `shouldPass`: Required by the general evaluator tests; `true` means the evaluator should rate the response as acceptable, and `false` means it should fail or score below the pass threshold.

## Scenario-specific fields

- `groundTruth`: Used by groundedness, equivalence, completeness, and composite evaluator scenarios.
- `retrievedContextChunks`: Used by retrieval evaluator scenarios.
- `expectedChanged`, `expectedFrom`, `expectedTo`: Used only by direction-change scenarios and mapped to `DirectionChangeRecord` instead of the common `EvaluationRecord`.

## Authoring guidance

Keep shared records generic. If a field is only meaningful for one evaluator or custom test, add it to a scenario-specific record type instead of the common schema. Prefer small, realistic pass/fail pairs so readers can quickly understand what each evaluator is checking.
