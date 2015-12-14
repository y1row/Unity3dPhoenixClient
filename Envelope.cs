using System;

namespace PhoenixChannels
{

    [Serializable]
    public class Envelope
    {
        public string topic;
        public string @event;
        public Payload payload;
        public string @ref;

        public override string ToString()
        {
            return "topic : " + topic + ", event : " + @event + ", Payload : " + payload.ToString() + ", Ref : " + @ref;
        }
    }

}
