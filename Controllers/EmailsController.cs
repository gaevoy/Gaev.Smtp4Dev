using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Gaev.Smtp4Dev.Controllers
{
    [Route("api/emails")]
    public class EmailsController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, List<StreamWriter>> Inboxes =
            new ConcurrentDictionary<string, List<StreamWriter>>();

        // GET api/emails/test@smtp4dev.gaevoy.com
        [HttpGet("{recipientEmail}")]
        public async Task ListenToMessages(string recipientEmail)
        {
            Response.Headers["Cache-Control"] = "no-cache"; // https://serverfault.com/a/801629
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.ContentType = "text/event-stream";
            await using var member = new StreamWriter(Response.Body);
            var listeners = Inboxes.GetOrAdd(recipientEmail.ToLower(), _ => new List<StreamWriter>(4));
            lock (listeners)
                listeners.Add(member);
            await member.WriteAsync("event: connected\ndata:\n\n");
            await member.FlushAsync();
            await HttpContext.RequestAborted.AsTask();
            lock (listeners)
                listeners.Remove(member);
        }

        public static async Task OnMessageReceived(string recipientEmail, string messageInJson)
        {
            List<StreamWriter> SafeCopy(List<StreamWriter> streamWriters)
            {
                lock (streamWriters)
                    return streamWriters.ToList();
            }

            if (!Inboxes.TryGetValue(recipientEmail.ToLower(), out var listeners))
                return;
            foreach (var listener in SafeCopy(listeners))
                try
                {
                    await listener.WriteAsync("data: " + messageInJson + "\n\n");
                    await listener.FlushAsync();
                }
                catch (ObjectDisposedException)
                {
                    lock (listeners)
                        listeners.Remove(listener);
                }
        }
    }
}