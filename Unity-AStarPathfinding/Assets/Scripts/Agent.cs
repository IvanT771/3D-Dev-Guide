using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PathfindingSystem
{
    /// <summary>
    /// Агент, способный перемещаться по сетке с использованием A* алгоритма
    /// Поддерживает взаимодействие с другими агентами и обработку очередей
    /// </summary>
    public class Agent : MonoBehaviour
    {
        [Header("Настройки движения")]
        [SerializeField] private float moveSpeed = 2.0f;
        [SerializeField] private float rotationSpeed = 10.0f;
        [SerializeField] private bool showPath = true;
        [SerializeField] private bool showTarget = true;

        [Header("Настройки поведения")]
        [SerializeField] private float pathfindingInterval = 0.5f;
        [SerializeField] private float stuckTimeout = 2.0f;
        [SerializeField] private int maxPathfindingAttempts = 3;

        [Header("Визуализация")]
        [SerializeField] private Color pathColor = Color.green;
        [SerializeField] private Color targetColor = Color.red;
        [SerializeField] private LineRenderer pathRenderer;

        // Состояние агента
        private Vector2Int currentGridPosition;
        private Vector2Int targetGridPosition;
        private List<Vector2Int> currentPath;
        private int pathIndex;
        private bool isMoving;
        private bool isRegistered;

        // Состояние ожидания и расталкивания
        private bool isWaiting;
        private Vector2Int waitingForPosition;
        private float lastMoveTime;
        private int pathfindingAttempts;

        // Корутины
        private Coroutine moveCoroutine;
        private Coroutine pathfindingCoroutine;

        // События
        public System.Action<Agent> OnReachedTarget;
        public System.Action<Agent> OnStuck;
        public System.Action<Agent, Vector2Int> OnPositionChanged;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeAgent();
        }

        private void Update()
        {
            CheckStuckState();
        }

        private void OnDrawGizmos()
        {
            if (!showPath && !showTarget) return;

            DrawPath();
            DrawTarget();
        }

        private void OnDestroy()
        {
            UnregisterFromGrid();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Инициализация агента
        /// </summary>
        private void InitializeAgent()
        {
            if (GridManager.Instance == null)
            {
                Debug.LogError($"GridManager не найден! Агент {name} не может быть инициализирован.");
                return;
            }

            // Установка начальной позиции на сетке
            currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
            RegisterOnGrid();

            // Инициализация компонентов визуализации
            InitializePathRenderer();

            // Запуск корутины автоматического поиска пути
            StartPathfindingCoroutine();

            lastMoveTime = Time.time;
        }

        /// <summary>
        /// Инициализация компонента отрисовки пути
        /// </summary>
        private void InitializePathRenderer()
        {
            if (pathRenderer == null)
            {
                pathRenderer = gameObject.AddComponent<LineRenderer>();
            }

            pathRenderer.material = new Material(Shader.Find("Sprites/Default"));
            pathRenderer.color = pathColor;
            pathRenderer.startWidth = 0.1f;
            pathRenderer.endWidth = 0.1f;
            pathRenderer.positionCount = 0;
        }

        #endregion

        #region Grid Registration

        /// <summary>
        /// Регистрация агента на сетке
        /// </summary>
        private void RegisterOnGrid()
        {
            if (GridManager.Instance != null && !isRegistered)
            {
                isRegistered = GridManager.Instance.RegisterAgent(this, currentGridPosition);
                if (isRegistered)
                {
                    // Установка позиции в центр клетки сетки
                    transform.position = GridManager.Instance.GridToWorld(currentGridPosition);
                }
            }
        }

        /// <summary>
        /// Снятие агента с регистрации на сетке
        /// </summary>
        private void UnregisterFromGrid()
        {
            if (GridManager.Instance != null && isRegistered)
            {
                GridManager.Instance.RemoveAgent(this);
                isRegistered = false;
            }
        }

        #endregion

        #region Movement

        /// <summary>
        /// Установка целевой позиции для агента
        /// </summary>
        public void SetTarget(Vector2Int gridTarget)
        {
            if (!GridManager.Instance.IsValidPosition(gridTarget) || 
                GridManager.Instance.IsObstacle(gridTarget))
            {
                Debug.LogWarning($"Невалидная цель для агента {name}: {gridTarget}");
                return;
            }

            targetGridPosition = gridTarget;
            RequestNewPath();
        }

        /// <summary>
        /// Установка целевой позиции в мировых координатах
        /// </summary>
        public void SetTarget(Vector3 worldTarget)
        {
            Vector2Int gridTarget = GridManager.Instance.WorldToGrid(worldTarget);
            SetTarget(gridTarget);
        }

        /// <summary>
        /// Запрос нового пути к цели
        /// </summary>
        private void RequestNewPath()
        {
            if (GridManager.Instance == null) return;

            var path = GridManager.Instance.FindPath(currentGridPosition, targetGridPosition, this);
            
            if (path != null && path.Count > 1)
            {
                currentPath = path;
                pathIndex = 1; // Начинаем с первого шага (0 - текущая позиция)
                pathfindingAttempts = 0;
                
                if (!isMoving)
                {
                    StartMovement();
                }
            }
            else
            {
                pathfindingAttempts++;
                
                if (pathfindingAttempts >= maxPathfindingAttempts)
                {
                    Debug.Log($"Агент {name} не может найти путь к цели {targetGridPosition}");
                    OnStuck?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Запуск движения по пути
        /// </summary>
        private void StartMovement()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }

            moveCoroutine = StartCoroutine(MoveAlongPath());
        }

        /// <summary>
        /// Корутина движения по пути
        /// </summary>
        private IEnumerator MoveAlongPath()
        {
            isMoving = true;

            while (pathIndex < currentPath.Count)
            {
                Vector2Int nextGridPos = currentPath[pathIndex];
                
                // Попытка движения к следующей позиции
                if (GridManager.Instance.TryMoveAgent(this, currentGridPosition, nextGridPos))
                {
                    // Успешное движение
                    yield return StartCoroutine(MoveToGridPosition(nextGridPos));
                    currentGridPosition = nextGridPos;
                    pathIndex++;
                    lastMoveTime = Time.time;
                    isWaiting = false;

                    OnPositionChanged?.Invoke(this, currentGridPosition);

                    // Проверка достижения цели
                    if (currentGridPosition == targetGridPosition)
                    {
                        OnReachedTarget?.Invoke(this);
                        break;
                    }
                }
                else
                {
                    // Неудачная попытка движения - ожидание или перепланирование
                    yield return new WaitForSeconds(0.1f);
                    
                    // Проверка необходимости перепланирования пути
                    if (Time.time - lastMoveTime > stuckTimeout * 0.5f)
                    {
                        RequestNewPath();
                        break;
                    }
                }
            }

            isMoving = false;
        }

        /// <summary>
        /// Плавное движение к позиции на сетке
        /// </summary>
        private IEnumerator MoveToGridPosition(Vector2Int gridPosition)
        {
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = GridManager.Instance.GridToWorld(gridPosition);
            
            float journey = 0.0f;
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            float journeyTime = journeyLength / moveSpeed;

            // Поворот к цели
            Vector3 direction = (targetPosition - startPosition).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                float rotationJourney = 0.0f;
                Quaternion startRotation = transform.rotation;

                while (rotationJourney <= journeyTime * 0.5f) // Поворот в первой половине движения
                {
                    rotationJourney += Time.deltaTime;
                    float fractionOfJourney = rotationJourney / (journeyTime * 0.5f);
                    transform.rotation = Quaternion.Slerp(startRotation, targetRotation, fractionOfJourney);
                    yield return null;
                }
            }

            // Движение к цели
            journey = 0.0f;
            while (journey <= journeyTime)
            {
                journey += Time.deltaTime;
                float fractionOfJourney = journey / journeyTime;
                transform.position = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);
                yield return null;
            }

            transform.position = targetPosition;
        }

        #endregion

        #region Queue and Push Handling

        /// <summary>
        /// Обработка ожидания в очереди
        /// </summary>
        public void OnWaitingInQueue(Vector2Int position)
        {
            isWaiting = true;
            waitingForPosition = position;
            Debug.Log($"Агент {name} ожидает в очереди на позицию {position}");
        }

        /// <summary>
        /// Обработка истечения времени ожидания в очереди
        /// </summary>
        public void OnQueueTimeout()
        {
            isWaiting = false;
            Debug.Log($"Агент {name} покидает очередь из-за таймаута");
            
            // Запрос нового пути после таймаута
            RequestNewPath();
        }

        /// <summary>
        /// Обработка расталкивания другим агентом
        /// </summary>
        public void OnPushed(Vector2Int newPosition)
        {
            Debug.Log($"Агент {name} был оттолкнут на позицию {newPosition}");
            
            // Остановка текущего движения
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                isMoving = false;
            }

            // Обновление позиции и запрос нового пути
            currentGridPosition = newPosition;
            transform.position = GridManager.Instance.GridToWorld(currentGridPosition);
            
            OnPositionChanged?.Invoke(this, currentGridPosition);
            
            // Небольшая задержка перед запросом нового пути
            StartCoroutine(DelayedPathRequest(0.2f));
        }

        /// <summary>
        /// Задержанный запрос пути
        /// </summary>
        private IEnumerator DelayedPathRequest(float delay)
        {
            yield return new WaitForSeconds(delay);
            RequestNewPath();
        }

        #endregion

        #region Pathfinding Coroutine

        /// <summary>
        /// Запуск корутины автоматического поиска пути
        /// </summary>
        private void StartPathfindingCoroutine()
        {
            if (pathfindingCoroutine != null)
            {
                StopCoroutine(pathfindingCoroutine);
            }

            pathfindingCoroutine = StartCoroutine(PathfindingLoop());
        }

        /// <summary>
        /// Основная корутина поиска пути
        /// </summary>
        private IEnumerator PathfindingLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(pathfindingInterval);

                // Проверка необходимости перепланирования
                if (ShouldReplanning())
                {
                    RequestNewPath();
                }
            }
        }

        /// <summary>
        /// Проверка необходимости перепланирования пути
        /// </summary>
        private bool ShouldReplanning()
        {
            // Нет цели
            if (targetGridPosition == Vector2Int.zero) return false;
            
            // Уже на цели
            if (currentGridPosition == targetGridPosition) return false;
            
            // Агент застрял
            if (Time.time - lastMoveTime > stuckTimeout) return true;
            
            // Нет текущего пути
            if (currentPath == null || currentPath.Count == 0) return true;
            
            // Путь заблокирован
            return IsPathBlocked();
        }

        /// <summary>
        /// Проверка блокировки пути
        /// </summary>
        private bool IsPathBlocked()
        {
            if (currentPath == null || pathIndex >= currentPath.Count) return false;

            // Проверка следующих нескольких шагов пути
            int checkSteps = Mathf.Min(3, currentPath.Count - pathIndex);
            
            for (int i = pathIndex; i < pathIndex + checkSteps; i++)
            {
                Vector2Int pos = currentPath[i];
                if (GridManager.Instance.IsObstacle(pos) || 
                    (GridManager.Instance.IsOccupied(pos) && pos != targetGridPosition))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Stuck Detection

        /// <summary>
        /// Проверка состояния застревания
        /// </summary>
        private void CheckStuckState()
        {
            if (targetGridPosition != Vector2Int.zero && 
                currentGridPosition != targetGridPosition &&
                Time.time - lastMoveTime > stuckTimeout)
            {
                Debug.LogWarning($"Агент {name} застрял на позиции {currentGridPosition}");
                OnStuck?.Invoke(this);
                lastMoveTime = Time.time; // Сброс таймера чтобы избежать спама
            }
        }

        #endregion

        #region Visualization

        /// <summary>
        /// Отрисовка пути агента
        /// </summary>
        private void DrawPath()
        {
            if (!showPath || currentPath == null || currentPath.Count < 2)
            {
                if (pathRenderer != null)
                    pathRenderer.positionCount = 0;
                return;
            }

            // Обновление LineRenderer
            pathRenderer.positionCount = currentPath.Count;
            
            for (int i = 0; i < currentPath.Count; i++)
            {
                Vector3 worldPos = GridManager.Instance.GridToWorld(currentPath[i]);
                worldPos.y += 0.1f; // Немного приподнять над землёй
                pathRenderer.SetPosition(i, worldPos);
            }

            // Gizmos для редактора
            Gizmos.color = pathColor;
            for (int i = pathIndex; i < currentPath.Count - 1; i++)
            {
                Vector3 from = GridManager.Instance.GridToWorld(currentPath[i]);
                Vector3 to = GridManager.Instance.GridToWorld(currentPath[i + 1]);
                from.y += 0.1f;
                to.y += 0.1f;
                Gizmos.DrawLine(from, to);
            }
        }

        /// <summary>
        /// Отрисовка цели агента
        /// </summary>
        private void DrawTarget()
        {
            if (!showTarget || targetGridPosition == Vector2Int.zero) return;

            Gizmos.color = targetColor;
            Vector3 targetPos = GridManager.Instance.GridToWorld(targetGridPosition);
            targetPos.y += 0.2f;
            Gizmos.DrawWireSphere(targetPos, 0.5f);
        }

        #endregion

        #region Public Properties and Methods

        public Vector2Int CurrentGridPosition => currentGridPosition;
        public Vector2Int TargetGridPosition => targetGridPosition;
        public bool IsMoving => isMoving;
        public bool IsWaiting => isWaiting;
        public bool HasPath => currentPath != null && currentPath.Count > 0;

        /// <summary>
        /// Принудительная остановка агента
        /// </summary>
        public void Stop()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                isMoving = false;
            }

            if (pathfindingCoroutine != null)
            {
                StopCoroutine(pathfindingCoroutine);
            }

            currentPath = null;
            targetGridPosition = Vector2Int.zero;
        }

        /// <summary>
        /// Возобновление работы агента
        /// </summary>
        public void Resume()
        {
            StartPathfindingCoroutine();
        }

        #endregion
    }
}