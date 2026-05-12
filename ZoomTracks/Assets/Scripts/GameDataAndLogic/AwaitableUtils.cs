using System;
using System.Threading;
using UnityEngine;

namespace ZoomTracks {
    public static class AwaitableUtils {
        public static async Awaitable RunWithPrintBusyAsync(Func<Awaitable> awaitableOperation) {
            using (CancellationTokenSource printBusyCts = new()) {
                Awaitable printBusyAwaitable = PrintBusyAsync(printBusyCts.Token);
                await awaitableOperation();
                printBusyCts.Cancel();
                await printBusyAwaitable;
            }
        }

        private static async Awaitable PrintBusyAsync(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                Debug.Log($"Busy {Time.realtimeSinceStartupAsDouble:F3}");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
