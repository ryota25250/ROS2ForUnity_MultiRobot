using UnityEngine;
using ROS2;
using Unity.Robotics.Core; // この行は残します

public class Ros2ClockPublisher : MonoBehaviour
{
    [Tooltip("時刻メッセージを配信する頻度 (Hz)")]
    public float publishRateHz = 100f;
    
    // ROS2関連
    private ROS2Node node;
    private IPublisher<rosgraph_msgs.msg.Clock> publisher;
    
    // 内部で使用する変数
    private rosgraph_msgs.msg.Clock clockMessage;
    private double lastPublishTimeSeconds;
    private double PublishPeriodSeconds => 1.0 / publishRateHz;

    // ★ 修正点: Clock.Now を Unity.Robotics.Core.Clock.Now に変更
    private bool ShouldPublishMessage => Unity.Robotics.Core.Clock.Now > lastPublishTimeSeconds + PublishPeriodSeconds;

    void Start()
    {
        // ROS2の初期化
        ROS2UnityComponent ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        node = ros2UnityComponent.CreateNode("unity_clock_publisher_ros2");
        publisher = node.CreatePublisher<rosgraph_msgs.msg.Clock>("/clock");
        
        // メッセージのテンプレートを作成
        clockMessage = new rosgraph_msgs.msg.Clock();
        clockMessage.Clock_ = new builtin_interfaces.msg.Time();
        
        // ★ 修正点: Clock.Now を Unity.Robotics.Core.Clock.Now に変更
        lastPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    void Update()
    {
        if (ShouldPublishMessage)
        {
            PublishMessage();
        }
    }

    private void PublishMessage()
    {
        // ★ 修正点: Clock.Now を Unity.Robotics.Core.Clock.Now に変更
        var publishTime = Unity.Robotics.Core.Clock.Now;
        var timestamp = new TimeStamp(publishTime);

        // メッセージのタイムスタンプを更新
        clockMessage.Clock_.Sec = timestamp.Seconds;
        clockMessage.Clock_.Nanosec = timestamp.NanoSeconds;
        
        // /clockトピックにメッセージを配信
        publisher.Publish(clockMessage);
        
        lastPublishTimeSeconds = publishTime;
    }
}