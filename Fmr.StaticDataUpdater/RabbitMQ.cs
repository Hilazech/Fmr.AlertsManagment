using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fmr.StaticDataUpdater
{
    public class RabbitMQ
    {
        ConnectionFactory _factory;
        IConnection _connection;
        IModel _channel;
        EventingBasicConsumer _consumer;
        string _host;
        string _user;
        string _pwd;
        string _topic;
        string _routingKey;
        bool _isConsumer;
        bool _isready;
        int _port = -1;
        public bool IsReady => _isready;
        ILogger _logger;
        bool _isFmrLoggerEnabled;

        public string RoutingKey => _routingKey;

        public event EventHandler<BasicDeliverEventArgs> Received;
        public event EventHandler<ShutdownEventArgs> ModelShutdown;

        public RabbitMQ(bool isFmrLoggerEnabled, ILogger logger, string host, string user, string pwd, string topic, string routingKey, bool isConsumer = false)
        {
            _isFmrLoggerEnabled = isFmrLoggerEnabled;
            _logger = logger;
            var parts = host.Split(':');
            _host = parts[0];
            if (parts.Length > 1)
            {
                if (!string.IsNullOrWhiteSpace(parts[1]) && int.TryParse(parts[1], out int p))
                {
                    _port = p;
                }
            }
            _user = user;
            _pwd = pwd;
            _topic = topic;
            _routingKey = routingKey;
            _isConsumer = isConsumer;
            InitRabbitMQ();
        }

        private void Error(string ex)
        {
            if (_logger == null)
            {
                Console.WriteLine(ex);
                return;
            }
            _logger.LogError(ex);
        }

        private void Error(Exception ex)
        {
            if (_logger == null)
            {
                Console.WriteLine(ex);
                return;
            }
            _logger.LogError(ex, ex.Message);
        }
        private void Info(string msg)
        {
            if (_logger == null)
            {
                Console.WriteLine(msg);
                return;
            }
            _logger.LogInformation(msg);
        }
        private void InitRabbitMQ()
        {
            try
            {
                _factory = new ConnectionFactory { HostName = _host, UserName = _user, Password = _pwd };
                if (_port != -1)
                {
                    _factory.Port = _port;
                }
                _connection = _factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ModelShutdown += ChannelModelShutdown;
                if (_isConsumer)
                {
                    _channel.ExchangeDeclare(_topic, "direct", true);
                
                    _channel.QueueDeclare(_routingKey, true, false, false, null);
                    _channel.QueueBind(_routingKey, _topic, _routingKey);
                    _consumer = new EventingBasicConsumer(_channel);
                    _consumer.Received += ConsumerReceived;
                    _channel.BasicConsume(_routingKey, false, _consumer);
                }
                _isready = _channel.IsOpen;
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public void Publish(string message, string routingKey)
        {
            lock (_locker)
            {
                try
                {
                    var body = Encoding.UTF8.GetBytes(message);
                    _channel.QueueDeclare(_topic, true, false, false, null);
                    _channel.BasicPublish(_topic, $"{_topic}.{routingKey}", null, body);
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }
        }

        private void ConsumerReceived(object sender, BasicDeliverEventArgs e)
        {
            lock (_locker)
            {
                try
                {
                    if (Received != null)
                    {
                        Received(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }
        }

        const int REQUEUWAIT = 5;
        internal void Acknowledge(ulong deliveryTag, bool isSendingAck, bool requeue = true, bool isInvalidMessage = false)
        {
            lock (_locker)
            {
                try
                {
                    if (isSendingAck)
                    {
                        if (!isInvalidMessage)
                            Info($"Acknowledge: Before BasicAck to {deliveryTag}");
                        else
                            Info($"Acknowledge: BasicAck to {deliveryTag} , item is not valid !!!!");

                        _channel.BasicAck(deliveryTag, false);

                        Info($"Acknowledge: after BasicAck to {deliveryTag}");

                    }
                    else
                    {
                        Info($"Acknowledge: BasicReject to {deliveryTag}, requeue:{requeue}");
                        _channel.BasicReject(deliveryTag, requeue);
                        if (requeue)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(REQUEUWAIT));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }
        }

        bool _alreadyShotdown = false;
        readonly static object _locker = new object();
        private void ChannelModelShutdown(object sender, ShutdownEventArgs e)
        {
            Error($"_channel_ModelShutdown: sender:{sender},Cause:{e.Cause}, ClassId:{e.ClassId}, Initiator:{e.Initiator}, MethodId:{e.MethodId}, ReplyCode:{e.ReplyCode}, ReplyText:{e.ReplyText}");
            lock (_locker)
            {
                try
                {
                    if (_alreadyShotdown)
                    {
                        return;
                    }
                    else
                    {
                        _alreadyShotdown = true;
                    }
                    if (ModelShutdown != null)
                    {
                        ModelShutdown(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            }
        }

        public void CloseRabbitMQ()
        {
            try
            {
                if (_isConsumer && _consumer != null)
                {
                    _consumer.Received -= ConsumerReceived;
                    _consumer = null;
                }
                if (_channel != null)
                {
                    _channel.ModelShutdown -= ChannelModelShutdown;
                    try
                    {
                        _channel.Close();
                    }
                    catch { }
                    try
                    {
                        _channel.Dispose();
                    }
                    catch { }
                    _channel = null;
                }
                if (_connection != null)
                {
                    try
                    {
                        _connection.Close();
                    }
                    catch { }
                    try
                    {
                        _connection.Dispose();
                    }
                    catch { }
                    _connection = null;
                }
                if (_factory != null)
                {
                    _factory = null;
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }
    }
}
