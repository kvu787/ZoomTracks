using UnityEngine;
using UnityEngine.Rendering;

namespace ZoomTracks {
    public static class PrintInfoUtility {
        public static void PrintDisplayInfo() {
            Display display = Display.main;
            Debug.Log($"Render resolution: {display.renderingWidth}*{display.renderingHeight}");
            Debug.Log($"Window resolution: {Screen.width}*{Screen.height}");

            Debug.Log($"Screen.fullScreenMode: {Screen.fullScreenMode}");
            Debug.Log($"Screen.fullScreen: {Screen.fullScreen}");

            switch (Screen.fullScreenMode) {
            case FullScreenMode.ExclusiveFullScreen:
                Debug.Log("Running in exclusive fullscreen mode");
                break;
            case FullScreenMode.FullScreenWindow:
                Debug.Log("Running in borderless fullscreen window mode");
                break;
            case FullScreenMode.MaximizedWindow:
                Debug.Log("Running in maximized window mode");
                break;
            case FullScreenMode.Windowed:
                Debug.Log("Running in windowed mode");
                break;
            default:
                break;
            }
        }

        public static void PrintGraphicsInfo() {
            GraphicsDeviceType api = SystemInfo.graphicsDeviceType;
            Debug.Log($"Graphics API in use: {api}");
            Debug.Log($"Graphics device: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"Graphics device vendor: {SystemInfo.graphicsDeviceVendor}");
            Debug.Log($"Graphics device version: {SystemInfo.graphicsDeviceVersion}");

            if (api == GraphicsDeviceType.Direct3D11) {
                Debug.Log("Running on Direct3D 11 / DX11");
            } else if (api == GraphicsDeviceType.Direct3D12) {
                Debug.Log("Running on Direct3D 12 / DX12");
            } else {
                Debug.Log($"Running on something else: {api}");
            }
        }
    }
}
