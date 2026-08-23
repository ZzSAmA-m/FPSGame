using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using Mono.Cecil.Cil;
using System;

public class UDPListener : MonoBehaviour
{
    Dictionary<uint,GameObject> syncObjs;
    IPEndPoint endPoint;
    UdpClient udpClient;
    public void StartListener(Dictionary<uint,GameObject> syncObjs)
    {
        try
        {
            this.syncObjs=syncObjs;
            udpClient=new(0);
            Debug.Log("Listener Started...");
            StartCoroutine(ListenLoop());
        }catch (Exception )
        {
            
        }
        
    }
    IEnumerator ListenLoop()
    {
        while (true)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] receiveBytes = udpClient.Receive(ref remoteEndPoint);
            string receivedData = Encoding.UTF8.GetString(receiveBytes);
            PlayerPos pp=Serializer.StringToPosition(receivedData);
            syncObjs[pp.id].transform.position=pp.pos;
            Debug.Log("Received: " + receivedData);
            yield return null;
        }
        
    }
}
