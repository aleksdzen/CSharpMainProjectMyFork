using Model; // Подключение пространства имен Model, содержащего основные классы модели игры
using Model.Runtime.Projectiles; // Подключение пространства имен для работы со снарядами
using System.Collections.Generic; // Подключение для использования обобщенных коллекций (List, Dictionary и т.д.)
using UnityEngine; // Подключение Unity-библиотеки для работы с Vector2Int и Time.deltaTime
using Utilities; // Подключение вспомогательных утилит (в т.ч. расширения CalcNextStepTowards)

namespace UnitBrains.Player // Объявление пространства имен для юнит-мозгов игрока
{
    public class ThirdUnitBrain : DefaultPlayerUnitBrain // Класс мозга третьего юнита, наследующий базовый класс игрового юнита
    {
        public override string TargetUnitName => "Ironclad Behemoth"; // Переопределение свойства - возвращает имя цели для этого юнита

        private const float OverheatTemperature = 3f; // Константа: температура перегрева оружия (3 градуса)
        private const float OverheatCooldown = 2f; // Константа: время полного остывания оружия (2 секунды)
        private float _temperature = 0f; // Текущая температура оружия (начинается с 0)
        private float _cooldownTime = 0f; // Время, прошедшее с начала процесса остывания
        private bool _overheated; // Флаг перегрева: true - оружие перегрето, false - работает нормально

        //поля для задержки атаки
        private float _attackDelay = 1f; // Задержка перед атакой после остановки движения (1 секунда)
        private float _timeSinceLastMovement = 0f; // Время, прошедшее с момента последнего движения
        private bool _isMoving = false; // Флаг: движется ли юнит в данный момент

        // Список целей, которые находятся вне зоны досягаемости - к ним юнит будет двигаться
        List<Vector2Int> _outOfRangeTargets = new List<Vector2Int>();

        // Статический счетчик для присвоения уникальных номеров каждому юниту этого типа
        private static int _unitCounter = 0;
        // Уникальный номер конкретного экземпляра юнита
        private int _unitNumber;
        // Константа: максимальное количество целей для "умного" распределения между юнитами
        private const int MaxTargetsForSmartSelection = 3;

        // Конструктор класса - вызывается при создании каждого экземпляра юнита
        public ThirdUnitBrain()
        {
            _unitNumber = _unitCounter; // Присваиваем текущий номер из счетчика
            _unitCounter++; // Увеличиваем счетчик для следующего юнита
        }

        // Метод генерации снарядов при атаке
        protected override void GenerateProjectiles(Vector2Int forTarget, List<BaseProjectile> intoList)
        {
            float overheatTemperature = OverheatTemperature; // Копируем значение температуры перегрева в локальную переменную

            if (GetTemperature() >= overheatTemperature) // Если текущая температура >= температуры перегрева (3)
            {
                return; // Выходим из метода - оружие перегрето, стрелять нельзя
            }

            int currentTemp = GetTemperature(); // Получаем текущую температуру (целое число)
            int projectileCount = currentTemp + 1; // Количество снарядов = температура + 1 (при 0 -> 1 снаряд, при 1 -> 2, при 2 -> 3)

            for (int i = 0; i < projectileCount; i++) // Цикл для создания указанного количества снарядов
            {
                var projectile = CreateProjectile(forTarget); // Создаем один снаряд, направленный в цель
                AddProjectileToList(projectile, intoList); // Добавляем созданный снаряд в общий список
            }

            IncreaseTemperature(); // Увеличиваем температуру оружия после выстрела на 1
        }

        // Метод определения следующего шага движения юнита
        public override Vector2Int GetNextStep()
        {
            // Проверяем, есть ли цели вне зоны досягаемости
            if (_outOfRangeTargets.Count > 0)
            {
                _isMoving = true; // Устанавливаем флаг движения
                Vector2Int target = _outOfRangeTargets[0]; // Берем первую цель из списка
                Vector2Int currentPos = unit.Pos; // Получаем текущую позицию юнита
                return currentPos.CalcNextStepTowards(target); // Вычисляем следующий шаг к цели
            }

            // Если целей вне зоны досягаемости нет, возвращаем текущую позицию (стоим на месте)
            _isMoving = false; // Сбрасываем флаг движения
            return unit.Pos; // Возвращаем текущую позицию (юнит стоит на месте)
        }

        // Метод выбора целей для атаки
        protected override List<Vector2Int> SelectTargets()
        {
            // Очищаем список целей вне зоны досягаемости
            _outOfRangeTargets.Clear();

            // Получаем все доступные цели
            List<Vector2Int> allTargets = new List<Vector2Int>(GetAllTargets());
            List<Vector2Int> result = new List<Vector2Int>(); // Список целей для атаки (результат)

            // БЛОКИРОВКА АТАКИ
            // Проверяем: движется ли юнит или прошло меньше 1 секунды после остановки
            if (_isMoving || _timeSinceLastMovement < _attackDelay)
            {
                // Если есть цели - добавляем их в список для движения
                if (allTargets.Count > 0)
                {
                    _outOfRangeTargets.AddRange(allTargets); // Добавляем все цели в список для движения
                }
                else
                {
                    // Если нет целей - двигаемся к базе противника
                    int enemyId = IsPlayerUnitBrain ? RuntimeModel.BotPlayerId : RuntimeModel.PlayerId; // Определяем ID врага
                    Vector2Int enemyBasePos = runtimeModel.RoMap.Bases[enemyId]; // Получаем позицию базы врага
                    _outOfRangeTargets.Add(enemyBasePos); // Добавляем базу врага в список для движения
                }

                // Возвращаем пустой список - АТАКА ЗАБЛОКИРОВАНА
                return result;
            }
            // === КОНЕЦ БЛОКИРОВКИ ===

            // Если список целей пуст (никого нет на поле)
            if (allTargets.Count == 0)
            {
                int enemyId = IsPlayerUnitBrain ? RuntimeModel.BotPlayerId : RuntimeModel.PlayerId; // Определяем ID врага
                Vector2Int enemyBasePos = runtimeModel.RoMap.Bases[enemyId]; // Получаем позицию базы врага

                if (IsTargetInRange(enemyBasePos)) // Если база в зоне досягаемости
                {
                    result.Add(enemyBasePos); // Добавляем базу в список целей для атаки
                    return result; // Возвращаем результат
                }
                else // Если база вне зоны досягаемости
                {
                    _outOfRangeTargets.Add(enemyBasePos); // Добавляем базу в список для движения
                    return result; // Возвращаем пустой результат (атака невозможна)
                }
            }

            // Сортируем все цели по расстоянию до нашей собственной базы (приоритет - ближние)
            SortByDistanceToOwnBase(allTargets);

            // Выбираем индекс цели для атаки, распределяя цели между юнитами
            int targetIndex;
            if (allTargets.Count <= MaxTargetsForSmartSelection) // Если целей меньше или равно 3
            {
                targetIndex = _unitNumber % allTargets.Count; // Берем остаток от деления номера юнита на количество целей
            }
            else // Если целей больше 3
            {
                targetIndex = _unitNumber % MaxTargetsForSmartSelection; // Берем остаток от деления на 3
                // Если индекс выходит за пределы списка
                if (targetIndex >= allTargets.Count)
                {
                    targetIndex = allTargets.Count - 1; // Корректируем на последний валидный индекс
                }
            }

            // Получаем выбранную цель по вычисленному индексу
            Vector2Int selectedTarget = allTargets[targetIndex];

            // Проверяем, находится ли выбранная цель в радиусе атаки
            if (IsTargetInRange(selectedTarget))
            {
                // Если цель в зоне досягаемости - добавляем в результат
                result.Add(selectedTarget);
            }
            else
            {
                // Если цель вне зоны досягаемости - добавляем в список для движения
                _outOfRangeTargets.Add(selectedTarget);
                // Ищем первую цель в списке, которая находится в зоне досягаемости
                foreach (Vector2Int target in allTargets)
                {
                    if (IsTargetInRange(target)) // Если цель в зоне досягаемости
                    {
                        result.Add(target); // Добавляем в результат
                        break; // Выходим из цикла, так как нашли цель
                    }
                }
                // Если ни одна цель не в зоне досягаемости
                if (result.Count == 0)
                {
                    _outOfRangeTargets.AddRange(allTargets); // Добавляем все цели в список для движения
                }
            }

            return result; // Возвращаем список целей для атаки
        }

        // Метод обновления состояния юнита (вызывается каждый кадр)
        public override void Update(float deltaTime, float time)
        {
            base.Update(deltaTime, time); // Вызов метода базового класса для стандартной логики обновления

            // Обновляем таймер без движения
            if (!_isMoving) // Если юнит не движется
            {
                _timeSinceLastMovement += Time.deltaTime; // Увеличиваем время с момента последнего движения
            }
            else // Если юнит движется
            {
                _timeSinceLastMovement = 0f; // Сбрасываем таймер
            }

            if (_overheated) // Если оружие перегрето
            {
                _cooldownTime += Time.deltaTime; // Увеличиваем время остывания (используем реальное время игры)
                float t = _cooldownTime / (OverheatCooldown / 10); // Вычисляем прогресс остывания (делим на 0.2 сек за шаг)
                _temperature = Mathf.Lerp(OverheatTemperature, 0, t); // Плавно уменьшаем температуру от 3 до 0
                if (t >= 1) // Если прогресс достиг 1 (полное остывание)
                {
                    _cooldownTime = 0; // Сбрасываем время остывания
                    _overheated = false; // Снимаем флаг перегрева
                }
            }
        }

        // Метод получения текущей температуры (возвращает целое число)
        private int GetTemperature()
        {
            if (_overheated) // Если оружие перегрето
            {
                return (int)OverheatTemperature; // Возвращаем 3 (температура перегрева)
            }
            else // Иначе
            {
                return (int)_temperature; // Возвращаем текущую температуру (0, 1 или 2)
            }
        }

        // Метод увеличения температуры после выстрела
        private void IncreaseTemperature()
        {
            _temperature += 1f; // Увеличиваем температуру на 1
            if (_temperature >= OverheatTemperature) // Если температура достигла или превысила порог перегрева (3)
                _overheated = true; // Устанавливаем флаг перегрева
        }
    }
}