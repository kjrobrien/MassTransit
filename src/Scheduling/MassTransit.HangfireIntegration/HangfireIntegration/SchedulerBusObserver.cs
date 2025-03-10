namespace MassTransit.HangfireIntegration
{
    using System;
    using System.Threading.Tasks;
    using Hangfire;


    /// <summary>
    /// Used to start and stop an in-memory scheduler using Hangfire
    /// </summary>
    class SchedulerBusObserver :
        IBusObserver
    {
        public const string DefaultQueueName = "mt-message-queue";
        readonly Action<BackgroundJobServerOptions>? _configureServer;

        readonly IHangfireComponentResolver _hangfireComponentResolver;
        readonly Uri _schedulerEndpointAddress;
        BackgroundJobServer? _server;

        public SchedulerBusObserver(IHangfireComponentResolver hangfireComponentResolver, Uri schedulerEndpointAddress,
            Action<BackgroundJobServerOptions>? configureServer)
        {
            _hangfireComponentResolver = hangfireComponentResolver;
            _schedulerEndpointAddress = schedulerEndpointAddress;
            _configureServer = configureServer;
        }

        public void PostCreate(IBus bus)
        {
        }

        public void CreateFaulted(Exception exception)
        {
        }

        public Task PreStart(IBus bus)
        {
            return Task.CompletedTask;
        }

        public async Task PostStart(IBus bus, Task<BusReady> busReady)
        {
            var backgroundJobServerOptions = new BackgroundJobServerOptions
            {
                TimeZoneResolver = _hangfireComponentResolver.TimeZoneResolver,
                FilterProvider = _hangfireComponentResolver.JobFilterProvider,
                ServerName = $"MT-Server-{NewId.NextGuid():N}"
            };

            _configureServer?.Invoke(backgroundJobServerOptions);

            backgroundJobServerOptions.Activator = new MassTransitJobActivator(bus);
            backgroundJobServerOptions.Queues = new[] { DefaultQueueName };

            _server = new BackgroundJobServer(
                backgroundJobServerOptions,
                _hangfireComponentResolver.JobStorage,
                _hangfireComponentResolver.BackgroundProcesses);

            LogContext.Debug?.Log("Hangfire Scheduler Starting: {InputAddress}", _schedulerEndpointAddress);

            await busReady.ConfigureAwait(false);

            LogContext.Debug?.Log("Hangfire Scheduler Started: {InputAddress}", _schedulerEndpointAddress);
        }

        public Task StartFaulted(IBus bus, Exception exception)
        {
            return Task.CompletedTask;
        }

        public Task PreStop(IBus bus)
        {
            _server?.SendStop();

            LogContext.Debug?.Log("Hangfire Scheduler Paused: {InputAddress}", _schedulerEndpointAddress);

            return Task.CompletedTask;
        }

        public Task PostStop(IBus bus)
        {
            _server?.Dispose();

            LogContext.Debug?.Log("Hangfire Scheduler Stopped: {InputAddress}", _schedulerEndpointAddress);

            return Task.CompletedTask;
        }

        public Task StopFaulted(IBus bus, Exception exception)
        {
            return Task.CompletedTask;
        }
    }
}
