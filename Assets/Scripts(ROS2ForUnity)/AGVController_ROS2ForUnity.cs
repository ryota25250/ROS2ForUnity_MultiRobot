using UnityEngine;
using ROS2;

public class AGVController_ROS2ForUnity : MonoBehaviour
{
    public enum ControlMode { Keyboard, ROS };
    public ControlMode mode = ControlMode.Keyboard;

    [Header("Robot Namespace")]
    [Tooltip("robot1 / robot2")]
    public string robotNamespace = "robot1";

    [Header("Robot Components")]
    public GameObject wheel1;
    public GameObject wheel2;

    [Header("Movement Parameters")]
    public float maxLinearSpeed = 2f;
    public float maxRotationalSpeed = 1f;
    public float wheelRadius = 0.033f;
    public float trackWidth = 0.288f;

    [Header("Physics Settings")]
    public float forceLimit = 10f;
    public float damping = 10f;

    [Header("ROS Settings")]
    public float ROSTimeout = 0.5f;

    private ArticulationBody wA1;
    private ArticulationBody wA2;
    private float lastCmdReceivedTimestamp = 0f;

    private ROS2Node node;
    private ISubscription<geometry_msgs.msg.Twist> twistSubscriber;
    private volatile float rosLinear = 0f;
    private volatile float rosAngular = 0f;

    string NsTopic(string baseName) => string.IsNullOrEmpty(robotNamespace) ? $"/{baseName}" : $"/{robotNamespace}/{baseName}";
    string NodeName(string baseName)  => string.IsNullOrEmpty(robotNamespace) ? baseName : $"{robotNamespace}_{baseName}";

    void Start()
    {
        wA1 = wheel1.GetComponent<ArticulationBody>();
        wA2 = wheel2.GetComponent<ArticulationBody>();
        SetParameters(wA1);
        SetParameters(wA2);

        var ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        node = ros2UnityComponent.CreateNode(NodeName("agv_controller_ros2")); // ← 同名重複を避けるため ns 付与
        twistSubscriber = node.CreateSubscription<geometry_msgs.msg.Twist>(
            NsTopic("cmd_vel"), ReceiveROSCmd); // ← /robotN/cmd_vel を購読
    }

    void ReceiveROSCmd(geometry_msgs.msg.Twist cmdVel)
    {
        rosLinear = (float)cmdVel.Linear.X;
        rosAngular = (float)cmdVel.Angular.Z;
    }

    void FixedUpdate()
    {
        float currentLinear = rosLinear;
        float currentAngular = rosAngular;

        if (Mathf.Abs(currentLinear) > 0.001f || Mathf.Abs(currentAngular) > 0.001f)
            lastCmdReceivedTimestamp = Time.time;

        if (mode == ControlMode.Keyboard) KeyBoardUpdate();
        else ROSUpdate(currentLinear, currentAngular);
    }

    private void SetParameters(ArticulationBody joint)
    {
        if (joint == null) return;
        var drive = joint.xDrive;
        drive.forceLimit = forceLimit;
        drive.damping = damping;
        joint.xDrive = drive;
    }

    private void SetSpeed(ArticulationBody joint, float wheelSpeed_deg_s)
    {
        if (joint == null) return;
        var drive = joint.xDrive;
        drive.targetVelocity = wheelSpeed_deg_s;
        joint.xDrive = drive;
    }

    private void KeyBoardUpdate()
    {
        float inputSpeed = Input.GetAxis("Vertical") * maxLinearSpeed;
        float inputRotationSpeed = Input.GetAxis("Horizontal") * maxRotationalSpeed;
        RobotInput(inputSpeed, inputRotationSpeed);
    }

    private void ROSUpdate(float linear, float angular)
    {
        if (Time.time - lastCmdReceivedTimestamp > ROSTimeout)
        {
            linear = 0f; angular = 0f;
        }
        RobotInput(linear, -3.5f * angular);
    }

    private void RobotInput(float speed, float rotSpeed)
    {
        speed = Mathf.Clamp(speed, -maxLinearSpeed, maxLinearSpeed);
        rotSpeed = Mathf.Clamp(rotSpeed, -maxRotationalSpeed, maxRotationalSpeed);

        float wheel1Rotation = (speed / wheelRadius);
        float wheel2Rotation = wheel1Rotation;
        float wheelSpeedDiff = (rotSpeed * trackWidth) / wheelRadius;

        wheel1Rotation = (wheel1Rotation + wheelSpeedDiff) * Mathf.Rad2Deg;
        wheel2Rotation = (wheel2Rotation - wheelSpeedDiff) * Mathf.Rad2Deg;

        SetSpeed(wA1, wheel1Rotation);
        SetSpeed(wA2, wheel2Rotation);
    }
}