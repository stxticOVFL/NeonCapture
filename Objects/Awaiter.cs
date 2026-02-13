using System.Net.WebSockets;
using System.Text;
using MelonLoader.TinyJSON;
using UnityEngine;
using static NeonCapture.Objects.Handler;

namespace NeonCapture.Objects
{
    internal class Awaiter : MonoBehaviour
    {
        internal static readonly List<Awaiter> instances = [];
        internal static Awaiter i; // current instance

        internal string hostname;

        ClientWebSocket ws = null;
        readonly CancellationTokenSource canceller = new();

        readonly SemaphoreSlim sendSemaphore = new(1, 1);
        readonly Queue<OBSPacket> sendQueue = [];

        async Task Connect(Uri uri)
        {
            while (!enabled)
            {
                try
                {
                    ws = new ClientWebSocket();
                    await ws.ConnectAsync(uri, CancellationToken.None);
                    //StreamView.Log.Msg($"Connected to OBS!");
                    enabled = true;
                }
                catch
                {
                    //StreamView.Log.Error($"Error connecting to OBS:");
                    //StreamView.Log.Error(e);
                    await Task.Delay(1000);
                }
            }
        }

        async void Start()
        {
            instances.Add(this);
            enabled = false;
            if (!Uri.TryCreate(hostname, UriKind.Absolute, out var uri))
            {
                StatusText.i.SetStatus($"Malformed URL! Check your port!", StatusText.errorColor);
                return;
            }

            await Connect(uri);
            i = this;

            var sender = Task.Run(Sender);

            ArraySegment<byte> buffer = new(new byte[8192]);
            using var ms = new MemoryStream(8192);
            using var reader = new StreamReader(ms, Encoding.UTF8);

            while (ws.State == WebSocketState.Open && !canceller.IsCancellationRequested)
            {
                ms.SetLength(0);

                WebSocketReceiveResult result = null;

                do
                {
                    try
                    {
                        result = await ws.ReceiveAsync(buffer, canceller.Token);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    catch
                    {
                        break;
                    }
                }
                while (!result.EndOfMessage && !canceller.IsCancellationRequested);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var close = (OBSInfo.CloseCode)result.CloseStatus.Value;

                    if (close == OBSInfo.CloseCode.NormalClosure || close == OBSInfo.CloseCode.GoingAway)
                        StatusText.i.SetStatus($"Disconnected to OBS.", StatusText.warnColor);
                    else
                        StatusText.i.SetStatus($"OBS closed connection with code {close}", StatusText.errorColor);
                    break;
                }


                if (canceller.IsCancellationRequested || ws.State != WebSocketState.Open)
                    break;

                reader.BaseStream.Position = 0;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = reader.ReadToEnd();
                    try
                    {
                        OBSPacket packet = new(message);
                        NeonCapture.Log.DebugMsg($"RECEIVED {packet.opcode} {JSON.Dump(packet.data, EncodeOptions.NoTypeHints)}");
                        Handler.i.ParseResponse(packet);
                    }
                    catch (Exception e)
                    {
                        NeonCapture.Log.Error($"Error parsing packet:");
                        NeonCapture.Log.Error(e);
                        NeonCapture.Log.Warning($"Recieved:\n{message}");
                    }
                }
            }

            NeonCapture.Log.Warning($"Closing... {ws.State}");
            await sender;
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);

            Handler.i.OnDestroy();
            Destroy(this);
        }

        void OnDestroy() => instances.Remove(this);


        async Task Sender()
        {
            while (ws.State == WebSocketState.Open && !canceller.IsCancellationRequested)
            {
                OBSPacket packet = null;
                await sendSemaphore.WaitAsync();
                while (sendQueue.Count > 0)
                {
                    packet = sendQueue.Dequeue();
                    if (packet != null)
                    {
                        var j = JSON.Dump(packet, EncodeOptions.NoTypeHints);
                        NeonCapture.Log.DebugMsg($"SENDING {packet.opcode} {JSON.Dump(packet.data, EncodeOptions.NoTypeHints)}");
                        var encoded = Encoding.UTF8.GetBytes(j);
                        var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length);

                        try
                        {
                            await ws.SendAsync(buffer, WebSocketMessageType.Text, true, canceller.Token);
                        }
                        catch
                        {
                            NeonCapture.Log.DebugMsg($"FAILED TO SEND");
                        }
                    }
                }
                sendSemaphore.Release();
            }

            NeonCapture.Log.DebugMsg($"Sender task died");
        }

        internal static void Send(OBSPacket packet)
        {
            if (!i)
                return;
            i.sendSemaphore.Wait();
            i.sendQueue.Enqueue(packet);
            i.sendSemaphore.Release();
        }

        internal void Cancel()
        {
            if (!enabled)
                Destroy(this);
            NeonCapture.Log.DebugMsg($"Cancel called");
            if (!canceller.IsCancellationRequested)
                canceller.Cancel();
        }
    }
}
