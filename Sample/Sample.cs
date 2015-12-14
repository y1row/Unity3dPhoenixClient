using UnityEngine;
using PhoenixChannels;

public class Sample : MonoBehaviour
{

    public Socket socket;


    void Start()
    {
        Payload payload = new Payload(string.Empty);
        payload.name = "name";

        socket.AddChannel("rooms:lobby", payload);
    }

}
