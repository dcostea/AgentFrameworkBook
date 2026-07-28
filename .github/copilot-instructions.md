# Copilot Instructions

## Project Guidelines
- User prefers using C# `required` properties for required data fields instead of helper validation constructs in sample/book code.
- User wants generic data structures for book samples; specific use-case logic should stay in Facts/Theories/tests, not in shared data models or JSONL schemas.
- User prefers separate JSONL model classes for standard evaluations and custom evaluations, without inheritance between those models.
- User prefers book/sample tests to minimize private helper methods and keep behavior visible in xUnit Facts/Theories, using InlineData where practical.

## Memory
- When asked to generate an article, save the completed content to the exact Markdown file provided by the user and verify the file was written successfully before reporting completion.