using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gaev.Smtp4Dev.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using netDumbster.smtp;

namespace Gaev.Smtp4Dev
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (__, e) =>
            {
                e.Cancel = true;
                cancellation.Cancel();
            };

            using var server = SimpleSmtpServer.Start(25);
            server.MessageReceived += (__, receivedArgs) =>
                Task.Run(() =>
                    OnMessageReceivedAsync(receivedArgs.Message));
            _ = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(60_000, cancellation.Token);
                    server.ClearReceivedEmail();
                }
            });
            await Host
                .CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
                .Build()
                .RunAsync(cancellation.Token);
        }

        static async Task OnMessageReceivedAsync(SmtpMessage message)
        {
            var messageInJson = JsonSerializer.Serialize(message.Data);
            foreach (var recipient in message.ToAddresses)
                await EmailsController.OnMessageReceived(recipient.Address, messageInJson);
        }

        class Startup
        {
            public void ConfigureServices(IServiceCollection services) =>
                services.AddMvc();

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env) =>
                app
                    .UseRouting()
                    .UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}