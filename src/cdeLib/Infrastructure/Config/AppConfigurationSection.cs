namespace cdeLib.Infrastructure.Config
{
    public class AppConfigurationSection
    {
        public AppConfigurationSection()
        {
            this.Display = new DisplaySection();
            this.Hashing = new HashingSection();
        }

        public DisplaySection Display { get; set; }
        public HashingSection Hashing { get; set; }
    }
}