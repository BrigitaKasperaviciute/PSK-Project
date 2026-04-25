using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class LoggingEnabledWebApplicationFactory : WorthBoardsWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true",
            });
        });
    }
}
