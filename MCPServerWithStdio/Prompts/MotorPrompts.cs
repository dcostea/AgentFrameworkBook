using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPServerWithStdio.Prompts;

[McpServerPromptType]
public class MotorPrompts
{
  [McpServerPrompt(Name = "string_prompt"), Description("A string prompt without arguments")]
  public static string StringPrompt()
  {
    return """
      ## Context
      Complex command: 
      "There is a tree directly in front of the car. Avoid it and then return to the original path."
      """;
  }

  [McpServerPrompt(Name = "message_prompt"), Description("A message prompt without arguments")]
  public static IEnumerable<ChatMessage> MessagePrompt()
  {
    return [
      new ChatMessage(ChatRole.User, """
        ## Context
        Complex command:
        "There is a tree directly in front of the car. Avoid it and then return to the original path."
        """)
    ];
  }

  [McpServerPrompt(Name = "parametrized_message_prompt"), Description("A message prompt with arguments")]
  public static IEnumerable<ChatMessage> MessagePromptWithArguments(
    [Description("The complex action to be performed")] string action)
  {
    return [
      new ChatMessage(ChatRole.User, $"""
        ## Context
        Complex command:
        {action}
        """)
    ];
  }
}