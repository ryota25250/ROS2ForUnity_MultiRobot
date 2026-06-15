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
    public bool publishTf = false;
    public bool useInitialPoseAsOdomOrigin = true;
    public string odomFrameId = "odom";
    public string baseLinkFrameId = "base_link";

    private ROS2Node node;
    private IPublisher<nav_msgs.msg.Odometry> publisher;
    private IPublisher<tf2_msgs.msg.TFMessage> tfPublisher;

    private Vector3 lastPosition;
    private float lastYawDeg;
    private double lastPublishTime;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

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

    private geometry_msgs.msg.Point ToRosPoint(Vector3 v)
    {
        return new geometry_msgs.msg.Point
        {
            X = v.z,
            Y = -v.x,
            Z = v.y
        };
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
        if (publishTf)
            tfPublisher = node.CreatePublisher<tf2_msgs.msg.TFMessage>("tf");

        initialPosition = baseLink.position;
        initialRotation = baseLink.rotation;

        Vector3 initialRelativePosition = GetRelativePosition();
        Quaternion initialRelativeRotation = GetRelativeRotation();

        lastPosition = initialRelativePosition;
        lastYawDeg = initialRelativeRotation.eulerAngles.y;
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

        Vector3 currentPosition = GetRelativePosition();
        Quaternion currentRotation = GetRelativeRotation();
        float currentYawDeg = currentRotation.eulerAngles.y;

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

        msg.Child_frame_id = NsFrame(baseLinkFrameId);

        msg.Pose = new geometry_msgs.msg.PoseWithCovariance();
        msg.Pose.Pose = new geometry_msgs.msg.Pose();
        msg.Pose.Pose.Position = ToRosPoint(currentPosition);
        msg.Pose.Pose.Orientation = ToRosQuaternion(currentRotation);

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

        if (publishTf && tfPublisher != null)
        {
            var tfMessage = new tf2_msgs.msg.TFMessage
            {
                Transforms = new[]
                {
                    new geometry_msgs.msg.TransformStamped
                    {
                        Header = new std_msgs.msg.Header
                        {
                            Frame_id = NsFrame(odomFrameId),
                            Stamp = new builtin_interfaces.msg.Time
                            {
                                Sec = timestamp.Seconds,
                                Nanosec = timestamp.NanoSeconds
                            }
                        },
                        Child_frame_id = NsFrame(baseLinkFrameId),
                        Transform = new geometry_msgs.msg.Transform
                        {
                            Translation = ToRosVector3(currentPosition),
                            Rotation = ToRosQuaternion(currentRotation)
                        }
                    }
                }
            };
            tfPublisher.Publish(tfMessage);
        }

        lastPosition = currentPosition;
        lastYawDeg = currentYawDeg;
        lastPublishTime = now;
    }

    private Vector3 GetRelativePosition()
    {
        if (!useInitialPoseAsOdomOrigin)
            return baseLink.position;

        return Quaternion.Inverse(initialRotation) * (baseLink.position - initialPosition);
    }

    private Quaternion GetRelativeRotation()
    {
        if (!useInitialPoseAsOdomOrigin)
            return baseLink.rotation;

        return Quaternion.Inverse(initialRotation) * baseLink.rotation;
    }
}
