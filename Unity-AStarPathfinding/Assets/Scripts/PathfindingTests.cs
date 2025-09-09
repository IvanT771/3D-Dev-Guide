using System.Collections.Generic;
using UnityEngine;
using PathfindingSystem;

namespace PathfindingSystem.Tests
{
    /// <summary>
    /// Простые тесты для проверки функциональности системы A*
    /// Можно запустить как компонент в редакторе для проверки работы
    /// </summary>
    public class PathfindingTests : MonoBehaviour
    {
        [Header("Настройки тестов")]
        [SerializeField] private bool runTestsOnStart = true;
        [SerializeField] private bool showTestResults = true;

        private void Start()
        {
            if (runTestsOnStart)
            {
                RunAllTests();
            }
        }

        /// <summary>
        /// Запуск всех тестов
        /// </summary>
        public void RunAllTests()
        {
            Debug.Log("=== Запуск тестов системы A* ===");

            TestSimpleAStarBasicPath();
            TestSimpleAStarNoPath();
            TestSimpleAStarWithObstacles();
            TestDeterministicBehavior();
            TestGridManagerBasics();

            Debug.Log("=== Тесты завершены ===");
        }

        /// <summary>
        /// Тест базового поиска пути
        /// </summary>
        private void TestSimpleAStarBasicPath()
        {
            Debug.Log("Тест: Базовый поиск пути");

            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int goal = new Vector2Int(3, 3);
            Vector2Int gridSize = new Vector2Int(5, 5);

            var path = SimpleAStar.FindPath(start, goal, gridSize, (pos) => false);

            if (path != null && path.Count > 0)
            {
                Debug.Log($"✓ Путь найден: {path.Count} шагов");
                if (showTestResults)
                {
                    Debug.Log($"Путь: {string.Join(" -> ", path)}");
                }
            }
            else
            {
                Debug.LogError("✗ Путь не найден!");
            }
        }

        /// <summary>
        /// Тест случая, когда путь невозможен
        /// </summary>
        private void TestSimpleAStarNoPath()
        {
            Debug.Log("Тест: Отсутствие пути");

            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int goal = new Vector2Int(2, 2);
            Vector2Int gridSize = new Vector2Int(5, 5);

            // Создание стены между стартом и целью
            var obstacles = new HashSet<Vector2Int>
            {
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(1, 2),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2)
            };

            var path = SimpleAStar.FindPath(start, goal, gridSize, (pos) => obstacles.Contains(pos));

            if (path == null)
            {
                Debug.Log("✓ Корректно определено отсутствие пути");
            }
            else
            {
                Debug.LogError("✗ Должен был вернуть null для заблокированного пути!");
            }
        }

        /// <summary>
        /// Тест поиска пути с препятствиями
        /// </summary>
        private void TestSimpleAStarWithObstacles()
        {
            Debug.Log("Тест: Поиск пути с препятствиями");

            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int goal = new Vector2Int(4, 0);
            Vector2Int gridSize = new Vector2Int(5, 5);

            // Создание препятствия, заставляющего идти в обход
            var obstacles = new HashSet<Vector2Int>
            {
                new Vector2Int(2, 0),
                new Vector2Int(2, 1)
            };

            var path = SimpleAStar.FindPath(start, goal, gridSize, (pos) => obstacles.Contains(pos));

            if (path != null && path.Count > 0)
            {
                Debug.Log($"✓ Путь с обходом найден: {path.Count} шагов");
                if (showTestResults)
                {
                    Debug.Log($"Путь: {string.Join(" -> ", path)}");
                }

                // Проверка, что путь не проходит через препятствия
                bool pathValid = true;
                foreach (var pos in path)
                {
                    if (obstacles.Contains(pos))
                    {
                        pathValid = false;
                        break;
                    }
                }

                if (pathValid)
                {
                    Debug.Log("✓ Путь корректно обходит препятствия");
                }
                else
                {
                    Debug.LogError("✗ Путь проходит через препятствия!");
                }
            }
            else
            {
                Debug.LogError("✗ Путь с обходом не найден!");
            }
        }

        /// <summary>
        /// Тест детерминированного поведения
        /// </summary>
        private void TestDeterministicBehavior()
        {
            Debug.Log("Тест: Детерминированное поведение");

            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int goal = new Vector2Int(3, 3);
            Vector2Int gridSize = new Vector2Int(5, 5);

            // Создание ситуации с несколькими равнозначными путями
            var obstacles = new HashSet<Vector2Int>
            {
                new Vector2Int(1, 1),
                new Vector2Int(2, 2)
            };

            var path1 = SimpleAStar.FindPath(start, goal, gridSize, (pos) => obstacles.Contains(pos));
            var path2 = SimpleAStar.FindPath(start, goal, gridSize, (pos) => obstacles.Contains(pos));

            if (path1 != null && path2 != null)
            {
                bool pathsEqual = path1.Count == path2.Count;
                if (pathsEqual)
                {
                    for (int i = 0; i < path1.Count; i++)
                    {
                        if (path1[i] != path2[i])
                        {
                            pathsEqual = false;
                            break;
                        }
                    }
                }

                if (pathsEqual)
                {
                    Debug.Log("✓ Детерминированное поведение подтверждено");
                }
                else
                {
                    Debug.LogError("✗ Пути различаются при одинаковых условиях!");
                    if (showTestResults)
                    {
                        Debug.Log($"Путь 1: {string.Join(" -> ", path1)}");
                        Debug.Log($"Путь 2: {string.Join(" -> ", path2)}");
                    }
                }
            }
            else
            {
                Debug.LogError("✗ Один из путей не найден!");
            }
        }

        /// <summary>
        /// Базовые тесты GridManager
        /// </summary>
        private void TestGridManagerBasics()
        {
            Debug.Log("Тест: Базовая функциональность GridManager");

            if (GridManager.Instance == null)
            {
                Debug.LogWarning("⚠ GridManager не найден в сцене, пропускаем тест");
                return;
            }

            var gridManager = GridManager.Instance;

            // Тест валидности позиций
            bool validPos = gridManager.IsValidPosition(new Vector2Int(5, 5));
            bool invalidPos = gridManager.IsValidPosition(new Vector2Int(-1, -1));

            if (validPos && !invalidPos)
            {
                Debug.Log("✓ Проверка валидности позиций работает корректно");
            }
            else
            {
                Debug.LogError("✗ Проблема с проверкой валидности позиций");
            }

            // Тест конвертации координат
            Vector2Int gridPos = new Vector2Int(3, 3);
            Vector3 worldPos = gridManager.GridToWorld(gridPos);
            Vector2Int backToGrid = gridManager.WorldToGrid(worldPos);

            if (gridPos == backToGrid)
            {
                Debug.Log("✓ Конвертация координат работает корректно");
            }
            else
            {
                Debug.LogError($"✗ Ошибка конвертации: {gridPos} -> {worldPos} -> {backToGrid}");
            }
        }

        /// <summary>
        /// Тест производительности для больших сеток
        /// </summary>
        [ContextMenu("Тест производительности")]
        public void TestPerformance()
        {
            Debug.Log("Тест производительности...");

            Vector2Int gridSize = new Vector2Int(50, 50);
            Vector2Int start = new Vector2Int(0, 0);
            Vector2Int goal = new Vector2Int(49, 49);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var path = SimpleAStar.FindPath(start, goal, gridSize, (pos) => false);

            stopwatch.Stop();

            if (path != null)
            {
                Debug.Log($"✓ Путь на сетке {gridSize.x}x{gridSize.y} найден за {stopwatch.ElapsedMilliseconds} мс");
                Debug.Log($"Длина пути: {path.Count} шагов");
            }
            else
            {
                Debug.LogError("✗ Путь не найден в тесте производительности");
            }
        }

        /// <summary>
        /// Бенчмарк для множественных запросов
        /// </summary>
        [ContextMenu("Бенчмарк множественных запросов")]
        public void BenchmarkMultipleRequests()
        {
            Debug.Log("Бенчмарк множественных запросов...");

            Vector2Int gridSize = new Vector2Int(20, 20);
            int requestCount = 100;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < requestCount; i++)
            {
                Vector2Int start = new Vector2Int(i % gridSize.x, (i * 2) % gridSize.y);
                Vector2Int goal = new Vector2Int((i * 3) % gridSize.x, (i * 5) % gridSize.y);

                SimpleAStar.FindPath(start, goal, gridSize, (pos) => false);
            }

            stopwatch.Stop();

            Debug.Log($"✓ {requestCount} запросов выполнено за {stopwatch.ElapsedMilliseconds} мс");
            Debug.Log($"Среднее время на запрос: {stopwatch.ElapsedMilliseconds / (float)requestCount:F2} мс");
        }
    }
}