using UnityEngine;
using System;
using System.Collections.Generic;
using System.Timers;

namespace PhoenixChannels
{

    public class Channel
    {
        public enum CHANNEL_STATE
        {
            closed,
            errored,
            joined,
            joining
        }

        public enum CHANNEL_EVENT
        {
            close,
            error,
            join,
            reply,
            leave
        }

        CHANNEL_STATE _state;
        IDictionary<string, List<Action<Payload, string>>> _bindings;
        bool _alreadyJoinedOnce;
        Push _joinPush;
        IList<Push> _pushBuffer;
        Timer _rejoinTimer;

        public string Topic { get; set; }
        public Socket Socket { get; set; }

        public Channel(string topic, Payload params_, Socket socket)
        {
            _state = CHANNEL_STATE.closed;
            Topic = topic;

            Socket = socket;
            _bindings = new Dictionary<string, List<Action<Payload, string>>>();
            _alreadyJoinedOnce = false;

            _joinPush = new Push(this, GetEvent(CHANNEL_EVENT.join), params_);
            _pushBuffer = new List<Push>();

            _joinPush.Receive("ok", (x) =>
            {
                _state = CHANNEL_STATE.joined;
            });

            OnClose((o, reference) =>
            {
                _state = CHANNEL_STATE.closed;
                Socket.Remove(this);
            });

            OnError((reason, reference) => //reason is not used
            {
                _state = CHANNEL_STATE.errored;
                _rejoinTimer.Start();
            });

            On(GetEvent(CHANNEL_EVENT.reply), (payload, reference) =>
            {
                Trigger(ReplyEventName(reference), payload, reference);
            });

            _rejoinTimer = new Timer(Socket.ReconnectAfterMs);
            _rejoinTimer.AutoReset = false;
            _rejoinTimer.Elapsed += (o, e) => RejoinUntilConnected();
            //_rejoinTimer.Enabled = true;
        }

        private void RejoinUntilConnected()
        {
            if (_state != CHANNEL_STATE.errored) return;

            if (Socket.IsConnected())
            {
                Rejoin();
            }
            else
            {
                _rejoinTimer.Start();
            }
        }

        public Push Join()
        {
            if (_alreadyJoinedOnce)
            {
                Debug.LogError("tried to join mulitple times. 'join' can only be called a singe time per channel instance");
            }
            else
            {
                _alreadyJoinedOnce = true;
            }
            SendJoin();
            return _joinPush;
        }

        public void OnClose(Action<Payload, string> callback)
        {
            On(GetEvent(CHANNEL_EVENT.close), callback);
        }

        public void OnError(Action<Payload, string> callback)
        {
            On(GetEvent(CHANNEL_EVENT.error), callback);
        }

        public void On(string evt, Action<Payload, string> callback)
        {
            if (!_bindings.ContainsKey(evt))
            {
                _bindings[evt] = new List<Action<Payload, string>>();
            }
            _bindings[evt].Add(callback);
        }

        public void Off(string evt)
        {
            _bindings.Remove(evt);
        }

        private bool CanPush()
        {
            return Socket.IsConnected() && _state == CHANNEL_STATE.joined;
        }

        public Push Push(string event_, Payload payload = null)
        {
            if (!_alreadyJoinedOnce)
            {
                Debug.LogError(string.Format("tried to push {0} to {1} before joining. Use Channel.Join() before pushing events", event_, payload));
            }

            var pushEvent = new Push(this, event_, payload);

            if (CanPush())
            {
                pushEvent.Send();
            }
            else
            {
                _pushBuffer.Add(pushEvent);
            }

            return pushEvent;
        }

        public Push Leave()
        {
            return Push(GetEvent(CHANNEL_EVENT.leave)).Receive("ok", (x) =>
            {
                this.Trigger(GetEvent(CHANNEL_EVENT.close));
            });
        }

        public bool IsMember(string topic)
        {
            return Topic == topic;
        }

        private void SendJoin()
        {
            _state = CHANNEL_STATE.joining;
            _joinPush.Send();
        }

        private void Rejoin()
        {
            SendJoin();
            foreach (var p in _pushBuffer)
            {
                p.Send();
            }
            _pushBuffer.Clear();
        }

        internal void Trigger(string event_, Payload payload = null, string reference = null)
        {
            if (_bindings.ContainsKey(event_))
            {
                foreach (var callback in _bindings[event_])
                {
                    callback(payload, reference);
                }
            }
        }

        public string ReplyEventName(string ref_)
        {
            return string.Format("chan_reply_{0}", ref_);
        }

        public static string GetState(CHANNEL_STATE state)
        {
            switch (state)
            {
                case CHANNEL_STATE.closed:
                    return "closed";
                case CHANNEL_STATE.errored:
                    return "errored";
                case CHANNEL_STATE.joined:
                    return "joined";
                default: // joining
                    return "joining";
            }
        }

        public static CHANNEL_STATE GetState(string state)
        {
            switch (state)
            {
                case "closed":
                    return CHANNEL_STATE.closed;
                case "errored":
                    return CHANNEL_STATE.errored;
                case "joined":
                    return CHANNEL_STATE.joined;
                default: // joining
                    return CHANNEL_STATE.joining;
            }
        }

        public static string GetEvent(CHANNEL_EVENT channelEvent)
        {
            switch (channelEvent)
            {
                case CHANNEL_EVENT.close:
                    return "phx_close";
                case CHANNEL_EVENT.error:
                    return "phx_error";
                case CHANNEL_EVENT.join:
                    return "phx_join";
                case CHANNEL_EVENT.reply:
                    return "phx_reply";
                default: // leave
                    return "phx_leave";
            }
        }

        public static CHANNEL_EVENT GetEvent(string channelEvent)
        {
            switch (channelEvent)
            {
                case "phx_close":
                    return CHANNEL_EVENT.close;
                case "phx_error":
                    return CHANNEL_EVENT.error;
                case "phx_join":
                    return CHANNEL_EVENT.join;
                case "phx_reply":
                    return CHANNEL_EVENT.reply;
                default: // leave
                    return CHANNEL_EVENT.leave;
            }
        }
    }

}
