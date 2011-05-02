using Autofac;
using Caliburn.Micro;
using Finder.Infrastructure;
using Finder.ViewModels;

namespace Finder
{
    //Bootstrapper<ShellViewModel>
    public class FinderBootStrapper : TypedAutofacBootStrapper<ShellViewModel>
    {
        private readonly ILog _logger = LogManager.GetLog(typeof (FinderBootStrapper));

        static FinderBootStrapper()
        {
            LogManager.GetLog = type => new DebugLogger(type);
        }

        protected override void ConfigureContainer(ContainerBuilder builder)
        {
            _logger.Info("Configuring Container.");
            base.ConfigureContainer(builder);

            //  good place to register application types or custom modules
            //builder.RegisterModule<RegistrationModule>();
        }
    }
}