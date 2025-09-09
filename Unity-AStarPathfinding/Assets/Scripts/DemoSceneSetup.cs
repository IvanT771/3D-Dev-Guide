using UnityEngine;
using PathfindingSystem;

namespace PathfindingSystem
{
    /// <summary>
    /// Демонстрационная сцена для системы поиска пути A*
    /// Создаёт агентов, препятствия и настраивает взаимодействие
    /// </summary>
    public class DemoSceneSetup : MonoBehaviour
    {
        [Header("Настройки демонстрации")]
        [SerializeField] private GameObject agentPrefab;
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private int numberOfAgents = 5;
        [SerializeField] private bool autoSetupObstacles = true;
        [SerializeField] private bool autoSetupAgents = true;

        [Header("Настройки препятствий")]
        [SerializeField] private Vector2Int[] manualObstacles;

        [Header("Настройки агентов")]
        [SerializeField] private Vector2Int[] agentStartPositions;
        [SerializeField] private Vector2Int[] agentTargetPositions;

        private GridManager gridManager;
        private Agent[] agents;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeDemo();
        }

        private void Update()
        {
            HandleInput();
        }

        #endregion

        #region Demo Initialization

        /// <summary>
        /// Инициализация демонстрационной сцены
        /// </summary>
        private void InitializeDemo()
        {
            gridManager = FindObjectOfType<GridManager>();
            
            if (gridManager == null)
            {
                Debug.LogError("GridManager не найден в сцене!");
                return;
            }

            // Ожидание инициализации GridManager
            StartCoroutine(DelayedSetup());
        }

        /// <summary>
        /// Отложенная настройка после инициализации GridManager
        /// </summary>
        private System.Collections.IEnumerator DelayedSetup()
        {
            yield return new WaitForSeconds(0.1f);

            if (autoSetupObstacles)
            {
                CreateDefaultObstacles();
            }
            else
            {
                CreateManualObstacles();
            }

            yield return new WaitForSeconds(0.1f);

            if (autoSetupAgents)
            {
                CreateDefaultAgents();
            }
            else
            {
                CreateManualAgents();
            }

            Debug.Log("Демонстрационная сцена настроена успешно!");
            Debug.Log("Используйте ЛКМ для установки новых целей агентам");
            Debug.Log("Используйте ПКМ для размещения/удаления препятствий");
        }

        #endregion

        #region Obstacle Creation

        /// <summary>
        /// Создание препятствий по умолчанию
        /// </summary>
        private void CreateDefaultObstacles()
        {
            Vector2Int gridSize = gridManager.GridSize;
            
            // Создание лабиринта для демонстрации узких проходов
            var obstacles = new System.Collections.Generic.List<Vector2Int>();

            // Границы (стены по периметру, кроме входов)
            for (int x = 0; x < gridSize.x; x++)
            {
                if (x != gridSize.x / 4 && x != 3 * gridSize.x / 4) // Оставляем проходы
                {
                    obstacles.Add(new Vector2Int(x, 0));
                    obstacles.Add(new Vector2Int(x, gridSize.y - 1));
                }
            }

            for (int y = 0; y < gridSize.y; y++)
            {
                if (y != gridSize.y / 4 && y != 3 * gridSize.y / 4) // Оставляем проходы
                {
                    obstacles.Add(new Vector2Int(0, y));
                    obstacles.Add(new Vector2Int(gridSize.x - 1, y));
                }
            }

            // Внутренние препятствия для создания узких проходов
            int centerX = gridSize.x / 2;
            int centerY = gridSize.y / 2;

            // Вертикальная стена с проходом
            for (int y = 2; y < gridSize.y - 2; y++)
            {
                if (y != centerY) // Оставляем проход в центре
                {
                    obstacles.Add(new Vector2Int(centerX, y));
                }
            }

            // Горизонтальные препятствия
            for (int x = 2; x < centerX - 1; x++)
            {
                obstacles.Add(new Vector2Int(x, centerY + 2));
                obstacles.Add(new Vector2Int(x, centerY - 2));
            }

            for (int x = centerX + 2; x < gridSize.x - 2; x++)
            {
                obstacles.Add(new Vector2Int(x, centerY + 2));
                obstacles.Add(new Vector2Int(x, centerY - 2));
            }

            // Установка препятствий в GridManager
            gridManager.SetObstacles(obstacles.ToArray());

            // Создание визуальных объектов препятствий
            CreateObstacleVisuals(obstacles.ToArray());
        }

        /// <summary>
        /// Создание препятствий вручную
        /// </summary>
        private void CreateManualObstacles()
        {
            if (manualObstacles != null && manualObstacles.Length > 0)
            {
                gridManager.SetObstacles(manualObstacles);
                CreateObstacleVisuals(manualObstacles);
            }
        }

        /// <summary>
        /// Создание визуальных объектов препятствий
        /// </summary>
        private void CreateObstacleVisuals(Vector2Int[] obstaclePositions)
        {
            if (obstaclePrefab == null) return;

            GameObject obstacleParent = new GameObject("Obstacles");
            obstacleParent.transform.SetParent(transform);

            foreach (var obstaclePos in obstaclePositions)
            {
                Vector3 worldPos = gridManager.GridToWorld(obstaclePos);
                GameObject obstacle = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, obstacleParent.transform);
                obstacle.name = $"Obstacle_{obstaclePos.x}_{obstaclePos.y}";
            }
        }

        #endregion

        #region Agent Creation

        /// <summary>
        /// Создание агентов по умолчанию
        /// </summary>
        private void CreateDefaultAgents()
        {
            agents = new Agent[numberOfAgents];
            GameObject agentParent = new GameObject("Agents");
            agentParent.transform.SetParent(transform);

            Vector2Int gridSize = gridManager.GridSize;

            for (int i = 0; i < numberOfAgents; i++)
            {
                // Случайная стартовая позиция (детерминированная)
                Vector2Int startPos = GetRandomValidPosition(gridSize, i * 1000);
                Vector3 worldPos = gridManager.GridToWorld(startPos);

                // Создание агента
                GameObject agentObj;
                if (agentPrefab != null)
                {
                    agentObj = Instantiate(agentPrefab, worldPos, Quaternion.identity, agentParent.transform);
                }
                else
                {
                    agentObj = CreateDefaultAgentObject(worldPos, agentParent.transform);
                }

                agentObj.name = $"Agent_{i}";

                // Получение компонента Agent
                Agent agent = agentObj.GetComponent<Agent>();
                if (agent == null)
                {
                    agent = agentObj.AddComponent<Agent>();
                }

                agents[i] = agent;

                // Установка случайной цели (детерминированная)
                Vector2Int targetPos = GetRandomValidPosition(gridSize, i * 2000 + 1000);
                agent.SetTarget(targetPos);

                // Подписка на события
                agent.OnReachedTarget += OnAgentReachedTarget;
                agent.OnStuck += OnAgentStuck;
            }
        }

        /// <summary>
        /// Создание агентов вручную
        /// </summary>
        private void CreateManualAgents()
        {
            if (agentStartPositions == null || agentStartPositions.Length == 0) return;

            int agentCount = agentStartPositions.Length;
            agents = new Agent[agentCount];
            GameObject agentParent = new GameObject("Agents");
            agentParent.transform.SetParent(transform);

            for (int i = 0; i < agentCount; i++)
            {
                Vector2Int startPos = agentStartPositions[i];
                Vector3 worldPos = gridManager.GridToWorld(startPos);

                // Создание агента
                GameObject agentObj;
                if (agentPrefab != null)
                {
                    agentObj = Instantiate(agentPrefab, worldPos, Quaternion.identity, agentParent.transform);
                }
                else
                {
                    agentObj = CreateDefaultAgentObject(worldPos, agentParent.transform);
                }

                agentObj.name = $"Agent_{i}";

                // Получение компонента Agent
                Agent agent = agentObj.GetComponent<Agent>();
                if (agent == null)
                {
                    agent = agentObj.AddComponent<Agent>();
                }

                agents[i] = agent;

                // Установка цели
                if (agentTargetPositions != null && i < agentTargetPositions.Length)
                {
                    agent.SetTarget(agentTargetPositions[i]);
                }

                // Подписка на события
                agent.OnReachedTarget += OnAgentReachedTarget;
                agent.OnStuck += OnAgentStuck;
            }
        }

        /// <summary>
        /// Создание объекта агента по умолчанию
        /// </summary>
        private GameObject CreateDefaultAgentObject(Vector3 position, Transform parent)
        {
            GameObject agentObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agentObj.transform.position = position;
            agentObj.transform.SetParent(parent);
            agentObj.transform.localScale = Vector3.one * 0.8f;

            // Добавление случайного цвета
            Renderer renderer = agentObj.GetComponent<Renderer>();
            renderer.material.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);

            return agentObj;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Получение случайной валидной позиции (детерминированная)
        /// </summary>
        private Vector2Int GetRandomValidPosition(Vector2Int gridSize, int seed)
        {
            Random.State oldState = Random.state;
            Random.InitState(seed);

            Vector2Int position;
            int attempts = 0;
            const int maxAttempts = 100;

            do
            {
                position = new Vector2Int(
                    Random.Range(1, gridSize.x - 1),
                    Random.Range(1, gridSize.y - 1)
                );
                attempts++;
            } 
            while ((gridManager.IsObstacle(position) || gridManager.IsOccupied(position)) && 
                   attempts < maxAttempts);

            Random.state = oldState;
            return position;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Обработчик достижения цели агентом
        /// </summary>
        private void OnAgentReachedTarget(Agent agent)
        {
            Debug.Log($"Агент {agent.name} достиг цели!");

            // Установка новой случайной цели через 2 секунды
            StartCoroutine(SetNewTargetAfterDelay(agent, 2.0f));
        }

        /// <summary>
        /// Обработчик застревания агента
        /// </summary>
        private void OnAgentStuck(Agent agent)
        {
            Debug.LogWarning($"Агент {agent.name} застрял!");

            // Установка новой цели для застрявшего агента
            Vector2Int newTarget = GetRandomValidPosition(gridManager.GridSize, 
                System.DateTime.Now.Millisecond);
            agent.SetTarget(newTarget);
        }

        /// <summary>
        /// Установка новой цели агенту с задержкой
        /// </summary>
        private System.Collections.IEnumerator SetNewTargetAfterDelay(Agent agent, float delay)
        {
            yield return new WaitForSeconds(delay);

            Vector2Int newTarget = GetRandomValidPosition(gridManager.GridSize, 
                System.DateTime.Now.Millisecond);
            agent.SetTarget(newTarget);
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Обработка пользовательского ввода
        /// </summary>
        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0)) // ЛКМ - установка цели
            {
                HandleLeftClick();
            }
            else if (Input.GetMouseButtonDown(1)) // ПКМ - препятствие
            {
                HandleRightClick();
            }
        }

        /// <summary>
        /// Обработка левого клика мыши (установка цели)
        /// </summary>
        private void HandleLeftClick()
        {
            Vector3 mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector2Int gridPos = gridManager.WorldToGrid(hit.point);
                
                if (gridManager.IsValidPosition(gridPos) && !gridManager.IsObstacle(gridPos))
                {
                    // Установка цели ближайшему агенту
                    Agent nearestAgent = GetNearestAgent(hit.point);
                    if (nearestAgent != null)
                    {
                        nearestAgent.SetTarget(gridPos);
                        Debug.Log($"Новая цель установлена для {nearestAgent.name}: {gridPos}");
                    }
                }
            }
        }

        /// <summary>
        /// Обработка правого клика мыши (препятствие)
        /// </summary>
        private void HandleRightClick()
        {
            Vector3 mousePos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector2Int gridPos = gridManager.WorldToGrid(hit.point);
                
                if (gridManager.IsValidPosition(gridPos))
                {
                    bool currentlyObstacle = gridManager.IsObstacle(gridPos);
                    gridManager.SetObstacle(gridPos, !currentlyObstacle);
                    
                    Debug.Log($"Препятствие {(currentlyObstacle ? "удалено" : "добавлено")} на {gridPos}");
                }
            }
        }

        /// <summary>
        /// Поиск ближайшего агента к указанной точке
        /// </summary>
        private Agent GetNearestAgent(Vector3 worldPosition)
        {
            if (agents == null || agents.Length == 0) return null;

            Agent nearestAgent = null;
            float nearestDistance = float.MaxValue;

            foreach (var agent in agents)
            {
                if (agent == null) continue;

                float distance = Vector3.Distance(agent.transform.position, worldPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestAgent = agent;
                }
            }

            return nearestAgent;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Остановка всех агентов
        /// </summary>
        public void StopAllAgents()
        {
            if (agents == null) return;

            foreach (var agent in agents)
            {
                if (agent != null)
                    agent.Stop();
            }
        }

        /// <summary>
        /// Возобновление работы всех агентов
        /// </summary>
        public void ResumeAllAgents()
        {
            if (agents == null) return;

            foreach (var agent in agents)
            {
                if (agent != null)
                    agent.Resume();
            }
        }

        #endregion
    }
}