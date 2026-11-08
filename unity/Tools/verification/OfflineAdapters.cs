// Only compiled by verify.py, never imported as a Unity asset.
// Models use the real production sources with in-memory preferences/resources.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityEngine
{
    public class Sprite { }
    public class TextAsset { public string text; }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public static class Mathf
    {
        public static int Clamp(int value, int min, int max) { return Math.Min(max, Math.Max(min, value)); }
        public static float Clamp(float value, float min, float max) { return Math.Min(max, Math.Max(min, value)); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
    }
    public static class Random
    {
        public struct State { public uint Seed; }
        public static State state { get; set; }
        public static void InitState(int seed) { state = new State { Seed = (uint)seed }; }
        public static float value
        {
            get
            {
                state = new State { Seed = unchecked(state.Seed * 1664525u + 1013904223u) };
                return (state.Seed >> 8) / 16777216f;
            }
        }
    }
    public static class Debug
    {
        public static void LogError(object message) { throw new Exception(Convert.ToString(message)); }
    }
    public static class Resources
    {
        public static string Root;
        public static T Load<T>(string path) where T : class
        {
            if (typeof(T) != typeof(TextAsset)) return null;
            foreach (string ext in new[] { ".csv", ".txt", ".json" })
            {
                string file = Path.Combine(Root, path + ext);
                if (File.Exists(file)) return new TextAsset { text = File.ReadAllText(file) } as T;
            }
            return null;
        }
    }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public static bool HasKey(string key) { return Values.ContainsKey(key); }
        public static int GetInt(string key, int fallback = 0)
        {
            int value; return Values.TryGetValue(key, out value) ? value : fallback;
        }
        public static void SetInt(string key, int value) { Values[key] = value; }
        public static void DeleteKey(string key) { Values.Remove(key); }
        public static void Save() { }
    }
}

internal static class OfflineModelRunner
{
    private static int Main(string[] args)
    {
        try
        {
            UnityEngine.Resources.Root = args[0];
            int count = RegressionChecks.RunAll(Console.WriteLine);
            Console.WriteLine(count + " model checks passed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
