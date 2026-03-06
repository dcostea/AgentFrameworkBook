using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPServerWithStdio.Resources;

[McpServerResourceType]
public class MotorResources
{
  [McpServerResource(Name = "bio"), Description("A direct text resource")]
  public static string Bio() => "My name is Robby, the robot.";
}
