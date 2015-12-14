using UnityEngine;

public class Payload
{

    public string json { get; private set; }
    public string status { get; set; }
    public string response { get; set; }

    public Payload(string _json)
    {
        json = _json;

        var parsed = JsonUtility.FromJson<Payload>(json);
        status = parsed.status;
        response = parsed.response;
    }

}
