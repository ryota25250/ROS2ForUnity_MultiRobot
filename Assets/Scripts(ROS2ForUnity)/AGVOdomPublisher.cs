using UnityEngine;
using ROS2;
using Unity.Robotics.Core;

public class AGVOdomPublisher : MonoBehaviour
{
    [Header("Robot Namespace")]
    public string robotNamespace = "robot1";

    [Header("References")]
    public Transform baseLink;

    [Header("Publish Settings")]
    public float publishRateHz = 20f;

    private ROS2Node node;
    private IPublisher<nav_msgs.msg.Odometry> publisher;

    private Vector3 lastPosition;
    private float lastYawDeg;
    private double lastPublishTime;

    private string NsTopic(string name)
    {
        return string.IsNullOrEmpty(robotNamespace) ? "/" + name : "/" + robotNamespace + "/" + name;
    }

    private string NsFrame(string name)
    {
        return string.IsNullOrEmpty(robotNamespace) ? name : robotNamespace + "/" + name;
    }

    private string NodeName(string name)
    {
        return string.IsNullOrEmpty(robotNamespace) ? name : robotNamespace + "_" + name;
    }

    private geometry_msgs.msg.Vector3 ToRosVector3(Vector3 v)
    {
        return new geometry_msgs.msg.Vector3
        {
            X = v.z,
            Y = -v.x,
            Z = v.y
        };
    }

    private geometry_msgs.msg.Quaternion ToRosQuaternion(Quaternion q)
    {
        return new geometry_msgs.msg.Quaternion
        {
            X = -q.z,
            Y = q.x,
            Z = -q.y,
            W = q.w
        };
    }

    void Start()
    {
        if (baseLink == null)
        {
            Debug.LogError("AGVOdomPublisher: baseLink is not assigned.", this);
            enabled = false;
            return;
        }

        var ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        if (ros2UnityComponent == null)
        {
            Debug.LogError("AGVOdomPublisher: ROS2UnityComponent not found.", this);
            enabled = false;
            return;
        }

        node = ros2UnityComponent.CreateNode(NodeName("odom_publisher"));
        publisher = node.CreatePublisher<nav_msgs.msg.Odometry>(NsTopic("odom"));

        lastPosition = baseLink.position;
        lastYawDeg = baseLink.eulerAngles.y;
        lastPublishTime = Unity.Robotics.Core.Clock.Now;
    }

    void FixedUpdate()
    {
        double now = Unity.Robotics.Core.Clock.Now;
        double period = 1.0 / publishRateHz;

        if (now - lastPublishTime < period)
            return;

        double dt = now - lastPublishTime;
        if (dt <= 0.0)
            return;

        Vector3 currentPosition = baseLink.position;
        float currentYawDeg = baseLink.eulerAngles.y;

        Vector3 linearVelUnity = (currentPosition - lastPosition) / (float)dt;
        float deltaYawDeg = Mathf.DeltaAngle(lastYawDeg, currentYawDeg);
        float angularZRad = Mathf.Deg2Rad * deltaYawDeg / (float)dt;

        var timestamp = new TimeStamp(now);

        var msg = new nav_msgs.msg.Odometry();
        msg.Header = new std_msgs.msg.Header();
        msg.Header.Frame_id = NsFrame("odom");
        msg.Header.Stamp = new builtin_interfaces.msg.Time
        {
            Sec = timestamp.Seconds,
            Nanosec = timestamp.NanoSeconds
        };

        msg.Child_frame_id = NsFrame("base_link");

        msg.Pose = new geometry_msgs.msg.PoseWithCovariance();
        msg.Pose.Pose = new geometry_msgs.msg.Pose();
        msg.Pose.Pose.Position = new geometry_msgs.msg.Point
        {
            X = currentPosition.z,
            Y = -currentPosition.x,
            Z = currentPosition.y
        };
        msg.Pose.Pose.Orientation = ToRosQuaternion(baseLink.rotation);

        // Covariance は read-only なので、配列ごと代入せず要素を書き込む
        for (int i = 0; i < msg.Pose.Covariance.Length; i++)
            msg.Pose.Covariance[i] = 0.0;

        msg.Pose.Covariance[0] = 0.05;   // x
        msg.Pose.Covariance[7] = 0.05;   // y
        msg.Pose.Covariance[35] = 0.1;   // yaw

        msg.Twist = new geometry_msgs.msg.TwistWithCovariance();
        msg.Twist.Twist = new geometry_msgs.msg.Twist();
        msg.Twist.Twist.Linear = ToRosVector3(linearVelUnity);
        msg.Twist.Twist.Angular = new geometry_msgs.msg.Vector3
        {
            X = 0.0,
            Y = 0.0,
            Z = angularZRad
        };

        // こちらも同様
        for (int i = 0; i < msg.Twist.Covariance.Length; i++)
            msg.Twist.Covariance[i] = 0.0;

        msg.Twist.Covariance[0] = 0.05;   // vx
        msg.Twist.Covariance[7] = 0.05;   // vy
        msg.Twist.Covariance[35] = 0.1;   // wz

        publisher.Publish(msg);

        lastPosition = currentPosition;
        lastYawDeg = currentYawDeg;
        lastPublishTime = now;
    }
}