```
IChatClient chatClient = new OpenAIClient(apiKey)
  .GetChatClient(model)
  .AsIChatClient().AsBuilder()
  .Use(ChatClientSharedFunctions.LimitRequests)
  .Use(ChatClientSharedFunctions.RemoveEmail)    // Story 2: GDPR Nightmare — sanitize input
  .Use(ChatClientResponses.EnforceTokenBudget, null)  // Story 1: token consumption spirals unchecked into a $10,847 bill
  .Use(ChatClientResponses.AddTimestamp, null)
  .UseFunctionInvocation(loggerFactory: null, configure: options =>
  {
    options.FunctionInvoker = async (context, ct) =>
    {
      var response = await Middleware.ChatClientFunctionCallings.ConstrainDistance(context, ct)  // Story 3: constrain backward distance
        ?? await Middleware.ChatClientFunctionCallings.AuditFunctionCalling(context, ct);
      return response;
    };
  })
  .Build();
```
This sample demonstrates how to use the MiddlewareMixed package to apply multiple middleware functions to an IChatClient. The code shows how to build a chat client with various middleware functions, such as removing email addresses from input, enforcing token budgets on responses, and constraining function call distances.

```
  .AsBuilder()
  .Use(ChatClientSharedFunctions.RemoveEmail)    // Story 2: GDPR Nightmare — sanitize input
  .Use(ChatClientResponses.EnforceTokenBudget, null)  // Story 1: token consumption spirals unchecked into a $10,847 bill
  .UseFunctionInvocation(loggerFactory: null, configure: options =>
  {
    options.FunctionInvoker = async (context, ct) => await Middleware.ChatClientFunctionCallings.ConstrainDistance(context, ct);  // Story 3: constrain backward distance
  })
  .Build();
```


```

ColorHelper.PrintColoredLine("""
  ===== TEST 1: SharedFunction Middleware (LimitRequests + RemoveEmail) ===== 
  (Without middleware: The email was NOT redacted — sent directly to the LLM provider, GDPR violation; no request limit — runaway costs go unchecked)
  (With middleware: The email was redacted — GDPR compliance; request limit enforced — costs controlled)
  """);
var query1 = "Navigate to original position. Contact me at john.doe@example.com for updates.";
ColorHelper.PrintColoredLine($"QUERY: {query1}", ConsoleColor.Yellow);
AgentResponse result1 = await motorsAgent.RunAsync(query1, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result1}\n", ConsoleColor.Yellow);

ColorHelper.PrintColoredLine("""
  ===== TEST 2: FunctionCalling Middleware (ConstrainDistance + AuditFunctionCalling) =====
  (Without middleware: Backward ran the full 10 m — no distance constraint, the robot could hit a wall; no audit — tool calls go untracked with no timing or result log)
  (With middleware: Backward constrained to 5 m max — safer default for obstacle avoidance; function calls audited — logs capture the moves executed with timestamps for traceability)
  """);
var query2 = "Move forward 10 meters then go backward 10 meters";
ColorHelper.PrintColoredLine($"QUERY: {query2}", ConsoleColor.Yellow);
AgentResponse result2 = await motorsAgent.RunAsync(query2, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result2}\n", ConsoleColor.Yellow);

ColorHelper.PrintColoredLine("""
  ===== TEST 3: Response Middleware (EnforceTokenBudget + AddTimestamp) ===== 
  (Without middleware: No token budget enforced — costs accumulate silently; no timestamp added — responses carry no audit trail)
  (With middleware: Token budget enforced — costs controlled; timestamp added — responses carry an audit trail)
  """);
var query3 = "Move forward 3 meters, turn right 90 degrees, move forward 3 meters";
ColorHelper.PrintColoredLine($"QUERY: {query3}", ConsoleColor.Yellow);
AgentResponse result3 = await motorsAgent.RunAsync(query3, session);
ColorHelper.PrintColoredLine($"\nRESULT: {result3}\n", ConsoleColor.Yellow);
```

This code demonstrates three tests of the middleware functions applied to the chat client. Each test compares the behavior of the chat client with and without the middleware functions, highlighting the benefits of using middleware for input sanitization, request limiting, function call constraints, and response auditing. The results show how middleware can help ensure compliance, control costs, and enhance traceability in interactions with LLM providers.