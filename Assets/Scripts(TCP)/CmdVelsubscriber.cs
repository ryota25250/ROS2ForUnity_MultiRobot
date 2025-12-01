using System;
using System.Collections.Generic;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
using RosMessageTypes.BuiltinInterfaces;
using Unity.Robotics.Core;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.Serialization;

public class CmdVelSubscriber : MonoBehaviour
{
    private ROSConnection ros;
    public string cmdVelTopic = "/cmd_vel";
    public Rigidbody robotRigidbody;

    // スケール調整用
    public float linearScale = 1.0f;
    public float angularScale = 1.0f;

    private Vector3 linearVelocity = Vector3.zero;
    private float angularVelocity = 0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TwistMsg>(cmdVelTopic, ReceiveCmdVel);
    }

    void ReceiveCmdVel(TwistMsg msg)
    {
        // ROS座標系(X前進, Z上) → Unity座標系(Z前進, Y上)に変換
        linearVelocity = new Vector3(
            0, 
            0,
            (float)msg.linear.x * linearScale
        );
        angularVelocity = (float)msg.angular.z * angularScale;
    }

    void FixedUpdate()
    {
        if (robotRigidbody != null)
        {
            // 移動
            robotRigidbody.velocity = transform.TransformDirection(linearVelocity);

            // 回転
            robotRigidbody.angularVelocity = new Vector3(0, angularVelocity, 0);
        }
    }
}

