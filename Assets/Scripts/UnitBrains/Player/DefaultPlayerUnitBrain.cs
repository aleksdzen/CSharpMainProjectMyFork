//using System.Collections.Generic;
//using Model;
//using Model.Runtime.Projectiles;
//using UnityEngine;

//namespace UnitBrains.Player
//{
//    public class DefaultPlayerUnitBrain : BaseUnitBrain
//    {
//        protected float DistanceToOwnBase(Vector2Int fromPos) =>
//            Vector2Int.Distance(fromPos, runtimeModel.RoMap.Bases[RuntimeModel.PlayerId]);

//        protected void SortByDistanceToOwnBase(List<Vector2Int> list)
//        {
//            list.Sort(CompareByDistanceToOwnBase);
//        }

//        private int CompareByDistanceToOwnBase(Vector2Int a, Vector2Int b)
//        {
//            var distanceA = DistanceToOwnBase(a);
//            var distanceB = DistanceToOwnBase(b);
//            return distanceA.CompareTo(distanceB);
//        }
//    }
//}

using System.Collections.Generic; 
using System.Linq; 
using Model; 
using Model.Runtime.Projectiles; 
using UnityEngine; 
using UnitBrains.Pathfinding; 

namespace UnitBrains.Player 
{
    public class DefaultPlayerUnitBrain : BaseUnitBrain 
    {
        protected PlayerUnitCoordinator _coordinator; //  Защищенное поле для доступа из наследников

        protected float DistanceToOwnBase(Vector2Int fromPos) => // Метод вычисления расстояния до своей базы
            Vector2Int.Distance(fromPos, runtimeModel.RoMap.Bases[RuntimeModel.PlayerId]); // Возвращаем расстояние

        protected void SortByDistanceToOwnBase(List<Vector2Int> list) // Метод сортировки по расстоянию до базы
        {
            list.Sort(CompareByDistanceToOwnBase); // Сортируем список с помощью компаратора
        }

        private int CompareByDistanceToOwnBase(Vector2Int a, Vector2Int b) // Метод сравнения двух позиций
        {
            var distanceA = DistanceToOwnBase(a); // Вычисляем расстояние для первой позиции
            var distanceB = DistanceToOwnBase(b); // Вычисляем расстояние для второй позиции
            return distanceA.CompareTo(distanceB); // Сравниваем расстояния
        }

        public DefaultPlayerUnitBrain() // Конструктор класса
        {
            _coordinator = PlayerUnitCoordinator.Instance; // Получаем экземпляр координатора
        }

        public override Vector2Int GetNextStep() // Переопределяем метод получения следующего шага
        {
            // Проверяем, есть ли цели в зоне атаки
            if (HasTargetsInRange()) // Если есть цели в зоне атаки
                return unit.Pos; // Остаемся на месте и атакуем

            // Проверяем рекомендации координатора
            if (ShouldFollowCoordinator()) // Если нужно следовать координатору
            {
                // Получаем целевую точку от координатора
                Vector2Int targetPoint = GetCoordinatedTarget(); // Получаем координированную цель

                // Создаем путь до целевой точки
                var path = new AStarUnitPath(runtimeModel, unit.Pos, targetPoint); // Создаем A* путь

                // Получаем следующий шаг по пути
                Vector2Int nextStep = path.GetNextStepFrom(unit.Pos); // Получаем следующий шаг

                //  Проверяем валидность шага
                Vector2Int delta = nextStep - unit.Pos; // Вычисляем разницу позиций

                // Если шаг валидный (не больше 1 клетки)
                if (Mathf.Abs(delta.x) <= 1 && Mathf.Abs(delta.y) <= 1) // Проверяем валидность
                {
                    // Проверяем проходимость клетки
                    if (!IsCellWalkable(nextStep)) // Если клетка непроходима
                    {
                        // Ищем альтернативную клетку
                        nextStep = FindNearbyWalkableCell(unit.Pos); // Ищем альтернативу
                    }
                    //  Возвращаем валидный шаг
                    return nextStep; //  Возвращаем шаг
                }
            }

            // Если нет рекомендаций или они неактуальны, используем базовую логику
            return base.GetNextStep(); // Возвращаем результат базового метода
        }

        //  Метод проверки, нужно ли следовать рекомендациям координатора
        protected virtual bool ShouldFollowCoordinator() // Виртуальный метод для возможного переопределения
        {
            // Проверяем наличие рекомендаций
            if (!_coordinator.HasRecommendations) //  Если нет рекомендаций
                return false; //  Не следуем координатору

            // Получаем расстояние до рекомендуемой цели
            float distance = Vector2Int.Distance(unit.Pos, _coordinator.RecommendedTarget); //  Вычисляем расстояние

            // Получаем радиус атаки как float
            float attackRange = unit.Config.AttackRange; // Получаем радиус атаки (float)

            //  Проверяем, находится ли цель в пределах двух радиусов атаки
            return distance <= attackRange * 2f; //  Сравниваем float с float
        }

        //  Метод получения координированной цели
        protected virtual Vector2Int GetCoordinatedTarget() //  Виртуальный метод для возможного переопределения
        {
            //  Получаем рекомендуемую цель
            var recommendedTarget = _coordinator.RecommendedTarget; //  Получаем цель координатора

            //  Получаем рекомендуемую точку
            var recommendedPoint = _coordinator.RecommendedPoint; //  Получаем точку координатора

            // Вычисляем расстояние до цели
            float distanceToTarget = Vector2Int.Distance(unit.Pos, recommendedTarget); //  Расстояние до цели

            //  Получаем радиус атаки как float
            float attackRange = unit.Config.AttackRange; //  Радиус атаки (float)

            //  Если цель близко, идем к ней
            if (distanceToTarget <= attackRange * 2f) //  Если цель в пределах двух радиусов
            {
                return recommendedTarget; //  Идем к цели
            }

            //  Иначе идем к рекомендуемой точке
            return recommendedPoint; //  Идем к точке
        }

        //  Метод проверки проходимости клетки
        protected bool IsCellWalkable(Vector2Int pos) //  Защищенный метод для доступа из наследников
        {
            //  Проверяем границы карты
            if (pos.x < 0 || pos.x >= runtimeModel.RoMap.Width || //  Проверяем границы по X
                pos.y < 0 || pos.y >= runtimeModel.RoMap.Height) //  Проверяем границы по Y
                return false; //  Непроходимо если за границами

            //  Проверяем наличие стены
            if (runtimeModel.RoMap[pos]) //  Если есть стена
                return false; //  Непроходимо

            //  Проверяем наличие юнита
            if (runtimeModel.RoUnits.Any(u => u.Pos == pos)) //  Если есть юнит
                return false; //  Непроходимо

            //  Клетка проходима
            return true; //  Проходимо
        }

        //  Метод поиска ближайшей проходимой клетки
        protected Vector2Int FindNearbyWalkableCell(Vector2Int currentPos) //  Защищенный метод для наследников
        {
            //  Массив направлений для проверки
            Vector2Int[] directions = new Vector2Int[] //  Массив направлений
            {
                Vector2Int.up, //  Вверх
                Vector2Int.right, //  Вправо
                Vector2Int.down, // Вниз
                Vector2Int.left // Н Влево
            };

            // Проверяем каждое направление
            foreach (Vector2Int direction in directions) // Для каждого направления
            {
                Vector2Int neighborPos = currentPos + direction; //  Вычисляем позицию соседа

                // Если соседняя клетка проходима, возвращаем её
                if (IsCellWalkable(neighborPos)) // Если сосед проходим
                {
                    return neighborPos; // возвращаем соседа
                }
            }

            // Если все клетки заняты, остаемся на месте
            return currentPos; //  Остаемся на месте
        }
    }
}