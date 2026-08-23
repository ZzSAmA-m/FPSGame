using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Text;
using System.Net.Sockets;

public class UDPSender : MonoBehaviour
{
    Dictionary<uint,GameObject> syncObjs;
    IPEndPoint endPoint;
    UdpClient udpClient;
    uint syncTime=0;
    bool isSending=false;
    public void StartSender(Dictionary<uint,GameObject> syncObjs,IPEndPoint endPoint,UdpClient udpClient)
    {
        this.syncObjs=syncObjs;
        this.endPoint=endPoint;
        this.udpClient=udpClient;
        isSending=true;
        Debug.Log("Sender Started...");
    }
    void Update()
    {
        if(!isSending) return;
        Debug.Log($"sync time: {++syncTime}");
        foreach(var i in syncObjs)
        {
            var data=Serializer.PositionToString(i.Key,i.Value.transform);
            ClientSendMessage(data);
            Debug.Log($"sync data: {data}");
        }
    }

    void ClientSendMessage(string message)
    {
        byte[] sendBytes = Encoding.UTF8.GetBytes(message);
        udpClient.Send(sendBytes, sendBytes.Length, endPoint);
    }
}
