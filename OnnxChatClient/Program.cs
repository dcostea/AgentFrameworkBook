using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using System.Diagnostics;

////var query = """
////    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.
////    You have to break down the provided complex commands into the basic moves you know.
////    There is a tree in front of the car. Avoid it and resume the original path.
////    Respond with a JSON array like [move1, move2, move3].
////    Do not respond with reasoning, comments, or any additional text.
////    """;

var query = """
  You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.  
  You have to break down the provided complex commands into basic moves you know.
  Respond only with the permitted moves, without any additional explanations.

  Complex command: 
  "There is a tree directly in front of the car. Avoid it and then come back to the original path."  
  """;

Console.WriteLine($"USER: {query}");

//var modelPath = @"c:\Temp\LLMs\ONNX\phi-3.5-mini-instruct\cpu_and_mobile\cpu-int4-awq-block-128-acc-level-4";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-4-mini-instruct\cpu_and_mobile\cpu-int4-rtn-block-32-acc-level-4";

var modelPath = @"c:\Temp\LLMs\ONNX\phi-4-multimodal-instruct-onnx\gpu\gpu-int4-rtn-block-32";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-3.5-mini-instruct\gpu\gpu-int4-awq-block-128";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-4-mini-instruct\gpu\gpu-int4-rtn-block-32";

Stopwatch sw = new();
sw.Start();

var config = new Config(modelPath);

// this is for GPU
config.ClearProviders();
config.AppendProvider("cuda");
config.SetProviderOption("cuda", "device_id", "0");
config.SetProviderOption("cuda", "enable_cuda_graph", "0");

var model = new Model(config);

// Using ONNX Client directly
using var onnxChatClient = new OnnxRuntimeGenAIChatClient(model);
ChatMessage message = new(ChatRole.User, query);

////ChatResponse response = await onnxChatClient.GetResponseAsync(message);
////Console.WriteLine($"\nAssistant (ONNX ChatClient): {response.Text}");

Console.WriteLine($"\nAssistant (ONNX ChatClient): ");
TimeSpan ltElapsed = sw.Elapsed;
Console.WriteLine($"Load time: {ltElapsed.TotalMilliseconds:#} ms");
ChatOptions options = new()
{
  MaxOutputTokens = 512,
};

bool firstRun = true;
var tokenCount = 0;
TimeSpan ttftElapsed = sw.Elapsed;
await foreach (var update in onnxChatClient.GetStreamingResponseAsync(message, options))
{
  if (firstRun)
  {
    firstRun = false;
    ttftElapsed = sw.Elapsed;
    Console.WriteLine($"First token time: {ttftElapsed.TotalMilliseconds:#} ms");
  }

  Console.Write(update);
  tokenCount++;
}
sw.Stop();
Console.WriteLine();
var ttltElapsed = sw.Elapsed;
Console.WriteLine($"Last token time: {ttltElapsed.TotalMilliseconds:#} ms");

var generationTime = ttltElapsed - ttftElapsed;
Console.WriteLine($"Speed: {tokenCount / generationTime.TotalSeconds:#} tokens/second ({tokenCount} tokens in {generationTime.TotalMilliseconds:#} ms)");
