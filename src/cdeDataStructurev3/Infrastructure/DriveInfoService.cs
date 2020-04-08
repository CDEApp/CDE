using System.IO;
using System.Runtime.InteropServices;

namespace cdeDataStructure3.Infrastructure
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