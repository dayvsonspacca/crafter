using System.Globalization;

namespace Crafter.Storage;

internal static class JsonFile
{
    public static void Write(string file, string json)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(file))!;
        Directory.CreateDirectory(folder);
        var draft = Path.Combine(folder, $".{Path.GetFileName(file)}.{Guid.NewGuid():N}.draft");

        try
        {
            using (var stream = new FileStream(draft, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(draft, file, overwrite: true);
        }
        finally
        {
            File.Delete(draft);
        }
    }

    public static void SetAside(string file)
    {
        var aside = $"{file}.{DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}.unreadable";

        try
        {
            File.Move(file, aside);
        }
        catch (Exception stuck) when (stuck is IOException or UnauthorizedAccessException)
        {
        }
    }
}
