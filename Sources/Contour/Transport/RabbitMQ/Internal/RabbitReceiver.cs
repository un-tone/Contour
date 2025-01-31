﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Common.Logging;
using Contour.Configuration;
using Contour.Helpers.CodeContracts;
using Contour.Receiving;
using Contour.Receiving.Consumers;
using Contour.Transport.RabbitMQ.Topology;

namespace Contour.Transport.RabbitMQ.Internal
{
    /// <summary>
    /// The rabbit receiver.
    /// </summary>
    internal class RabbitReceiver : AbstractReceiver
    {
        private readonly ILog logger;
        private readonly RabbitBus bus;
        private readonly IConnectionPool<IRabbitConnection> connectionPool;
        private readonly ConcurrentQueue<IListener> listeners = new ConcurrentQueue<IListener>();
        private readonly RabbitReceiverOptions receiverOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitReceiver"/> class. 
        /// </summary>
        /// <param name="bus">
        /// The bus.
        /// </param>
        /// <param name="configuration">
        /// The configuration.
        /// </param>
        /// <param name="connectionPool">
        /// The connection Pool.
        /// </param>
        public RabbitReceiver(RabbitBus bus, IReceiverConfiguration configuration, IConnectionPool<IRabbitConnection> connectionPool)
            : base(configuration)
        {
            this.bus = bus;
            this.connectionPool = connectionPool;
            this.receiverOptions = (RabbitReceiverOptions)configuration.Options;

            this.logger = LogManager.GetLogger($"{this.GetType().FullName}({this.bus.Endpoint}, {this.Configuration.Label})");
        }

        public event EventHandler<ListenerCreatedEventArgs> ListenerCreated = (sender, args) => { };

        /// <summary>
        /// Gets a value indicating whether is started.
        /// </summary>
        public bool IsStarted { get; private set; }
        
        public override bool IsHealthy => this.IsStarted;

        /// <summary>
        /// Checks if a receiver is able to process messages with <paramref name="label"/> label
        /// </summary>
        /// <param name="label">
        /// The label.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool CanReceive(MessageLabel label)
        {
            this.Configure();
            return this.listeners.Any(l => l.Supports(label));
        }

        /// <summary>
        /// Registers a new consumer of messages with label <paramref name="label"/>
        /// </summary>
        /// <param name="label">
        /// The label.
        /// </param>
        /// <param name="consumer">
        /// The consumer.
        /// </param>
        /// <typeparam name="T">
        /// The message payload type
        /// </typeparam>
        public override void RegisterConsumer<T>(MessageLabel label, IConsumer<T> consumer)
        {
            this.logger.Trace($"Registering consumer of [{typeof(T).Name}] in receiver of label [{label}]");

            foreach (var listener in this.listeners)
            {
                listener.RegisterConsumer(label, consumer, this.Configuration.Validator);
            }
        }

        /// <summary>
        /// Starts the message receiver
        /// </summary>
        public override void Start()
        {
            if (this.IsStarted)
            {
                return;
            }

            this.logger.Trace(m => m("Starting receiver of [{0}].", this.Configuration.Label));

            this.StartListeners();
            this.IsStarted = true;
        }

        /// <summary>
        /// Stops the message receiver
        /// </summary>
        public override void Stop()
        {
            if (!this.IsStarted)
            {
                return;
            }

            this.logger.Trace(m => m("Stopping receiver of [{0}].", this.Configuration.Label));

            this.StopListeners();
            this.IsStarted = false;
        }

        public IListener GetListener(Func<IListener, bool> predicate)
        {
            this.Configure();
            return this.listeners.FirstOrDefault(predicate);
        }

        /// <summary>
        /// Checks if a <paramref name="listener"/> created by some other receiver is compatible with this one
        /// </summary>
        /// <param name="listener">A listener to check</param>
        /// <exception cref="BusConfigurationException">Raises a <see cref="BusConfigurationException"/> error if <paramref name="listener"/> is not compatible</exception>
        public void CheckIfCompatible(IListener listener)
        {
            var listenerOptions = listener.ReceiverOptions;

            // Check only listeners at the same URL and attached to the same listening source (queue); ensure the listener is not one of this receiver's listeners
            var checkList =
                this.listeners.Where(
                    l =>
                        l != listener
                        && l.BrokerUrl == listener.BrokerUrl
                        && l.Endpoint.ListeningSource.Address == listener.Endpoint.ListeningSource.Address);

            foreach (var existingListener in checkList)
            {
                var existingOptions = existingListener.ReceiverOptions;

                Action<Func<RabbitReceiverOptions, object>, string> compareAndThrow = (getOption, optionName) =>
                {
                    if (getOption(existingOptions) != getOption(listenerOptions))
                    {
                        throw
                            new BusConfigurationException(
                                $"Listener on [{listener.Endpoint.ListeningSource}] is not compatible with subscription of [{this.Configuration.Label}] due to option mismatch [{optionName}]");
                    }
                };

                compareAndThrow(o => o.IsAcceptRequired(), "AcceptIsRequired");
                compareAndThrow(o => o.GetParallelismLevel(), "ParallelismLevel");
                compareAndThrow(o => o.GetFailedDeliveryStrategy(), "FailedDeliveryStrategy");
                compareAndThrow(o => o.GetQoS(), "QoS");
            }
        }

        /// <summary>
        /// Fires an event if a listener has been registered
        /// </summary>
        /// <param name="listener">The listener which has been registered in the receiver</param>
        protected virtual void OnListenerCreated(IListener listener)
        {
            this.ListenerCreated(this, new ListenerCreatedEventArgs(listener));
        }

        /// <summary>
        /// Starts the listeners
        /// </summary>
        private void StartListeners()
        {
            this.logger.Trace(m => m("Starting listeners in receiver of [{0}]", this.Configuration.Label));
            this.Configure();

            foreach (var listener in this.listeners)
            {
                listener.StartConsuming();
            }
        }

        private void Configure()
        {
            this.BuildListeners();
        }

        /// <summary>
        /// Stops the listeners
        /// </summary>
        private void StopListeners()
        {
            this.logger.Trace(m => m("Stopping listeners in receiver of [{0}]", this.Configuration.Label));

            IListener listener;
            while (this.listeners.Any() && this.listeners.TryDequeue(out listener))
            {
                try
                {
                    listener.StopConsuming();
                    listener.Dispose();
                }
                catch (Exception ex)
                {
                    this.logger.Error(
                        $"Failed to stop a listener [{listener}] in receiver of [{this.Configuration.Label}] due to {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Builds a set of listeners constructing one listener for each URL in the connection string
        /// </summary>
        private void BuildListeners()
        {   
            this.logger.Trace(
                $"Building listeners of [{this.Configuration.Label}]:\r\n\t{string.Join("\r\n\t", this.receiverOptions.RabbitConnectionString.Select(url => $"Listener({this.Configuration.Label}): URL\t=>\t{url}"))}");

            foreach (var url in this.receiverOptions.RabbitConnectionString)
            {
                var newListener = this.CreateListener(url);

                // There is no need to register another listener at the same URL and for the same listening source (queue); consuming actions can be registered in a single listener
                var listener =
                    this.listeners.FirstOrDefault(
                        l =>
                            l.BrokerUrl == newListener.BrokerUrl &&
                            newListener.Endpoint.ListeningSource.Address == l.Endpoint.ListeningSource.Address);

                if (listener == null)
                {
                    listener = newListener;
                    this.listeners.Enqueue(listener);
                    this.Configuration.ReceiverRegistration?.Invoke(this);
                }
                else
                {
                    // Check if an existing listener can be a substitute for a new one and if so just skip the new listeners
                    this.CheckIfCompatible(newListener);
                    listener = newListener;
                }

                listener.Stopped += (sender, args) => this.OnListenerStopped(args, sender);

                this.OnListenerCreated(listener);
            }
        }

        private void OnListenerStopped(ListenerStoppedEventArgs args, object sender)
        {
            if (args.Reason == OperationStopReason.Regular)
            {
                return;
            }

            this.logger.Warn($"Listener [{sender.GetHashCode()}] has been stopped and will be reenlisted");

            while (true)
            {
                IListener delistedListener;
                if (this.listeners.TryDequeue(out delistedListener))
                {
                    if (sender == delistedListener)
                    {
                        this.logger.Trace($"Listener [{delistedListener.GetHashCode()}] has been delisted");
                        break;
                    }

                    this.listeners.Enqueue(delistedListener);
                }
            }
        }

        private Listener CreateListener(string url)
        {
            var reuseConnectionProperty = this.receiverOptions.GetReuseConnection();
            var reuseConnection = reuseConnectionProperty.HasValue && reuseConnectionProperty.Value;

            var source = new CancellationTokenSource();
            var connection = this.connectionPool.Get(url, reuseConnection, source.Token);
            this.logger.Trace($"Using connection [{connection.Id}] at URL=[{url}] to resolve a listener");

            using (var topologyBuilder = new TopologyBuilder(connection))
            {
                var builder = new SubscriptionEndpointBuilder(this.bus.Endpoint, topologyBuilder, this.Configuration);

                var endpointBuilder = this.Configuration.Options.GetEndpointBuilder();
                Assumes.True(endpointBuilder != null, "EndpointBuilder is null for [{0}].", this.Configuration.Label);

                var endpoint = endpointBuilder.Value(builder);

                var newListener = new Listener(
                    this.bus,
                    connection,
                    endpoint,
                    this.receiverOptions,
                    this.bus.Configuration.ValidatorRegistry);

                return newListener;
            }
        }
    }
}
