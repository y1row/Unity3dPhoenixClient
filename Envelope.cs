namespace PhoenixChannels
{
    public class Envelope
    {
        public string Topic { get; set; }
        public string Event { get; set; }
        public Payload Payload { get; set; }
        public string Ref { get; set; }
    }

}
