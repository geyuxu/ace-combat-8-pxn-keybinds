using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Windows.Media.Imaging;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

internal sealed record PhotoPoint(string Type, int Index, double X, double Y)
{
    public FlightInput Input => new(Type, Index);
}

internal sealed record ControllerPhoto(string File, List<PhotoPoint> Points);

internal static class ControllerPhotos
{
    private static string Profile(uint id) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AceCombat8Keybinds", "photos", $"{id:X8}.json");

    public static ControllerPhoto? Load(uint id)
    {
        try
        {
            var file = Profile(id);
            if (!File.Exists(file)) return null;
            var photo = JsonSerializer.Deserialize<ControllerPhoto>(File.ReadAllText(file));
            return photo != null && File.Exists(photo.File) ? photo : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (JsonException) { return null; }
    }

    public static BitmapImage ReadImage(string path)
    {
        var image = new BitmapImage();
        using var stream = File.OpenRead(path);
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 1200;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static bool TryReadImage(string path, [NotNullWhen(true)] out BitmapImage? image)
    {
        try { image = ReadImage(path); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            image = null;
            return false;
        }
    }

    public static void Save(uint id, ControllerPhoto photo)
    {
        string path = Profile(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(photo));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Reset(uint id) => File.Delete(Profile(id));
}
