using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;
using FrontServerEmulation.Models;

namespace FrontServerEmulation.Services
{
    public interface IOnKeysEventsSubscribeService
    {
        public Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun, TimeSpan ttl);
        public void SubscribeOnEventFrom(EventKeyNames eventKeysSet);
    }

    public class OnKeysEventsSubscribeService : IOnKeysEventsSubscribeService
    {
        
        private readonly ILogger<OnKeysEventsSubscribeService> _logger;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;
        private readonly IFrontServerEmulationService _front;

        public OnKeysEventsSubscribeService(
            ILogger<OnKeysEventsSubscribeService> logger,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents,
            IFrontServerEmulationService front
            )
        {
            _logger = logger;
            _cache = cache;
            _keyEvents = keyEvents;
            _front = front;
        }

        public async Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun, TimeSpan ttl)
        {
            await _front.FrontServerEmulationCreateGuidField(eventKeyRun, eventFieldRun, ttl); // создаём эмулятором сервера guid поле для ключа "task:run" (и сразу же его читаем)

            string eventGuidFieldRun = await _cache.GetHashedAsync<string>(eventKeyRun, eventFieldRun); //получить guid поле для "task:run"

            return eventGuidFieldRun;
        }

        public void SubscribeOnEventFrom(EventKeyNames eventKeysSet)            
        {
            // ждёт команды с консоли с количеством генерируемых пакетов            
            string eventKey = eventKeysSet.EventKeyFrom;
            KeyEvent eventCmd = eventKeysSet.EventCmd;

            _keyEvents.Subscribe(eventKey, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventCmd)
                {
                    // по получению начинает цикл создания пакетов с задачами
                    _logger.LogInformation("Key {Key} with command {Cmd} was received.", eventKey, cmd);
                    await _front.FrontServerEmulationMain(eventKeysSet);
                }
            });

            string eventKeyCommand = $"Key = {eventKey}, Command = {eventCmd}";
            _logger.LogInformation("You subscribed on event - {EventKey}.", eventKeyCommand);
        }        
    }
}
