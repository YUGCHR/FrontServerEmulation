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

            await _cache.SetHashedAsync<string>(eventKeyRun, eventFieldRun, eventGuidFieldRun, ttl); // создаём ключ ("task:run"), на который подписана очередь и в значении передаём имя ключа, содержащего пакет задач

            _logger.LogInformation("Guid Field {0} for key {1} was created and set.", eventGuidFieldRun, eventKeyRun);
        }

        public async Task FrontServerEmulationMain(EventKeyNames eventKeysSet)
        {
            // получаем условия задач по стартовому ключу 
            int tasksPackegesCount = await FrontServerFetchConditions(eventKeysSet.EventKeyFrom, eventKeysSet.EventFieldFrom);

            // начинаем цикл создания и размещения пакетов задач
            _logger.LogInformation(" - Creation cycle of key EventKeyFrontGivesTask fields started with {1} steps.", tasksPackegesCount);

            for (int i = 0; i < tasksPackegesCount; i++)
            {
                // guid - главный номер задания, используемый в дальнейшем для доступа к результатам
                string taskPackageGuid = Guid.NewGuid().ToString();
                int tasksCount = Math.Abs(taskPackageGuid.GetHashCode()) % 10; // просто (псевдо)случайное число
                if (tasksCount < 3)
                {
                    tasksCount += 3;
                }
                // создаём пакет задач (в реальности, опять же, пакет задач положил отдельный контроллер)
                Dictionary<string, int> taskPackage = FrontServerCreateTasks(tasksCount);

                // при создании пакета сначала создаётся пакет задач в ключе, а потом этот номер создаётся в виде поля в подписном ключе

                // создаем ключ taskPackageGuid и кладем в него пакет 
                // записываем ключ taskPackageGuid пакета задач в поле ключа eventKeyFrontGivesTask и в значение ключа - тоже taskPackageGuid
                int inPackageTaskCount = await FrontServerSetTasks(taskPackage, eventKeysSet, taskPackageGuid);
                // можно возвращать количество созданных задач и проверять, что не нуль - но это чтобы хоть что-то проверять (или проверять наличие созданных ключей)
                // на создание ключа с пакетом задач уйдёт заметное время, поэтому промежуточный ключ оправдан (наверное)
            }
        }

        private async Task<int> FrontServerFetchConditions(string eventKeyFrom, string eventFieldFrom)
        {
            //получить число пакетов задач (по этому ключу метод вызвали)
            int tasksCount = await _cache.GetHashedAsync<int>(eventKeyFrom, eventFieldFrom);

            _logger.LogInformation("TaskCount = {TasksCount} from key {Key} was fetched.", tasksCount, eventKeyFrom);

            if (tasksCount < 3) tasksCount = 3;
            if (tasksCount > 50) tasksCount = 50;

            return tasksCount;
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
                _logger.LogInformation("Task {I} from {TasksCount} with ID {Guid} and {CycleCount} cycles was added to Dictionary.", i, tasksCount, guid, cycleCount);
            }
            return taskPackage;
        }

        private async Task<int> FrontServerSetTasks(Dictionary<string, int> taskPackage, EventKeyNames eventKeysSet, string taskPackageGuid)
        {
            int inPackageTaskCount = 0;
            foreach (KeyValuePair<string, int> t in taskPackage)
            {
                (string guid, int cycleCount) = t;
                // записываем пакет задач в ключ пакета задач
                await _cache.SetHashedAsync(taskPackageGuid, guid, cycleCount, eventKeysSet.Ttl);
                inPackageTaskCount++;
                _logger.LogInformation("TaskPackage No. {0}, with Task No. {1} with {2} cycles was set.", taskPackageGuid, guid, cycleCount);
            }

            // только после того, как создан ключ с пакетом задач, можно положить этот ключ в подписной ключ eventKeyFrontGivesTask
            // записываем ключ пакета задач в ключ eventKeyFrontGivesTask, а в поле и в значение - ключ пакета задач
            await _cache.SetHashedAsync(eventKeysSet.EventKeyFrontGivesTask, taskPackageGuid, taskPackageGuid, eventKeysSet.Ttl);
            _logger.LogInformation(" --- Key EventKeyFrontGivesTask with TaskPackage No. {1} was created.", taskPackageGuid);
            // сервера подписаны на ключ eventKeyFrontGivesTask и пойдут забирать задачи, на этом тут всё
            return inPackageTaskCount;
        }
    }
}
