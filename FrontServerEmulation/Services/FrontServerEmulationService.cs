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
    public interface IFrontServerEmulationService
    {
        public Task FrontServerEmulationCreateGuidField(string eventKeyRun, string eventFieldRun, TimeSpan ttl);
        public Task FrontServerEmulationMain(EventKeyNames eventKeysSet); // все ключи положить в константы
    }

    public class FrontServerEmulationService : IFrontServerEmulationService
    {
        private readonly ILogger<FrontServerEmulationService> _logger;
        private readonly ICacheProviderAsync _cache;

        public FrontServerEmulationService(ILogger<FrontServerEmulationService> logger, ICacheProviderAsync cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task FrontServerEmulationCreateGuidField(string eventKeyRun, string eventFieldRun, TimeSpan ttl)
        {
            string eventGuidFieldRun = Guid.NewGuid().ToString(); // 

            await _cache.SetHashedAsync(eventKeyRun, eventFieldRun, eventGuidFieldRun, ttl); // создаём ключ ("task:run"), на который подписана очередь и в значении передаём имя ключа, содержащего пакет задач

            _logger.LogInformation("Guid Field {0} for key {1} was created and set.", eventGuidFieldRun, eventKeyRun);
        }

        public async Task FrontServerEmulationMain(EventKeyNames eventKeysSet)
        {
            string eventKeyFrom = eventKeysSet.EventKeyFrom;
            string eventFieldFrom = eventKeysSet.EventFieldFrom;
            KeyEvent eventCmd = eventKeysSet.EventCmd;
            string eventKeyBackReadiness = eventKeysSet.EventKeyBackReadiness;
            string eventFieldBack = eventKeysSet.EventFieldBack;
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            string eventFieldFront = eventKeysSet.EventFieldFront;
            TimeSpan ttl = eventKeysSet.Ttl;

            // после получения задачи фронт опрашивает (не подписка) ключ eventKeyBackReadiness и получает список полей - это готовые к работе бэк-сервера
            // дальше фронт выбирает первое поле или случайнее (так надёжнее?) и удаляет его - забирает заявку
            string capturedBackServerGuid = await CaptureBackServerGuid(eventKeyBackReadiness);


            // затем фронт создаёт в ключе кафе (eventKeyFrontGivesTask) поле с захваченным guid бэка, а в значение кладёт имя ключа (тоже guid) пакета задач
            // или кафе не создавать, а сразу идти на ключ (guid бэк-сервера) для получения задачи
            // кафе позволяет стороннему процессу узнать количество серверов за работой - для чего ещё может понадобиться кафе?


            // создаём имя ключа, содержащего пакет задач 
            string taskPackageGuid = Guid.NewGuid().ToString();
            // получаем условия задач по стартовому ключу 
            int tasksCount = await FrontServerFetchConditions(eventKeyFrom, eventFieldFrom);
            // создаём пакет задач (в реальности, опять же, пакет задач положил контроллер)
            Dictionary<string, int> taskPackage = FrontServerCreateTasks(tasksCount);
            // в методе записываем ключ пакета задач в ключ eventKeyFrontGivesTask, а в сам ключ - сами задачи
            // можно положить новые переменные тоже в eventKeysSet
            int inPackageTaskCount = await FrontServerSetTasks(taskPackage, eventKeysSet, taskPackageGuid, capturedBackServerGuid);
            // можно возвращать количество созданных задач и проверять, что не нуль - но это чтобы хоть что-то проверять (или проверять наличие созданных ключей)
            // на создание ключа с пакетом задач уйдёт заметное время, поэтому кафе оправдано - можно будет положить этот ключ в кафе на имя сервера после его создания
            if (inPackageTaskCount > 0)
            {
                //then all rirght
            }

            // бэк подписан на ключ кафе (или на ключ свой guid, если без кафе) и получив сообщение о событии, проверяет своё поле (или сразу берёт задачу)
            // начав работу, бэк кладёт в ключ сообщение о ходе выполнения пакета и/или отдельной задачи (типовой класс - номер цикла, всего цикла, время цикла, всего время и так далее)
            // окончив задачу, бэк должен вернуть поле со своим guid на биржу
            // но сначала проверить сколько там есть свободных серверов - если больше х + какой-то запас, тогда просто раствориться
            // ключ об отчёте выполнения останется на заданное время и потом тоже исчезнет


            //await _cache.SetHashedAsync(eventKeyRun, eventFieldRun, packageGuid, ttl); // создаём ключ ("task:run"), на который подписана очередь и в значении передаём имя ключа, содержащего пакет задач

            //_logger.LogInformation("Key {0}, field {1} with {2} KeyName was set.", eventKeyRun, eventFieldRun, packageGuid);
        }

        private async Task<string> CaptureBackServerGuid(string eventKeyBackReadiness)
        {
            // secede in method            
            // проверить, что ключ вообще существует, это автоматически означает, что в нём есть хоть одно поле - есть свободный сервер
            IDictionary<string, string> taskPackage;
            string capturedBackServerGuid = "empty";
            bool isExistEventKeyBackReadiness = await _cache.KeyExistsAsync(eventKeyBackReadiness);
            if (isExistEventKeyBackReadiness)
            {
                // после получения задачи фронт опрашивает ключ eventKeyBackReadiness и получает список полей
                taskPackage = await _cache.GetHashedAllAsync<string>(eventKeyBackReadiness);

                // дальше фронт выбирает первое поле или случайнее (так надёжнее?) и удаляет его - забирает заявку

                // если удаление получилось, значит, бэк-сервер получен и можно ставить ему задачу
                // если удаление не прошло, фронт (в цикле) опять опрашивает ключ
                // если полей в ключе нет, ключ исчезнет - надо что-то предусмотреть
                // например, не брать задачу, если в списке только один сервер/поле - подождать X секунд и ещё раз опросить ключ
                // после удачного захвата сервера надо дать команду на запуск ещё одного бэка - восстановить свободное количество

                // ----------------- вы находитесь здесь

                foreach (var t in taskPackage)
                {
                    var (backServerGuid, unusedValue) = t; // пока пробуем первое поле
                    capturedBackServerGuid = backServerGuid;
                    // пробуем удалить поле ключа - захватить свободный сервер
                    bool isDeleteSuccess = await _cache.RemoveHashedAsync(eventKeyBackReadiness, backServerGuid);
                    _logger.LogInformation("Background server No: {0} captured successfully = {1}.", backServerGuid, isDeleteSuccess);
                    if (isDeleteSuccess)
                    {
                        return capturedBackServerGuid;
                    }
                }
            }

            return default;
        }

        private async Task<int> FrontServerFetchConditions(string eventKeyFrom, string eventFieldFrom)
        {
            int tasksCount = await _cache.GetHashedAsync<int>(eventKeyFrom, eventFieldFrom); //получить число задач (по этому ключу метод вызвали)

            _logger.LogInformation("TaskCount = {TasksCount} from key {Key} was fetched.", tasksCount, eventKeyFrom);

            if (tasksCount < 3) tasksCount = 3;
            if (tasksCount > 50) tasksCount = 50;

            return tasksCount;
        }

        private async Task<int> FrontServerSetTasks(Dictionary<string, int> taskPackage, EventKeyNames eventKeysSet, string taskPackageGuid, string capturedBackServerGuid)
        {
            int inPackageTaskCount = 0;
            foreach (KeyValuePair<string, int> t in taskPackage)
            {
                (string guid, int cycleCount) = t;
                // записываем пакет задач в ключ пакета задач
                await _cache.SetHashedAsync(taskPackageGuid, guid, cycleCount, eventKeysSet.Ttl);
                inPackageTaskCount++;
                _logger.LogInformation("Key {0}, field {1} with {2} cycles was set.", taskPackageGuid, guid, cycleCount);
            }

            // только после того, как создан ключ с пакетом задач, можно положить этот ключ в поле имени сервера
            // записываем ключ пакета задач в ключ eventKeyFrontGivesTask, а в поле - guid захваченного сервера
            await _cache.SetHashedAsync(eventKeysSet.EventKeyFrontGivesTask, capturedBackServerGuid, taskPackageGuid, eventKeysSet.Ttl);
            // сервер подписан на ключ кафе и пойдёт забирать задачи, на этом тут всё
            return inPackageTaskCount;
        }

        private Dictionary<string, int> FrontServerCreateTasks(int tasksCount)
        {
            Dictionary<string, int> taskPackage = new Dictionary<string, int>();

            for (int i = 0; i < tasksCount; i++)
            {
                string guid = Guid.NewGuid().ToString();
                int cycleCount = Math.Abs(guid.GetHashCode()) % 10;

                if (cycleCount < 3)
                {
                    cycleCount += 3;
                }

                taskPackage.Add(guid, cycleCount);
                _logger.LogInformation("Task {I} from {TasksCount} with ID {Guid} and {CycleCount} cycles was added to taskPackage key.", i, tasksCount, guid, cycleCount);
            }
            return taskPackage;
        }
    }
}
