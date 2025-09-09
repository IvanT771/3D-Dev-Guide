using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathfindingSystem
{
    /// <summary>
    /// Простая детерминированная реализация алгоритма A* для поиска пути на сетке
    /// Обеспечивает воспроизводимые результаты при одинаковых входных данных
    /// </summary>
    public static class SimpleAStar
    {
        /// <summary>
        /// Структура узла для алгоритма A*
        /// </summary>
        private struct Node : IComparable<Node>
        {
            public Vector2Int position;
            public int gCost;      // Стоимость пути от старта
            public int hCost;      // Эвристическая стоимость до цели
            public int fCost => gCost + hCost;
            public Vector2Int parent;
            public bool hasParent;

            public Node(Vector2Int pos, int g, int h, Vector2Int parentPos, bool parent)
            {
                position = pos;
                gCost = g;
                hCost = h;
                parent = parentPos;
                hasParent = parent;
            }

            // Детерминированное сравнение для консистентного поведения
            public int CompareTo(Node other)
            {
                int compare = fCost.CompareTo(other.fCost);
                if (compare == 0)
                {
                    compare = hCost.CompareTo(other.hCost);
                    if (compare == 0)
                    {
                        // Детерминированное разрешение конфликтов по координатам
                        compare = position.x.CompareTo(other.position.x);
                        if (compare == 0)
                            compare = position.y.CompareTo(other.position.y);
                    }
                }
                return compare;
            }
        }

        /// <summary>
        /// Направления движения (4-связность для детерминизма)
        /// </summary>
        private static readonly Vector2Int[] directions = new Vector2Int[]
        {
            Vector2Int.up,      // (0, 1)
            Vector2Int.right,   // (1, 0)  
            Vector2Int.down,    // (0, -1)
            Vector2Int.left     // (-1, 0)
        };

        /// <summary>
        /// Поиск пути от начальной до конечной точки на сетке
        /// </summary>
        /// <param name="start">Начальная позиция</param>
        /// <param name="goal">Целевая позиция</param>
        /// <param name="gridSize">Размер сетки</param>
        /// <param name="isObstacle">Функция проверки препятствий</param>
        /// <param name="isOccupied">Функция проверки занятости клетки другими агентами</param>
        /// <returns>Список позиций пути или null если путь не найден</returns>
        public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, Vector2Int gridSize, 
            Func<Vector2Int, bool> isObstacle, Func<Vector2Int, bool> isOccupied = null)
        {
            // Проверка валидности входных данных
            if (!IsValidPosition(start, gridSize) || !IsValidPosition(goal, gridSize))
                return null;

            if (isObstacle(start) || isObstacle(goal))
                return null;

            if (start == goal)
                return new List<Vector2Int> { start };

            // Инициализация структур данных для A*
            var openSet = new SortedSet<Node>();
            var closedSet = new HashSet<Vector2Int>();
            var nodeMap = new Dictionary<Vector2Int, Node>();

            // Создание стартового узла
            var startNode = new Node(start, 0, GetHeuristic(start, goal), Vector2Int.zero, false);
            openSet.Add(startNode);
            nodeMap[start] = startNode;

            // Основной цикл A*
            while (openSet.Count > 0)
            {
                // Извлечение узла с наименьшей стоимостью
                var current = openSet.Min;
                openSet.Remove(current);
                closedSet.Add(current.position);

                // Проверка достижения цели
                if (current.position == goal)
                {
                    return ReconstructPath(nodeMap, goal);
                }

                // Обработка соседних узлов
                foreach (var direction in directions)
                {
                    var neighborPos = current.position + direction;

                    // Проверка валидности позиции
                    if (!IsValidPosition(neighborPos, gridSize) || 
                        closedSet.Contains(neighborPos) ||
                        isObstacle(neighborPos))
                        continue;

                    // Проверка занятости (если цель не занята)
                    if (isOccupied != null && neighborPos != goal && isOccupied(neighborPos))
                        continue;

                    int tentativeGCost = current.gCost + 1; // Стоимость движения = 1
                    
                    // Проверка, найден ли лучший путь к соседу
                    if (nodeMap.TryGetValue(neighborPos, out Node existingNode))
                    {
                        if (tentativeGCost >= existingNode.gCost)
                            continue;
                        
                        // Удаление старого узла из openSet
                        openSet.Remove(existingNode);
                    }

                    // Создание нового узла
                    var neighborNode = new Node(
                        neighborPos, 
                        tentativeGCost, 
                        GetHeuristic(neighborPos, goal),
                        current.position,
                        true
                    );

                    nodeMap[neighborPos] = neighborNode;
                    openSet.Add(neighborNode);
                }
            }

            // Путь не найден
            return null;
        }

        /// <summary>
        /// Восстановление пути из карты узлов
        /// </summary>
        private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Node> nodeMap, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            var current = goal;

            while (nodeMap.TryGetValue(current, out Node node))
            {
                path.Add(current);
                if (!node.hasParent)
                    break;
                current = node.parent;
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Манхэттенская эвристика для расчёта расстояния
        /// Обеспечивает детерминированное поведение
        /// </summary>
        private static int GetHeuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Проверка валидности позиции на сетке
        /// </summary>
        private static bool IsValidPosition(Vector2Int position, Vector2Int gridSize)
        {
            return position.x >= 0 && position.x < gridSize.x && 
                   position.y >= 0 && position.y < gridSize.y;
        }

        /// <summary>
        /// Проверка доступности соседних клеток для позиции
        /// Используется для определения проходимости
        /// </summary>
        public static List<Vector2Int> GetNeighbors(Vector2Int position, Vector2Int gridSize)
        {
            var neighbors = new List<Vector2Int>();
            
            foreach (var direction in directions)
            {
                var neighbor = position + direction;
                if (IsValidPosition(neighbor, gridSize))
                {
                    neighbors.Add(neighbor);
                }
            }
            
            return neighbors;
        }
    }
}