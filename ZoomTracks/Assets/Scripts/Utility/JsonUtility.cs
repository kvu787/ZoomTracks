using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

namespace ZoomTracks {
    public static class JsonUtility {
        public static T Deserialize<T>(string relativePath) {
            string filePath = Path.Combine(Application.streamingAssetsPath.Replace('/', '\\'), relativePath);
            Assert.IsTrue(File.Exists(filePath), $"ReadJsonFile: File does not exist at '{filePath}'.");
            string fileContents = File.ReadAllText(filePath);
            return UnityEngine.JsonUtility.FromJson<T>(fileContents);
        }
    }
}
