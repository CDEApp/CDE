using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Autofac;
using Caliburn.Micro;
using Finder.Model;
using Finder.Services;
using IContainer = Autofac.IContainer;

namespace Finder.Infrastructure
{
    public class TypedAutofacBootStrapper<TRootViewModel> : Bootstrapper<TRootViewModel>
    {
        #region Fields
        private readonly ILog _logger = LogManager.GetLog(typeof(TypedAutofacBootStrapper<>));
        private IContainer _container;
        #endregion

        #region Properties
        protected IContainer Container
        {
            get { return _container; }
        }
        #endregion

        #region Overrides
        protected override void Configure()
        { //  configure container
            var builder = new ContainerBuilder();

            //  register view models
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                //  must be a type that ends with ViewModel
              .Where(type => type.Name.EndsWith("ViewModel"))
                //  must be in a namespace ending with ViewModels
              .Where(type => !(string.IsNullOrWhiteSpace(type.Namespace)) && type.Namespace.EndsWith("ViewModels"))
                //  must implement INotifyPropertyChanged (deriving from PropertyChangedBase will statisfy this)
              .Where(type => type.GetInterface(typeof(INotifyPropertyChanged).Name) != null)
                //  registered as self
              .AsSelf()
                //  always create a new one
              .InstancePerDependency();

            //  register views
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                //  must be a type that ends with View
              .Where(type => type.Name.EndsWith("View"))
                //  must be in a namespace that ends in Views
              .Where(type => !(string.IsNullOrWhiteSpace(type.Namespace)) && type.Namespace.EndsWith("Views"))
                //  registered as self, not as interface
              .AsSelf()
                //  always create a new one
              .InstancePerDependency();

            //  register the single window manager for this container
            builder.Register<IWindowManager>(c => new WindowManager()).InstancePerLifetimeScope();
            //  register the single event aggregator for this container
            builder.Register<IEventAggregator>(c => new EventAggregator()).InstancePerLifetimeScope();
            AutoProperties(builder);
            ConfigureContainer(builder);

            _container = builder.Build();
        }

        private void AutoProperties(ContainerBuilder builder)
        {
            builder.RegisterType<SearchService>().As<IBackend>().InstancePerLifetimeScope();
            //builder.RegisterType<QueryResult<object>>
            //QueryResult<TResponse>(query);
        }
        
        protected override object GetInstance(Type serviceType, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (Container.IsRegistered(serviceType))
                    return Container.Resolve(serviceType);
            }
            else
            {
                if (Container.IsRegisteredWithName(key, serviceType))
                    return Container.ResolveNamed(key, serviceType);
            }
            throw new Exception(string.Format("Could not locate any instances of contract {0}.", key ?? serviceType.Name));
        }
        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return Container.Resolve(typeof(IEnumerable<>).MakeGenericType(serviceType)) as IEnumerable<object>;
        }
        protected override void BuildUp(object instance)
        {
            Container.InjectProperties(instance);
        }
        #endregion

        protected virtual void ConfigureContainer(ContainerBuilder builder)
        {
        }
    }
}