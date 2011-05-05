using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Finder.Model;

namespace Finder.Services
{
    public interface IBackend
    {
        void Send(ICommand command);
        void Send<TResponse>(IQuery<TResponse> query, Action<TResponse> reply);
    }

    public class SearchService : IBackend
    {
        private List<SearchDTO> searchResults = new List<SearchDTO>()
                                                    {
                                                        new SearchDTO()
                                                            {
                                                                Id = Guid.NewGuid(),
                                                                ModifiedDate = DateTime.Now,
                                                                Name = "readme.txt",
                                                                Path = "C:\\"
                                                            },
                                                        new SearchDTO
                                                            {
                                                                Id = Guid.NewGuid(),
                                                                ModifiedDate = DateTime.Now,
                                                                Name = "help.doc",
                                                                Path = "C:\\temp\\"
                                                            }
                                                    };

        #region IBackend Members

        public void Send(ICommand command)
        {
            Invoke(command, command);
        }

        public void Send<TResponse>(IQuery<TResponse> query, Action<TResponse> reply)
        {
            Invoke(query, query, reply);
        }

        private readonly IEnumerable<MethodInfo> methods = typeof(SearchService)
           .GetMethods()
           .Where(x => x.Name == "Handle");

        void Invoke(object request, params object[] args)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                //Thread.Sleep(1000); //simulating network

                var requestType = request.GetType();
                var handler = methods.Where(x => requestType.IsAssignableFrom(x.GetParameters().First().ParameterType)).First();

                handler.Invoke(this, args);
            });
        }

        #endregion
    }
}