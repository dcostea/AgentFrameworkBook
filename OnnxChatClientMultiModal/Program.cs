using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using System.Diagnostics;

//var modelPath = @"c:\Temp\LLMs\ONNX\phi-3.5-mini-instruct\cpu_and_mobile\cpu-int4-awq-block-128-acc-level-4";
var modelPath = @"c:\Temp\LLMs\ONNX\phi-4-mini-instruct\cpu_and_mobile\cpu-int4-rtn-block-32-acc-level-4";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-4-multimodal-instruct-onnx\gpu\gpu-int4-rtn-block-32";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-3.5-mini-instruct\gpu\gpu-int4-awq-block-128";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-4-mini-instruct\gpu\gpu-int4-rtn-block-32";
//var modelPath = @"c:\Users\danie\.foundry\cache\models\Microsoft\gpt-oss-20b-cuda-gpu-1\v1";
//var modelPath = @"c:\Users\danie\.foundry\cache\models\Microsoft\mistralai-Mistral-7B-Instruct-v0-2-cuda-gpu-1\mistral-7b-instruct-v0.2-cuda-int4-rtn-block-32";
//var modelPath = @"c:\Temp\LLMs\ONNX\phi-3.5-vision-instruct-onnx-cpu\vision-cpu-fp32";

var config = new Config(modelPath);

// this is for GPU
//config.ClearProviders();
//config.AppendProvider("cuda");
//config.SetProviderOption("cuda", "device_id", "0");
//config.SetProviderOption("cuda", "enable_cuda_graph", "0");

var model = new Model(config);

////var query = """
////    You are an AI assistant controlling a robot car capable of performing basic moves: forward, backward, turn left, turn right, and stop.  
////    You have to break down the provided complex commands into basic moves you know.
////    Respond only with the permitted moves, without any additional explanations.

////    Complex command: 
////    "There is a tree directly in front of the car. Avoid it and then come back to the original path."  
////    """;

var onnxOptions = new OnnxRuntimeGenAIChatClientOptions { EnableCaching = true };

using var onnxChatClient = new OnnxRuntimeGenAIChatClient(model, options: onnxOptions);

byte[] imageBytes = File.ReadAllBytes(@"Data/path.jpg");
///byte[] audioBytes = File.ReadAllBytes(@"Data/task.mp3");
//////var images = Images.Load(imageBytes);
//////var mmProcessor = new MultiModalProcessor(model);
//////var namedTensors = mmProcessor.ProcessImages("Look at the image and tell what you see.", images);

ChatMessage message =
    ////new(ChatRole.User, [
    ////    new TextContent("Look at the image and tell what you see."),
    ////    new UriContent(@"http://apexcode.ro/path.jpg", "image/jpeg")
    ////])
    new(ChatRole.User, [
        new TextContent("Look at the image and tell what you see."),
        new DataContent(imageBytes, "image/jpeg")
    ])
//////new(ChatRole.User, query)
//new(ChatRole.User, [
//    new TextContent(query),
//    new DataContent(imageBytes, "image/jpeg")
//])
////new(ChatRole.User, [
////    new UriContent(new Uri(@"https://apexcode.ro/task.mp3"), "audio/mpeg")
////]),
////new(ChatRole.User, [
////    new DataContent(audioBytes, "audio/mpeg")
////]),
;

Stopwatch sw = new();
sw.Start();

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
await foreach (var update in onnxChatClient.GetStreamingResponseAsync([message], options))
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
