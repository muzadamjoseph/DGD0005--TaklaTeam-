// Pong — Step 7. The one real ball. Moved on the SERVER only; NetworkTransform (client
// authoritative OFF) sends its position to every client. Clients only draw it.
// Movement is LocalBall's arithmetic, with the paddles taken from PaddleController.All.
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkBall : NetworkBehaviour
{
    public static NetworkBall Instance;

    [SerializeField] private float speed = 7f;

    /// Grey when false (waiting for a serve), white when true.
    public readonly SyncVar<bool> InPlay = new SyncVar<bool>();

    private Vector3 _velocity;                 // exists on the server only

    private void Awake()
    {
        Instance = this;
        InPlay.OnChange += OnInPlayChanged;
        Court.Paint(gameObject, Color.grey);
    }

    private void OnDestroy()
    {
        InPlay.OnChange -= OnInPlayChanged;
        if (Instance == this) Instance = null;
    }

    private void OnInPlayChanged(bool prev, bool next, bool asServer)
    {
        Court.Paint(gameObject, next ? Color.white : Color.grey);
    }

    /// Server only: launch the ball toward a side (0 = LEFT, 1 = RIGHT).
    public void Serve(int towardSide)
    {
        if (!IsServerInitialized) return;
        float dx = towardSide == PaddleController.Left ? -1f : 1f;
        float dz = Random.Range(-0.6f, 0.6f);
        _velocity = new Vector3(dx, 0, dz).normalized * speed;
        InPlay.Value = true;
    }

    private void Update()
    {
        if (!IsServerInitialized) return;      // clients never move the ball
        if (!InPlay.Value) return;

        Vector3 p = transform.position + _velocity * Time.deltaTime;

        // walls
        float zLimit = Court.HalfHeight - Court.BallRadius;
        if (Mathf.Abs(p.z) > zLimit) { p.z = Mathf.Sign(p.z) * zLimit; _velocity.z = -_velocity.z; }

        // paddles: the server's copies, as last reported by their owners
        foreach (PaddleController paddle in PaddleController.All)
        {
            if (paddle == null || paddle.Side.Value == PaddleController.Spectator) continue;
            Vector3 pp = paddle.transform.position;
            bool towards = (pp.x < 0 && _velocity.x < 0) || (pp.x > 0 && _velocity.x > 0);
            if (!towards) continue;
            if (Mathf.Abs(p.x - pp.x) < 0.5f && Mathf.Abs(p.z - pp.z) < Court.PaddleHalfLength + Court.BallRadius)
            {
                _velocity.x = -_velocity.x;
                _velocity.z += (p.z - pp.z) * 3f;              // hit near the end: more angle
                _velocity = _velocity.normalized * speed;
                p.x = pp.x + (pp.x < 0 ? 0.5f : -0.5f);
            }
        }

        transform.position = p;

        // goals
        if (p.x < -Court.HalfWidth - 0.5f)     ScoreFor(PaddleController.Right);
        else if (p.x > Court.HalfWidth + 0.5f) ScoreFor(PaddleController.Left);
    }

    private void ScoreFor(int scorerSide)
    {
        ResetToCentre();
        GameState.Instance.Goal(scorerSide);
    }

    private void ResetToCentre()
    {
        transform.position = new Vector3(0, Court.BallY, 0);
        _velocity = Vector3.zero;
        InPlay.Value = false;
    }
}
