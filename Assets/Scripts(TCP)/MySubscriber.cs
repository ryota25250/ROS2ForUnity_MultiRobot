using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class MySubscriber : MonoBehaviour{
ROSConnection ros;

void Start(){
    // ROSコネクションへのサブスクライバーの登録
    ros = ROSConnection.GetOrCreateInstance();
    ros.Subscribe<StringMsg>("my_topic", OnSubscribe);
}

void OnSubscribe(StringMsg msg){
    Debug.Log("Subscribe : "+ msg.data);
}
}
