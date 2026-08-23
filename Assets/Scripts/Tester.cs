using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;


public class Tester : MonoBehaviour
{
    [SerializeField]private GameObject go;
    UDPClient client;
    void Start()
    {
        client=GetComponent<UDPClient>();
        client.AddSyncObj(go);
        client.StartClient();
    }
}