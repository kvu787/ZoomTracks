using System;
using System.Threading;
using UnityEngine;

namespace ZoomTracks {
    public static class AwaitableUtility {
        public static async Awaitable RunWithPrintBusyEachFrameAsync(Func<Awaitable> awaitableOperation) {
            using (CancellationTokenSource printBusyEachFrameCts = new()) {
                Awaitable printBusyEachFrameAwaitable = PrintBusyEachFrameAsync(printBusyEachFrameCts.Token);
                try {
                    await awaitableOperation();
                } finally {
                    printBusyEachFrameCts.Cancel();
                    await printBusyEachFrameAwaitable;
                }
            }
        }

        private static async Awaitable PrintBusyEachFrameAsync(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                try {
                    await Awaitable.NextFrameAsync(cancellationToken);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                }
                Debug.Log($"Busy {Time.realtimeSinceStartupAsDouble:F3}");
            }
        }
    }
}
