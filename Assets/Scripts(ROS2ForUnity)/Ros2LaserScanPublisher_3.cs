using System.Collections.Generic;
using UnityEngine;
using ROS2;
using Unity.Robotics.Core;

public class Ros2LaserScanPublisher_3 : MonoBehaviour
{
    [Header("Robot Namespace")]
    public string robotNamespace = "robot1";

    [Header("ROS2 Settings")]
    public string topicBase = "scan";
    public string frameIdBase = "base_scan";

    [Header("Scan Parameters")]
    public double publishPeriodSeconds = 0.1;
    public float rangeMetersMin = 0.1f;
    public float rangeMetersMax = 10f;
    public float scanAngleStartDegrees = 135f;
    public float scanAngleEndDegrees = -135f;
    public int numMeasurementsPerScan = 360;

    private ROS2Node node;
    private IPublisher<sensor_msgs.msg.LaserScan> publisher;
    private List<float> ranges;
    private double nextPublishTimeSeconds;

    string NsTopic(string baseName) =>
        string.IsNullOrEmpty(robotNamespace) ? $"/{baseName}" : $"/{robotNamespace}/{baseName}";

    string NsFrame(string baseFrame) =>
        string.IsNullOrEmpty(robotNamespace) ? baseFrame : $"{robotNamespace}/{baseFrame}";

    string NodeName(string baseName) =>
        string.IsNullOrEmpty(robotNamespace) ? baseName : $"{robotNamespace}_{baseName}";

    void Start()
    {
        var ros2UnityComponent = FindObjectOfType<ROS2UnityComponent>();
        if (ros2UnityComponent == null)
        {
            Debug.LogError("ROS2UnityComponent not found.", this);
            enabled = false;
            return;
        }

        node = ros2UnityComponent.CreateNode(NodeName("laser_scan_publisher"));
        publisher = node.CreatePublisher<sensor_msgs.msg.LaserScan>(NsTopic(topicBase));
        ranges = new List<float>(numMeasurementsPerScan);
        nextPublishTimeSeconds = Unity.Robotics.Core.Clock.Now;
    }

    void FixedUpdate()
    {
        if (Unity.Robotics.Core.Clock.Now < nextPublishTimeSeconds)
            return;

        PerformScan();
        PublishScan();
        nextPublishTimeSeconds = Unity.Robotics.Core.Clock.Now + publishPeriodSeconds;
    }

    private void PerformScan()
    {
        ranges.Clear();

        var yawBaseDegrees = transform.rotation.eulerAngles.y;
        for (int i = 0; i < numMeasurementsPerScan; i++)
        {
            float t = (numMeasurementsPerScan > 1) ? (float)i / (numMeasurementsPerScan - 1) : 0f;
            float yawSensorDegrees = Mathf.Lerp(scanAngleStartDegrees, scanAngleEndDegrees, t);
            float yawDegrees = yawBaseDegrees - yawSensorDegrees;

            var directionVector = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
            var measurementStart = transform.position + directionVector * rangeMetersMin;
            var measurementRay = new Ray(measurementStart, directionVector);

            if (Physics.Raycast(measurementRay, out var hit, rangeMetersMax))
                ranges.Add(hit.distance);
            else
                ranges.Add(float.PositiveInfinity);
        }
    }

    private void PublishScan()
    {
        var timestamp = new TimeStamp(Unity.Robotics.Core.Clock.Now);

        float angleStartRos = -scanAngleEndDegrees * Mathf.Deg2Rad;
        float angleEndRos = -scanAngleStartDegrees * Mathf.Deg2Rad;

        if (angleStartRos > angleEndRos)
        {
            float temp = angleEndRos;
            angleEndRos = angleStartRos;
            angleStartRos = temp;
            ranges.Reverse();
        }

        var msg = new sensor_msgs.msg.LaserScan
        {
            Header = new std_msgs.msg.Header
            {
                Frame_id = NsFrame(frameIdBase),
                Stamp = new builtin_interfaces.msg.Time
                {
                    Sec = timestamp.Seconds,
                    Nanosec = timestamp.NanoSeconds
                }
            },
            Angle_min = angleStartRos,
            Angle_max = angleEndRos,
            Angle_increment = (numMeasurementsPerScan > 1)
                ? (angleEndRos - angleStartRos) / (numMeasurementsPerScan - 1)
                : 0f,
            Time_increment = 0f,
            Scan_time = (float)publishPeriodSeconds,
            Range_min = rangeMetersMin,
            Range_max = rangeMetersMax,
            Ranges = ranges.ToArray(),
            Intensities = new float[ranges.Count]
        };

        publisher.Publish(msg);
    }
}