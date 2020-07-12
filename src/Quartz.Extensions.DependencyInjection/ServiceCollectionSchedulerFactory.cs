using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Quartz.Impl;
using Quartz.Util;

namespace Quartz
{
    /// <summary>
    /// Wrapper to initialize registered jobs.
    /// </summary>
    internal class ServiceCollectionSchedulerFactory : StdSchedulerFactory
    {
        private readonly IServiceProvider serviceProvider;
        private bool initialized;

        public ServiceCollectionSchedulerFactory(
            IServiceProvider serviceProvider, 
            NameValueCollection properties) : base(properties)
        {
            this.serviceProvider = serviceProvider;

            // check if logging provider configured and let if configure
            serviceProvider.GetService<LoggingProvider>();
        }

        public override async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
        {
            var scheduler = await base.GetScheduler(cancellationToken);
            if (initialized)
            {
                return scheduler;
            }

            foreach (var listener in serviceProvider.GetServices<ISchedulerListener>())
            {
                scheduler.ListenerManager.AddSchedulerListener(listener);
            }

            var jobListeners = serviceProvider.GetServices<IJobListener>();
            foreach (var configuration in serviceProvider.GetServices<JobListenerConfiguration>())
            {
                var listener = jobListeners.First(x => x.GetType() == configuration.ListenerType);
                scheduler.ListenerManager.AddJobListener(listener, configuration.Matchers);
            }

            var triggerListeners = serviceProvider.GetServices<ITriggerListener>();
            foreach (var configuration in serviceProvider.GetServices<TriggerListenerConfiguration>())
            {
                var listener = triggerListeners.First(x => x.GetType() == configuration.ListenerType);
                scheduler.ListenerManager.AddTriggerListener(listener, configuration.Matchers);
            }

            ContainerConfigurationProcessor configurationProcessor = new ContainerConfigurationProcessor(serviceProvider);
            await configurationProcessor.ScheduleJobs(scheduler, cancellationToken);
            initialized = true;
            return scheduler;
        }

        protected override T InstantiateType<T>(Type? implementationType)
        {
            var service = serviceProvider.GetService<T>();
            if (service is null)
            {
                service = ObjectUtils.InstantiateType<T>(implementationType);
            }
            return service;
        }
    }
}