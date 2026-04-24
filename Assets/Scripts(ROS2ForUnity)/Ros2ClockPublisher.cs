using UnityEngine;
using ROS2;
using Unity.Robotics.Core;

public class Ros2ClockPublisher : MonoBehaviour
{
    public float publishRateHz = 100f;

    private ROS2Node node;
    private IPublisher<rosgraph_msgs.msg.Clock> publisher;
    private rosgraph_msgs.msg.Clock clockMessage;
    private double lastPublishTimeSeconds;

    private double PublishPeriodSeconds => 1.0 / publishRateHz;

    void Start()
    {
        var ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        if (ros2UnityComponent == null)
        {
            Debug.LogError("ROS2UnityComponent not found.", this);
            enabled = false;
            return;
        }

        node = ros2UnityComponent.CreateNode("unity_clock_publisher_ros2");
        publisher = node.CreatePublisher<rosgraph_msgs.msg.Clock>("/clock");

        clockMessage = new rosgraph_msgs.msg.Clock
        {
            Clock_ = new builtin_interfaces.msg.Time()
        };

        lastPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    void FixedUpdate()
    {
        if (Unity.Robotics.Core.Clock.Now < lastPublishTimeSeconds + PublishPeriodSeconds)
            return;

        var publishTime = Unity.Robotics.Core.Clock.Now;
        var timestamp = new TimeStamp(publishTime);

        clockMessage.Clock_.Sec = timestamp.Seconds;
        clockMessage.Clock_.Nanosec = timestamp.NanoSeconds;

        publisher.Publish(clockMessage);
        lastPublishTimeSeconds = publishTime;
    }
}