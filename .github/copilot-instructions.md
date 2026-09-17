# Copilot Instructions

## Project Guidelines
- User prefers using C# `required` properties for required data fields instead of helper validation constructs in sample/book code.
- User wants generic data structures for book samples; specific use-case logic should stay in Facts/Theories/tests, not in shared data models or JSONL schemas.
- User prefers separate JSONL model classes for standard evaluations and custom evaluations, without inheritance between those models.
- User prefers book/sample tests to minimize private helper methods and keep behavior visible in xUnit Facts/Theories, using InlineData where practical.
- For Agent Framework book samples, keep implementations intentionally simple and educational rather than production-oriented; reuse existing shared AITools such as SensorTools and MaintenanceTools instead of creating custom simulations or improvised equivalents.
- In this repository, prefer enums with nameof(...) over duplicated string literals for agent workflow states and routing keywords.

## Memory
- When asked to generate an article, save the completed content to the exact Markdown file provided by the user and verify the file was written successfully before reporting completion.
- For educational agent samples in this repository, keep agent instructions readable with hardcoded output keywords; use enums in C# manager logic, but do not interpolate nameof(...) into prompts.
- For educational samples in this repository, prefer readable hardcoded agent names in routing comparisons over indirect name access when the names are fixed.