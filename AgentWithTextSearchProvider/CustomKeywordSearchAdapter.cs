using Microsoft.Agents.AI;

namespace Adapters;

public static class CustomKeywordSearchAdapter
{
  private static List<SearchItem>? _knowledgeBase;
  private static TextSearchProviderOptions? _searchOptions;
  private static int _topResults;

  public static void Initialize(TextSearchProviderOptions searchOptions, int topResults = 5)
  {
    _searchOptions = searchOptions;
    _topResults = topResults;

    _knowledgeBase =
    [
      new()
      {
        Key = "1",
        Text = "The robot car can move backward by a specified distance. Reverse motion is limited to 5 meters.",
        SourceName = "RobotCar Movement Policy",
        Keywords = ["command", "move", "distance"]
      },
      new()
      {
        Key = "2",
        Text = "The robot can turn left or right by angles of 30, 45, and 60 degrees.",
        SourceName = "RobotCar Turning Policy",
        Keywords = ["command", "move", "turn", "angle"]
      },
      new()
      {
        Key = "3",
        Text = "The robot car can move forward by a specified distance. Maximum range is 10 meters.",
        SourceName = "RobotCar Movement Policy",
        Keywords = ["command", "move", "distance"]
      },
      new()
      {
        Key = "4",
        Text = "Emergency stop immediately halts all motion.",
        SourceName = "RobotCar Safety Manual",
        Keywords = ["command", "move", "distance", "emergency"]
      },
      ////new()
      ////{
      ////  Key = "5",
      ////  Text = "June 1, 2025 - Morning: 14°C, partly cloudy, wind 8 km/h, dry. Afternoon: 20°C, mostly sunny, wind 12 km/h, no rain. Night: 13°C, clear, wind 6 km/h, calm.",
      ////  SourceName = "Weather Forecast",
      ////  Keywords = ["weather", "temperature", "wind"]
      ////},
      ////new()
      ////{
      ////  Key = "6",
      ////  Text = "June 2, 2025 - Morning: 15°C, sunny, wind 10 km/h, dry. Afternoon: 22°C, mostly sunny, wind 14 km/h, dry roads. Night: 14°C, few clouds, wind 8 km/h, no precipitation.",
      ////  SourceName = "Weather Forecast",
      ////  Keywords = ["weather", "temperature", "wind"]
      ////},
      ////new()
      ////{
      ////  Key = "7",
      ////  Text = "June 3, 2025 - Morning: 13°C, cloudy, wind 10 km/h, dry. Afternoon: 21°C, clearing skies, wind 13 km/h, dry. Night: 13°C, mostly clear, wind 7 km/h, calm.",
      ////  SourceName = "Weather Forecast",
      ////  Keywords = ["weather", "temperature", "wind"]
      ////},
      ////new()
      ////{
      ////  Key = "8",
      ////  Text = "June 4, 2025 - Morning: 12°C, overcast, wind 11 km/h, dry. Afternoon: 19°C, showers likely, wind 16 km/h, wet roads possible. Night: 12°C, cloudy, wind 9 km/h, light drizzle.",
      ////  SourceName = "Weather Forecast",
      ////  Keywords = ["weather", "temperature", "wind"]
      ////},
      ////new()
      ////{
      ////  Key = "9",
      ////  Text = "June 5, 2025 - Morning: 12°C, cloudy, wind 13 km/h, occasional light rain. Afternoon: 18°C, overcast, wind 18 km/h, scattered rain showers. Night: 11°C, mostly cloudy, wind 10 km/h, some drizzle.",
      ////  SourceName = "Weather Forecast",
      ////  Keywords = ["weather", "temperature", "wind"]
      ////},
    ];
  }

  public static async Task<IEnumerable<TextSearchProvider.TextSearchResult>> KeywordSearch(string query, CancellationToken cancellationToken)
  {
    if (_knowledgeBase == null || _searchOptions == null)
    {
      throw new InvalidOperationException("KeywordSearchAdapter has not been initialized. Call Initialize first.");
    }

    // Split query into words
    var keywords = query.ToLowerInvariant().Split([' ', '.', ',', ':', ';'], StringSplitOptions.RemoveEmptyEntries);

    // Count matches and order by relevance
    var results = _knowledgeBase
      .Select(item => new
      {
        Item = item,
        MatchCount = keywords.Count(keyword => 
          item.Keywords.Any(k => k.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
      })
      .Where(x => x.MatchCount > 0)
      .OrderByDescending(x => x.MatchCount)
      .Take(_topResults)
      .Select(x => new TextSearchProvider.TextSearchResult
      {
        Text = x.Item.Text,
        SourceName = x.Item.SourceName,
        SourceLink = string.Join(", ", x.Item.Keywords)
      });

    return results;
  }
}
