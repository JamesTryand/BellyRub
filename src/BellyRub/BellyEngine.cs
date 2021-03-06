using System;
using System.IO;
using System.Dynamic;
using System.Reflection;
using System.Collections.Generic;

namespace BellyRub
{
	public class BellyEngine
    {
        public class Message
        {
            public string Subject { get; private set; }
            public string Token { get; private set; }
            public dynamic Body { get; private set; }

            public Message(string subject, string token, dynamic body) {
                Subject = subject;
                Token = token;
                Body = body;
            }
        }

        private Random _portChooser = new Random();
        private WebServer.Server _server;
        private Messaging.Channel _channel;
        private UI.Browser _client;
        private Dictionary<string, Action<dynamic>> _handlers = new Dictionary<string, Action<dynamic>>();
        private Dictionary<string, Action<dynamic, Action<object>>> _responders = new Dictionary<string, Action<dynamic, Action<object>>>();

        public bool HasConnectedClients { get { return _channel.HasConnectedClients; } }
        public string ServerUrl { get { return _server.Url + "/site/index.html?channel="+_channel.Url; } }
        public string ChannelUrl { get { return _channel.Url; } }

        public BellyEngine() {
            initialize(null);
        }

        public BellyEngine(string rootPath) {
            initialize(rootPath);
        }

        public BellyEngine On(string subject, Action<dynamic> action) {
            _handlers.Add(subject, action);
            return this;
        }

        public BellyEngine RespondTo(string subject, Action<dynamic, Action<object>> responder) {
            _responders.Add(subject, responder);
            return this;
        }

        public BellyEngine OnConnected(Action action) {
            _channel.OnConnected(action);
            return this;
        }

        public BellyEngine OnDisconnected(Action action) {
            _channel.OnDisconnected(action);
            return this;
        }

        public BellyEngine OnSendException(Action<Exception> action) {
            _channel.OnSendException(action);
            return this;
        } 

        public UI.Browser Start() {
            return Start(null, null);
        }

        public UI.Browser Start(int serverPort) {
            return Start(serverPort, null, null);
        }

        public UI.Browser Start(int serverPort, int channelPort) {
            return Start(serverPort, channelPort, null, null);
        }

        public UI.Browser Start(UI.Point position) {
            return Start(position, null);
        }

        public UI.Browser Start(int serverPort, UI.Point position) {
            return Start(serverPort, position, null);
        } 

        public UI.Browser Start(int serverPort, int channelPort, UI.Point position) {
            return Start(serverPort, channelPort, position, null);
        }

        public UI.Browser Start(UI.Point position, UI.Size size) {
            StartHeadless();
            return start(position, size);
        }

        public UI.Browser Start(int serverPort, UI.Point position, UI.Size size) {
            StartHeadless(serverPort);
            return start(position, size);
        }

        public UI.Browser Start(int serverPort, int channelPort, UI.Point position, UI.Size size) {
            StartHeadless(serverPort, channelPort);
            return start(position, size);
        }

        public UI.Browser start(UI.Point position, UI.Size size) {
            _client = new UI.Browser(() => {
                var body = Request("belly:get-window-title");
                if (body != null)
                    return body.title;
                return "";
            });
            var url = _server.Url; 
            var ws = _channel.Url;
            _client.Launch(url, ws, position, size);
            return _client;
        }

        public void StartHeadless() {
            var serverPort = _portChooser.Next(1025, 65535);
            var channelPort = _portChooser.Next(1025, 65535);
            StartHeadless(serverPort, channelPort);
        }

        public void StartHeadless(int serverPort) {
            var channelPort = _portChooser.Next(1025, 65535);
            StartHeadless(serverPort, channelPort);
        }

        public void StartHeadless(int serverPort, int channelPort) {
            _server = new WebServer.Server();
            _server.Start(serverPort);
            _channel.Start(channelPort);
        }

        public void WaitForFirstClientToConnect() {
            if (_channel == null)
                return;
            _channel.WaitForFirstClientToConnect();
        }

        public void WaitForFirstClientToConnect(int timeout) {
            if (_channel == null)
                return;
            _channel.WaitForFirstClientToConnect(timeout);
        }

        public void Stop() {
            _channel.Stop();
            if (_server != null)
                _server.Stop();
            if (_client != null)
                _client.Kill();
        }

        public void Send(string subject) {
            Send(subject, new object());
        }

        public void Send(string subject, object body) {
            if (_channel != null)
                _channel.Send(subject, body);
        }

        public dynamic Request(string subject) {
            return Request(subject, new object());
        }

        public dynamic Request(string subject, object body) {
            if (_channel != null)
                return _channel.Request(subject, body);
            return null;
        }

        private void initialize(string rootPath) {
            if (rootPath != null)
                WebServer.RESTBootstrapper.SetRootDir(rootPath);
            _channel = new Messaging.Channel();
            _channel.OnReceive((msg) =>  {
                if (_handlers.ContainsKey(msg.Subject)) {
                    _handlers[msg.Subject](msg.Body);
                } else if (_responders.ContainsKey(msg.Subject)) {
                    var token = msg.Token.ToString();
                    var body = (object)msg.Body;
                    _responders[msg.Subject](body, (o) => {
                        Send(token, o);
                    });
                }
            });
            RespondTo("belly:get-bellyrub-client-content", (body, respondWith) => {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "BellyRub.WebServer.site.bellyrub-client.js";
                using (Stream stream = assembly.GetManifestResourceStream(resourceName)) {
                    using (StreamReader reader = new StreamReader(stream)) {
                        dynamic o = new ExpandoObject();
                        o.content = reader.ReadToEnd();
                        respondWith(o);
                    }
                }
            });
        }
	}
}
