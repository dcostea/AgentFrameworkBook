using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Plugins.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Direction
{
    [Description("North wind direction")] North,
    [Description("East wind direction")] East,
    [Description("South wind direction")] South,
    [Description("West wind direction")] West,
    [Description("NorthWest wind direction")] NorthWest,
    [Description("NorthEast wind direction")] NorthEast,
    [Description("SouthWest wind direction")] SouthWest,
    [Description("SouthEast wind direction")] SouthEast
}
