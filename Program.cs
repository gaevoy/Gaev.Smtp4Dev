using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gaev.Smtp4Dev.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using netDumbster.smtp;
using netDumbster.smtp.Logging;
using Serilog;
using Serilog.Events;

namespace Gaev.Smtp4Dev
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.RollingFile("log-{Date}.txt")
                //.MinimumLevel.Is(LogEventLevel.Debug)
                .CreateLogger();
            LogManager.GetLogger = type => new DumbsterLogger(Log.Logger.ForContext(type));
            var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (__, e) =>
            {
                Log.Information("Stopping");
                e.Cancel = true;
                cancellation.Cancel();
            };

            using var server = Configuration.Configure().WithPort(25).Build();
            server.MessageReceived += (__, receivedArgs) =>
                Task.Run(() =>
                    OnMessageReceivedAsync(receivedArgs.Message));
            Log.Information("Started");
            try
            {
                await Host
                    .CreateDefaultBuilder(args)
                    .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
                    .Build()
                    .RunAsync(cancellation.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Stopping");
            }
            finally
            {
                Log.Information("Stopped");
                Log.CloseAndFlush();
            }
        }

        static async Task OnMessageReceivedAsync(SmtpMessage message)
        {
            var messageId = Guid.NewGuid().ToString("N");
            Log.Information("Message received {@msg}", new
            {
                messageId,
                To = message.Headers["To"],
                Subject = message.Headers["Subject"]
            });
            var messageInJson = JsonSerializer.Serialize(message.Data);
            foreach (var recipient in message.ToAddresses)
                await EmailsController.OnMessageReceived(recipient.Address, messageInJson, messageId);
        }

        class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMvc();
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                });
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env) =>
                app
                    .UseRouting()
                    .UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}