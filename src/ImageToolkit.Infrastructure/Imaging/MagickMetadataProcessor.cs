using ImageMagick;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickMetadataProcessor
{
    private static readonly ExifTag[] GpsTags =
    [
        ExifTag.GPSVersionID,
        ExifTag.GPSLatitudeRef,
        ExifTag.GPSLatitude,
        ExifTag.GPSLongitudeRef,
        ExifTag.GPSLongitude,
        ExifTag.GPSAltitudeRef,
        ExifTag.GPSAltitude,
        ExifTag.GPSTimestamp,
        ExifTag.GPSSatellites,
        ExifTag.GPSStatus,
        ExifTag.GPSMeasureMode,
        ExifTag.GPSDOP,
        ExifTag.GPSSpeedRef,
        ExifTag.GPSSpeed,
        ExifTag.GPSTrackRef,
        ExifTag.GPSTrack,
        ExifTag.GPSImgDirectionRef,
        ExifTag.GPSImgDirection,
        ExifTag.GPSMapDatum,
        ExifTag.GPSDestLatitudeRef,
        ExifTag.GPSDestLatitude,
        ExifTag.GPSDestLongitudeRef,
        ExifTag.GPSDestLongitude,
        ExifTag.GPSDestBearingRef,
        ExifTag.GPSDestBearing,
        ExifTag.GPSDestDistanceRef,
        ExifTag.GPSDestDistance,
        ExifTag.GPSProcessingMethod,
        ExifTag.GPSAreaInformation,
        ExifTag.GPSDateStamp,
        ExifTag.GPSDifferential,
        ExifTag.GPSIFDOffset
    ];

    public void ApplyInputOrientation(MagickImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        image.AutoOrient();
        image.Orientation = OrientationType.TopLeft;
        image.GetExifProfile()?.SetValue(ExifTag.Orientation, (ushort)1);
    }

    public void ApplyOutputMetadata(MagickImage image, MetadataOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.PreserveExif)
        {
            image.RemoveProfile("exif");
        }
        else if (!options.PreserveGps)
        {
            var profile = image.GetExifProfile();
            if (profile is not null)
            {
                foreach (var tag in GpsTags)
                {
                    profile.RemoveValue(tag);
                }

                image.SetProfile(profile);
            }

            image.RemoveProfile("8bim");
        }

        if (!options.PreserveIccProfile)
        {
            if (options.ConvertToSrgbWhenIccCannotBePreserved &&
                image.GetColorProfile() is not null)
            {
                image.TransformColorSpace(ColorProfiles.SRGB);
            }

            image.RemoveProfile("icc");
            image.RemoveProfile("icm");
        }
    }
}
