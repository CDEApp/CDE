using System.Runtime.InteropServices;
using NUnit.Framework;
using Shouldly;

namespace cdeLibTest.IO.DriveInfoService;

public class CanGetDriveInfo
{
    private readonly cdeLib.IO.DriveInfoService _driveInfoService;

    public CanGetDriveInfo()
    {
        _driveInfoService = new cdeLib.IO.DriveInfoService();
    }

    [Test]
    public void GetDriveInfoForLocalDriveOnWindows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var result = _driveInfoService.GetDriveSpace("C:\\");
            result.TotalBytes.ShouldNotBeNull();
            result.TotalBytes.Value.ShouldBeGreaterThan(0);

            result.AvailableBytes.ShouldNotBeNull();
            result.AvailableBytes.Value.ShouldBeGreaterThan(0);
        }
    }
}