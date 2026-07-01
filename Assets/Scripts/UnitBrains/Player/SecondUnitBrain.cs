using Model;
using Model.Runtime; //импортирует пространство имен для работы с моделью игры
using Model.Runtime.Projectiles; //импортирует классы для работы со снарядами
using System;
using System.Collections.Generic; //импортирует коллекции (List, Dictionary и т.д.)
using UnityEngine; //импортирует основной функционал Unity
using Utilities;
using static UnityEngine.GraphicsBuffer; //импортирует статические методы GraphicsBuffer

namespace UnitBrains.Player //определяет пространство имен для логики юнитов игрока
{
    public class SecondUnitBrain : DefaultPlayerUnitBrain //объявление публичного класса для второго юнита : наследование от базового класса для всех юнитов игрока
    {
        public override string TargetUnitName => "Cobra Commando"; //Переопределение свойства TargetUnitName - возвращает имя цели для этого юнита ("Cobra Commando")
        private const float OverheatTemperature = 3f; //константа температуры перегрева (при достижении 3 оружие перегревается)
        private const float OverheatCooldown = 2f; //константа времени остывания в секундах
        private float _temperature = 0f; //текущая температура оружия (начинается с 0)
        private float _cooldownTime = 0f; //время остывания (используется для таймера)
        private bool _overheated; //флаг перегрева (true = оружие перегрето)

        // Новое поле для хранения целей, к которым нужно идти
        List<Vector2Int> _targetsToChase = new List<Vector2Int>();

        protected override void GenerateProjectiles(Vector2Int forTarget, List<BaseProjectile> intoList) //Переопределение метода - вызывается для создания снарядов по цели(forTarget - координаты цели, intoList - список для добавления созданных снарядов)
        {
            /*float overheatTemperature = OverheatTemperature;*/ //Создает локальную переменную с температурой перегрева (дублирование константы)

            // Homework 1.3 (1st block, 3rd module)

            //проверяем, не перегрето ли оружие
            if (GetTemperature() >= OverheatTemperature)
            {
                return; //оружие перегрелось, прерываем, если температура >= 3, метод завершается (ничего не стреляет)
            }

            // Определяем количество снарядов (минимум 1)
            int projectileCount = Mathf.Max(1, Mathf.FloorToInt(GetTemperature() + 1));
            //Mathf.FloorToInt - округляет вниз до целого числа
            //GetTemperature() + 1 - при температуре 0 дает 1, при 1 дает 2, при 2 дает 3
            //Mathf.Max(1, ...) - гарантирует минимум 1 снаряд
            //Примеры: temp = 0 → count = 1, temp = 1 → count = 2, temp = 2 → count = 3
            
                
            //Создание нужного количества снарядов
            for (int i = 0; i < projectileCount; i++)
            {
                var projectile = CreateProjectile(forTarget); //CreateProjectile - создает снаряд для цели
                AddProjectileToList(projectile, intoList); //добавляет снаряд в список (активирует его в игре)
            }

            // Увеличиваем температуру на 1, вызов метода
            IncreaseTemperature();

            // Проверяем перегрев после выстрела
            if (GetTemperature() >= OverheatTemperature)
            {
                _overheated = true; //Если температура >= 3, устанавливает флаг перегрева
                _cooldownTime = Time.time + OverheatCooldown;//Запоминает время, когда оружие остынет (текущее время + 2 секунды)
            }
        }

        public override Vector2Int GetNextStep()//Переопределение метода перемещения
        {
            // Проверяем, есть ли цели для преследования
            if (_targetsToChase.Count > 0)
            {
                Vector2Int target = _targetsToChase[0];

                // Проверяем, находится ли цель в зоне досягаемости
                if (!IsTargetInRange(target))
                {
                    // Если цель вне зоны досягаемости - двигаемся к ней
                    return unit.Pos.CalcNextStepTowards(target);
                }
                else
                {
                    // Если цель в зоне досягаемости - остаемся на месте
                    return unit.Pos;
                }
            }

            // Если целей для преследования нет - остаемся на месте
            return unit.Pos;

        }

        protected override List<Vector2Int> SelectTargets()
        {
            // A. Получаем все доступные цели
            List<Vector2Int> allTargets = (List<Vector2Int>)GetAllTargets();
            List<Vector2Int> result = new List<Vector2Int>();

            // Очищаем список целей для преследования
            _targetsToChase.Clear();

            // B. Если есть цели
            if (allTargets.Count > 0)
            {
                // Находим ближайшую к нашей базе цель (самая опасная)
                Vector2Int closestTarget = allTargets[0];
                float closestDistance = DistanceToOwnBase(closestTarget);

                for (int i = 1; i < allTargets.Count; i++)
                {
                    Vector2Int target = allTargets[i];
                    float distance = DistanceToOwnBase(target);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = target;
                    }
                }

                // Проверяем, находится ли самая опасная цель в зоне досягаемости
                if (IsTargetInRange(closestTarget))
                {
                    // Если цель в зоне досягаемости - добавляем в result
                    result.Add(closestTarget);
                }
                else
                {
                    // Если цель вне зоны досягаемости - добавляем в список для преследования
                    _targetsToChase.Add(closestTarget);
                }
            }
            else
            {
                // C. Если целей нет - добавляем базу противника
                int enemyBaseId = IsPlayerUnitBrain ? RuntimeModel.BotPlayerId : RuntimeModel.PlayerId;
                Vector2Int enemyBase = runtimeModel.RoMap.Bases[enemyBaseId];

                // Проверяем, находится ли база противника в зоне досягаемости
                if (IsTargetInRange(enemyBase))
                {
                    result.Add(enemyBase);
                }
                else
                {
                    // Если база вне зоны досягаемости - добавляем в список для преследования
                    _targetsToChase.Add(enemyBase);
                }
            }

            return result;
        }

        public override void Update(float deltaTime, float time) //Метод вызывается каждый кадр, deltaTime - время между кадрами,
                                                                 //time - текущее время игры, Проверяет, перегрето ли оружие
        {
            if (_overheated)
            {              
                _cooldownTime += deltaTime; //Увеличивает время остывания на время кадра
                
                float t = _cooldownTime / OverheatCooldown; //Вычисляет прогресс остывания, t будет 2 секунды
                
                _temperature = Mathf.Lerp(OverheatTemperature, 0, t); //Mathf.Lerp - линейная интерполяция, Плавно уменьшает температуру от 3 до 0 за время t
                
                if (t >= 1) //Когда прогресс достиг 1 (прошло 2 секунды), Сбрасывает таймер и флаг перегрева
                {
                    _cooldownTime = 0;
                    _overheated = false;
                }
            }
        }

        private int GetTemperature() //Возвращает температуру как целое число
        {
            if (_overheated)
            {
                return (int)OverheatTemperature; //Если перегрет → возвращает 3
            }

            else
            {
                return (int)_temperature; //Иначе возвращает текущую температуру (0, 1, 2 или 3)
            }

        }

        private void IncreaseTemperature()
        {
            _temperature += 1f; //Увеличивает температуру на 1
            /*if (_temperature >= OverheatTemperature) _overheated = true;*/ //Если достигла 3, устанавливает флаг перегрева
        }

        // Вспомогательный метод для проверки, находится ли цель в зоне досягаемости
        private new bool IsTargetInRange(Vector2Int target)
        {
            return unit.IsTargetInAttackRange(target);
        }
    }
}