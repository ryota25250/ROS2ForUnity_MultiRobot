using UnityEngine;
using System.Collections.Generic;
using ROS2;
using Unity.Robotics.Core;

public static class RosGeometry
{
    public static geometry_msgs.msg.Transform ToRosTransform(this Transform t) =>
        new geometry_msgs.msg.Transform
        {
            Translation = t.position.ToRos(),
            Rotation = t.rotation.ToRos()
        };

    public static geometry_msgs.msg.Vector3 ToRos(this Vector3 v) =>
        new geometry_msgs.msg.Vector3 { X = v.z, Y = -v.x, Z = v.y };

    public static geometry_msgs.msg.Quaternion ToRos(this Quaternion q) =>
        new geometry_msgs.msg.Quaternion { X = -q.z, Y = q.x, Z = -q.y, W = q.w };

    public static geometry_msgs.msg.Transform ToTransform(
        this geometry_msgs.msg.Vector3 translation,
        geometry_msgs.msg.Quaternion rotation) =>
        new geometry_msgs.msg.Transform { Translation = translation, Rotation = rotation };
}

public class TransformTreeNode
{
    public Transform Transform { get; private set; }
    public string name => Transform.name;
    public List<TransformTreeNode> Children { get; private set; }
    public bool IsALeafNode => Children.Count == 0;

    public TransformTreeNode(GameObject go)
    {
        Transform = go.transform;
        Children = new List<TransformTreeNode>();

        foreach (Transform child in go.transform)
        {
            if (child.GetComponent<ArticulationBody>() != null)
            {
                Children.Add(new TransformTreeNode(child.gameObject));
            }
        }
    }
}

public class Ros2TransformTreePublisher : MonoBehaviour
{
    [Header("Robot Namespace")]
    public string robotNamespace = "robot1";

    [Header("Publish Settings")]
    [SerializeField] double publishRateHz = 30.0;
    [SerializeField] List<string> globalFrameIds = new List<string> { "odom" };
    [SerializeField] GameObject rootGameObject;

    private double lastPublishTimeSeconds;
    private TransformTreeNode transformRoot;

    private ROS2Node node;
    private IPublisher<tf2_msgs.msg.TFMessage> publisher;

    string NsFrame(string baseFrame) =>
        string.IsNullOrEmpty(robotNamespace) ? baseFrame : $"{robotNamespace}/{baseFrame}";

    string NodeName(string baseName) =>
        string.IsNullOrEmpty(robotNamespace) ? baseName : $"{robotNamespace}_{baseName}";

    double PublishPeriodSeconds => 1.0 / publishRateHz;

    void Start()
    {
        if (rootGameObject == null)
        {
            Debug.LogError("Root Game Object is not set!", this);
            enabled = false;
            return;
        }

        var ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        if (ros2UnityComponent == null)
        {
            Debug.LogError("ROS2UnityComponent not found.", this);
            enabled = false;
            return;
        }

        node = ros2UnityComponent.CreateNode(NodeName("transform_tree_publisher_ros2"));
        publisher = node.CreatePublisher<tf2_msgs.msg.TFMessage>("/tf");

        transformRoot = new TransformTreeNode(rootGameObject);
        lastPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    void FixedUpdate()
    {
        if (Unity.Robotics.Core.Clock.Now < lastPublishTimeSeconds + PublishPeriodSeconds)
            return;

        PublishMessage();
        lastPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    void PublishMessage()
    {
        var tfMessageList = new List<geometry_msgs.msg.TransformStamped>();
        var timestamp = new TimeStamp(Unity.Robotics.Core.Clock.Now);

        if (globalFrameIds.Count > 0)
        {
            tfMessageList.Add(new geometry_msgs.msg.TransformStamped
            {
                Header = new std_msgs.msg.Header
                {
                    Frame_id = NsFrame(globalFrameIds[globalFrameIds.Count - 1]),
                    Stamp = new builtin_interfaces.msg.Time
                    {
                        Sec = timestamp.Seconds,
                        Nanosec = timestamp.NanoSeconds
                    }
                },
                Child_frame_id = NsFrame(transformRoot.name),
                Transform = rootGameObject.transform.ToRosTransform()
            });
        }

        PopulateTFList(tfMessageList, transformRoot, NsFrame, timestamp);

        publisher.Publish(new tf2_msgs.msg.TFMessage
        {
            Transforms = tfMessageList.ToArray()
        });
    }

    static void PopulateTFList(
        List<geometry_msgs.msg.TransformStamped> tfList,
        TransformTreeNode tfNode,
        System.Func<string, string> nsFrame,
        TimeStamp timestamp)
    {
        foreach (var childTf in tfNode.Children)
        {
            tfList.Add(new geometry_msgs.msg.TransformStamped
            {
                Header = new std_msgs.msg.Header
                {
                    Frame_id = nsFrame(tfNode.name),
                    Stamp = new builtin_interfaces.msg.Time
                    {
                        Sec = timestamp.Seconds,
                        Nanosec = timestamp.NanoSeconds
                    }
                },
                Child_frame_id = nsFrame(childTf.name),
                Transform = childTf.Transform.localPosition.ToRos()
                    .ToTransform(childTf.Transform.localRotation.ToRos())
            });

            if (!childTf.IsALeafNode)
                PopulateTFList(tfList, childTf, nsFrame, timestamp);
        }
    }
}