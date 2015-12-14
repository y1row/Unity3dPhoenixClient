using System;
using System.Collections.Generic;
using System.Timers;
using UnityEngine;

namespace PhoenixChannels
{
    public class RecHook
    {
        public string Status { get; set; }
        public Action<Payload> Callback { get; set; }
    }

    public class AfterHook
    {
        public int Ms { get; set; }
        public Action Callback { get; set; }
        public Timer Timer { get; set; }
    }

    public class Push
    {
        private Channel _channel;
        private string _event;
        private Payload _payload;

        private Payload _receivedResp;
        private AfterHook _afterHook;
        private List<RecHook> _recHooks;
        private bool _sent;

        private string _ref;
        private string _refEvent;

        public Push(Channel channel, string event_, Payload payload)
        {
            _channel = channel;
            _event = event_;
            _payload = payload;

            _receivedResp = null;
            _afterHook = null;
            _recHooks = new List<RecHook>();
            _sent = false;
        }

        public void Send()
        {
            _ref = _channel.Socket.MakeRef();
            _refEvent = _channel.ReplyEventName(_ref);
            _receivedResp = null;
            _sent = false;

            _channel.On(_refEvent, (payload, reference) =>
            {
                _receivedResp = payload;
                MatchReceive(payload);
                CancelRefEvent();
                CancelAfter();
            });

            StartAfter();
            _sent = true;

            var env = new Envelope()
            {
                Topic = _channel.Topic,
                Event = _event,
                Payload = _payload,
                Ref = _ref,
            };

            _channel.Socket.Push(env);
        }

        public Push Receive(string status, Action<Payload> callback)
        {
            if (_receivedResp != null && _receivedResp.status == status)
            {
                callback(new Payload(_receivedResp.response));
            }

            _recHooks.Add(new RecHook() { Status = status, Callback = callback });

            return this;
        }

        public Push After(int timeoutMs, Action callback)
        {
            if (_afterHook != null)
            {
                //throw
                throw new Exception("only a single after hook can be applied to a push");
            }

            var timer = new Timer(timeoutMs);
            if (_sent)
            {
                timer.Elapsed += (o, e) => callback();
                timer.AutoReset = false;
                timer.Start();//.Enabled = true;
            }

            _afterHook = new AfterHook { Ms = timeoutMs, Callback = callback, Timer = timer };

            return this;
        }

        //// private
        private void MatchReceive(Payload payload) //string status, object response, int reference)
        {
            foreach (var rh in _recHooks)
            {
                if (rh.Status == payload.status)
                {
                    rh.Callback(new Payload(payload.response));
                }
            }
        }

        private void CancelRefEvent()
        {
            _channel.Off(_refEvent);
        }

        private void CancelAfter()
        {
            if (_afterHook == null) return;
            //reset timer
            _afterHook.Timer.Stop();
            //_afterHook.Timer.Enabled = false;
            _afterHook.Timer = null;
        }

        private void StartAfter()
        {
            if (_afterHook == null) return;

            Action callback = () =>
                {
                    CancelRefEvent();
                    _afterHook.Callback();
                };
            _afterHook.Timer = new Timer(_afterHook.Ms);
            _afterHook.Timer.Elapsed += (o, e) => callback();
        }
    }
}
