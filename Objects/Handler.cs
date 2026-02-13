using System.Security.Cryptography;
using System.Text;
using I2.Loc;
using MelonLoader.TinyJSON;
using UnityEngine;
using static NeonCapture.OBSInfo;
using Settings = NeonCapture.NeonCapture.Settings;

namespace NeonCapture.Objects
{
    internal class Handler : MonoBehaviour
    {
        internal static Handler i;
        [Serializable]
        public class OBSPacket(Opcodes opc, ProxyObject data)
        {
            public OBSPacket() : this(0, null) { }
            public OBSPacket(string j) : this(0, null) => ParseJSON(j);


            internal Opcodes opcode = opc;
            [Include]
            public int op { get { return (int)opcode; } private set { opcode = (Opcodes)value; } }

            internal ProxyObject data = data;
            [Include]
            public ProxyObject d { get { return (ProxyObject)JSON.Load(JSON.Dump(data, EncodeOptions.NoTypeHints)); } }

            public string ToJSON() => JSON.Dump(this, EncodeOptions.NoTypeHints);

            public void ParseJSON(string json)
            {
                var v = JSON.Load(json);
                op = (int)v["op"];
                data = (ProxyObject)v["d"];
            }
        }

        internal string basePath;
        internal string queuedPath;
        internal string queuedPrefix;
        internal string usedBonus;
        internal bool dontSave;

        string responseID;
        string potentialOut;

        internal bool ready = false;

        public float endingTimer = 0;

        internal enum RecordStatus
        {
            Stopped,
            RecordSent,
            RecordRecieved,
            Starting,
            Recording,
            Stopping
        }
        RecordStatus recordStatus = RecordStatus.Stopped;

        internal enum WaitingStatus
        {
            Nothing = -1,
            Save,
            Record,
            //Split,
        }
        WaitingStatus waitingStatus = WaitingStatus.Nothing;


        internal bool CheckStatus(RecordStatus comp, bool checkStop = true)
        {
            bool ret = recordStatus >= comp;
            if (checkStop && recordStatus == RecordStatus.Stopping)
                ret = false;

            NeonCapture.Log.DebugMsg($"CheckStatus: {comp} {recordStatus} = {ret} ({checkStop})");

            return ret;
        }


        float recordTimer;
        public long time;

        public void Awake() => i = NeonCapture.handler = this;

        public void Update()
        {
            if (endingTimer > 0)
            {
                endingTimer -= Time.unscaledDeltaTime;
                if (endingTimer <= 0)
                    SaveVideo();
                else if (endingTimer < 3 && Settings.AutoAlert.Value)
                    StatusText.i.SetStatus($"Auto-save in {(int)endingTimer + 1}...");
            }

            if (recordStatus == RecordStatus.RecordRecieved)
            {
                recordTimer -= Time.unscaledDeltaTime;
                if (recordTimer <= 0)
                {
                    recordTimer = .05f;
                    SendStartRecord();
                }
            }
        }

        public void OnDestroy()
        {
            if (ready && CheckStatus(RecordStatus.Starting))
            {
                OBSPacket packet = PrepareRequest("StopRecord");
                packet.data["requestId"] = new ProxyString("discard");

                waitingStatus = WaitingStatus.Nothing;

                NeonCapture.Log.Msg("Trying to send closing packets..");
                Awaiter.Send(packet); // hope this sent

                // manually have this instead of using fileIO
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
            recordStatus = RecordStatus.Stopped;
            ready = false;
            Awaiter.i.Cancel();
        }

        OBSPacket PrepareRequest(string type)
        {
            OBSPacket ret = new(OBSInfo.Opcodes.Request, []);
            ret.data["requestType"] = new ProxyString(type);
            ret.data["requestId"] = new ProxyString(DateTime.Now.ToString("yyyyMMddHHmmssfffffff"));
            ret.data["requestData"] = new ProxyObject();
            return ret;
        }

        public void StartRecording()
        {
            if (queuedPath != null)
                SaveVideo();
            else if (CheckStatus(RecordStatus.Recording))
                DiscardVideo();

            if (waitingStatus != WaitingStatus.Nothing)
                waitingStatus = WaitingStatus.Record;
            else
                SendStartRecord();
        }

        public void SendStartRecord()
        {
            if (Settings.StallLoad.Value)
                Hooks.showWaiting = true;
            else
                StatusText.i.ClearStatus();
            Awaiter.Send(PrepareRequest("StartRecord"));
        }

        static readonly char[] replacements = ['%', 'l', 'L', 't', 'T', 'm', 'd', 'D', 'B'];
        static readonly StringBuilder newFile = new();

        public void QueueVideo(string bonus = null, bool skipAuto = false)
        {
            if (!ready)
                return;
            if (bonus != null)
                usedBonus = bonus;
            var filename = Settings.OutputFile.Value;
            // based loosely off of neonlite's formatting since it's the quickest i have access to regarding C#
            // https://github.com/MOPSKATER/NeonLite/blob/main/Modules/DiscordActivity.cs#L180

            newFile.Clear();
            var level = Singleton<Game>.Instance.GetCurrentLevel();

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
                            var display = level.GetLevelDisplayName();
                            var local = LocalizationManager.GetTranslation(display);
                            if (string.IsNullOrEmpty(local))
                                local = level.levelDisplayName;
                            if (string.IsNullOrEmpty(local))
                                goto case 'l';
                            newFile.Append(local);
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
            if (Settings.GhostDir.Value)
                GhostUtils.GetPath(level.levelID, GhostUtils.GhostType.PersonalGhost, ref queuedPrefix);
            else
                queuedPrefix = basePath;

            if (Settings.AutoSeconds.Value != -1 && !skipAuto)
                endingTimer = Settings.AutoSeconds.Value;
            dontSave = skipAuto;
        }

        void SetStopping()
        {
            queuedPath = null;
            queuedPrefix = null;
            usedBonus = null;

            endingTimer = 0;
            recordStatus = RecordStatus.Stopping;
            waitingStatus = WaitingStatus.Save;
            dontSave = false;
        }

        public void SaveVideo()
        {
            if (!CheckStatus(RecordStatus.Starting))
                return;

            if (queuedPath == null)
                QueueVideo();

            StatusText.i.SetStatus($"Saving to {queuedPath}...");
            OBSPacket packet = PrepareRequest("StopRecord");
            packet.data["requestId"] = new ProxyString($"{queuedPrefix}|{queuedPath}");

            SetStopping();
            Awaiter.Send(packet);
        }

        public void DiscardVideo(bool split = true)
        {
            if (!CheckStatus(RecordStatus.Starting))
                return;

            split &= Settings.SplitFile.Value;

            OBSPacket packet = PrepareRequest(split ? "SplitRecordFile" : "StopRecord");
            packet.data["requestId"] = new ProxyString("discard");

            if (split && Settings.StallLoad.Value)
                Hooks.showWaiting = true;

            SetStopping();
            Awaiter.Send(packet);
        }

        void PrepareForMove(string path, string responseID)
        {
            var split = responseID.Split('|');

            string ext = Path.GetExtension(path);
            string pre = split[0] + Path.DirectorySeparatorChar;
            string post = split[1];
            string full = pre + post;
            int index = 0;
            if (File.Exists(full + ext))
            {
                while (File.Exists(full + $" {++index})" + ext)) ;
                full += $" ({index})";
                post += $" ({index})";
            }

            FileIO.Move(path, full + ext, (success) =>
            {
                if (!success)
                    StatusText.i.SetStatus($"Failed to move video.", StatusText.errorColor);
                else
                    StatusText.i.SetStatus($"Saved to {post + ext}!");
            });
        }

        public void ParseResponse(OBSPacket response)
        {
            OBSPacket message = new(0, []);
            switch (response.opcode)
            {
                case Opcodes.Hello:
                    {
                        message.opcode = Opcodes.Identify;

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
                        Awaiter.Send(message);
                        return;
                    }
                case Opcodes.Identified:
                    {
                        ready = true;
                        StatusText.i.SetStatus("Sucessfully identified with OBS!");
                        Awaiter.Send(PrepareRequest("GetRecordDirectory"));
                        return;
                    }
                case Opcodes.Event:
                    {
                        switch ((string)response.data["eventType"])
                        {
                            case "RecordStateChanged":
                                {
                                    var state = response.data["eventData"]["outputState"];
                                    switch (state.ToString())
                                    {
                                        case "OBS_WEBSOCKET_OUTPUT_STARTING":
                                            recordStatus = RecordStatus.Starting;
                                            StatusText.i.ClearStatus();
                                            return;
                                        case "OBS_WEBSOCKET_OUTPUT_STARTED":
                                            recordStatus = RecordStatus.Recording;
                                            potentialOut = response.data["eventData"]["outputPath"];
                                            StatusText.i.ClearStatus();

                                            Awaiter.Send(PrepareRequest("GetRecordDirectory")); // just to make sure we have it for later
                                            if (Settings.StartAlert.Value)
                                                StatusText.i.SetStatus("Recording started!", 1);
                                            return;

                                        default:
                                            return;

                                        case "OBS_WEBSOCKET_OUTPUT_STOPPED":
                                            break;
                                    }

                                    string realSave = response.data["eventData"]["outputPath"];
                                    if (!File.Exists(realSave))
                                    {
                                        // sometimes splitting the recording file can be weird
                                        // try using potential man
                                        realSave = potentialOut;
                                    }

                                    if (responseID.StartsWith("discard"))
                                    {
                                        FileIO.Discard(realSave, (success) =>
                                        {
                                            if (!success)
                                                StatusText.i.SetStatus($"Failed to discard video.", StatusText.errorColor);
                                        });

                                        if (waitingStatus == WaitingStatus.Record)
                                            SendStartRecord();

                                        waitingStatus = WaitingStatus.Nothing;

                                        return;
                                    }

                                    PrepareForMove(realSave, responseID);
                                    if (waitingStatus == WaitingStatus.Record)
                                        SendStartRecord();

                                    waitingStatus = WaitingStatus.Nothing;

                                    return;
                                }

                            case "RecordFileChanged":
                                {
                                    // this happens on split
                                    // only discards can split so we should discard the old file
                                    FileIO.Discard(potentialOut, (success) =>
                                    {
                                        if (!success)
                                            StatusText.i.SetStatus($"Failed to discard video.", StatusText.errorColor);
                                    });

                                    potentialOut = response.data["eventData"]["newOutputPath"];
                                    waitingStatus = WaitingStatus.Nothing;
                                    recordStatus = RecordStatus.Recording;

                                    StatusText.i.ClearStatus();

                                    Awaiter.Send(PrepareRequest("GetRecordDirectory")); // just to make sure we have it for later
                                    if (Settings.StartAlert.Value)
                                        StatusText.i.SetStatus("Recording started!", 1);

                                    return;
                                }
                        }
                        return;
                    }
                case Opcodes.RequestResponse:
                    {
                        switch ((string)response.data["requestType"])
                        {
                            case "StartRecord":
                                {
                                    if (!response.data["requestStatus"]["result"])
                                    {
                                        ProxyObject statData = response.data["requestStatus"] as ProxyObject;
                                        if ((RequestStatus)(int)statData["code"] != RequestStatus.OutputRunning)
                                        {
                                            string error = statData.Keys.Contains("comment") ? statData["comment"] : ((RequestStatus)(int)statData["code"]).ToString();
                                            StatusText.i.SetStatus($"OBS failed to start recording: {error}", StatusText.errorColor);
                                        }
                                        return;
                                    }

                                    recordStatus = RecordStatus.RecordRecieved;
                                    recordTimer = .05f;
                                    return;
                                }
                            case "StopRecord":
                                {
                                    if (!response.data["requestStatus"]["result"])
                                    {
                                        ProxyObject statData = response.data["requestStatus"] as ProxyObject;
                                        string error = statData.Keys.Contains("comment") ? statData["comment"] : ((RequestStatus)(int)statData["code"]).ToString();
                                        StatusText.i.SetStatus($"OBS failed to save: {error}", StatusText.errorColor);
                                        return;
                                    }
                                    responseID = response.data["requestId"];
                                    return;
                                }
                            case "GetRecordDirectory":
                                {
                                    if (!response.data["requestStatus"]["result"])
                                    {
                                        // just try again tbh
                                        Awaiter.Send(PrepareRequest("GetRecordDirectory"));

                                        return;
                                    }

                                    basePath = (string)response.data["responseData"]["recordDirectory"];
                                    return;
                                }
                            case "SplitRecordFile":
                                {
                                    if (!response.data["requestStatus"]["result"])
                                    {
                                        // splitting is not currently supported, do the regular thing
                                        recordStatus = RecordStatus.Recording;
                                        DiscardVideo(false);
                                        waitingStatus = WaitingStatus.Record;
                                    }
                                    return;
                                }

                        }
                        return;
                    }
            }
        }
    }
}
