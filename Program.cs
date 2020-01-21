using System;
using System.Linq;
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
            await Host
                .CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
                .Build()
                .RunAsync(cancellation.Token);
        }

        static async Task OnMessageReceivedAsync(SmtpMessage message)
        {
            var recipients = message.ToAddresses.Select(e => e.Address).ToArray();
            var headers = message.Headers.AllKeys.ToDictionary(key => key, key => message.Headers[key]);
            var messageInJson = JsonSerializer.Serialize(JsonSerializer.Serialize(new
            {
                from = message.FromAddress.Address,
                to = recipients,
                headers,
                subject = headers.ContainsKey("Subject") ? message.Headers["Subject"] : null,
                bodyAsText = message.MessageParts
                    .Where(e => e.HeaderData.StartsWith("text/plain"))
                    .Select(e => e.BodyData)
                    .FirstOrDefault(),
                bodyAsHtml = message.MessageParts
                    .Where(e => e.HeaderData.StartsWith("text/html"))
                    .Select(e => e.BodyData)
                    .FirstOrDefault()
            }));
            foreach (var recipient in recipients)
                await EmailsController.OnMessageReceived(recipient, messageInJson);
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