using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
public class UDPClient:MonoBehaviour
{

    [SerializeField]private string serverIp="127.0.0.1";
    [SerializeField]private int port=7777;
    

    private UDPListener listener;
    private UDPSender sender;

    private UdpClient udpClient;
    private IPEndPoint endPoint;

    Dictionary<uint,GameObject> syncObjs=new();
    uint idNamer=0;

    

    public void StartClient()
    {
        endPoint = new IPEndPoint(IPAddress.Parse(serverIp), port);
        udpClient = new UdpClient();

        sender=GetComponent<UDPSender>();
        listener=GetComponent<UDPListener>();

        sender.StartSender(syncObjs,endPoint,udpClient);
        listener.StartListener(syncObjs);
        Debug.Log("Client started...");
        //Task.Run(DataReceiver);
    }
 
    
    async Task DataReceiver()
    {
        Debug.Log("Receiver Started...");
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] receiveBytes = udpClient.Receive(ref remoteEndPoint);
            string receivedData = Encoding.UTF8.GetString(receiveBytes);
            PlayerPos pp=Serializer.StringToPosition(receivedData);
            syncObjs[pp.id].transform.position=pp.pos;
            Console.WriteLine("Received: " + receivedData);
        }
    } 

    public void AddSyncObj(GameObject obj)
    {
        syncObjs.Add(++idNamer,obj);
    }


}