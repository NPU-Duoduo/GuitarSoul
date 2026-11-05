using NAudio.Wave;

namespace GuitarSoul.Audio.Windows;

internal static class RiffWaveFile
{
    public static string Write(string destination, byte[] bytes, WaveFormat format)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        bool created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                created = true;
                using var writer = new WaveFileWriter(stream, format);
                writer.Write(bytes, 0, bytes.Length);
            }
            File.Move(temporary, destination, overwrite: false);
            return destination;
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
