using AITools;
using Microsoft.Agents.AI;

namespace Skills;

public static class RobotCarSkills
{
  public static AgentSkillsProvider CreateProvider()
  {
    var fireResponse = new AgentInlineSkill(
      name: "fire-response",
      description: "Respond to possible fire when the measured temperature is above 60 degrees Celsius. Takes priority over rain. Finish by stopping the car with the movement skill.",
      instructions: """
        ## PERSONA
        Handle the fire condition previously assigned to FireDetectorAgent.

        ## ACTIONS
        1. Run SoundAlarm, then StartWaterSprinkle using run_skill_script.
        2. Check the returned tool results; do not claim an action succeeded if it failed.
        3. Load the movement skill and follow its hazard procedure: Stop, then summarize.
        4. Do not continue the exploration mission after detecting fire.

        StopWaterSprinkle is available for a later explicit request after the fire is extinguished.
        Do not stop the sprinkler immediately just to finish this mission.
        """)
      .AddScript("SoundAlarm", FireDetectorTools.SoundAlarmAsync, "Sound the mock fire alarm.")
      .AddScript("StartWaterSprinkle", FireDetectorTools.StartWaterSprinkleAsync, "Start the mock sprinkler.")
      .AddScript("StopWaterSprinkle", FireDetectorTools.StopWaterSprinkleAsync, "Stop the mock sprinkler.");

    var rainResponse = new AgentInlineSkill(
      name: "rain-response",
      description: "Respond to heavy rain when the measured droplet level is High and temperature is not above 60 degrees Celsius. Finish by stopping the car with the movement skill.",
      instructions: """
        ## PERSONA
        Handle the rain condition previously assigned to RainDetectorAgent.

        ## ACTIONS
        1. Run StartWipers using run_skill_script, passing the measured dropletLevel.
        2. Check the returned tool result; do not claim the wipers started if the call failed.
        3. Load the movement skill and follow its hazard procedure: Stop, then summarize.
        4. Do not continue the exploration mission in heavy rain.

        StopWipers is available for a later explicit request when droplets are no longer detected.
        """)
      .AddScript("StartWipers", RainDetectorTools.StartWipersAsync, "Start the mock wipers for the measured droplet level.")
      .AddScript("StopWipers", RainDetectorTools.StopWipersAsync, "Stop the mock wipers.");

    var movement = new AgentInlineSkill(
      name: "movement",
      description: "Control Robby's movements. Stop after fire or heavy rain; otherwise execute the user's exploration mission when the sensor readings are safe.",
      instructions: """
        ## PERSONA
        Handle the movements previously assigned to MotorsAgent.

        ## ACTIONS
        1. Read the movement-rules resource before operating the motors.
        2. If the measured temperature was above 60 degrees Celsius, the droplet level was High,
           or fire/wiper actions appear in this mission's tool results, run Stop and summarize.
           Do not execute any other movement in this hazard case, even if a mitigation call failed.
        3. Otherwise use the user's mission to choose a short movement sequence and run its scripts.
        4. Finish with Stop and summarize only the actions confirmed by tool results.
        """)
      .AddResource("movement-rules", """
        # Movement rules
        - Available moves: forward, backward, turn left, turn right, and stop.
        - Distances are meters; turn angles are degrees.
        - Stop before reversing between forward and backward.
        - For a tree 50 meters ahead, turn away before reaching it, pass it with clearance,
          and turn back toward the original path. Never drive straight into the tree.
        - These methods only print and return simulated results; they do not control hardware.
        """, "Movement vocabulary, units, and the exploration mission's constraints.")
      .AddScript("Forward", MotorTools.ForwardAsync, "Move the mock car forward by distance meters.")
      .AddScript("backward", MotorTools.BackwardAsync, "Move the mock car backward by distance meters.")
      .AddScript("TurnLeft", MotorTools.TurnLeftAsync, "Turn the mock car left by angle degrees.")
      .AddScript("TurnRight", MotorTools.TurnRightAsync, "Turn the mock car right by angle degrees.")
      .AddScript("Stop", MotorTools.StopAsync, "Stop the mock car.");

    return new AgentSkillsProvider([fireResponse, rainResponse, movement], new AgentSkillsProviderOptions
    {
      // Trusted, in-process mock operations only. Real device actions need an approval policy.
      DisableLoadSkillApproval = true,
      DisableReadSkillResourceApproval = true,
      DisableRunSkillScriptApproval = true
    });
  }
}
