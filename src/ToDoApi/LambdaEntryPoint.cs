using Amazon.Lambda.AspNetCoreServer;

namespace todo_serverless;

public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IHostBuilder builder)
    {
        // Använd Program.cs konfiguration
    }
}