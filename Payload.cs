using System;
using UnityEngine;

[Serializable]
public class Payload
{

    public string json { get; private set; }
    public string name;
    public string status;
    public string response;

    public Payload(string _json)
    {
        json = _json;

        var parsed = JsonUtility.FromJson<Payload>(json);
        if (parsed != null)
        {
            status = parsed.status;
            response = parsed.response;
        }
    }

    public override string ToString()
    {
        return "json : " + json + ", status : " + status + ", response : " + response;
    }

}
