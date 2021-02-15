using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FrontServerEmulation.Services;
using FrontServerEmulation.Models;

namespace FrontServerEmulation
{
    public class MonitorLoop
    {
        private readonly ILogger<MonitorLoop> _logger;
        private readonly ISettingConstants _constant;
        private readonly CancellationToken _cancellationToken;
        private readonly IOnKeysEventsSubscribeService _subscribe;

        public MonitorLoop(
            ILogger<MonitorLoop> logger,
            ISettingConstants constant,
            IHostApplicationLifetime applicationLifetime,
            IOnKeysEventsSubscribeService subscribe)
        {
            _logger = logger;
            _constant = constant;
            _subscribe = subscribe;
            _cancellationToken = applicationLifetime.ApplicationStopping;
        }

        public void StartMonitorLoop()
        {
            _logger.LogInformation("Monitor Loop is starting.");

            // Run a console user input loop in a background thread
            Task.Run(Monitor, _cancellationToken);
        }

        public async Task Monitor()
        {
            // To start tasks batch enter from Redis console the command - hset subscribeOnFrom tasks:count 30 (where 30 is tasks count - from 10 to 50)            
            

            // на старте фронт сразу запускает два (взять из constant) бэка - чтобы были
            int serverCount = 2;
            // пока запустить руками, потом в контейнерах
            _logger.LogInformation("Please, start {0} instances of BackgroundTasksQueue server", serverCount);

            // имена ключей eventKeyStart (биржа труда) и eventKeyRun (кафе выдачи задач) фронт передаёт бэку
            // биржа труда - key event back processes servers readiness list - eventKeyBackReadiness
            // кафе выдачи задач - key event front server gives task package - eventKeyFrontGivesTask
            // пока имена взять из констант
            

            EventKeyNames eventKeysSet = new EventKeyNames
            {
                EventKeyFrom = _constant.GetEventKeyFrom, // "subscribeOnFrom" - ключ для подписки на команду запуска эмулятора сервера
                EventFieldFrom = _constant.GetEventFieldFrom, // "count" - поле для подписки на команду запуска эмулятора сервера
                EventCmd = KeyEvent.HashSet,
                EventKeyBackReadiness = _constant.GetEventKeyBackReadiness, // биржа труда
                EventFieldBack = _constant.GetEventFieldBack,
                EventKeyFrontGivesTask = _constant.GetEventKeyFrontGivesTask, // кафе выдачи задач
                EventFieldFront = _constant.GetEventFieldFront,
                Ttl = TimeSpan.FromDays(_constant.GetKeyFromTimeDays) // срок хранения ключа eventKeyFrom
            };

            // бэк после старта кладёт в ключ eventKeyStart поле со своим сгенерированным guid - это заявление на биржу, что готов трудиться
            

            
            
            




            _subscribe.SubscribeOnEventFrom(eventKeysSet);

            while (IsCancellationNotYet())
            {
                var keyStroke = Console.ReadKey();

                if (keyStroke.Key == ConsoleKey.W)
                {
                    _logger.LogInformation("ConsoleKey was received {KeyStroke}.", keyStroke.Key);
                }
            }
        }

        private bool IsCancellationNotYet()
        {
            return !_cancellationToken.IsCancellationRequested; // add special key from Redis?
        }
    }
}

