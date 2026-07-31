using Model;
using Model.Runtime.Projectiles;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace UnitBrains.Player
{
    public class SecondUnitBrain : DefaultPlayerUnitBrain
    {
        public override string TargetUnitName => "Cobra Commando";
        private const float OverheatTemperature = 3f; //температура перегрева
        private const float OverheatCooldown = 2f; //время остывания
        private float _temperature = 0f; //текущая температур оружия
        private float _cooldownTime = 0f; //время с начала остывания
        private bool _overheated; //флаг перегрева (true - перегрето)

        // B.Создаем новое поле для хранения целей, к которым нужно идти, но которые вне зоны досягаемости
        List<Vector2Int> _outOfRangeTargets = new List<Vector2Int>();

        // A. Создаем статическое поле-счетчик для выдачи номеров юнитам
        private static int _unitCounter = 0;
        // A. Создаем поле с номером юнита
        private int _unitNumber;
        // A. Создаем константу для максимального количества целей для умного выбора
        private const int MaxTargetsForSmartSelection = 3;

        // Конструктор для присвоения номера юниту при создании
        public SecondUnitBrain()
        {
            _unitNumber = _unitCounter;
            _unitCounter++;
        }

        protected override void GenerateProjectiles(Vector2Int forTarget, List<BaseProjectile> intoList) //метод генерации снарядов (forTarget - цель для выстрела, список куда добавляются созданные снаряды)
        {
            float overheatTemperature = OverheatTemperature; //локальная переменная для температуры перегрева
                                                             ///////////////////////////////////////
                                                             // Homework 1.3 (1st block, 3rd module)
                                                             ///////////////////////////////////////           
            if (GetTemperature() >= overheatTemperature)
            {
                return;
            }

            int currentTemp = GetTemperature();
            int projectileCount = currentTemp + 1;

            for (int i = 0; i < projectileCount; i++)
            {
                var projectile = CreateProjectile(forTarget); //создание снаряда направленного в указанную цель
                AddProjectileToList(projectile, intoList); //добавление созданного снаряда в список
            }

            IncreaseTemperature(); //увеличиваем температуру после выстрела
        }

        public override Vector2Int GetNextStep()
        {
            // Если есть цели вне зоны досягаемости, идем к первой из них
            if (_outOfRangeTargets.Count > 0)
            {
                Vector2Int target = _outOfRangeTargets[0];
                Vector2Int currentPos = unit.Pos;
                return currentPos.CalcNextStepTowards(target);
            }

            // Если целей вне зоны досягаемости нет, возвращаем текущую позицию юнита
            return unit.Pos;
        }

        protected override List<Vector2Int> SelectTargets() //получает список всех достижимых целей
        {
            ///////////////////////////////////////
            // Homework 1.4 (1st block, 4rd module)
            ///////////////////////////////////////

            // Очищаем список целей вне зоны досягаемости
            _outOfRangeTargets.Clear();

            // B. Получаем все цели и очищаем список
            List<Vector2Int> allTargets = new List<Vector2Int>(GetAllTargets());
            List<Vector2Int> result = new List<Vector2Int>();

            // Если в списке целей никого нет, добавляем базу противника
            if (allTargets.Count == 0)
            {
                // Определяем ID противника
                int enemyId = IsPlayerUnitBrain ? RuntimeModel.BotPlayerId : RuntimeModel.PlayerId;
                // Получаем позицию базы противника
                Vector2Int enemyBasePos = runtimeModel.RoMap.Bases[enemyId];

                // Проверяем, находится ли база в зоне досягаемости
                if (IsTargetInRange(enemyBasePos))
                {
                    // Если в зоне досягаемости - возвращаем как цель
                    result.Add(enemyBasePos);
                    return result;
                }
                else
                {
                    // Если вне зоны досягаемости - добавляем в список для движения
                    _outOfRangeTargets.Add(enemyBasePos);
                    return result;
                }
            }

            // B. Сортируем цели по дистанции до нашей базы
            SortByDistanceToOwnBase(allTargets);

            // B. Рассчитываем, какую цель по счету должен атаковать текущий юнит
            // Если целей меньше, чем MaxTargetsForSmartSelection, берем остаток от деления номера юнита на количество целей
            int targetIndex;
            if (allTargets.Count <= MaxTargetsForSmartSelection)
            {
                targetIndex = _unitNumber % allTargets.Count;
            }
            else
            {
                // Если целей больше, чем MaxTargetsForSmartSelection, берем остаток от деления на MaxTargetsForSmartSelection
                targetIndex = _unitNumber % MaxTargetsForSmartSelection;
                // Но если индекс выходит за пределы списка, корректируем
                if (targetIndex >= allTargets.Count)
                {
                    targetIndex = allTargets.Count - 1;
                }
            }

            // Получаем выбранную цель по индексу
            Vector2Int selectedTarget = allTargets[targetIndex];

            // Проверяем, что цель в радиусе атаки
            if (IsTargetInRange(selectedTarget))
            {
                // Если цель в зоне досягаемости, добавляем в результат
                result.Add(selectedTarget);
            }
            else
            {
                // Если цель вне зоны досягаемости, записываем в коллекцию для движения
                _outOfRangeTargets.Add(selectedTarget);
                // Ищем первую цель в списке, которая находится в зоне досягаемости
                foreach (Vector2Int target in allTargets)
                {
                    if (IsTargetInRange(target))
                    {
                        result.Add(target);
                        break;
                    }
                }
                // Если ни одна цель не в зоне досягаемости, добавляем все цели в список для движения
                if (result.Count == 0)
                {
                    _outOfRangeTargets.AddRange(allTargets);
                }
            }

            return result;
        }

        public override void Update(float deltaTime, float time) //обновляет состояние юнита каждый кадр
        {
            if (_overheated)
            {
                _cooldownTime += Time.deltaTime; //если оружия перегрето, увелиичивает время остываения
                float t = _cooldownTime / (OverheatCooldown / 10); //вычисляет процесс остывания 0,2 сек
                _temperature = Mathf.Lerp(OverheatTemperature, 0, t); //плавное уменьшение температуры от 3 до 0 в зависимотси от прогресса остывания
                if (t >= 1) //после завершения остывания сброс таймера и флага перегрева
                {
                    _cooldownTime = 0;
                    _overheated = false;
                }
            }
        }

        private int GetTemperature() //возвращает текущую температуру как целое число
        {
            if (_overheated) // если перегрето
            {
                return (int)OverheatTemperature; //возврат 3
            }
            else //иначе
            {
                return (int)_temperature; // текущая температа
            }

        }

        private void IncreaseTemperature() //метод увеличения температуры
        {
            _temperature += 1f; //увеличивает температуру на 1
            if (_temperature >= OverheatTemperature) _overheated = true; //если текущая температура = 3, то перегрев
        }
    }
}