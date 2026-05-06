using UnityEngine;
using System;

namespace ZoomTracks {
    internal static class QuitOnException {
        // This attribute ensures the method runs automatically at startup
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init() {
            RemoveHandlers();

            // Catch Unity-logged exceptions (Main thread and Unity-managed worker threads)
            // NOTE: This will catch calls to "Debug.LogException"
            Application.logMessageReceivedThreaded += HandleLog;

            // Catch raw .NET unhandled exceptions (Unmanaged background threads)
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        }

        private static void HandleLog(string logString, string stackTrace, LogType logType) {
            if (logType == LogType.Exception) {
                RemoveHandlers();
                TriggerExit();
            }
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e) {
            RemoveHandlers();
            Debug.LogException((Exception)e.ExceptionObject);
            TriggerExit();
        }

        private static void RemoveHandlers() {
            Application.logMessageReceivedThreaded -= HandleLog;
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
        }

        private static void TriggerExit() {
#if UNITY_EDITOR
            // If we are in the Unity Editor, stop Play Mode
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // If we are in a standalone build (Windows, Mac, Mobile, Console), quit the application.
            // Passing '1' indicates an abnormal/error exit code to the OS.
            Application.Quit(1);

            // FAILSAFE: If you find that Application.Quit() hangs due to other background processes, 
            // uncomment the line below for a brutal, immediate process kill at the OS level.
            // System.Diagnostics.Process.GetCurrentProcess().Kill();
#endif
        }
    }
}
