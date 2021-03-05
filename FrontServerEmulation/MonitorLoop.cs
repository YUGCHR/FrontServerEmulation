﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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
        private readonly ICacheProviderAsync _cache;
        private readonly CancellationToken _cancellationToken;
        private readonly IOnKeysEventsSubscribeService _subscribe;
        private readonly string _guid;

        public MonitorLoop(
            GenerateThisBackServerGuid thisGuid,
            ILogger<MonitorLoop> logger,
            ISettingConstants constant,
            ICacheProviderAsync cache,
            IHostApplicationLifetime applicationLifetime,
            IOnKeysEventsSubscribeService subscribe)
        {
            _logger = logger;
            _cache = cache;
            _constant = constant;
            _subscribe = subscribe;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _guid = thisGuid.ThisBackServerGuid();
        }

        public void StartMonitorLoop()
        {
            _logger.LogInformation("Monitor Loop is starting.");

            // Run a console user input loop in a background thread
            Task.Run(Monitor, _cancellationToken);
        }

        public async Task Monitor()
        {
            // на старте проверить наличие ключа с константами
            // в сервисе констант при старте удалять ключ и создавать новый
            // константы можно делить по полям, а можно общим классом
            // а можно и то и другое
            // если все константы классом, то получится надо везде держать модель - одинаковый код
            // на старте фронт сразу запускает два (взять из constant) бэка - чтобы были
            int serverCount = 1;
            // пока запустить руками, потом в контейнерах - вроде как не получится


            // тут можно проверить наличие минимум двух бэк-серверов
            // а можно перенести в цикл ожидания нажатия клавиши
            // в this эмуляторе подписаться на ключи серверов по префикс:* или по ключу регистрации серверов и ждать регистрации нужного количества

            IDictionary<string, string> tasksList = await _cache.GetHashedAllAsync<string>(_constant.GetEventKeyBackReadiness);
            int tasksListCount = tasksList.Count;
            if (tasksListCount < serverCount)
            {
                _logger.LogInformation("Please, start {0} instances of BackgroundTasksQueue server", serverCount);
            }

            // при старте, если констант вдруг нет, можно подписаться на стандартный общий ключ получения констант
            // когда константы создадутся, они оповестят по подписке, что константы уже есть и тогда по другому стандартному ключу их взять
            // например, constants:fetch - забирать, constants:await - подписка на ожидание
            // в контейнерах будет непросто разбираться, в каком порядке все должны стартовать, редис первым - уже достижение
            // ещё регистрация сервера в ключе может быть, а сам сервер уже (давно) неживой
            // поэтому в значение поля с номером сервера можно класть не тот же самый номер, а ключ для проверки живости сервера, на который тот будет подписан
            // а лучше просто чистый гуид сервера - они к нему применят разные префиксы для подписки из общих констант
            // скажем, сервер будет подписан на динг:гуид, а фронт, получив гуид сервера, подпишется на донг:гуид и сделает запись на динг
            // после этого сервер, увидев запрос, запишет в ключ донг:гуид и фронт узнает, что сервер живой и общается
            // после чего оба ключа надо сразу же удалить

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
            // тут необходимо очистить ключ EventKeyFrontGivesTask, может быть временно, для отладки
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            // можно не проверять наличие ключа, а сразу пробовать удалить, там внутри есть своя проверка
            bool isExistEventKeyFrontGivesTask = await _cache.KeyExistsAsync(eventKeyFrontGivesTask);
            if (isExistEventKeyFrontGivesTask)
            {
                bool isDeleteSuccess = await _cache.RemoveAsync(eventKeyFrontGivesTask);
                _logger.LogInformation("FrontServerEmulation reported - isDeleteSuceess of the key {0} is {1}.", eventKeyFrontGivesTask, isDeleteSuccess);

            }

            // новая версия, теперь это только эмулятор контроллера фронт-сервера

            // множественные контроллеры по каждому запросу (пользователей) создают очередь - каждый создаёт ключ, на который у back-servers подписка, в нём поле со своим номером, а в значении или имя ключа с заданием или само задание            
            // дальше бэк-сервера сами разбирают задания
            // эмулятор сейчас выполняет старт многих пакетов (задаётся пользователем при старте), в работе от одного контроллера будет один пакет задач
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
                TaskDelayTimeInSeconds = _constant.GetTaskDelayTimeInSeconds, // время задержки в секундах для эмулятора счета задачи
                BalanceOfTasksAndProcesses = _constant.GetBalanceOfTasksAndProcesses, // соотношение количества задач и процессов для их выполнения на back-processes-servers (количества задач разделить на это число и сделать столько процессов)
                MaxProcessesCountOnServer = _constant.GetMaxProcessesCountOnServer, // максимальное количество процессов на back-processes-servers (минимальное - 1)
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
                BackServerGuid = _guid, // this server guid
                BackServerPrefixGuid = $"{_constant.GetPrefixBackServer}:{_guid}", // backserver:(this server guid)
                PrefixProcessAdd = _constant.GetPrefixProcessAdd, // process:add
                PrefixProcessCancel = _constant.GetPrefixProcessCancel, // process:cancel
                PrefixProcessCount = _constant.GetPrefixProcessCount, // process:count
                EventFieldFront = _constant.GetEventFieldFront,
                EventKeyBacksTasksProceed = _constant.GetEventKeyBacksTasksProceed, //  ключ выполняемых/выполненных задач                
                EventKeyFromTimeDays = TimeSpan.FromDays(_constant.GetEventKeyFromTimeDays), // срок хранения ключа eventKeyFrom
                EventKeyBackReadinessTimeDays = TimeSpan.FromDays(_constant.GetEventKeyBackReadinessTimeDays), // срок хранения 
                EventKeyFrontGivesTaskTimeDays = TimeSpan.FromDays(_constant.GetEventKeyFrontGivesTaskTimeDays), // срок хранения ключа 
                EventKeyBackServerMainTimeDays = TimeSpan.FromDays(_constant.GetEventKeyBackServerMainTimeDays), // срок хранения ключа 
                EventKeyBackServerAuxiliaryTimeDays = TimeSpan.FromDays(_constant.GetEventKeyBackServerAuxiliaryTimeDays), // срок хранения ключа 
            };
        }
    }
}

