using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace cdeWin
{
    public delegate void EventAction();

    public interface IPresenter
    {
        void Display();
    }

    public interface IView : IDisposable
    {
        DialogResult ShowDialog();
        void Close();
    }

    // from http://cre8ivethought.com/blog/2009/12/19/using-conventions-with-passive-view
    public abstract class Presenter<TView> where TView : class, IView
    {
        protected Presenter(TView view)
        {
            if (!IsTestMode(view))
            {
                HookUpViewEvents(view);
            }
        }

        private bool IsTestMode(TView view)
        {
            // In past Rhino.Mocks in GetViewEvents() returned method names, not sure how.
            // Now the result from mock view is no methods and this IPresenter wiring fails.
            // To fix we are detecting unit test mode.
            return view.GetType().FullName.Contains("Proxy");
        }

        private void HookUpViewEvents(TView view)
        {
            var viewDefinedEvents = GetViewDefinedEvents();
            var viewEvents = GetViewEvents(view, viewDefinedEvents);
            var presenterEventHandlers = GetPresenterEventHandlers(viewDefinedEvents, this);

            foreach (var viewDefinedEvent in viewDefinedEvents)
            {
                var eventInfo = viewEvents[viewDefinedEvent];
                var eventName = viewDefinedEvent.Substring(2);
                MethodInfo presenterMethodInfo;
                if (!presenterEventHandlers.TryGetValue(eventName, out presenterMethodInfo))
                {
                    throw new Exception(string.Format(
                        "\n\nThere is no event handler for event '{0}' on presenter '{1}' expected '{2}'\n\n",
                        eventInfo.Name, GetType().FullName, eventName));
                }
                var newDelegate = Delegate.CreateDelegate(typeof(EventAction), this, presenterMethodInfo);
                eventInfo.AddEventHandler(view, newDelegate);
            }
        }

        private static List<string> GetViewDefinedEvents()
        {
            return typeof(TView).GetEvents().Select(x => x.Name).ToList();
        }

        private static IDictionary<string, EventInfo> GetViewEvents(TView view, ICollection<string> actionProperties)
        {
            return view
                .GetType()
                .GetEvents()
                .Where(x => Contains(actionProperties, x))
                .ToList()
                .Select(x => new KeyValuePair<string, EventInfo>(x.Name, x))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private static bool Contains(ICollection<string> actionProperties, EventInfo x)
        {
            return actionProperties.Contains(x.Name);
        }

        private static IDictionary<string, MethodInfo> GetPresenterEventHandlers<TPresenter>(ICollection<string> actionProperties, TPresenter presenter)
        {
            return presenter
                .GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => Contains(actionProperties, x))
                .ToList()
                .Select(x => new KeyValuePair<string, MethodInfo>(x.Name, x))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private static bool Contains(ICollection<string> actionProperties, MethodInfo x)
        {
            return actionProperties.Contains(string.Format("On{0}", x.Name));
        }
    }

}