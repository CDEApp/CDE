using Caliburn.Micro;

namespace Finder.ViewModels
{
    public class ShellViewModel : Conductor<IScreen>, IShell
    {
        private readonly StartViewModel startScreen;

        public ShellViewModel(StartViewModel startScreen)
        {
            this.startScreen = startScreen;
        }

        protected override void OnInitialize()
        {
            ActivateItem(startScreen);
            base.OnInitialize();
        }
    }
}