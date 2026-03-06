dotnet run --project MCPServerWithHttp --launch-profile http
dotnet run --project MCPServerWithHttp --launch-profile https

dotnet build MCPServerWithHttp
MCPServerWithHttp.exe --urls "https://localhost:7133;http://localhost:3001"

npx @modelcontextprotocol/inspector
