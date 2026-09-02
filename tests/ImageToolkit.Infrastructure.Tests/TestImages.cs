using ImageMagick;

namespace ImageToolkit.Infrastructure.Tests;

internal static class TestImages
{
    public static string CreateJpeg(
        string directory,
        string fileName = "source.jpg",
        uint width = 400,
        uint height = 300,
        OrientationType orientation = OrientationType.TopLeft,
        bool withExif = false)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var image = new MagickImage(MagickColors.CornflowerBlue, width, height)
        {
            Format = MagickFormat.Jpeg,
            Orientation = orientation,
            Quality = 92
        };

        if (withExif || orientation != OrientationType.TopLeft)
        {
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.Orientation, (ushort)orientation);

            if (withExif)
            {
                profile.SetValue(ExifTag.DateTimeOriginal, "2026:09:02 12:00:00");
                profile.SetValue(ExifTag.GPSLatitudeRef, "N");
                profile.SetValue(
                    ExifTag.GPSLatitude,
                    [new Rational(31), new Rational(12), new Rational(0)]);
            }

            image.SetProfile(profile);
        }

        image.Write(path);
        return path;
    }

    public static string CreateTransparentPng(
        string directory,
        string fileName = "source.png")
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var image = new MagickImage(MagickColors.Transparent, 200, 100)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }
}
