using System.Threading;
using System.Threading.Tasks;

namespace Gaev.Smtp4Dev
{
    public static class CancellationTokenExt
    {
        public static async Task AsTask(this CancellationToken cancellation)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}