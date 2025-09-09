using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PathfindingSystem
{
    /// <summary>
    /// Менеджер сетки, управляющий картой, агентами и очередями ожидания
    /// Обеспечивает детерминированное поведение системы поиска пути
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Настройки сетки")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
        [SerializeField] private float cellSize = 1.0f;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private Color gridColor = Color.white;
        [SerializeField] private Color obstacleColor = Color.red;
        [SerializeField] private Color agentColor = Color.blue;

        [Header("Настройки очередей")]
        [SerializeField] private int maxQueueSize = 3;
        [SerializeField] private float queueWaitTime = 0.5f;

        // Данные сетки
        private bool[,] obstacles;
        private Dictionary<Vector2Int, Agent> agentPositions;
        private Dictionary<Vector2Int, Queue<Agent>> waitingQueues;
        private Dictionary<Agent, float> queueTimers;

        // События для уведомления агентов
        public event Action<Vector2Int> OnCellBlocked;
        public event Action<Vector2Int> OnCellFreed;

        // Статические ссылки для доступа
        public static GridManager Instance { get; private set; }

        #region Unity Lifecycle

        private void Awake()
        {
            // Паттерн Singleton для глобального доступа
            if (Instance == null)
            {
                Instance = this;
                InitializeGrid();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            ProcessQueues();
        }

        private void OnDrawGizmos()
        {
            if (!showGrid) return;

            DrawGrid();
            DrawObstacles();
            DrawAgents();
            DrawQueues();
        }

        #endregion

        #region Grid Initialization

        /// <summary>
        /// Инициализация сетки и структур данных
        /// </summary>
        private void InitializeGrid()
        {
            obstacles = new bool[gridSize.x, gridSize.y];
            agentPositions = new Dictionary<Vector2Int, Agent>();
            waitingQueues = new Dictionary<Vector2Int, Queue<Agent>>();
            queueTimers = new Dictionary<Agent, float>();

            Debug.Log($"Сетка инициализирована: {gridSize.x}x{gridSize.y}");
        }

        /// <summary>
        /// Установка препятствия на указанной позиции
        /// </summary>
        public void SetObstacle(Vector2Int position, bool isObstacle)
        {
            if (!IsValidPosition(position)) return;

            obstacles[position.x, position.y] = isObstacle;
            
            if (isObstacle)
            {
                // Удаление агента с позиции при установке препятствия
                if (agentPositions.ContainsKey(position))
                {
                    var agent = agentPositions[position];
                    RemoveAgent(agent);
                }
            }
        }

        /// <summary>
        /// Установка нескольких препятствий из массива позиций
        /// </summary>
        public void SetObstacles(Vector2Int[] positions)
        {
            foreach (var pos in positions)
            {
                SetObstacle(pos, true);
            }
        }

        #endregion

        #region Agent Management

        /// <summary>
        /// Регистрация агента на сетке
        /// </summary>
        public bool RegisterAgent(Agent agent, Vector2Int position)
        {
            if (!IsValidPosition(position) || IsObstacle(position))
                return false;

            // Проверка занятости позиции
            if (agentPositions.ContainsKey(position))
            {
                // Добавление в очередь ожидания
                AddToQueue(position, agent);
                return false;
            }

            agentPositions[position] = agent;
            return true;
        }

        /// <summary>
        /// Попытка перемещения агента на новую позицию
        /// </summary>
        public bool TryMoveAgent(Agent agent, Vector2Int fromPosition, Vector2Int toPosition)
        {
            if (!IsValidPosition(toPosition) || IsObstacle(toPosition))
                return false;

            // Проверка занятости целевой позиции
            if (agentPositions.ContainsKey(toPosition))
            {
                var occupyingAgent = agentPositions[toPosition];
                
                // Попытка расталкивания агентов
                if (TryPushAgent(occupyingAgent, toPosition, fromPosition))
                {
                    // Если расталкивание успешно, перемещаем агента
                    agentPositions.Remove(fromPosition);
                    agentPositions[toPosition] = agent;
                    OnCellFreed?.Invoke(fromPosition);
                    return true;
                }
                else
                {
                    // Добавление в очередь ожидания
                    AddToQueue(toPosition, agent);
                    return false;
                }
            }

            // Свободная клетка - обычное перемещение
            agentPositions.Remove(fromPosition);
            agentPositions[toPosition] = agent;
            OnCellFreed?.Invoke(fromPosition);
            ProcessQueueAtPosition(fromPosition);
            return true;
        }

        /// <summary>
        /// Попытка расталкивания агента с позиции
        /// </summary>
        private bool TryPushAgent(Agent targetAgent, Vector2Int currentPos, Vector2Int pushDirection)
        {
            // Поиск свободной соседней клетки для расталкивания
            var neighbors = SimpleAStar.GetNeighbors(currentPos, gridSize);
            
            // Детерминированная сортировка соседей
            neighbors.Sort((a, b) =>
            {
                int compare = a.x.CompareTo(b.x);
                if (compare == 0)
                    compare = a.y.CompareTo(b.y);
                return compare;
            });

            foreach (var neighbor in neighbors)
            {
                if (!IsObstacle(neighbor) && !agentPositions.ContainsKey(neighbor))
                {
                    // Перемещение расталкиваемого агента
                    agentPositions.Remove(currentPos);
                    agentPositions[neighbor] = targetAgent;
                    targetAgent.OnPushed(neighbor);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Удаление агента с сетки
        /// </summary>
        public void RemoveAgent(Agent agent)
        {
            var positionToRemove = Vector2Int.zero;
            bool found = false;

            foreach (var kvp in agentPositions)
            {
                if (kvp.Value == agent)
                {
                    positionToRemove = kvp.Key;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                agentPositions.Remove(positionToRemove);
                OnCellFreed?.Invoke(positionToRemove);
                ProcessQueueAtPosition(positionToRemove);
            }

            // Удаление из очередей и таймеров
            queueTimers.Remove(agent);
            foreach (var queue in waitingQueues.Values)
            {
                if (queue.Contains(agent))
                {
                    var tempList = queue.ToList();
                    queue.Clear();
                    foreach (var a in tempList)
                    {
                        if (a != agent) queue.Enqueue(a);
                    }
                }
            }
        }

        #endregion

        #region Queue Management

        /// <summary>
        /// Добавление агента в очередь ожидания
        /// </summary>
        private void AddToQueue(Vector2Int position, Agent agent)
        {
            if (!waitingQueues.ContainsKey(position))
            {
                waitingQueues[position] = new Queue<Agent>();
            }

            var queue = waitingQueues[position];
            
            if (queue.Count < maxQueueSize && !queue.Contains(agent))
            {
                queue.Enqueue(agent);
                queueTimers[agent] = Time.time + queueWaitTime;
                agent.OnWaitingInQueue(position);
            }
        }

        /// <summary>
        /// Обработка очередей ожидания
        /// </summary>
        private void ProcessQueues()
        {
            var expiredAgents = new List<Agent>();

            // Проверка таймеров агентов в очередях
            foreach (var kvp in queueTimers.ToList())
            {
                if (Time.time >= kvp.Value)
                {
                    expiredAgents.Add(kvp.Key);
                }
            }

            // Обработка агентов с истёкшим временем ожидания
            foreach (var agent in expiredAgents)
            {
                queueTimers.Remove(agent);
                agent.OnQueueTimeout();
            }
        }

        /// <summary>
        /// Обработка очереди на конкретной позиции при её освобождении
        /// </summary>
        private void ProcessQueueAtPosition(Vector2Int position)
        {
            if (!waitingQueues.ContainsKey(position)) return;

            var queue = waitingQueues[position];
            
            while (queue.Count > 0)
            {
                var waitingAgent = queue.Dequeue();
                
                if (waitingAgent != null && agentPositions.ContainsValue(waitingAgent))
                {
                    var agentCurrentPos = GetAgentPosition(waitingAgent);
                    if (agentCurrentPos.HasValue)
                    {
                        // Попытка переместить агента из очереди
                        if (TryMoveAgent(waitingAgent, agentCurrentPos.Value, position))
                        {
                            queueTimers.Remove(waitingAgent);
                            break;
                        }
                    }
                }
            }

            // Удаление пустой очереди
            if (queue.Count == 0)
            {
                waitingQueues.Remove(position);
            }
        }

        #endregion

        #region Pathfinding

        /// <summary>
        /// Поиск пути для агента с учётом других агентов
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, Agent requestingAgent = null)
        {
            return SimpleAStar.FindPath(
                start, 
                goal, 
                gridSize, 
                IsObstacle,
                (pos) => IsOccupiedByOtherAgent(pos, requestingAgent)
            );
        }

        /// <summary>
        /// Проверка занятости клетки другим агентом
        /// </summary>
        private bool IsOccupiedByOtherAgent(Vector2Int position, Agent excludeAgent)
        {
            if (!agentPositions.ContainsKey(position)) return false;
            
            var occupyingAgent = agentPositions[position];
            return occupyingAgent != excludeAgent;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Проверка валидности позиции
        /// </summary>
        public bool IsValidPosition(Vector2Int position)
        {
            return position.x >= 0 && position.x < gridSize.x && 
                   position.y >= 0 && position.y < gridSize.y;
        }

        /// <summary>
        /// Проверка на препятствие
        /// </summary>
        public bool IsObstacle(Vector2Int position)
        {
            if (!IsValidPosition(position)) return true;
            return obstacles[position.x, position.y];
        }

        /// <summary>
        /// Проверка занятости позиции агентом
        /// </summary>
        public bool IsOccupied(Vector2Int position)
        {
            return agentPositions.ContainsKey(position);
        }

        /// <summary>
        /// Получение позиции агента
        /// </summary>
        public Vector2Int? GetAgentPosition(Agent agent)
        {
            foreach (var kvp in agentPositions)
            {
                if (kvp.Value == agent)
                    return kvp.Key;
            }
            return null;
        }

        /// <summary>
        /// Конвертация мировых координат в координаты сетки
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / cellSize),
                Mathf.FloorToInt(worldPosition.z / cellSize)
            );
        }

        /// <summary>
        /// Конвертация координат сетки в мировые координаты
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            return new Vector3(
                gridPosition.x * cellSize + cellSize * 0.5f,
                0,
                gridPosition.y * cellSize + cellSize * 0.5f
            );
        }

        #endregion

        #region Gizmos Drawing

        /// <summary>
        /// Отрисовка сетки в редакторе
        /// </summary>
        private void DrawGrid()
        {
            Gizmos.color = gridColor;
            
            for (int x = 0; x <= gridSize.x; x++)
            {
                Vector3 start = new Vector3(x * cellSize, 0, 0);
                Vector3 end = new Vector3(x * cellSize, 0, gridSize.y * cellSize);
                Gizmos.DrawLine(start, end);
            }
            
            for (int y = 0; y <= gridSize.y; y++)
            {
                Vector3 start = new Vector3(0, 0, y * cellSize);
                Vector3 end = new Vector3(gridSize.x * cellSize, 0, y * cellSize);
                Gizmos.DrawLine(start, end);
            }
        }

        /// <summary>
        /// Отрисовка препятствий
        /// </summary>
        private void DrawObstacles()
        {
            if (obstacles == null) return;

            Gizmos.color = obstacleColor;
            
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    if (obstacles[x, y])
                    {
                        Vector3 center = GridToWorld(new Vector2Int(x, y));
                        Gizmos.DrawCube(center, Vector3.one * cellSize * 0.8f);
                    }
                }
            }
        }

        /// <summary>
        /// Отрисовка агентов
        /// </summary>
        private void DrawAgents()
        {
            Gizmos.color = agentColor;
            
            foreach (var kvp in agentPositions)
            {
                Vector3 center = GridToWorld(kvp.Key);
                Gizmos.DrawSphere(center, cellSize * 0.3f);
            }
        }

        /// <summary>
        /// Отрисовка очередей ожидания
        /// </summary>
        private void DrawQueues()
        {
            Gizmos.color = Color.yellow;
            
            foreach (var kvp in waitingQueues)
            {
                if (kvp.Value.Count > 0)
                {
                    Vector3 center = GridToWorld(kvp.Key);
                    center.y += 0.5f;
                    Gizmos.DrawWireCube(center, Vector3.one * cellSize * 0.6f);
                }
            }
        }

        #endregion

        #region Public Properties

        public Vector2Int GridSize => gridSize;
        public float CellSize => cellSize;
        public int AgentCount => agentPositions.Count;
        public int QueueCount => waitingQueues.Sum(q => q.Value.Count);

        #endregion
    }
}