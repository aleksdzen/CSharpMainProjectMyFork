using Model; 
using Model.Runtime.Projectiles; 
using System.Collections.Generic; 
using UnityEngine; 
using Utilities; 
using UnitBrains.Pathfinding; 

namespace UnitBrains.Player 
{
    public class ThirdUnitBrain : DefaultPlayerUnitBrain 
    {
        public override string TargetUnitName => "Ironclad Behemoth"; 
        private float _attackDelay = 1f; // Задержка атаки после движения (1 секунда)
        private float _timeSinceLastMovement = 0f; // Время с последнего движения
        private bool _isMoving = false; // Флаг движения

        List<Vector2Int> _outOfRangeTargets = new List<Vector2Int>(); // Цели вне зоны досягаемости

        private static int _unitCounter = 0; // Статический счетчик юнитов
        private int _unitNumber; // Номер текущего юнита
        private const int MaxTargetsForSmartSelection = 3; // Максимум целей для умного выбора

        public ThirdUnitBrain() // Конструктор класса
        {
            _unitNumber = _unitCounter; // Присваиваем номер юнита
            _unitCounter++; // Увеличиваем счетчик для следующего юнита
        }

        protected override void GenerateProjectiles(Vector2Int forTarget, List<BaseProjectile> intoList) // Генерация снарядов
        {
            int projectileCount = 3; // Всегда создаем 3 снаряда

            for (int i = 0; i < projectileCount; i++) // Создаем указанное количество снарядов
            {
                var projectile = CreateProjectile(forTarget); // Создаем снаряд
                AddProjectileToList(projectile, intoList); // Добавляем в список
            }
        }

        public override Vector2Int GetNextStep() // ИЗМЕНЕННЫЙ: Получение следующего шага
        {
            //  Проверяем цели в зоне атаки в первую очередь
            if (HasTargetsInRange()) // Если есть цели в зоне атаки
            {
                _isMoving = false; // Сбрасываем флаг движения
                return unit.Pos; // Остаемся на месте и атакуем
            }

            //  Используем логику координатора из родительского класса
            if (ShouldFollowCoordinator()) // Если нужно следовать координатору
            {
                _isMoving = true; // Устанавливаем флаг движения

                Vector2Int target = GetCoordinatedTarget(); // Получаем координированную цель

                if (unit.Pos == target) // Если уже на цели
                {
                    _isMoving = false; // Сбрасываем флаг
                    return unit.Pos; // Остаемся на месте
                }

                // Используем A* для поиска пути
                ActivePath = new AStarUnitPath(runtimeModel, unit.Pos, target); // Создаем путь
                Vector2Int nextStep = ActivePath.GetNextStepFrom(unit.Pos); // Получаем следующий шаг

                // Проверяем валидность шага
                Vector2Int delta = nextStep - unit.Pos; // Разница позиций
                if (Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1) // Если шаг невалидный
                {
                    Debug.LogWarning($"ThirdUnitBrain: invalid A* step, using fallback"); // Логируем
                    nextStep = unit.Pos.CalcNextStepTowards(target); // Используем прямое движение
                }

                // Проверяем проходимость
                if (!IsCellWalkable(nextStep)) // Если клетка непроходима
                {
                    nextStep = FindNearbyWalkableCell(unit.Pos); // Ищем альтернативу
                }

                return nextStep; // Возвращаем шаг
            }

            //  Если нет рекомендаций, используем стандартную логику
            if (_outOfRangeTargets.Count > 0) // Если есть цели вне зоны
            {
                _isMoving = true; // Устанавливаем флаг движения
                Vector2Int target = _outOfRangeTargets[0]; // Берем первую цель

                if (unit.Pos == target) // Если на цели
                {
                    _isMoving = false; // Сбрасываем флаг
                    return unit.Pos; // Остаемся на месте
                }

                ActivePath = new AStarUnitPath(runtimeModel, unit.Pos, target); // Создаем путь
                Vector2Int nextStep = ActivePath.GetNextStepFrom(unit.Pos); // Получаем шаг

                Vector2Int delta = nextStep - unit.Pos; // Разница позиций
                if (Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1) // Проверка валидности
                {
                    Debug.LogWarning($"ThirdUnitBrain: invalid A* step, using fallback"); // Логируем
                    nextStep = unit.Pos.CalcNextStepTowards(target); // Прямое движение
                }

                if (!IsCellWalkable(nextStep)) // Если непроходимо
                {
                    nextStep = FindNearbyWalkableCell(unit.Pos); // Ищем альтернативу
                }

                return nextStep; // Возвращаем шаг
            }

            //  Если нет целей для движения
            _isMoving = false; // Сбрасываем флаг движения
            return unit.Pos; // Остаемся на месте
        }

        protected override List<Vector2Int> SelectTargets() // ИЗМЕНЕННЫЙ: Выбор целей
        {
            _outOfRangeTargets.Clear(); // Очищаем список целей

            List<Vector2Int> allTargets = new List<Vector2Int>(GetAllTargets()); // Все цели
            List<Vector2Int> result = new List<Vector2Int>(); // Результат

            // Блокировка атаки только если юнит реально движется
            if (_isMoving || _timeSinceLastMovement < _attackDelay) // Если движется или задержка не прошла
            {
                // Если есть рекомендации координатора
                if (ShouldFollowCoordinator()) // Если нужно следовать координатору
                {
                    Vector2Int recommendedTarget = _coordinator.RecommendedTarget; // Получаем цель координатора

                    if (IsTargetInRange(recommendedTarget)) // Если цель в зоне атаки
                    {
                        result.Add(recommendedTarget); // Добавляем как цель
                        return result; // Возвращаем результат
                    }
                    else // Если цель не в зоне
                    {
                        _outOfRangeTargets.Add(recommendedTarget); // Добавляем для движения
                        return result; // Возвращаем пустой результат
                    }
                }

                //  Если нет рекомендаций, используем стандартную логику
                if (allTargets.Count > 0) // Если есть цели
                {
                    _outOfRangeTargets.AddRange(allTargets); // Все цели для движения
                }
                else // Если целей нет
                {
                    int enemyId = IsPlayerUnitBrain ? RuntimeModel.BotPlayerId : RuntimeModel.PlayerId; // ID врага
                    Vector2Int enemyBasePos = runtimeModel.RoMap.Bases[enemyId]; // База врага
                    _outOfRangeTargets.Add(enemyBasePos); // База для движения
                }

                return result; // Возвращаем пустой результат
            }

            // Проверяем рекомендации координатора
            if (ShouldFollowCoordinator()) // Если нужно следовать координатору
            {
                Vector2Int recommendedTarget = _coordinator.RecommendedTarget; // Цель координатора

                if (IsTargetInRange(recommendedTarget)) // Если в зоне атаки
                {
                    result.Add(recommendedTarget); // Добавляем
                    return result; // Возвращаем
                }
                else // Если не в зоне
                {
                    _outOfRangeTargets.Add(recommendedTarget); // Для движения
                    return result; // Возвращаем пустой
                }
            }

            // Стандартная логика если нет рекомендаций
            if (allTargets.Count == 0) // Если целей нет
            {
                int enemyId = IsPlayerUnitBrain ? RuntimeModel.BotPlayerId : RuntimeModel.PlayerId; // ID врага
                Vector2Int enemyBasePos = runtimeModel.RoMap.Bases[enemyId]; // База врага

                if (IsTargetInRange(enemyBasePos)) // Если база в зоне
                {
                    result.Add(enemyBasePos); // Добавляем
                    return result; // Возвращаем
                }
                else // Если не в зоне
                {
                    _outOfRangeTargets.Add(enemyBasePos); // Для движения
                    return result; // Возвращаем пустой
                }
            }

            SortByDistanceToOwnBase(allTargets); // Сортируем цели

            int targetIndex; // Индекс цели
            if (allTargets.Count <= MaxTargetsForSmartSelection) // Если целей мало
            {
                targetIndex = _unitNumber % allTargets.Count; // Распределяем по номеру
            }
            else // Если целей много
            {
                targetIndex = _unitNumber % MaxTargetsForSmartSelection; // Берем остаток
                if (targetIndex >= allTargets.Count) // Если за границами
                {
                    targetIndex = allTargets.Count - 1; // Корректируем
                }
            }

            Vector2Int selectedTarget = allTargets[targetIndex]; // Выбранная цель

            if (IsTargetInRange(selectedTarget)) // Если в зоне
            {
                result.Add(selectedTarget); // Добавляем
            }
            else // Если не в зоне
            {
                _outOfRangeTargets.Add(selectedTarget); // Для движения

                foreach (Vector2Int target in allTargets) // Ищем цель в зоне
                {
                    if (IsTargetInRange(target)) // Если в зоне
                    {
                        result.Add(target); // Добавляем
                        break; // Выходим
                    }
                }

                if (result.Count == 0) // Если нет целей в зоне
                {
                    _outOfRangeTargets.AddRange(allTargets); // Все для движения
                }
            }

            return result; // Возвращаем результат
        }

        public override void Update(float deltaTime, float time) // Обновление состояния
        {
            base.Update(deltaTime, time); // Вызываем базовый метод

            if (!_isMoving) // Если не движется
            {
                _timeSinceLastMovement += Time.deltaTime; // Увеличиваем таймер
            }
            else // Если движется
            {
                _timeSinceLastMovement = 0f; // Сбрасываем таймер
            }
        }

       
    }
}