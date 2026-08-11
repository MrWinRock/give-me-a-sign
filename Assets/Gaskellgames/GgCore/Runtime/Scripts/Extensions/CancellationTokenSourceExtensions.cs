using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gaskellgames
{
    public static class CancellationTokenSourceExtensions
    {
        /// <summary>
        /// Cancels the CancellationTokenSource, and then disposes of the CancellationTokenSource the following frame.
        /// </summary>
        /// <param name="cancellationTokenSource"></param>
        public static async void CancelAndDispose(this CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource?.Cancel();
            await GgTask.WaitUntilNextFrame();
            cancellationTokenSource?.Dispose();
        }
        
        /// <summary>
        /// Cancels the CancellationTokenSource after a specified delay in seconds, and then disposes of the CancellationTokenSource the following frame.
        /// </summary>
        /// <param name="cancellationTokenSource"></param>
        /// <param name="delay"></param>
        public static async Task CancelAndDisposeAfter(this CancellationTokenSource cancellationTokenSource, int delay)
        {
            if (cancellationTokenSource == null) { return; }
            if (0 < delay) { await GgTask.WaitForSeconds(delay, cancellationTokenSource); }
            cancellationTokenSource?.Cancel();
            await GgTask.WaitUntilNextFrame();
            cancellationTokenSource?.Dispose();
        }
        
        /// <summary>
        /// Cancels the CancellationTokenSource after a specified delay, and then disposes of the CancellationTokenSource the following frame.
        /// </summary>
        /// <param name="cancellationTokenSource"></param>
        /// <param name="delay"></param>
        public static async Task CancelAndDisposeAfter(this CancellationTokenSource cancellationTokenSource, TimeSpan delay)
        {
            await cancellationTokenSource.CancelAndDisposeAfter(delay.Seconds);
        }
        
    } // class end
}
