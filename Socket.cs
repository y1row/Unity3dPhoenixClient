using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using UnityEngine;
using WebSocketSharp;

namespace PhoenixChannels
{
    public class Socket : MonoBehaviour
    {
        IList<Action> _openCallbacks;
        IList<Action<CloseEventArgs>> _closeCallbacks;
        IList<Action<ErrorEventArgs>> _errorCallbacks;
        IList<Action<string, string, Payload>> _messageCallbacks;
        IList<Channel> _channels;
        IList<Action> _sendBuffer;
        int _ref = 0;
        WebSocket _conn;

        public string EndPoint = "ws://localhost:4000/socket/websocket";
        public int ReconnectAfterMs = 5000;
        public int HeartbeatIntervalMs = 30000;

        public void Awake()
        {
            _openCallbacks = new List<Action>();
            _closeCallbacks = new List<Action<CloseEventArgs>>();
            _errorCallbacks = new List<Action<ErrorEventArgs>>();
            _messageCallbacks = new List<Action<string, string, Payload>>();

            _channels = new List<Channel>();
            _sendBuffer = new List<Action>();
            _ref = 0;
        }

        public void Start()
        {
            Connect();
            StartCoroutine(Reconnect());
        }

        public void Connect()
        {
            Disconnect(() =>
            {
                _conn = new WebSocket(EndPoint);
                _conn.OnOpen += OnConnOpen;
                _conn.OnError += OnConnError;
                _conn.OnMessage += OnConnMessage;
                _conn.OnClose += OnConnClose;
                _conn.Connect();
            });
        }

        IEnumerator Reconnect()
        {
            yield return new WaitForSeconds(ReconnectAfterMs / 1000);
            //Connect();
        }

        public void Disconnect(Action callback, CloseStatusCode code = CloseStatusCode.NoStatus, string reason = null)
        {
            if (_conn != null)
            {
                if (code != CloseStatusCode.NoStatus)
                {
                    _conn.Close(code, reason);
                }
                _conn = null;
            }
            if (callback != null) callback();
        }

        public Socket OnOpen(Action callback)
        {
            _openCallbacks.Add(callback);
            return this;
        }

        public Socket OnClose(Action<CloseEventArgs> callback)
        {
            _closeCallbacks.Add(callback);
            return this;
        }

        public Socket OnError(Action<ErrorEventArgs> callback)
        {
            _errorCallbacks.Add(callback);
            return this;
        }

        public Socket OnMessage(Action<string, string, Payload> callback)
        {
            _messageCallbacks.Add(callback);
            return this;
        }

        private void OnConnOpen(object sender, EventArgs e)
        {
            FlushSendBuffer();

            foreach (var callback in _openCallbacks)
            {
                callback();
            }
        }

        private void OnConnClose(object sender, CloseEventArgs e)
        {
            TriggerChanError();

            foreach (var callback in _closeCallbacks) callback(e);
        }

        private void OnConnError(object sender, ErrorEventArgs e)
        {
            TriggerChanError();
            foreach (var callback in _errorCallbacks) callback(e);
        }

        private void TriggerChanError()
        {
            foreach (var c in _channels)
            {
                c.Trigger(Channel.GetEvent(Channel.CHANNEL_EVENT.error));
            }
        }

        private WebSocketState ConnectionState()
        {
            return _conn.ReadyState;
        }

        public bool IsConnected()
        {
            return ConnectionState() == WebSocketState.Open;
        }

        public void Remove(Channel chan)
        {
            _channels = System.Linq.Enumerable.ToList(_channels.Where(c => !c.IsMember(chan.Topic)));
        }

        public Channel AddChannel(string topic, Payload payload)
        {
            var chan = gameObject.AddComponent<Channel>();
            chan.Topic = topic;
            chan.Payload = payload;
            chan.Socket = this;
            _channels.Add(chan);

            return chan;
        }

        public void Push(Envelope envelope)
        {
            Action callback = () => _conn.Send(JsonUtility.ToJson(envelope.Payload));

            if (IsConnected())
            {
                callback();
            }
            else
            {
                _sendBuffer.Add(callback);
            }
        }

        public string MakeRef()
        {
            var newRef = _ref + 1;
            _ref = (newRef < Int32.MaxValue) ? _ref = newRef : _ref = 0;

            return _ref.ToString();
        }

        private void SendHeartbeat()
        {
            var env = new Envelope()
            {
                Topic = "phoenix",
                Event = "heartbeat",
                Payload = new Payload(String.Empty),
                Ref = MakeRef(),
            };
            Push(env);
        }

        private void FlushSendBuffer()
        {
            if (this.IsConnected() && _sendBuffer.Count > 0)
            {
                foreach (var c in _sendBuffer)
                {
                    c();
                }
                _sendBuffer.Clear();
            }
        }

        private void OnConnMessage(object sender, MessageEventArgs e)
        {
            var env = JsonUtility.FromJson<Envelope>(e.Data);

            foreach (var chan in System.Linq.Enumerable.ToList(_channels.Where((c) => c.IsMember(env.Topic))))
            {
                chan.Trigger(env.Event, env.Payload, env.Ref);
            }

            foreach (var callback in _messageCallbacks) callback(env.Topic, env.Event, env.Payload);
        }

    }
}
