using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPServerWithStdio.Tools;

[McpServerToolType, Description("Robot car maintenance tools.")]
public class MaintenanceTools
{
  [McpServerTool(Name = "run_diagnostics", Title = "Run Diagnostics", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description("Runs a full diagnostics check on all robot car motors. This is a long-running operation.")]
  public static async Task<string> RunDiagnosticsAsync(CancellationToken cancellationToken)
  {
    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    return "Diagnostics complete. All 4 motors passed.";
  }

  [McpServerTool(Name = "run_diagnostics_with_progress", Title = "Run Diagnostics with Progress", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description("Runs a full diagnostics check on all robot car motors and reports progress notifications.")]
  public static async Task<string> RunDiagnosticsWithProgressAsync(
    IProgress<ProgressNotificationValue> progress,
    CancellationToken cancellationToken)
  {
    for (int motor = 1; motor <= 4; motor++)
    {
      await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
      progress.Report(new() { Progress = motor, Total = 4 });
    }

    return "Diagnostics complete. All 4 motors passed.";
  }
}
