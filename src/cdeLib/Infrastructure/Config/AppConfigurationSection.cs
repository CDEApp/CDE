namespace cdeLib.Infrastructure.Config;

public class AppConfigurationSection
{
    public AppConfigurationSection()
    {
        Display = new DisplaySection();
        Hashing = new HashingSection();
    }

    public DisplaySection Display { get; set; }
    public HashingSection Hashing { get; set; }
}