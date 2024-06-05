using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MelonLoader.TinyJSON;
using I2.Loc;
using TMPro;
using Settings = NeonCapture.NeonCapture.Settings;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace NeonCapture.Objects
{
    public class CaptureManager : MonoBehaviour
    {
        public class OBSPacket(OBSInfo.Opcodes opc, ProxyObject data)
        {
            public OBSPacket() : this(0, null) { }

            [Exclude]
            public OBSInfo.Opcodes opcode = opc;

            [Include]
            public int op { get { return (int)opcode; } }
            [Exclude]
            public ProxyObject data = data;
            [Include]
            public ProxyObject d { get { return data; } }

            public override string ToString()
            {
                return $"{opcode}: {data.ToJSON()}";
            }
        }
        public class Transition(Func<float, float, float, float> ease, Action finish)
        {
            readonly Func<float, float, float, float> easeFunc = ease;
            public Action finishFunc = finish;

            public bool skip = false;
            public float start;
            public float goal;
            public float result;
            public bool running;
            public float speed = 4;
            public float time;


            public void Start(float? s, float g)
            {
                running = true;
                time = 0;
                start = s ?? result;
                result = start;
                goal = g;
            }

            public void Process()
            {
                if (time == 1f)
                {
                    time = 0;
                    result = goal;
                    finishFunc?.Invoke();
                    running = false;
                }
                if (!running)
                    return;
                time = Math.Min(1f, time + (Time.unscaledDeltaTime * speed));
                result = easeFunc(start, goal, time);
            }
        }

        readonly Transition _moveY = new(AxKEasing.EaseInOutCubic, null);
        readonly Transition _opacityT = new(AxKEasing.Linear, null);

        public ClientWebSocket webSocket = null;
        string basePath;
        public string queuedPath;
        public string queuedPrefix;
        public string usedBonus;
        public LevelData level; // just to make sure we have itLevelData level;
        public bool recording;
        string responseID;
        string potentialOut;

        bool waitForSave;
        bool readyToRecord;

        TextMeshProUGUI statusText;
        Color statusColor = Color.white;
        float statusTimer = -1;

        public bool ready = false;

        public float endingTimer = 0;

        bool recordRecieved;
        float recordTimer;

        public long time;

        static readonly Color errorColor = new Color32(209, 61, 62, 255);

        public void Awake()
        {
            NeonCapture.manager = this;
            _moveY.finishFunc = () => statusTimer = statusTimer == 0 ? -1 : 3;
            // create a canvas for the text to go into
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        public void Start()
        {
            statusText = new GameObject("Status", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            statusText.transform.parent = transform;
            (statusText.transform as RectTransform).pivot = new(0, 0);
            statusText.margin = new Vector4(100, 0, -2000, 0);
            statusText.fontSize = 24;
            statusText.font = Resources.Load<TMP_FontAsset>("fonts/source code pro/SourceCodePro-Black SDF");
            statusText.outlineWidth = 0.15f;
            statusText.alignment = TextAlignmentOptions.TopLeft;

            _moveY.result = 20;
            _opacityT.result = 0;
            Update();

            Connect();
        }

        public void Update()
        {
            float size = 1f / 1080 * Screen.height;
            statusText.transform.position = new(10f / 1920 * Screen.width, -_moveY.result / 1080 * Screen.height);
            statusText.transform.localScale = new(size, size, 1);
            statusText.faceColor = Color.black;
            statusText.outlineColor = statusColor;
            statusText.alpha = _opacityT.result;
            _moveY.Process();
            _opacityT.Process();
            if (!_moveY.running && statusTimer > 0)
            {
                statusTimer -= Time.unscaledDeltaTime;
                if (statusTimer <= 0)
                {
                    statusTimer = 0;
                    _moveY.Start(10, 20);
                    _opacityT.Start(1, 0);
                }
            }
            if (endingTimer > 0)
            {
                endingTimer -= Time.unscaledDeltaTime;
                if (endingTimer <= 0)
                    SaveVideo();
                else if (endingTimer <= 3 && Settings.AutoAlert.Value)
                    SetStatus($"Auto-save in {(int)endingTimer + 1}...");
            }
            if (recordRecieved)
            {
                recordTimer -= Time.unscaledDeltaTime;
                if (recordTimer <= 0)
                {
                    recordTimer = .05f;
                    SendStartRecord();
                }
            }
        }

        bool closing = false;

        public void OnDestroy()
        {
            if (ready && recording)
            {
                OBSPacket packet = PrepareRequest("StopRecord");
                packet.data["requestId"] = new ProxyString("discard");
                waitForSave = true;
                closing = true;
                Debug.Log("Trying to send closing packets..");
                Send(packet); // hope this sent 
                int tries = 20;
                while (File.Exists(potentialOut) && --tries != 0)
                {
                    try
                    {
                        File.Delete(potentialOut);
                    }
                    catch
                    {
                        Thread.Sleep(50);
                    }
                }
            }
        }

        OBSPacket PrepareRequest(string type)
        {
            OBSPacket ret = new(OBSInfo.Opcodes.Request, []);
            ret.d["requestType"] = new ProxyString(type);
            ret.d["requestId"] = new ProxyString(DateTime.Now.ToString("yyyyMMddHHmmssfffffff"));
            ret.d["requestData"] = new ProxyObject();
            return ret;
        }

        public void StartRecording()
        {
            if (recording || !Settings.EnableRecord.Value)
                return;

            if (queuedPath != null)
                SaveVideo();
            else if (recording)
                DiscardVideo();

            if (waitForSave)
                readyToRecord = true;
            else
                SendStartRecord();
        }

        public void SendStartRecord()
        {
            if (Settings.StallLoad.Value)
                SetStatus("Waiting for OBS...");
            else
                ClearStatus();
            Send(PrepareRequest("StartRecord")); 
        }

        static readonly char[] replacements = ['%', 'l', 'L', 't', 'T', 'm', 'd', 'D', 'B'];
        public void QueueVideo(string bonus = null)
        {
            if (!ready)
                return;
            if (bonus != null)
                usedBonus = bonus;
            var filename = Settings.OutputFile.Value;
            // based loosely off of neonlite's formatting since it's the quickest i have access to regarding C#
            // https://github.com/MOPSKATER/NeonLite/blob/main/Modules/DiscordActivity.cs#L180

            StringBuilder newFile = new();

            bool percent = false;
            foreach (char chr in filename)
            {
                if (chr == '%')
                {
                    percent = true;
                    continue;
                }
                else if (percent && replacements.Contains(chr))
                {
                    percent = false;
                    switch (chr)
                    {
                        case '%':
                            newFile.Append("%");
                            break;
                        case 'l':
                            newFile.Append(Singleton<Game>.Instance.GetCurrentLevel().levelID);
                            break;
                        case 'L':
                            newFile.Append(LocalizationManager.GetTranslation(Singleton<Game>.Instance.GetCurrentLevel().GetLevelDisplayName()));
                            break;
                        case 't':
                            newFile.Append(time);
                            break;
                        case 'T':
                            {
                                // newFile.Append(Utils.LongToTime(time, "#00.000").Replace(':', '.')); // THE FORMAT IS BUGGED???
                                // edit: the format is **SUPER** bugged holy shit
                                long ms = Utils.ConvertMicrosecondsToMilliseconds(time);
                                newFile.Append($"{(int)Utils.ConvertMillisecondsToSeconds_Float(ms):##00}.{ms % 1000:000}");

                                break;
                            }
                        case 'D':
                            newFile.Append(DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
                            break;
                        case 'd':
                            newFile.Append(DateTime.Now.ToString("yyyyMMddHHmmss"));
                            break;
                        case 'B':
                            newFile.Append(usedBonus);
                            break;
                    }
                }
                else
                {
                    if (percent)
                        newFile.Append("%");
                    newFile.Append(chr);
                }
            }

            queuedPath = newFile.ToString();
            queuedPrefix = basePath;
            if (Settings.GhostDir.Value)
                queuedPrefix = Path.GetDirectoryName(GhostRecorder.GetCompressedSavePathForLevel(level)) + Path.DirectorySeparatorChar;

            if (Settings.AutoSeconds.Value != -1)
                endingTimer = Settings.AutoSeconds.Value;
        }

        public void SaveVideo()
        {
            if (!recording)
                return;
            if (queuedPath == null)
                QueueVideo();
            SetStatus($"Saving to {queuedPath}...");
            OBSPacket packet = PrepareRequest("StopRecord");
            packet.data["requestId"] = new ProxyString($"{queuedPrefix}|{queuedPath}");
            queuedPath = null;
            queuedPrefix = null;
            usedBonus = null;
            endingTimer = 0;
            recording = false;
            waitForSave = true;
            Send(packet);
        }

        public void DiscardVideo()
        {
            if (!recording)
                return;
            OBSPacket packet = PrepareRequest("StopRecord");
            packet.data["requestId"] = new ProxyString("discard");
            queuedPath = null;
            queuedPrefix = null;
            usedBonus = null;
            endingTimer = 0;
            recording = false;
            waitForSave = true;
            Send(packet);
        }

        public void SetStatus(string status) => SetStatus(status, Color.white);
        public void SetStatus(string status, Color color)
        {
            if (status != statusText.text)
                Debug.Log($"Status: {status}");
            statusText.text = status;
            statusColor = color;
            statusTimer = 3;
            if (_moveY.goal != 10)
                _moveY.Start(null, 10);
            if (_opacityT.goal != 1)
                _opacityT.Start(null, 1);
        }

        public void ClearStatus()
        {
            _moveY.goal = _moveY.result = 20;
            _moveY.time = 0;
            _moveY.running = false;
            _opacityT.goal = _opacityT.result = 0;
            _opacityT.time = 0;
            _opacityT.running = false;
            statusTimer = 0;
        }

        public async void ParseResponse(OBSPacket response)
        {
            OBSPacket message = new(0, []);
            Debug.Log($"Recieved response: {response}");
            switch (response.opcode)
            {
                case OBSInfo.Opcodes.Hello:
                    {
                        message.opcode = OBSInfo.Opcodes.Identify;

                        if (response.data.Keys.Contains("authentication"))
                        {
                            using var sha = SHA256.Create();
                            string challenge = response.data["authentication"]["challenge"];
                            string salt = response.data["authentication"]["salt"];
                            // the chain is here

                            string base64 = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Settings.Password.Value + salt)));
                            message.data["authentication"] = new ProxyString(Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(base64 + challenge))));
                        }

                        message.data["rpcVersion"] = new ProxyNumber(1);
                        message.data["eventSubscriptions"] = new ProxyNumber(1 << 6);
                        await Send(message);
                        return;
                    }
                case OBSInfo.Opcodes.Identified:
                    {
                        ready = true;
                        SetStatus("Sucessfully identified with OBS!");
                        await Send(PrepareRequest("GetRecordDirectory"));
                        return;
                    }
                case OBSInfo.Opcodes.Event:
                    {
                        switch ((string)response.data["eventType"])
                        {
                            case "RecordStateChanged":
                                {
                                    if (recordRecieved && response.data["eventData"]["outputState"] == "OBS_WEBSOCKET_OUTPUT_STARTING")
                                    {
                                        ClearStatus();
                                        recordRecieved = false;
                                    }
                                    if (response.data["eventData"]["outputState"] == "OBS_WEBSOCKET_OUTPUT_STARTED")
                                    {
                                        recordRecieved = false;
                                        recording = true;
                                        potentialOut = response.data["eventData"]["outputPath"];
                                        ClearStatus();
                                        Send(PrepareRequest("GetRecordDirectory")); // just to make sure we have it for later
                                        if (Settings.StartAlert.Value)
                                            SetStatus("Recording started!");
                                    }
                                    if (response.data["eventData"]["outputState"] != "OBS_WEBSOCKET_OUTPUT_STOPPED")
                                        return;

                                    waitForSave = false;

                                    string realSave = response.data["eventData"]["outputPath"];
                                    int tries;
                                    if (responseID == "discard")
                                    {
                                        tries = 20;
                                        while (tries-- != 0)
                                        { // we have to spin until I/O lets us
                                            try
                                            {
                                                File.Delete(realSave);
                                                break;
                                            }
                                            catch
                                            {
                                                Thread.Sleep(50);
                                            }
                                        }
                                        if (tries == -1)
                                            SetStatus($"Failed to discard video.", errorColor);

                                        if (closing)
                                            await webSocket.CloseAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
                                        else if (readyToRecord)
                                        {
                                            readyToRecord = false;
                                            SendStartRecord();
                                        }
                                        return;
                                    }
                                    var split = responseID.Split('|');

                                    string ext = Path.GetExtension(realSave);
                                    string pre = split[0];
                                    string post = split[1];
                                    string full = pre + post;
                                    int index = 0;
                                    if (File.Exists(full + ext))
                                    {
                                        while (File.Exists(full + $" {++index})" + ext)) ;
                                        full += $" ({index})";
                                        post += $" ({index})";
                                    }

                                    tries = 20;
                                    Directory.CreateDirectory(Path.GetDirectoryName(full + ext));
                                    while (tries-- != 0)
                                    {
                                        try
                                        {
                                            File.Move(realSave, full + ext);
                                            break;
                                        }
                                        catch { Thread.Sleep(50); }
                                    }
                                    if (tries == -1)
                                        SetStatus($"Failed to move video.", errorColor);
                                    else
                                        SetStatus($"Saved to {post + ext}!");


                                    if (closing)
                                        await webSocket.CloseAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
                                    else if (readyToRecord)
                                    {
                                        readyToRecord = false;
                                        SendStartRecord();
                                    }
                                    return;
                                }
                        }
                        return;
                    }
                case OBSInfo.Opcodes.RequestResponse:
                    {
                        switch ((string)response.data["requestType"])
                        {
                            case "StartRecord":
                                {
                                    if (!response.data["requestStatus"]["result"])
                                    {
                                        ProxyObject statData = response.data["requestStatus"] as ProxyObject;
                                        if ((OBSInfo.RequestStatus)(int)statData["code"] != OBSInfo.RequestStatus.OutputRunning)
                                        {
                                            string error = statData.Keys.Contains("comment") ? statData["comment"] : ((OBSInfo.RequestStatus)(int)statData["code"]).ToString();
                                            SetStatus($"OBS failed to start recording: {error}", errorColor);
                                        }
                                        return;
                                    }
                                    recording = false;
                                    recordTimer = .05f;
                                    recordRecieved = true;
                                    return;
                                }
                            case "StopRecord":
                                {
                                    if (!response.data["requestStatus"]["result"])
                                    {
                                        waitForSave = false;
                                        ProxyObject statData = response.data["requestStatus"] as ProxyObject;
                                        string error = statData.Keys.Contains("comment") ? statData["comment"] : ((OBSInfo.RequestStatus)(int)statData["code"]).ToString();
                                        SetStatus($"OBS failed to save: {error}", errorColor);
                                        return;
                                    }
                                    responseID = response.data["requestId"];
                                    return;
                                }
                            case "GetRecordDirectory":
                                {
                                    basePath = (string)response.data["responseData"]["recordDirectory"] + Path.DirectorySeparatorChar;
                                    return;
                                }

                        }
                        return;
                    }
            }
        }


        public async Task Connect()
        {
            try
            {
                webSocket = new ClientWebSocket();
                await webSocket.ConnectAsync(new Uri($"ws://localhost:{Settings.Port.Value}"), CancellationToken.None);

                SetStatus("Successfully connected to OBS!");
                await Recieve();
            }
            catch (Exception e)
            {
                SetStatus($"Error connecting to OBS: {e.Message}", errorColor);
            }
        }

        public async Task Send(OBSPacket message)
        {
            var encoded = Encoding.UTF8.GetBytes(JSON.Dump(message, EncodeOptions.NoTypeHints));
            var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length);

            Debug.Log($"Sending {JSON.Dump(message, EncodeOptions.NoTypeHints)}..");

            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"Sent.");
            return;
        }

        public async Task Recieve()
        {
            ArraySegment<byte> buffer = new(new byte[8192]);

            while (webSocket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();

                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    string message = reader.ReadToEnd();
                    Variant variant = new ProxyObject();
                    OBSPacket packet = null;
                    try
                    {
                        variant = JSON.Load(message);
                        packet = new((OBSInfo.Opcodes)(int)(variant["op"] as ProxyNumber), variant["d"] as ProxyObject);
                        ParseResponse(packet);
                    }
                    catch (Exception e)
                    {
                        SetStatus($"Error parsing packet: {e.Message}", errorColor);
                        Debug.LogError(e);
                        Debug.LogWarning($"Recieved:\n{message}");
                        Debug.LogWarning($"Parsed JSON is:\n{variant.ToJSON()}");
                        Debug.LogWarning($"Built packet is:\n{packet}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    ready = false;
                    SetStatus($"OBS closed with code {(OBSInfo.CloseCode)result.CloseStatus.Value}", errorColor);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
        }
    }
}
