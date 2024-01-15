using Bombd.Logging;
using Bombd.Types.ServerCommunication;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Directory = Bombd.Services.Directory;

namespace Bombd.Core
{
    public class ServerCommunication
    {
        private static ClientWebSocket Socket = new ClientWebSocket();

        public static List<Message> MessageBuffer = new List<Message>();

        public static async Task Initialize()
        {
            string url = BombdConfig.Instance.ApiURL.TrimEnd('/');

            if (url.StartsWith("http"))
                url = url.Replace("http", "ws");
            else
                url = url.Replace("https", "wss");

            Socket.Options.SetRequestHeader("server_id", BombdServer.Instance.ClusterUuid);

            try
            {
                await Socket.ConnectAsync(new Uri($"{url}/api/ServerCommunication"), CancellationToken.None);
            }
            catch (Exception)
            {
                Logger.LogError<ServerCommunication>("Failed to connect to server communications api!");
                return;
            }

            var serverInfo = new ServerInfo
            {
                Type = "DIRECTORY",
                Address = BombdConfig.Instance.ExternalIP,
                Port = BombdServer.Instance.GetService<Directory>().Port,
                ServerPrivateKey = "MIGrAgEAAiEAq0cOe8L1tOpnc7e+ouVD" //is this even used for bombd? 
            };
            var message = new Message
            {
                Type = "ServerInfo",
                From = BombdServer.Instance.ClusterUuid,
                To = "API",
                Content = JsonSerializer.Serialize(serverInfo)
            };
            await Send(JsonSerializer.Serialize(message));

            new Thread(() => Receive().Wait()).Start();
        }

        private static async Task Receive()
        {
            var receiveResult = new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);

            while (!receiveResult.CloseStatus.HasValue && Socket.State == WebSocketState.Open)
            {
                var buffer = new byte[4096];
                try
                {
                    receiveResult = await Socket.ReceiveAsync(buffer, CancellationToken.None);
                }
                catch (Exception e)
                {
                    Logger.LogError<ServerCommunication>($"There was an error receiving message: {e}");
                }

                string message = Encoding.UTF8.GetString(buffer).Trim('\0');

                if (!string.IsNullOrEmpty(BombdConfig.Instance.ServerCommunicationKey))
                    message = Decrypt(message);

                try
                {
                    var clientMessage = JsonSerializer.Deserialize<Message>(message);
                    if (clientMessage != null)
                        MessageBuffer.Add(clientMessage);
                }
                catch (Exception e)
                {
                    Logger.LogDebug<ServerCommunication>($"Failed to process message: {e}");
                }
            }

            try
            {
                await Socket.CloseAsync(receiveResult.CloseStatus != null ? receiveResult.CloseStatus.Value : WebSocketCloseStatus.NormalClosure,
                    receiveResult.CloseStatusDescription, CancellationToken.None);
            }
            catch (Exception e)
            {
                Logger.LogDebug<ServerCommunication>($"There was an error while closing connection: {e}");
            }
        }

        public static void UpdatePlayerCount(int playerCount)
        {
            var message = new Message
            {
                Type = "PlayerCountUpdate",
                From = BombdServer.Instance.ClusterUuid,
                To = "API",
                Content = playerCount.ToString()
            };
            Send(JsonSerializer.Serialize(message)).Wait();
        }

        public static async Task Send(string message)
        {
            byte[] bytes;

            if (!string.IsNullOrEmpty(BombdConfig.Instance.ServerCommunicationKey))
                bytes = Encoding.UTF8.GetBytes(Encrypt(message));
            else
                bytes = Encoding.UTF8.GetBytes(message);

            if (Socket.State == WebSocketState.Open)
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static string Encrypt(string message)
        {
            var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(BombdConfig.Instance.ServerCommunicationKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            var stream = new MemoryStream();
            var cryptoTransform = aes.CreateEncryptor(aes.Key, null);
            var cryptoStream = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Write);

            cryptoStream.Write(Encoding.UTF8.GetBytes(message));

            return Convert.ToBase64String(stream.ToArray());
        }

        private static string Decrypt(string message)
        {
            var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(BombdConfig.Instance.ServerCommunicationKey);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            var stream = new MemoryStream(Convert.FromBase64String(message));
            var cryptoTransform = aes.CreateDecryptor(aes.Key, null);
            var cryptoStream = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Read);
            var streamReader = new StreamReader(cryptoStream, Encoding.UTF8);

            return streamReader.ReadToEnd();
        }
    }
}
