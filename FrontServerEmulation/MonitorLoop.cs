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
            // на старте фронт сразу запускает два (взять из constant) бэка - чтобы были
            int serverCount = 2;
            // пока запустить руками, потом в контейнерах
            _logger.LogInformation("Please, start {0} instances of BackgroundTasksQueue server", serverCount);

            // тут можно проверить наличие минимум двух бэк-серверов
            // а можно перенести в цикл ожидания нажатия клавиши

            // имена ключей eventKeyStart (биржа труда) и eventKeyRun (кафе выдачи задач) фронт передаёт бэку
            // биржа труда - key event back processes servers readiness list - eventKeyBackReadiness
            // кафе выдачи задач - key event front server gives task package - eventKeyFrontGivesTask
            // пока имена взять из констант

            // добавить дополнительный ключ с количеством пакетов задач
            // в стартовом ключе в значении указывать задержку -
            // положительная - в секундах,
            // 0 - без задержки,
            // отрицательная - случайная задержка, но не более значения в сек

            // generate random integers from 5 to 10
            Random rand = new Random();
            rand.Next(5, 11);


            // сделать два сообщения в консоли - подсказки, как запустить эмулятор
            // To start tasks batch enter from Redis console the command - hset subscribeOnFrom tasks:count 30 (where 30 is tasks count - from 10 to 50)            

            EventKeyNames eventKeysSet = InitialiseEventKeyNames();


            // новая версия, теперь это только эмулятор контроллера фронт-сервера

            // множественные контроллеры по каждому запросу (пользователей) создают очередь - каждый создаёт ключ, на который у back-servers подписка, в нём поле со своим номером, а в значении или имя ключа с заданием или само задание            
            // дальше бэк-сервера сами разбирают задания

            // все бэк-сервера подписаны на базовый ключ и получив сообщение по подписке, стараются взять задание - у кого получилось удалить ключ, тот и взял
            // у контроллера остаётся базовый ключ, который он вернёт пользователю и тот потом сможет контролировать ход выполнения задания
            // тогда лишняя сущность диспетчера не нужна, но если задание упадёт, восстановить его будет некому


            // второй вариант - диспетчер собирает всё задания (от множества контроллеров) и ставит у себя в очередь, потом берёт по очереди бэк-сервера и выдаёт им задания
            // named it controllers-dispatcher-queue-back-servers
            // тогда диспетчер по подписке на ключ сервера знает о ходе выполнения и если сообщения прекратятся, но ещё не 100%, сможет перезапустить задачу
            // можно усреднять время выполнения каждого этапа задания и хранить предполагаемое полное время выполнения, а по его истечению принимать какие-то меры


            // первый вариант позволяет его потом дополнить надстройкой диспетчера, которому надо будет следить только за целостностью бэк-серверов и давать команду на восстановление ключа задачи (и, возможно, удаление зависшего сервера)


            // план работы эмулятора
            // ждёт команды с консоли с количеством генерируемых пакетов
            // по получению начинает цикл создания пакетов с задачами
            // первый гуид - главный номер задания, второй - ключ доступа к заданию (или один и тот же номер)
            // при создании пакета сначала создаётся пакет задач в ключе, а потом этот номер создаётся в виде поля в подписном ключе
            // собственно, это пока всё (потом можно добавить случайную задержку между генерацией отдельных пакетов)


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

        private EventKeyNames InitialiseEventKeyNames()
        {
            return new EventKeyNames
            {
                EventKeyFrom = _constant.GetEventKeyFrom, // "subscribeOnFrom" - ключ для подписки на команду запуска эмулятора сервера
                EventFieldFrom = _constant.GetEventFieldFrom, // "count" - поле для подписки на команду запуска эмулятора сервера
                EventCmd = KeyEvent.HashSet,
                EventKeyBackReadiness = _constant.GetEventKeyBackReadiness, // ключ регистрации серверов
                EventFieldBack = _constant.GetEventFieldBack,
                EventKeyFrontGivesTask = _constant.GetEventKeyFrontGivesTask, // кафе выдачи задач
                PrefixRequest = _constant.GetPrefixRequest, // request:guid
                PrefixPackage = _constant.GetPrefixPackage, // package:guid
                PrefixTask = _constant.GetPrefixTask, // task:guid
                PrefixBackServer = _constant.GetPrefixBackServer, // backserver:guid
                EventFieldFront = _constant.GetEventFieldFront,
                EventKeyBacksTasksProceed = _constant.GetEventKeyBacksTasksProceed, //  ключ выполняемых/выполненных задач                
                Ttl = TimeSpan.FromDays(_constant.GetKeyFromTimeDays) // срок хранения ключа eventKeyFrom
            };
        }
    }
}

