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
        ? ConsoleColor.DarkYellow
        : ConsoleColor.Yellow);

    await next(sanitizedMessages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Email] POST: Completed", ConsoleColor.Yellow);
  }

  /// <summary>
  /// Limits the number of agent runs per session using a session-scoped counter.
  /// Evolved from <c>ChatClientSharedFunctions.LimitRequests</c>: that version uses a
  /// global static counter (app-instance-wide, round-trip-centric). This version
  /// stores the counter in <c>session.StateBag</c>, making it per-session and user-centric.
  ///
  /// Why Agent layer: ChatClient cannot access session state; its limit is shared
  /// across every user and every session in the process. Here each session enforces
  /// its own budget, so one noisy user cannot starve others.
  ///
  /// Story 1 (evolved): The $10,000 Weekend — now with per-user fairness.
  /// </summary>
  public static async Task SessionScopedLimitRequests(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    Func<IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    const int MaxRunsPerSession = 5; // low for demo — real apps would configure per tenant
    const string CounterKey = "RoundTripCount";

    int currentCount = 0;
    if (session?.StateBag.TryGetValue<object>(CounterKey, out object? value) == true && value is int count)
    {
      currentCount = count;
    }
    currentCount++;
    session?.StateBag.SetValue<object>(CounterKey, currentCount);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Limit] PRE: Session run {currentCount} of {MaxRunsPerSession} max", ConsoleColor.Yellow);

    if (currentCount > MaxRunsPerSession)
    {
      throw new SessionLimitExceededException(currentCount, MaxRunsPerSession);
    }

    await next(messages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Limit] POST: Session run {currentCount} completed", ConsoleColor.Yellow);
  }

  /// <summary>
  /// Injects guardrail system messages based on tenant identity and region stored in
  /// <c>session.StateBag</c>. For EU tenants the middleware prepends strict data-handling
  /// instructions; for internal tenants it permits richer logging.
  ///
  /// New: impossible at ChatClient because there is no AgentSession, no tenant, and
  /// no region identity. Only the Agent layer can read per-session context and inject
  /// policy-aware system messages before the LLM sees the conversation.
  /// </summary>
  public static async Task TenantGuardrails(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    Func<IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    string? tenantId = null;
    string? region = null;

    session?.StateBag.TryGetValue<string>("TenantId", out tenantId);
    session?.StateBag.TryGetValue<string>("Region", out region);

    tenantId ??= "unknown";
    region ??= "unknown";

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Tenant] PRE: Tenant='{tenantId}', Region='{region}'", ConsoleColor.Yellow);

    List<ChatMessage> enrichedMessages = [];

    if (region.Equals("EU", StringComparison.OrdinalIgnoreCase))
    {
      enrichedMessages.Add(new ChatMessage(ChatRole.System,
        """
        GUARDRAIL (EU tenant): You must NOT include personal data in your responses.
        Do not reference names, addresses, or identifiers. Comply with GDPR at all times.
        If the user shares personal data, acknowledge it without repeating it.
        """));
      ColorHelper.PrintColoredLine("[Agent] [SharedFunction] [Tenant] Injected EU strict data-handling guardrail", ConsoleColor.DarkYellow);
    }
    else if (tenantId.Equals("internal", StringComparison.OrdinalIgnoreCase))
    {
      enrichedMessages.Add(new ChatMessage(ChatRole.System,
        """
        GUARDRAIL (internal tenant): Verbose diagnostic output is permitted.
        Include tool call details and intermediate reasoning when helpful.
        """));
      ColorHelper.PrintColoredLine("[Agent] [SharedFunction] [Tenant] Injected internal verbose-logging guardrail", ConsoleColor.DarkYellow);
    }

    enrichedMessages.AddRange(messages);

    await next(enrichedMessages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Tenant] POST: Completed for tenant '{tenantId}'", ConsoleColor.Yellow);
  }

  /// <summary>
  /// Turns feature flags into session-local switches that tools and downstream logic
  /// can read. Uses session state keys <c>AgentName</c> and <c>Environment</c> to
  /// resolve flags and stores the result in <c>session.StateBag["FeatureFlags"]</c>.
  ///
  /// New: flags are per-agent and per-session, not a global process-wide toggle.
  /// ChatClient has no session, so it cannot vary features by user or environment.
  ///
  /// Why Agent layer: each agent instance may run in a different environment
  /// ("production", "staging") and need different behaviors per session.
  /// </summary>
  public static async Task FeatureFlagBootstrap(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    Func<IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken, Task> next,
    CancellationToken cancellationToken)
  {
    string? agentName = null;
    string? environment = null;

    session?.StateBag.TryGetValue("AgentName", out agentName);
    session?.StateBag.TryGetValue("Environment", out environment);

    agentName ??= "default";
    environment ??= "production";

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Flags] PRE: Resolving flags for agent='{agentName}', env='{environment}'", ConsoleColor.Yellow);

    // Simulated feature flag resolution — real apps would call a config provider
    Dictionary<string, bool> flags = new()
    {
      ["VerboseToolLogging"] = environment.Equals("staging", StringComparison.OrdinalIgnoreCase),
      ["EnableExperimentalTools"] = environment.Equals("staging", StringComparison.OrdinalIgnoreCase),
      ["StrictSafetyMode"] = agentName.Equals("MotorsAgent", StringComparison.OrdinalIgnoreCase)
                             && environment.Equals("production", StringComparison.OrdinalIgnoreCase),
    };

    session?.StateBag.SetValue("FeatureFlags", flags);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Flags] Flags: {string.Join(", ", flags.Select(f => $"{f.Key}={f.Value}"))}", ConsoleColor.Yellow);

    await next(messages, session, options, cancellationToken);

    ColorHelper.PrintColoredLine($"[Agent] [SharedFunction] [Flags] POST: Completed", ConsoleColor.Yellow);
  }
}
