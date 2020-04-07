using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace cdeLib.IO
{
    public interface IDriveInfoService
    {
        DriveInformation GetDriveSpace(string path);
    }

    public class DriveInfoService : IDriveInfoService
    {
        public DriveInformation GetDriveSpace(string path)
        {
            if (IsUncPath(path))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var props = FileSystemProperties.GetProperties(path);
                    return new DriveInformation
                    {
                        TotalBytes = props.TotalBytes,
                        AvailableBytes = props.AvailableBytes
                    };
                }

                Log.Logger.Warning("Get Disk space for unc path currently not supported");
                return new DriveInformation();
            }

            var driveInfo = new DriveInfo(path);
            return new DriveInformation
            {
                AvailableBytes = driveInfo.AvailableFreeSpace,
                TotalBytes = driveInfo.TotalSize
            };
        }

        public bool IsUncPath(string path)
        {
            return path.StartsWith("\\");
        }
    }
}