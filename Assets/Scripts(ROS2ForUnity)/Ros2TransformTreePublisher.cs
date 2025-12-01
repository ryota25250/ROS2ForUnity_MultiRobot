using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ROS2;
using Unity.Robotics.Core;

public static class RosGeometry
{
    public static geometry_msgs.msg.Transform ToRosTransform(this Transform t) => new geometry_msgs.msg.Transform { Translation = t.position.ToRos(), Rotation = t.rotation.ToRos() };
    public static geometry_msgs.msg.Vector3 ToRos(this Vector3 v) => new geometry_msgs.msg.Vector3 { X = v.z, Y = -v.x, Z = v.y };
    public static geometry_msgs.msg.Quaternion ToRos(this Quaternion q) => new geometry_msgs.msg.Quaternion { X = -q.z, Y = q.x, Z = -q.y, W = q.w };
    public static geometry_msgs.msg.Transform ToTransform(this geometry_msgs.msg.Vector3 translation, geometry_msgs.msg.Quaternion rotation) => new geometry_msgs.msg.Transform { Translation = translation, Rotation = rotation };
}

public class TransformTreeNode
{
    public Transform Transform { get; private set; }
    public string name => Transform.name;
    public TransformTreeNode Parent { get; private set; }
    public List<TransformTreeNode> Children { get; private set; }
    public bool IsALeafNode => Children.Count == 0;

    public TransformTreeNode(GameObject go, TransformTreeNode parent = null)
    {
        Transform = go.transform;
        Parent = parent;
        Children = new List<TransformTreeNode>();
        foreach (Transform child in go.transform)
        {
            if (child.GetComponent<ArticulationBody>() != null)
            {
                Children.Add(new TransformTreeNode(child.gameObject, this));
            }
        }
    }
}

public class Ros2TransformTreePublisher : MonoBehaviour
{
    [Header("Robot Namespace")]
    [Tooltip("robot1 / robot2")]
    [SerializeField] string robotNamespace = "robot1";

    [SerializeField] double m_PublishRateHz = 20f;
    [SerializeField] List<string> m_GlobalFrameIds = new List<string> { "map", "odom" };
    [SerializeField] GameObject m_RootGameObject;

    double m_LastPublishTimeSeconds;
    TransformTreeNode m_TransformRoot;

    ROS2Node node;
    IPublisher<tf2_msgs.msg.TFMessage> publisher;

    string NsFrame(string baseFrame) => string.IsNullOrEmpty(robotNamespace) ? baseFrame : $"{robotNamespace}/{baseFrame}";
    string NodeName(string baseName)   => string.IsNullOrEmpty(robotNamespace) ? baseName : $"{robotNamespace}_{baseName}";

    double PublishPeriodSeconds => 1.0f / m_PublishRateHz;
    bool ShouldPublishMessage => Unity.Robotics.Core.Clock.Now > m_LastPublishTimeSeconds + PublishPeriodSeconds;

    void Start()
    {
        if (m_RootGameObject == null)
        {
            Debug.LogError("Root Game Object is not set!", this);
            return;
        }

        var ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        node = ros2UnityComponent.CreateNode(NodeName("transform_tree_publisher_ros2"));
        publisher = node.CreatePublisher<tf2_msgs.msg.TFMessage>("tf");

        m_TransformRoot = new TransformTreeNode(m_RootGameObject);
        m_LastPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    void Update()
    {
        if (m_RootGameObject != null && ShouldPublishMessage)
            PublishMessage();
    }

    void PublishMessage()
    {
        var tfMessageList = new List<geometry_msgs.msg.TransformStamped>();
        var timestamp = new Unity.Robotics.Core.TimeStamp(Unity.Robotics.Core.Clock.Now);

        // root ←→ 最後のグローバルフレーム（通常は odom）: ns を付与
        if (m_GlobalFrameIds.Count > 0)
        {
            var tfRootToGlobal = new geometry_msgs.msg.TransformStamped
            {
                Header = new std_msgs.msg.Header
                {
                    Frame_id = NsFrame(m_GlobalFrameIds.Last()),
                    Stamp = new builtin_interfaces.msg.Time { Sec = timestamp.Seconds, Nanosec = timestamp.NanoSeconds }
                },
                Child_frame_id = NsFrame(m_TransformRoot.name),
                Transform = m_RootGameObject.transform.ToRosTransform()
            };
            tfMessageList.Add(tfRootToGlobal);
        }

        // map→odom のようなグローバル間: ns を付与
        for (var i = 1; i < m_GlobalFrameIds.Count; ++i)
        {
            var tfGlobalToGlobal = new geometry_msgs.msg.TransformStamped
            {
                Header = new std_msgs.msg.Header
                {
                    Frame_id = NsFrame(m_GlobalFrameIds[i - 1]),
                    Stamp = new builtin_interfaces.msg.Time { Sec = timestamp.Seconds, Nanosec = timestamp.NanoSeconds }
                },
                Child_frame_id = NsFrame(m_GlobalFrameIds[i]),
                Transform = new geometry_msgs.msg.Transform() { Rotation = new geometry_msgs.msg.Quaternion { W = 1.0f } }
            };
            tfMessageList.Add(tfGlobalToGlobal);
        }

        // ボディ以下の各リンク: ns を付与
        PopulateTFList(tfMessageList, m_TransformRoot, NsFrame);

        var tfMessage = new tf2_msgs.msg.TFMessage { Transforms = tfMessageList.ToArray() };
        publisher.Publish(tfMessage);
        m_LastPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    static void PopulateTFList(List<geometry_msgs.msg.TransformStamped> tfList, TransformTreeNode tfNode, System.Func<string,string> nsFrame)
    {
        foreach (var childTf in tfNode.Children)
        {
            var timestamp = new Unity.Robotics.Core.TimeStamp(Unity.Robotics.Core.Clock.Now);
            tfList.Add(new geometry_msgs.msg.TransformStamped
            {
                Header = new std_msgs.msg.Header
                {
                    Frame_id = nsFrame(tfNode.name),
                    Stamp = new builtin_interfaces.msg.Time { Sec = timestamp.Seconds, Nanosec = timestamp.NanoSeconds }
                },
                Child_frame_id = nsFrame(childTf.name),
                Transform = childTf.Transform.localPosition.ToRos().ToTransform(childTf.Transform.localRotation.ToRos())
            });

            if (!childTf.IsALeafNode)
                PopulateTFList(tfList, childTf, nsFrame);
        }
    }
}
