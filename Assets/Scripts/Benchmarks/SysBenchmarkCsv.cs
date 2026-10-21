using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Shared writer. The separator is a comma; floating-point numbers use a decimal point.
public sealed class SysBenchmarkCsv : IDisposable
{
    private readonly StreamWriter writer;
    public string PathName { get; private set; }

    public SysBenchmarkCsv(string fileName, params string[] header)
    {
        string folder = Path.Combine(Application.persistentDataPath, "SYSBenchmarks");
        Directory.CreateDirectory(folder);
        PathName = Path.Combine(folder, fileName);
        writer = new StreamWriter(PathName, false, new UTF8Encoding(false));
        Write(header);
    }

    public void Write(params object[] values)
    {
        var line = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0) line.Append(',');
            object value = values[i];
            string s = value == null ? "" :
                value is bool ? ((bool)value ? "true" : "false") :
                value is IFormattable ? ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture) :
                value.ToString();
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                line.Append('"').Append(s.Replace("\"", "\"\"")).Append('"');
            else line.Append(s);
        }
        writer.WriteLine(line.ToString());
    }

    public void Flush() { writer.Flush(); }
    public void Dispose() { writer.Dispose(); }

    public static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    public static string F(float x) { return x.ToString("R", CultureInfo.InvariantCulture); }

    public static void WriteEnvironment(string testName)
    {
        string folder = Path.Combine(Application.persistentDataPath, "SYSBenchmarks");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, testName + "_environment.txt");
        File.WriteAllText(path,
            "utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\n" +
            "test=" + testName + "\n" +
            "unity=" + Application.unityVersion + "\n" +
            "platform=" + Application.platform + "\n" +
            "cpu=" + SystemInfo.processorType + "\n" +
            "gpu=" + SystemInfo.graphicsDeviceName + "\n" +
            "ram_mb=" + SystemInfo.systemMemorySize + "\n" +
            "resolution=" + Screen.width + "x" + Screen.height + "\n" +
            "quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\n" +
            "vsync=" + QualitySettings.vSyncCount + "\n" +
            "target_frame_rate=" + Application.targetFrameRate + "\n",
            new UTF8Encoding(false));
    }
}
