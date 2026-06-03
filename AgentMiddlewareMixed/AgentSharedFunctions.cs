using Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

namespace Middleware;

/// <summary>
/// Agent SharedFunction Middleware - Pre/Post Processing Pattern
/// 
/// Pattern: Use(sharedFunc: delegate)
/// Signature: Task(messages, session, options, next, cancellationToken)
/// 
/// This middleware type executes BEFORE and AFTER the agent runs.
/// Perfect for: validation, transformation, cleanup, rate limiting.
/// 
/// Key Feature: Has access to AgentSession (unlike ChatClient SharedFunction)
/// </summary>
public static class AgentSharedFunctions
{
  private const string EmailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
  private const string EmailMask = "[REDACTED-EMAIL]";
  /// <summary>
  /// Removes email addresses from user messages before the agent processes them.
  /// Demonstrates: Message transformation with session context (per-agent sanitization).
  /// Unlike ChatClient.RemoveEmail, this applies only to this specific agent instance.
  /// 
  /// Sanitization is PERSISTENT: the sanitized messages flow into the agent and get
  /// stored in the session chat history. Subsequent LLM round-trips will see
  /// [REDACTED-EMAIL] in the history, not the original email address.
  /// 
  /// Story 2: The GDPR Nightmare - prevents email addresses from reaching the LLM.
  /// </summary>
  public static async Task PersistentRemoveEmail(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    Func<IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Email] PRE: Scanning {messages.Count()} messages for email...", ConsoleColor.Yellow);

    bool emailFound = false;
    List<ChatMessage> sanitizedMessages = [];

    foreach (ChatMessage message in messages)
    {
      if (message.Role == ChatRole.User && message.Text is not null)
      {
        string sanitized = Regex.Replace(message.Text, EmailPattern, EmailMask);
        if (sanitized != message.Text) emailFound = true;
        sanitizedMessages.Add(new ChatMessage(message.Role, sanitized));
      }
      else
      {
        sanitizedMessages.Add(message);
      }
    }

    ColorHelper.PrintColoredLine(
      emailFound
        ? "[Agent] [SharedFunction] [Email] Email detected and removed!"
        : "[Agent] [SharedFunction] [Email] No email found",
      emailFound
        ? ConsoleColor.Yellow
        : ConsoleColor.Yellow);

    await next(sanitizedMessages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Email] POST: Completed", ConsoleColor.Yellow);
  }

  private const string AuthorisedOperatorRole = "driver";

  /// <summary>
  /// Guards command execution
  /// - <c>OperatorName</c>: must equal <c>"driver"</c> (case-insensitive); any other value throws
  ///   <see cref="OperationDeniedException"/> before <c>next</c> is called — the agent never runs.
  /// - <c>MissionTag</c>: prefixed onto every user message so the LLM is always anchored to the
  ///   active mission context, even in long multi-turn conversations.
  ///
  /// Demonstrates two distinct StateBag patterns in one middleware:
  /// a hard PRE gate (operator check) and a message transformation (mission prefix).
  /// Both are impossible at the ChatClient layer — there is no session there.
  /// </summary>
  public static async Task AgentGuardrails(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    Func<IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    // OperatorName: authorisation gate — only authorized may proceed.
    string? operatorName = null;
    session?.StateBag.TryGetValue("OperatorName", out operatorName);

    // MissionTag: prefixed onto user messages to anchor the LLM to the active mission.
    string? missionTag = null;
    session?.StateBag.TryGetValue("MissionTag", out missionTag);
    
    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Guardrails] PRE: operator='{operatorName ?? "none"}', mission='{missionTag ?? "none"}'", ConsoleColor.Yellow);

    if (!string.Equals(operatorName, AuthorisedOperatorRole))
    {
      throw new OperationDeniedException(operatorName ?? "none");
    }

    ChatMessage[] enrichedMessages = [.. messages];

    // Prefix only the last message (always the new user command) with the mission tag.
    if (missionTag is not null)
      enrichedMessages[^1] = new ChatMessage(ChatRole.User, $"[{missionTag}] {enrichedMessages[^1].Text ?? string.Empty}");

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Guardrails] Mission tag: '{missionTag ?? "none"}'", ConsoleColor.Yellow);

    await next(enrichedMessages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Guardrails] POST: Completed for operator='{operatorName}', mission='{missionTag ?? "none"}'", ConsoleColor.Yellow);
  }
}
