using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPServerWithStdio.Resources;

[McpServerResourceType]
public class MotorResources
{
  [McpServerResource(UriTemplate = "resource://mcp/bio", Name = "bio"), Description("A static resource: returns a fixed bio for the robot")]
  public static string Bio() => "My name is Robby, the robot.";

  [McpServerResource(UriTemplate = "resource://mcp/greet/{name}", Name = "greet"), Description("A template resource: returns a personalised greeting for the given name")]
  public static TextResourceContents Greet(
    RequestContext<ReadResourceRequestParams> context,
    [Description("The name to greet")] string name)
  {
    return new TextResourceContents
    {
      Uri = context.Params!.Uri,
      MimeType = "text/plain",
      Text = $"Hello, {name}! I am Robby, the robot.",
    };
  }
}
