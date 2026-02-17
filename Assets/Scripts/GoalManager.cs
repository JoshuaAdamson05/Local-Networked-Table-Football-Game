using UnityEngine;
using Unity.Netcode;

public class GoalManager : NetworkBehaviour
{
    [SerializeField] private CircleCollider2D _ballBounds;
    private BoxCollider2D _clientGoalCollider;
    private BoxCollider2D _hostGoalCollider;
    private ushort _nextGoalID;

    public NetworkList<GoalEvent> GoalEvents { get; private set; }

    private void Awake()
    {
        GoalEvents = new NetworkList<GoalEvent>();
    }

    private void RegisterGoal(TeamEnums scoringTeam)
    {
        GoalEvent newGoal = new GoalEvent
        {
            GoalID = _nextGoalID++,
            ScoringTeam = scoringTeam
        };
        GoalEvents.Add(newGoal);

        const int maxGoalsToKeep = 2;
        if (GoalEvents.Count > maxGoalsToKeep)
            GoalEvents.RemoveAt(0);
    }

    private void OnGoalEventsChanged(NetworkListEvent<GoalEvent> change)
    {
        if (change.Type == NetworkListEvent<GoalEvent>.EventType.Add)
        {
            GoalEvent newGoal = change.Value;
            GoalEventBus.RaiseGoalScored(newGoal);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            GoalEvents.OnListChanged -= OnGoalEventsChanged;
        }
    }

    public override void OnNetworkSpawn()
    {
        _clientGoalCollider = GameObject.FindGameObjectWithTag("ClientGoal").GetComponent<BoxCollider2D>();
        _hostGoalCollider = GameObject.FindGameObjectWithTag("HostGoal").GetComponent<BoxCollider2D>();

        if (IsServer)
        {
            if (_clientGoalCollider == null)
            {
                Debug.LogError("Client Goal Collider not found. Please ensure there is a GameObject with the 'ClientGoal' tag and a BoxCollider2D component.");
                return;
            }
            if (_hostGoalCollider == null)
            {
                Debug.LogError("Host Goal Collider not found. Please ensure there is a GameObject with the 'HostGoal' tag and a BoxCollider2D component.");
                return;
            }
            if (_ballBounds == null)
            {
                Debug.LogError("Ball reference is not assigned in GoalManager.");
                return;
            }
        }

        if (IsClient)
        {
            GoalEvents.OnListChanged += OnGoalEventsChanged;
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (MatchStateManager.Instance.CurrentMatchState.Value != MatchState.InPlay) return;

        if (hasHostScoredGoal())
        {
            RegisterGoal(TeamEnums.Blue);
            Debug.Log("Host scored a goal!");
        }
        else if (hasClientScoredGoal())
        {
            RegisterGoal(TeamEnums.Red);
            Debug.Log("Client scored a goal!");
        }
    }

    private bool hasHostScoredGoal()
    {
        float goalBottom = _clientGoalCollider.bounds.min.y;
        float ballBottom = _ballBounds.bounds.min.y;

        return ballBottom > goalBottom;
    }

    private bool hasClientScoredGoal()
    {
        float goalTop = _hostGoalCollider.bounds.max.y;
        float ballTop = _ballBounds.bounds.max.y;
        return ballTop < goalTop;
    }
}
