using Fmr.Spark.InfoServer.Common;
using Fmr.Spark.InfoServer.Common.Cache;
using Fmr.Spark.InfoServer.Common.Elastic;
using Fmr.Spark.InfoServer.Common.Models;
using Fmr.Spark.InfoServer.Common.Models.Enums;
using Fmr.Spark.InfoServer.Common.Models.StaticData;
using Fmr.Spark.InfoServer.Common.Models.Subscribe;
using Fmr.Spark.InfoServer.Common.RabbitMQ;
using Fmr.Spark.InfoServer.Common.RabbitMQ.Interfaces;
using Fmr.StaticDataUpdater.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace Fmr.StaticDataUpdater
{
    public class StaticDataUpdaterService : IHostedService
    {
        private readonly RabbitMqConsumer _queueConsumer;
        private readonly RabbitMqProducer _queueProducer;
        private readonly ILogger<StaticDataUpdaterService> _logger;
        private RedisCacheService _redisCacheService;
        RabbitMQManager _rabbitManager;
        IConfiguration _config;

        public StaticDataUpdaterService(ILogger<StaticDataUpdaterService> logger, RedisCacheService redisCacheService, IConfiguration config)
        {
            _redisCacheService = redisCacheService;
            _config = config;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("StaticDataUpdaterService is starting...");
                    bool israbbitready = _rabbitManager?.IsReady ?? false;
                    if (!israbbitready)
                    {
                        _logger.LogInformation("StaticDataUpdaterService init connection to rabbit mq ...");
                        israbbitready = InitRabbitMQ();
                    }
                    if (israbbitready)
                        break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connect to RabbitMQ failed. Retrying in 5 seconds...");
                }
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        private bool UpdateData(AlertStaticDataDTO incommingData)
        {
            try
            {
                const string CHART_DATA = "ChartData";
                string stockId = incommingData.Key;
                // Get topic from Redis cache
                var data = _redisCacheService.HashGet(CHART_DATA, stockId, typeof(AlertStaticDataDTO));
                if (incommingData == null || data == null)
                {
                    _logger.LogError($"UpdateData :: error - no data recived, recived data :  {incommingData.ToString() ?? null} , data in redis : {data.ToString() ?? null}");
                    return false;
                }

                _logger.LogInformation($"Get data from redis");

                AlertStaticDataDTO cacheData = (AlertStaticDataDTO)data;

                if (cacheData != null && incommingData.LastUpdate > cacheData.LastUpdate)
                {
                    cacheData.DailyLow = GetMin(cacheData?.DailyLow, incommingData.DailyLow);
                    cacheData.DailyHight = GetMax(cacheData.DailyHight, incommingData.DailyHight);

                    cacheData.YearlyLow = GetMin(cacheData.YearlyLow, incommingData.YearlyLow);

                    cacheData.YearlyHigh = GetMax(cacheData.YearlyHigh, incommingData.YearlyHigh);

                    cacheData.LastClosePrice = incommingData.LastClosePrice;

                    cacheData.LastUpdate = DateTime.UtcNow;
                    try
                    {
                        _redisCacheService.HashSet(CHART_DATA, stockId, cacheData);
                    }catch(Exception ex)
                    {
                        _logger.LogError($"UpdateData :: Error while updating redis data for stock id {stockId}: {ex}");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateData :: Error {ex}");
                return false;
            }
        }

        private double? GetMax(double? currentVal, double? newValue)
        {
            if (currentVal.HasValue && newValue.HasValue)
            {
                return Math.Max(currentVal.Value, newValue.Value);
            }
            else if (currentVal.HasValue)
            {
                return currentVal;
            }
            else if (newValue.HasValue)
            {
                return newValue;
            }
            return null;
        }

        private double? GetMin(double? currentVal, double? newValue)
        {
            if (currentVal.HasValue && newValue.HasValue)
            {
                return Math.Min(currentVal.Value, newValue.Value);
            }
            else if (currentVal.HasValue)
            {
                return currentVal;
            }
            else if (newValue.HasValue)
            {
                return newValue;
            }
            return null;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            CloseRabbitMQ();
            _logger.LogInformation("StaticDataUpdaterService is finish...");
            return Task.CompletedTask;
        }

        private bool InitRabbitMQ()
        {
            try
            {
                RabbitConnectionDetails rabbitConnectionDetails = _config.GetSection("RabbitConnectionDetails").Get<RabbitConnectionDetails>();

                _rabbitManager = new RabbitMQManager(rabbitConnectionDetails.EnableFmrLogger, _logger
                    , CryptographerHelper.Decrypt(rabbitConnectionDetails.RabbitHost)
                    , CryptographerHelper.Decrypt(rabbitConnectionDetails.RabbitUser)
                    , CryptographerHelper.Decrypt(rabbitConnectionDetails.RabbitPassword)
                    , CryptographerHelper.Decrypt(rabbitConnectionDetails.TopicGetAlerts)
                    , CryptographerHelper.Decrypt(rabbitConnectionDetails.TopicPusblishAlerts)
                    , rabbitConnectionDetails.RoutingKey);

                _rabbitManager.Received += RabbitManager_ReceivedMsg;
                _rabbitManager.ModelShutdown += RabbitManager_ModelShutdown;

                _logger.LogInformation($"InitRabbitMQ, is rabbit ready:{_rabbitManager.IsReady}");

                return _rabbitManager.IsReady;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in InitRabbitMQ: {ex}");
                return false;
            }
        }

        private void RabbitManager_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogError($"RabbitMQ channel shutdown: {e.ReplyText} (Code: {e.ReplyCode})");
            // Run reconnection logic in a background task
            Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Attempting to reconnect to RabbitMQ...");
                    bool isReady = InitRabbitMQ();
                    while (!isReady)
                    {
                        isReady = InitRabbitMQ();
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                    _logger.LogInformation("Reconnect to RabbitMQ succeeded...");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in RabbitMQ reconnection task.");
                }
            });
        }


        private void RabbitManager_ReceivedMsg(object sender, BasicDeliverEventArgs e)
        {
            string routingKey = _rabbitManager.GetConsumerRoutingKey();
            string message = null;
            AlertStaticDataDTO deserializedMessage = null;
            try
            {
                if (string.IsNullOrWhiteSpace(e.RoutingKey) || !routingKey.Equals(e.RoutingKey))
                {
                    _logger.LogError($"_rabbitManager_Received failed: routingkey isn't valid, routingKey:{e.RoutingKey}, deliveryTag:{e.DeliveryTag}");
                    _rabbitManager.Acknowledge(e.DeliveryTag, true, false, true);
                    return;
                }

                message = Encoding.UTF8.GetString(e.Body.ToArray());
                if (string.IsNullOrWhiteSpace(message))
                {
                    _logger.LogError($"_rabbitManager_Received failed: could not getting data from rabbitmq, routingKey:{e.RoutingKey}, deliveryTag:{e.DeliveryTag}");
                    _rabbitManager.Acknowledge(e.DeliveryTag, false, false, true);
                    return;
                }

                deserializedMessage = JsonConvert.DeserializeObject<AlertStaticDataDTO>(message);
                if (deserializedMessage == null)
                {
                    _logger.LogError($"_rabbitManager_Received failed: could not deserializing data from rabbitmq, routingKey:{e.RoutingKey}, deliveryTag:{e.DeliveryTag}");
                    _rabbitManager.Acknowledge(e.DeliveryTag, false, false, true);
                    return;
                }
                _logger.LogInformation($"_rabbitManager_ReceivedMsg : start handle request for stock id : {deserializedMessage.Key}");

                // update the redis with this massage
                if (UpdateData(deserializedMessage))
                {
                    _logger.LogInformation($"_rabbitManager_ReceivedMsg : finish handle request for stock id : {deserializedMessage.Key}");
                    _rabbitManager.Acknowledge(e.DeliveryTag, true,false,false);
                }
                else
                {
                    _logger.LogError($"_rabbitManager_ReceivedMsg : failed to update data for stock id : {deserializedMessage.Key}");
                    _rabbitManager.Acknowledge(e.DeliveryTag, false, true, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"_rabbitManager_ReceivedMsg :: Error {ex}, routingKey:{routingKey}, deliveryTag:{e.DeliveryTag}, message:{message}");
                _rabbitManager.Acknowledge(e.DeliveryTag, false,true,true);
            }
        }

        private void CloseRabbitMQ()
        {
            try
            {
                if (_rabbitManager != null)
                {
                    _rabbitManager.Received -= RabbitManager_ReceivedMsg;
                    _rabbitManager.ModelShutdown -= RabbitManager_ModelShutdown;
                    _rabbitManager.CloseRabbitMQ();
                    _rabbitManager = null;
                    _logger.LogInformation($"rabbitmq closed..");

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"CloseRabbitMQ :: Error {ex}");
            }
        }
    }
}
