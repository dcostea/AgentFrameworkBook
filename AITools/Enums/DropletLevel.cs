using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Plugins.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DropletLevel
{
    [Description("None drop level")] None,
    [Description("Low drop level")] Low,
    [Description("Medium drop level")] Medium,
    [Description("High drop level")] High,
}

