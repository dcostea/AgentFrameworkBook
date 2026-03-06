using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPServerWithHttp.Resources;

[McpServerResourceType]
public class MotorResources
{
  [McpServerResource(Name = "bio"), Description("A direct text resource")]
  public static string DirectTextResource() => "My name is Robby, the robot.";
}
