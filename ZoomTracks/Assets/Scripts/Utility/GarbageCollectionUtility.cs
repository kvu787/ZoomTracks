using System;
using UnityEngine.Assertions;
using UnityEngine.Scripting;

namespace ZoomTracks {
    public static class GarbageCollectionUtility {
        public static void ForceGarbageCollection() {
            Assert.IsTrue(GarbageCollector.GCMode == GarbageCollector.Mode.Enabled);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
