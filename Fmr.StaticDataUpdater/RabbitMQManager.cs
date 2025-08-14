using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fmr.Spark.InfoServer.Common.RabbitMQ;

namespace Fmr.StaticDataUpdater
{
    public class RabbitMQManager
    {
        bool _isready;
        public bool IsReady => _isready;

        public event EventHandler<BasicDeliverEventArgs> Received;
        public event EventHandler<ShutdownEventArgs> ModelShutdown;

        RabbitMQ _listenr;
        RabbitMQ _publisher;
        ILogger _logger;
        bool _isFmrLoggerEnabled;

        public RabbitMQManager(bool isFmrLoggerEnabled, ILogger logger, string host, string user, string pwd, string topicGetalerts, string topicPublishAlerts, string routingKey)
        {
            _isFmrLoggerEnabled = isFmrLoggerEnabled;
            _logger = logger;
            try
            {
                _listenr = new RabbitMQ(isFmrLoggerEnabled, _logger, host, user, pwd, topicGetalerts, routingKey, true);
                _listenr.Received += ListenerReceived;
                _listenr.ModelShutdown += ListenerModelShutdown;
                _isready = _listenr.IsReady;

                _logger.LogInformation($"RabbitMQManager, _isReady:{_isready}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in create RabbitMq connection:{ex}");
            }
        }


        public string GetConsumerRoutingKey() => _listenr.RoutingKey;

        readonly static object _locker = new object();
        private void ListenerReceived(object sender, BasicDeliverEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    int threadId = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine($"Processing on Thread ID: {threadId}");
                    if (Received != null)
                    {
                        Received(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    lock (_locker)
                    {
                        _logger.LogError($"_listenr_Received :: Error {ex}");
                    }
                }
            });
        }

        private void PublisherModelShutdown(object sender, ShutdownEventArgs e)
        {
            try
            {
                if (ModelShutdown != null)
                {
                    ModelShutdown(sender, e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"_publisher_ModelShutdown :: Error {ex}");
            }
        }

        private void ListenerModelShutdown(object sender, ShutdownEventArgs e)
        {
            try
            {
                if (ModelShutdown != null)
                {
                    ModelShutdown(sender, e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"_listenr_ModelShutdown :: Error {ex}");
            }
        }

        public void Acknowledge(ulong deliveryTag, bool isSendingAck, bool requeue = true, bool isInvalidMessage = false)
        {
            try
            {
                _listenr.Acknowledge(deliveryTag, isSendingAck, requeue, isInvalidMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"RabitMQ Acknowledge :: Error {ex}");
            }
        }

        public void CloseRabbitMQ()
        {
            try
            {
                if (_listenr != null)
                {
                    _isready = false;
                    _listenr.Received -= ListenerReceived;
                    _listenr.ModelShutdown -= ListenerModelShutdown;
                    _listenr.CloseRabbitMQ();
                    _listenr = null;
                }
                if (_publisher != null)
                {
                    _isready = false;
                    _publisher.ModelShutdown -= PublisherModelShutdown;
                    _publisher.CloseRabbitMQ();
                    _publisher = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Close RabbitMQ :: Error {ex}");
            }
        }
    }

}

