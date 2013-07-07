﻿using Microsoft.AspNet.SignalR;
using System;
using System.Threading;
using System.Web.Hosting;
using cdeWeb.Hubs;

namespace cdeWeb
{
    public class BackgroundServerTimeTimer : IRegisteredObject
    {
        private Timer taskTimer;
        private IHubContext hub;

        public BackgroundServerTimeTimer()
        {
            HostingEnvironment.RegisterObject(this);

            hub = GlobalHost.ConnectionManager.GetHubContext<ClientPushHub>();
            taskTimer = new Timer(OnTimerElapsed, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        }

        private int count = 1;
        private void OnTimerElapsed(object sender)
        {
            hub.Clients.All.serverTime(
                string.Format("{0} {1}", DateTime.UtcNow.ToString(), count++));
        }

        public void Stop(bool immediate)
        {
            taskTimer.Dispose();

            HostingEnvironment.UnregisterObject(this);
        }
    }
}