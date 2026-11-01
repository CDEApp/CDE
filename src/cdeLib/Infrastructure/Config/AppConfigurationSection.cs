namespace cdeLib.Infrastructure.Config;

public class AppConfigurationSection
{
    public AppConfigurationSection()
    {
        Display = new DisplaySection();
        Hashing = new HashingSection();
        Scan = new ScanSection();
    }

    public DisplaySection Display { get; set; }
    public HashingSection Hashing { get; set; }
    public ScanSection Scan { get; set; }
}