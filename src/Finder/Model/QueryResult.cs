using System;
using Caliburn.Micro;
using Finder.Services;

namespace Finder.Model
{
    public class QueryResult<TResponse> : IResult
    {
        private readonly IQuery<TResponse> query;
        private IBackend backend;

        public QueryResult(IQuery<TResponse> query)
        {
            this.query = query;
        }

        //TODO Get autofac to inject this, instead of doing service locator.
        //[Import]
        public IBackend Backend
        {
            get { 
                
                var back = IoC.Get<IBackend>();
                return back;
            }
            set { this.backend = value; }
        }
            
        public TResponse Response { get; set; }

        public void Execute(ActionExecutionContext context)
        {
            Backend.Send(query, response=>
                                    {
                                        Response = response;
                                        Caliburn.Micro.Execute.OnUIThread(() => Completed(this,new ResultCompletionEventArgs()));
                                    });
        }

        public event EventHandler<ResultCompletionEventArgs> Completed = delegate { };
    }
    
}