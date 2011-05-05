using System;
using Caliburn.Micro;
using Finder.Services;
//using System.ComponentModel.Composition;

namespace Finder.Model
{
    public class CommandResult : IResult
    {
        readonly ICommand command;

        //TODO Autofac to inject.
        //[Import]
        public IBackend Backend { get; set; }

        public CommandResult(ICommand command)
        {
            this.command = command;
        }

        public void Execute(ActionExecutionContext context)
        {
            Backend.Send(command);
            Completed(this, new ResultCompletionEventArgs());
        }

        public event EventHandler<ResultCompletionEventArgs> Completed = delegate { };
    }
}