using System.Collections;
using NUnit.Framework;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.TestTools;
// Тестовая сборка PlayModeTests (includePlatforms: Editor не указан) — тесты PlayMode
// ИНДИВИДУАЛЬНЫЙ ВАРИАНТ №3: Health, проверка механики Die()
public class HealthDieTests
{
    // PLAYMODE ТЕСТ (вариант №3): Die() приводит HP к 0, IsAlive == false
    [UnityTest]
    public IEnumerator PlayMode_HealthDie_HPReachesZero_IsAliveFalse()
    {
        // Создаем временный объект с реальным скриптом здоровья из игры
        GameObject playerObj = new GameObject("TestPlayerDie");
        // Откладываем вызов Awake: неактивный объект вызывает Awake только после активации
        playerObj.SetActive(false);
        Health health = playerObj.AddComponent<Health>();
        // Задаем стартовое здоровье (3 хитпоинта) ДО инициализации компонента
        health.maxHP = 3;
        playerObj.SetActive(true); // Awake выставит currentHP = maxHP = 3

        // Персонаж жив до вызова Die()
        Assert.IsTrue(health.IsAlive, "Ошибка: персонаж должен быть жив до вызова Die()!");

        // Вызываем механику мгновенной смерти: Die() уменьшает HP до нуля
        health.Die();

        // После Die() HP равно 0 — персонаж мертв
        Assert.IsFalse(health.IsAlive, "Ошибка: после Die() персонаж должен быть мертв (IsAlive == false)!");

        // Удаляем объект после теста, чтобы очистить память
        Object.Destroy(playerObj);
        // Пропускаем один кадр для симуляции игрового времени
        yield return null;
    }
}
