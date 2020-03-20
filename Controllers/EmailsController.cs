﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog.Core;

namespace Gaev.Smtp4Dev.Controllers
{
    [Route("api/emails")]
    public class EmailsController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, List<ClientHandle>> Inboxes =
            new ConcurrentDictionary<string, List<ClientHandle>>();

        public class ClientHandle
        {
            public string Ip { get; set; }
            public string Ip2 { get; set; }
            public int Port { get; set; }
            public StreamWriter Response { get; set; }
        }

        private static Logger Logger => Program.Logger;

        // GET api/emails/test@smtp4dev.gaevoy.com
        [HttpGet("{recipientEmail}")]
        public async Task ListenToMessages(string recipientEmail)
        {
            Response.Headers["Cache-Control"] = "no-cache"; // https://serverfault.com/a/801629
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.ContentType = "text/event-stream";
            await using var response = new StreamWriter(Response.Body);
            var clients = Inboxes.GetOrAdd(recipientEmail.ToLower(), _ => new List<ClientHandle>(4));
            var client = new ClientHandle
            {
                Ip = Request.Headers["X-Real-IP"].ToString(),
                Port = HttpContext.Connection.RemotePort,
                Response = response
            };
            lock (clients)
                clients.Add(client);
            await response.WriteAsync("event: connected\ndata:\n\n");
            await response.FlushAsync();
            Logger.Information("Client connected {@cli}", new {client.Ip, client.Port});
            await HttpContext.RequestAborted.AsTask();
            lock (clients)
                clients.Remove(client);
            Logger.Information("Client disconnected {@cli}", new {client.Ip, client.Port});
        }

        public static async Task OnMessageReceived(string recipientEmail, string messageInJson, Guid messageId)
        {
            List<ClientHandle> SafeCopy(List<ClientHandle> streamWriters)
            {
                lock (streamWriters)
                    return streamWriters.ToList();
            }

            if (!Inboxes.TryGetValue(recipientEmail.ToLower(), out var clients))
                return;
            foreach (var client in SafeCopy(clients))
                try
                {
                    await client.Response.WriteAsync("data: " + messageInJson + "\n\n");
                    await client.Response.FlushAsync();
                    Logger.Information("Message sent {@cli}", new {client.Ip, client.Port, messageId});
                }
                catch (ObjectDisposedException)
                {
                    lock (clients)
                        clients.Remove(client);
                    Logger.Information("Client is disposed {@cli}", new {client.Ip, client.Port, messageId});
                }
        }
    }
}