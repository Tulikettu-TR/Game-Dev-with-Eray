using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Clawbles
{
    public sealed class ClawSession : MonoBehaviour
    {
        bool wasPilot, delayedHost;
        int recordedRun=-1;
        float mouseSensitivity=.12f;
        bool invertY;
        const string Protocol = "CLAWBLES/16";
        public ClawSimulation State { get; private set; } = new ClawSimulation();
        public ulong LocalId => net != null ? net.LocalClientId : ClawSimulation.Nobody;
        public bool Connected => net != null && net.IsConnectedClient;
        public bool Hosting => net != null && net.IsServer;
        public bool InputActive => Connected && !menuOpen && !chatOpen && !State.Shift.Blocking;
        public float LookYaw { get; private set; }
        public float LookPitch { get; private set; }
        public ClawVoice Voice { get; private set; }
        NetworkManager net; UnityTransport transport;
        string address = "127.0.0.1", seedText = "7241", status = "Host a seed, or join a friend on your LAN.";
        float sendClock, snapshotClock, connectionStart, localChatTime;
        int actionSequence, buttons, port = 7777, snapshots, peakPlayers;
        bool connecting, registered, menuOpen, chatOpen, focusChat;
        string draft = "";
        Vector2 chatScroll;
        readonly List<string> chat = new List<string>();
        readonly Dictionary<ulong, float> chatTimes = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> voiceTimes = new Dictionary<ulong, float>();
        GUIStyle title, body, muted, small, chatStyle;
        bool smoke, socialSmoke, chatSent, captured, overview, impactDemo, impactTriggered;
        float smokeStarted;
        string reportFile, captureFile;
        int chatReceived;
        public string Prompt
        {
            get
            {
                if (!State.Players.TryGetValue(LocalId, out var p)) return "";
                if (p.KnockTimer > 0) return "KNOCKED DOWN  /  getting up in " + p.KnockTimer.ToString("0.0") + "s";
                if(State.Operator==LocalId&&State.Tool==ClawTool.Scoop)return "SLIDE SCOOP UNDER LOAD / SHIFT precision / brake gently / E does not attach";
                if(State.Operator==LocalId&&State.PhysicalHook)return "THREAD TIP THROUGH RING / SHIFT precision / Lower and reverse to release / R recover";
                if (State.Operator == LocalId) return "DEAF PILOT - READ CHAT  /  E grip or release  /  Z/X room  /  C camera  /  R recover";
                if(State.Tool==ClawTool.Magnet){int target=State.PoleTarget(p);return target==-3?"Approach a toy or the lowered magnet to change its pole": "E / switch "+(target==-2?"MAGNET":target==-1?"MISSION TOY":"PLUSH "+(target+1))+" polarity";}
                if (p.Attachment == 1) return "HOLDING  /  A D turn  /  hold F to secure hook  /  SPACE let go";
                if (p.Attachment == 2) return "HANGING  /  E or SPACE let go";
                if (State.Map.RoomCount==0 && Vector3.Distance(p.Position, ClawSimulation.ConsolePosition) < 1.8f) return "E  /  use crane controls";
                if (Vector3.Distance(p.Position, State.Prize) < 2.2f) return "E  /  hold the load";
                return "";
            }
        }
        void Awake()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true; QualitySettings.vSyncCount = 0;
            var go = new GameObject("Session network"); transport = go.AddComponent<UnityTransport>(); net = go.AddComponent<NetworkManager>();
            net.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true,
                ForceSamePrefabs = false, TickRate = 30, ConnectionData = Encoding.ASCII.GetBytes(Protocol) };
            net.ConnectionApprovalCallback += Approve; net.OnClientConnectedCallback += Joined; net.OnClientDisconnectCallback += Left;
            Voice = gameObject.AddComponent<ClawVoice>(); Voice.Session = this; Voice.SendFrame = SendVoice;
            gameObject.AddComponent<ClawWorld>().Session = this;
            gameObject.AddComponent<ClawFeedback>().Session = this;
        }
        void Start()
        {
            mouseSensitivity=PlayerPrefs.GetFloat("MouseSensitivity",.12f);invertY=PlayerPrefs.GetInt("InvertY",0)==1;
            var args = Environment.GetCommandLineArgs(); string mode = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-clawHost") mode = "host";
                if (args[i] == "-clawDelayedHost") delayedHost=true;
                if (args[i] == "-clawClient" && i + 1 < args.Length) { mode = "client"; address = args[++i]; }
                if (args[i] == "-clawPort" && i + 1 < args.Length && int.TryParse(args[++i], out var n)) port = n;
                if (args[i] == "-clawSeed" && i + 1 < args.Length) seedText = args[++i];
                if (args[i] == "-clawSmoke" && i + 1 < args.Length) { smoke = true; reportFile = args[++i]; }
                if (args[i] == "-clawSocialSmoke") socialSmoke = true;
                if (args[i] == "-clawImpactDemo") impactDemo = true;
                if (args[i] == "-clawCapture" && i + 1 < args.Length) captureFile = args[++i];
                if (args[i] == "-clawOverview") overview = true;
            }
            if (mode != "") Connect(mode == "host");
            if (socialSmoke) Voice.Volume = 0; // Automated packet checks never play test audio aloud.
            smokeStarted = Time.realtimeSinceStartup;
        }
        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            bool version = Encoding.ASCII.GetString(req.Payload) == Protocol;
            res.Approved = version && net.ConnectedClientsIds.Count < 4; res.CreatePlayerObject = false; res.Pending = false;
            if (!res.Approved) res.Reason = !version ? "Different game version. Use the same build." : "The crew is full (4 players).";
        }
        void Joined(ulong id)
        {
            Register();
            if (Hosting) { State.Add(id); BroadcastChat("P" + (State.Players[id].Slot + 1) + " joined the crew."); }
            if (id == LocalId) { connecting = false; status = "Connected."; }
        }
        void Left(ulong id)
        {
            if (Hosting) { State.Remove(id); chatTimes.Remove(id); voiceTimes.Remove(id); }
            if (id == LocalId) { connecting = false; status = "Disconnected. " + net.DisconnectReason; Voice.ResetSession(); }
        }
        void Register()
        {
            if (registered || net.CustomMessagingManager == null) return;
            net.CustomMessagingManager.RegisterNamedMessageHandler("claw.input", ReadInput);
            net.CustomMessagingManager.RegisterNamedMessageHandler("claw.state", ReadState);
            net.CustomMessagingManager.RegisterNamedMessageHandler("claw.chat", ReadChat);
            net.CustomMessagingManager.RegisterNamedMessageHandler("claw.voice", ReadVoice);
            registered = true;
        }
        void Connect(bool host)
        {
            if (net.IsListening || net.ShutdownInProgress) return;
            if (!int.TryParse(seedText, out var seed)) { status = "Seed must be a whole number."; return; }
            address = address.Trim();
            if (!System.Net.IPAddress.TryParse(address, out _)) { status = "Enter a valid host IP."; return; }
            recordedRun=-1; State.DisposePhysics(); State = new ClawSimulation(seed,9); if(host && !smoke && !socialSmoke) State.Shift.NewRun(State,seed); Voice.ResetSession(); registered = false; actionSequence = buttons = 0;
            chat.Clear(); chatTimes.Clear(); voiceTimes.Clear(); chatReceived = snapshots = peakPlayers = 0;
            LookYaw = LookPitch = 0;
            transport.SetConnectionData(address, (ushort)Mathf.Clamp(port, 1024, 65535), "0.0.0.0");
            try
            {
                bool ok = host ? net.StartHost() : net.StartClient();
                if (ok) { Register(); connecting = !host; connectionStart = Time.unscaledTime; status = host ? "Hosting on UDP " + port : "Connecting..."; }
                else status = "Could not start the session.";
            }
            catch (Exception e) { status = "Connection failed: " + e.Message; net.Shutdown(); }
        }
        void Update()
        {
            if(Connected && !socialSmoke && !smoke && State.Shift.Phase==ShiftPhase.Won && recordedRun!=State.Shift.RunId){
                recordedRun=State.Shift.RunId;PlayerPrefs.SetInt("BestStars",Mathf.Max(PlayerPrefs.GetInt("BestStars",0),State.Shift.Stars));
                PlayerPrefs.SetInt("CompletedRuns",PlayerPrefs.GetInt("CompletedRuns",0)+1);PlayerPrefs.Save();
            }
            bool isPilot = Connected && State.Operator == LocalId;

            if(isPilot && !wasPilot){LookYaw=0;LookPitch=0;}
            wasPilot = isPilot;
            ClawDeafCheck.Tick(this);
            if(delayedHost && Time.realtimeSinceStartup-smokeStarted>1){delayedHost=false;Connect(true);}
            ClawCampaignCheck.Tick(this);
            var previewArgs=Environment.GetCommandLineArgs();int toolPreview=Array.IndexOf(previewArgs,"-clawToolPreview");
            if(Connected&&Hosting&&toolPreview>=0&&toolPreview+1<previewArgs.Length&&State.Shift.Phase==ShiftPhase.Briefing){State.Shift.Stage=Mathf.Clamp(int.Parse(previewArgs[toolPreview+1]),0,3);State.SetSeed(7241,State.Shift.Stage==0?9:State.Shift.Stage==1?16:25);State.ResetRig();State.Shift.Start();if(State.Tool==ClawTool.Scoop){State.Claw=State.Anchor=State.Map.RigSpawn-Vector3.up*2.5f;State.Prize=State.Claw+Vector3.up*(-1.48f+State.CurrentExtents(0).y+.002f);}}
            if(Connected&&Hosting&&Array.IndexOf(Environment.GetCommandLineArgs(),"-clawThreadPreview")>=0&&State.Shift.Phase==ShiftPhase.Briefing){State.Shift.Stage=2;State.SetSeed(7241,25);State.ResetRig();State.Shift.Start();var origin=State.Map.PrizeSpawn(0);State.Claw=State.Anchor=new Vector3(origin.x,4,origin.z);State.Prize=State.Claw-Vector3.up*(State.MissionOffset+.22f);}
            if(Connected && Hosting && Array.IndexOf(Environment.GetCommandLineArgs(),"-clawPilotPreview")>=0 && State.Shift.Phase==ShiftPhase.Briefing){State.Shift.Start();}
            var kb = Keyboard.current;
            if (kb != null && Application.isFocused && !smoke && !socialSmoke)
            {
                if (kb.escapeKey.wasPressedThisFrame) { if (chatOpen) chatOpen = false; else menuOpen = !menuOpen; }
                if (Connected && !menuOpen && (kb.enterKey.wasPressedThisFrame || (!chatOpen && kb.tKey.wasPressedThisFrame)))
                {
                    if (chatOpen) { SendChat(draft); draft = ""; chatOpen = false; }
                    else { chatOpen = true; focusChat = true; }
                }
            }
            bool focusedInput = InputActive && Application.isFocused && !smoke && !socialSmoke;
            Cursor.lockState = focusedInput ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !focusedInput;
            if (focusedInput && Mouse.current != null)
            {
                var d = Mouse.current.delta.ReadValue();
                LookYaw = Mathf.Repeat(LookYaw + d.x * mouseSensitivity, 360); LookPitch = Mathf.Clamp(LookPitch - d.y * mouseSensitivity * (invertY?-1:1), -80, 80);
            }
            Voice.Capture(Connected && !menuOpen && !chatOpen && Application.isFocused && !smoke && !socialSmoke && kb != null && kb.vKey.isPressed);
            if (connecting && Time.unscaledTime - connectionStart > 12) { Disconnect(); status = "Timed out. Check the host IP and LAN."; }
            if (Connected)
            {
                peakPlayers = Mathf.Max(peakPlayers, State.Players.Count);
                if (focusedInput && kb != null)
                {
                    if (State.Operator != LocalId)
                    {
                        if(kb.digit1Key.wasPressedThisFrame) SendChat("[SIGNAL] WEST / -X");
                        if(kb.digit2Key.wasPressedThisFrame) SendChat("[SIGNAL] EAST / +X");
                        if(kb.digit3Key.wasPressedThisFrame) SendChat("[SIGNAL] NORTH / +Z");
                        if(kb.digit4Key.wasPressedThisFrame) SendChat("[SIGNAL] SOUTH / -Z");
                        if(kb.digit5Key.wasPressedThisFrame) SendChat("[SIGNAL] RAISE THE HOOK");
                        if(kb.digit6Key.wasPressedThisFrame) SendChat("[SIGNAL] LOWER THE HOOK");
                        if(kb.digit7Key.wasPressedThisFrame) SendChat("[SIGNAL] STOP AND LET IT SETTLE");
                        if(kb.digit8Key.wasPressedThisFrame) SendChat("[SIGNAL] RELEASE NOW");
                    }
                    if (kb.spaceKey.wasPressedThisFrame) buttons |= 1;
                    if (kb.eKey.wasPressedThisFrame) buttons |= 2;
                    if (kb.qKey.wasPressedThisFrame) buttons |= 4;
                    if (kb.rKey.wasPressedThisFrame) buttons |= 8;
                    if (kb.gKey.wasPressedThisFrame) buttons |= 16;
                }
                sendClock += Time.unscaledDeltaTime;
                if (sendClock >= 0.05f)
                {
                    sendClock = 0;
                    Vector2 local = Vector2.zero; float lift = 0; bool securing = false;
                    if (focusedInput && kb != null)
                    {
                        local = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                        lift = (kb.spaceKey.isPressed ? 1 : 0) - (kb.leftCtrlKey.isPressed ? 1 : 0); securing = kb.fKey.isPressed;
                        if(State.Operator==LocalId&&kb.leftShiftKey.isPressed){local*=.12f;lift*=.12f;}
                    }
                    if (smoke) local = Time.realtimeSinceStartup - smokeStarted < 3 ? Vector2.right : Vector2.zero;
                    var world = Quaternion.Euler(0, LookYaw, 0) * new Vector3(local.x, 0, local.y); var move = new Vector2(world.x, world.z);
                    if (buttons != 0) actionSequence++;
                    if (Hosting) State.Input(LocalId, move, lift, actionSequence, buttons, Time.unscaledTime, LookYaw, local.x, securing);
                    else
                    {
                        using var w = new FastBufferWriter(48, Allocator.Temp);
                        w.WriteValueSafe(move); w.WriteValueSafe(lift); w.WriteValueSafe(actionSequence); w.WriteValueSafe(buttons);
                        w.WriteValueSafe(LookYaw); w.WriteValueSafe(local.x); w.WriteValueSafe(securing);
                        net.CustomMessagingManager.SendNamedMessage("claw.input", NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
                    }
                    buttons = 0;
                    if (socialSmoke)
                    {
                        if (!chatSent && Time.realtimeSinceStartup - smokeStarted > 2) { SendChat("Synthetic connection check"); chatSent = true; }
                        Voice.SyntheticFrame(Time.realtimeSinceStartup);
                    }
                }
            }
            float age = Time.realtimeSinceStartup - smokeStarted;
            if (impactDemo && Hosting && !impactTriggered && age > 5)
            {
                impactTriggered = true;
                foreach (var p in State.Players.Values) if (p.Id != LocalId)
                { p.Position = State.Players[LocalId].Position + new Vector3(1.5f, 0, 1.5f); State.Prize = p.Position + Vector3.up * 4; State.PrizeVelocity = Vector3.down * 5; break; }
            }
            if (!captured && !string.IsNullOrEmpty(captureFile) && age > 6)
            { captured = true; GetComponent<ClawWorld>().Capture(captureFile, overview, impactDemo); }
            if (smoke && age > 14)
            {
                bool ok = Connected && peakPlayers >= 2 && (Hosting || snapshots > 10) && (!socialSmoke || (chatReceived > 0 && Voice.ReceivedFrames > 0));
                File.WriteAllText(reportFile, "{\"ok\":" + ok.ToString().ToLowerInvariant() + ",\"peakPlayers\":" + peakPlayers + ",\"snapshots\":" + snapshots +
                    ",\"chat\":" + chatReceived + ",\"voiceFrames\":" + Voice.ReceivedFrames + ",\"seed\":" + State.Map.Seed + ",\"obstacles\":" + State.Map.Obstacles.Count + ",\"knocks\":" + State.KnockEvents + "}");
                smoke = false; Application.Quit(ok ? 0 : 1);
            }
        }
        void FixedUpdate()
        {
            if (!Hosting) return;
            ClawContactCheck.BeforeStep(this);State.Step(Time.fixedDeltaTime, Time.unscaledTime); snapshotClock += Time.fixedDeltaTime;
            if (snapshotClock < 0.05f) return; snapshotClock = 0;
            using var w = new FastBufferWriter(8192, Allocator.Temp);
            var shift=State.Shift;
            w.WriteValueSafe((int)shift.Phase);w.WriteValueSafe(shift.Stage);w.WriteValueSafe(shift.Seconds);w.WriteValueSafe(shift.Credits);w.WriteValueSafe(shift.Stars);
            w.WriteValueSafe(shift.StageStartScore);w.WriteValueSafe(shift.RunId);w.WriteValueSafe(shift.GripLevel);w.WriteValueSafe(shift.TeamLevel);w.WriteValueSafe(shift.ExtraTime);w.WriteValueSafe(shift.InitialSeed);
            w.WriteValueSafe(State.Map.Seed);w.WriteValueSafe(State.Map.RoomCount); w.WriteValueSafe(State.Claw); w.WriteValueSafe(State.Anchor); w.WriteValueSafe(State.LaserResets);w.WriteValueSafe(State.LaserClock); w.WriteValueSafe(State.Prize); w.WriteValueSafe(State.PrizeYaw);w.WriteValueSafe(State.PrizeRotation);
            w.WriteValueSafe(State.Gripped); w.WriteValueSafe(State.GripQuality); w.WriteValueSafe(State.Operator); w.WriteValueSafe(State.Score);
            w.WriteValueSafe(State.ResetTimer); w.WriteValueSafe(State.KnockEvents); w.WriteValueSafe(State.SlipEvents); w.WriteValueSafe(State.Notice); w.WriteValueSafe(State.Players.Count);
            foreach (var p in State.Players.Values)
            { w.WriteValueSafe(p.Id); w.WriteValueSafe(p.Slot); w.WriteValueSafe(p.Position); w.WriteValueSafe(p.Attachment); w.WriteValueSafe(p.Yaw); w.WriteValueSafe(p.KnockTimer); w.WriteValueSafe(p.KnockVelocity); w.WriteValueSafe(p.SignalTimer);w.WriteValueSafe(p.Signal); }
            w.WriteValueSafe(State.HeldToy);
            int changed=0;foreach(var toy in State.Map.Toys)if(toy.Changed)changed++;
            w.WriteValueSafe(changed);
            foreach(var toy in State.Map.Toys)if(toy.Changed){w.WriteValueSafe(toy.Id);w.WriteValueSafe(toy.Bounds.center);w.WriteValueSafe(toy.Pole);}
            w.WriteValueSafe(State.MagnetPole);w.WriteValueSafe(State.PrizePole);w.WriteValueSafe(State.MagneticToys.Count);
            for(int i=0;i<State.MagneticToys.Count;i++){w.WriteValueSafe(State.MagneticToys[i]);w.WriteValueSafe(State.MagneticOffsets[i]);}
            foreach (var id in net.ConnectedClientsIds) if (id != LocalId) net.CustomMessagingManager.SendNamedMessage("claw.state", id, w, NetworkDelivery.ReliableFragmentedSequenced);
        }
        void ReadInput(ulong sender, FastBufferReader r)
        {
            if (!Hosting || !r.TryBeginRead(29)) return;
            r.ReadValueSafe(out Vector2 move); r.ReadValueSafe(out float lift); r.ReadValueSafe(out int seq); r.ReadValueSafe(out int keys);
            r.ReadValueSafe(out float yaw); r.ReadValueSafe(out float turn); r.ReadValueSafe(out bool securing);
            State.Input(sender, move, lift, seq, keys, Time.unscaledTime, yaw, turn, securing);
        }
        void ReadState(ulong sender, FastBufferReader r)
        {
            if (Hosting || sender != NetworkManager.ServerClientId) return;
            var shift=State.Shift;r.ReadValueSafe(out int phase);shift.Phase=(ShiftPhase)phase;
            r.ReadValueSafe(out shift.Stage);r.ReadValueSafe(out shift.Seconds);r.ReadValueSafe(out shift.Credits);r.ReadValueSafe(out shift.Stars);
            r.ReadValueSafe(out shift.StageStartScore);r.ReadValueSafe(out shift.RunId);r.ReadValueSafe(out shift.GripLevel);r.ReadValueSafe(out shift.TeamLevel);r.ReadValueSafe(out shift.ExtraTime);r.ReadValueSafe(out shift.InitialSeed);
            r.ReadValueSafe(out int seed);r.ReadValueSafe(out int rooms); State.SetSeed(seed,rooms);
            r.ReadValueSafe(out State.Claw); r.ReadValueSafe(out State.Anchor); r.ReadValueSafe(out State.LaserResets);r.ReadValueSafe(out State.LaserClock);State.Map.UpdateLasers(State.Shift.Stage,State.LaserClock); r.ReadValueSafe(out State.Prize); r.ReadValueSafe(out State.PrizeYaw);r.ReadValueSafe(out State.PrizeRotation);
            r.ReadValueSafe(out State.Gripped); r.ReadValueSafe(out State.GripQuality); r.ReadValueSafe(out State.Operator); r.ReadValueSafe(out State.Score);
            r.ReadValueSafe(out State.ResetTimer); r.ReadValueSafe(out State.KnockEvents); r.ReadValueSafe(out State.SlipEvents); r.ReadValueSafe(out State.Notice); r.ReadValueSafe(out int count);
            if (count < 0 || count > 4) return;
            // Snapshots are complete; replacing the small list also handles simultaneous leave/join.
            State.Players.Clear();
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out ulong id); r.ReadValueSafe(out int slot); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out int attachment);
                r.ReadValueSafe(out float yaw); r.ReadValueSafe(out float knock); r.ReadValueSafe(out Vector3 impulse); r.ReadValueSafe(out float signal);r.ReadValueSafe(out int gesture);
                State.Players.Add(id, new ClawSimulation.Player { Id = id, Slot = slot, Position = pos, Attachment = attachment, Yaw = yaw, KnockTimer = knock, SignalTimer = signal, Signal=gesture, KnockVelocity = impulse });
            }
            r.ReadValueSafe(out State.HeldToy);r.ReadValueSafe(out int changed);
            if(changed<0||changed>State.Map.Toys.Count)return;
            for(int i=0;i<changed;i++){r.ReadValueSafe(out int id);r.ReadValueSafe(out Vector3 center);r.ReadValueSafe(out int pole);if(id>=0&&id<State.Map.Toys.Count){State.Map.Toys[id].SetCenter(center);State.Map.Toys[id].Pole=pole;}}
            r.ReadValueSafe(out State.MagnetPole);r.ReadValueSafe(out State.PrizePole);r.ReadValueSafe(out int attached);
            State.MagneticToys.Clear();State.MagneticOffsets.Clear();
            for(int i=0;i<attached;i++){r.ReadValueSafe(out int id);r.ReadValueSafe(out Vector3 offset);State.MagneticToys.Add(id);State.MagneticOffsets.Add(offset);}
            snapshots++;
        }
        public static string CleanChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var b = new StringBuilder();
            foreach (char c in text) if (!char.IsControl(c) && b.Length < 160) b.Append(c);
            return b.ToString().Trim();
        }
        void SendChat(string text)
        {
            text = CleanChat(text); if (!Connected || text.Length == 0 || Time.unscaledTime - localChatTime < 0.7f) return;
            localChatTime = Time.unscaledTime;
            if (Hosting) AcceptChat(LocalId, text);
            else { using var w = new FastBufferWriter(400, Allocator.Temp); w.WriteValueSafe(text); net.CustomMessagingManager.SendNamedMessage("claw.chat", NetworkManager.ServerClientId, w); }
        }
        void ReadChat(ulong sender, FastBufferReader r)
        {
            if (r.Length - r.Position > 420) return;
            try { r.ReadValueSafe(out string text); if (Hosting) AcceptChat(sender, text); else if (sender == NetworkManager.ServerClientId) AddChat(text); }
            catch (Exception) { /* Reject malformed chat without interrupting gameplay. */ }
        }
        void AcceptChat(ulong sender, string text)
        {
            if (!State.Players.TryGetValue(sender, out var p)) return;
            if (chatTimes.TryGetValue(sender, out var last) && Time.unscaledTime - last < 0.7f) return;
            text = CleanChat(text); if (text.Length == 0) return; chatTimes[sender] = Time.unscaledTime;
            int gesture=ClawGestures.Parse(text);if(gesture!=0 && sender!=State.Operator && p.KnockTimer<=0){p.Signal=gesture;p.SignalTimer=2.5f;}
            BroadcastChat("P" + (p.Slot + 1) + ": " + text);
        }
        void BroadcastChat(string text)
        {
            AddChat(text);
            using var w = new FastBufferWriter(440, Allocator.Temp); w.WriteValueSafe(text);
            foreach (var id in net.ConnectedClientsIds) if (id != LocalId) net.CustomMessagingManager.SendNamedMessage("claw.chat", id, w);
        }
        void AddChat(string text) { chat.Add(text); if (chat.Count > 24) chat.RemoveAt(0); chatReceived++; chatScroll.y = float.MaxValue; }
        void SendVoice(byte[] bytes, uint sequence)
        {
            if (!Connected) return;
            if (Hosting) RelayVoice(LocalId, sequence, bytes);
            else
            {
                using var w = new FastBufferWriter(340, Allocator.Temp); w.WriteValueSafe(LocalId); w.WriteValueSafe(sequence);
                w.WriteBytesSafe(bytes, bytes.Length);
                net.CustomMessagingManager.SendNamedMessage("claw.voice", NetworkManager.ServerClientId, w, NetworkDelivery.UnreliableSequenced);
            }
        }
        void ReadVoice(ulong sender, FastBufferReader r)
        {
            if (r.Length - r.Position != 12 + ClawVoice.FrameSamples) return;
            r.ReadValueSafe(out ulong talker); r.ReadValueSafe(out uint seq);
            byte[] data = new byte[ClawVoice.FrameSamples]; r.ReadBytesSafe(ref data, data.Length);
            if (Hosting) RelayVoice(sender, seq, data); // Never trust a client's claimed talker id.
            else if (sender == NetworkManager.ServerClientId) Voice.Receive(talker, seq, data);
        }
        void RelayVoice(ulong sender, uint seq, byte[] bytes)
        {
            if (!State.Players.TryGetValue(sender, out var talker)) return;
            if (voiceTimes.TryGetValue(sender, out var last) && Time.unscaledTime - last < 0.012f) return;
            voiceTimes[sender] = Time.unscaledTime;
            if (sender != LocalId && State.Operator != LocalId && State.Players.TryGetValue(LocalId, out var listener) && (sender==State.Operator || ClawVoice.InRange(talker.Position, listener.Position))) Voice.Receive(sender, seq, bytes);
            using var w = new FastBufferWriter(340, Allocator.Temp); w.WriteValueSafe(sender); w.WriteValueSafe(seq); w.WriteBytesSafe(bytes, bytes.Length);
            foreach (var p in State.Players.Values)
                if (p.Id != LocalId && p.Id != sender && p.Id != State.Operator && (sender==State.Operator || ClawVoice.InRange(talker.Position, p.Position)))
                    net.CustomMessagingManager.SendNamedMessage("claw.voice", p.Id, w, NetworkDelivery.UnreliableSequenced);
        }
        void Disconnect()
        {
            Voice.ResetSession(); net.Shutdown(); connecting = registered = menuOpen = chatOpen = false; State.DisposePhysics(); State = new ClawSimulation();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void OnDestroy(){State.DisposePhysics();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (net == null) return;
            net.ConnectionApprovalCallback -= Approve; net.OnClientConnectedCallback -= Joined; net.OnClientDisconnectCallback -= Left;
            net.Shutdown(); Destroy(net.gameObject);
        }
        void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1, 0.78f, 0.36f) } };
                body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = false, normal = { textColor = Color.white } };
                muted = new GUIStyle(body) { normal = { textColor = new Color(0.5f, 0.92f, 0.8f) } };
                small = new GUIStyle(body) { fontSize = 12 };
                chatStyle = new GUIStyle(small) { wordWrap = true, richText = false };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f); GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            Panel(new Rect(18, 16, 315, 86)); GUI.Label(new Rect(34, 22, 300, 38), "CLAWBLES", title);
            GUI.Label(new Rect(34, 61, 290, 27), "SEED " + State.Map.Seed + "  /  DELIVERED " + State.Score, muted);
            if (Connected)
            {
                GUI.Label(new Rect(w - 295, 20, 280, 27), "CREW " + State.Players.Count + " / 4   |   ESC menu", body);
                GUI.Label(new Rect(w - 295, 47, 280, 42), State.Operator == LocalId ? "INCOMING VOICE OFF / READ CREW CHAT" : Voice.Status, Voice.Recording ? muted : small);
                if (State.HasLoad)
                {
                    Panel(new Rect(w / 2 - 150, 22, 300, 59));
                    GUI.Label(new Rect(w / 2 - 139, 28, 280, 23), (State.HeldToy>=0?"WRONG PLUSH  ":"HOOK  ") + Mathf.RoundToInt(State.GripQuality * 100) + "%  " + (State.GripQuality >= 0.85f ? "SECURE" : "SLIPPING"), body);
                    var color = GUI.color; GUI.color = State.GripQuality >= 0.85f ? Color.green : new Color(1, 0.35f, 0.2f);
                    GUI.DrawTexture(new Rect(w / 2 - 139, 58, 278 * Mathf.Clamp01(State.GripQuality), 7), Texture2D.whiteTexture); GUI.color = color;
                }
                GUI.Label(new Rect(w / 2 - 4, h / 2 - 12, 20, 28), "+", body);
                GUI.Label(new Rect(w / 2 - 320, h / 2 + 38, 640, 55), Prompt, muted);
                Panel(new Rect(18, h - 116, w - 424, 98));
                GUI.Label(new Rect(32, h - 108, w - 450, 45), State.Notice, muted);
                GUI.Label(new Rect(32, h - 61, w - 450, 38), State.Operator == LocalId ? "WASD crane / SPACE-CTRL lift / E grip / Z-X room / C camera / SHIFT precision" : "WASD walk / E hold / G polarity / V talk / R recover (-10s)", small);
                DrawShiftHUD(w,h);
                DrawChat(w, h);
            }
            if(Connected && State.Shift.Blocking && !menuOpen) DrawShiftModal(w,h);
            if (!Connected || menuOpen)
            {
                float x = w / 2 - 235, y = h / 2 - 270; Panel(new Rect(x, y, 470, 540));
                GUILayout.BeginArea(new Rect(x + 24, y + 18, 422, 504));
                GUILayout.Label(Connected ? "TAKE A BREATHER" : "CLOCK IN. CLAW OUT.", title); GUILayout.Space(10);
                if (Connected)
                {
                    if (GUILayout.Button("BACK TO GAME", GUILayout.Height(36))) menuOpen = false;
                    Voice.Muted = GUILayout.Toggle(Voice.Muted, "Mute incoming voice");
                    GUILayout.Label("Voice volume", body); Voice.Volume = GUILayout.HorizontalSlider(Voice.Volume, 0, 1);
                    GUILayout.Label("Microphone (hold V in game)", body);
                    if (GUILayout.Button(Voice.DeviceLabel, GUILayout.Height(30))) Voice.NextDevice();
                    GUILayout.Space(12);
                    GUILayout.Label("Mouse sensitivity",body);mouseSensitivity=GUILayout.HorizontalSlider(mouseSensitivity,.04f,.3f);
                    invertY=GUILayout.Toggle(invertY,"Invert vertical look");
                    var feedback=GetComponent<ClawFeedback>();GUILayout.Label("Effects volume",body);feedback.Volume=GUILayout.HorizontalSlider(feedback.Volume,0,1);
                    if(GUILayout.Button("SAVE SETTINGS")){PlayerPrefs.SetFloat("MouseSensitivity",mouseSensitivity);PlayerPrefs.SetInt("InvertY",invertY?1:0);PlayerPrefs.SetFloat("EffectsVolume",feedback.Volume);PlayerPrefs.Save();}
                    if (GUILayout.Button("LEAVE SESSION", GUILayout.Height(34))) Disconnect();
                }
                else
                {
                    GUILayout.Label("1-4 crew / 9-25 rooms / random CCTV pilot",small);
                    GUILayout.Label("Host IP", body); address = GUILayout.TextField(address, GUILayout.Height(28));
                    GUILayout.Label("Map seed", body); seedText = GUILayout.TextField(seedText, GUILayout.Height(28));
                    if (GUILayout.Button("NEW RANDOM SEED", GUILayout.Height(27))) seedText = UnityEngine.Random.Range(1, int.MaxValue).ToString();
                    GUI.enabled = !connecting && !net.ShutdownInProgress;
                    if (GUILayout.Button("HOST / SOLO", GUILayout.Height(37))) Connect(true);
                    if (GUILayout.Button("JOIN FRIEND", GUILayout.Height(34))) Connect(false);
                    GUI.enabled = true;
                    if (connecting && GUILayout.Button("CANCEL")) Disconnect();
                    GUILayout.Label(status, muted);
                    GUILayout.Label("BEST SHIFT RATING  "+PlayerPrefs.GetInt("BestStars",0)+" / 12",small);
                }
                GUILayout.EndArea();
            }
        }
        void DrawShiftHUD(float w,float h)
        {
            var shift=State.Shift;if(shift.Phase==ShiftPhase.Practice)return;
            Panel(new Rect(18,112,315,132));
            GUI.Label(new Rect(32,120,290,23),"SHIFT "+(shift.Stage+1)+" / "+ClawShift.StageCount+"   "+shift.Title,muted);
            int seconds=Mathf.CeilToInt(shift.Seconds);
            GUI.Label(new Rect(32,148,280,30),$"{seconds/60:00}:{seconds%60:00}   |   {State.Score-shift.StageStartScore} / {shift.Quota} DELIVERED",body);
            GUI.Label(new Rect(32,181,280,48),"ROOMS "+State.Map.RoomCount+" / "+State.Tool+" / CREDITS "+shift.Credits+"\n"+(State.Tool==ClawTool.Magnet?"Magnet "+(State.MagnetPole>0?"N":"S")+" / Target "+(State.PrizePole>0?"N":"S")+" / Crew G: polarity":"Laser contact costs 15 seconds."),small);
        }
        void DrawShiftModal(float w,float h)
        {
            var shift=State.Shift;float x=w/2-310,y=h/2-195;
            Panel(new Rect(x,y,620,390));GUILayout.BeginArea(new Rect(x+28,y+22,564,345));
            string heading=shift.Phase==ShiftPhase.Briefing?shift.Title:shift.Phase==ShiftPhase.Upgrade?"SHIFT CLEARED":shift.Phase==ShiftPhase.Won?"CREW OF THE NIGHT":"SHIFT CLOSED";
            GUILayout.Label(heading,title);GUILayout.Space(12);
            if(shift.Phase==ShiftPhase.Briefing){
                GUILayout.Label(shift.Rule,body);GUILayout.Space(8);
                GUILayout.Label("Deliver "+shift.Quota+" x "+shift.LoadName+" before the clock runs out.",muted);
                GUILayout.Label("PILOT: randomly assigned on START. Isolated CCTV booth; Z/X rooms, C camera.\nCREW: hold the load with E, secure with F. Send direction signals with 1-8.\nDrop inside the green hatch. A loose load can flatten your friends.",body);
                GUILayout.Space(12);GUI.enabled=Hosting;
                if(GUILayout.Button("START SHIFT",GUILayout.Height(42)))shift.Start();
            }else if(shift.Phase==ShiftPhase.Upgrade){
                GUILayout.Label("Credits "+shift.Credits+"   /   Earned stars "+shift.Stars,muted);
                GUILayout.Label("Choose one team upgrade for the remaining shifts.",body);GUILayout.Space(10);
                GUI.enabled=Hosting && shift.Credits>=150;
                if(GUILayout.Button("150  |  GRIP LINER - slower slipping",GUILayout.Height(37)))shift.Next(State,0);
                if(GUILayout.Button("150  |  RIGGING KIT - faster crew securing",GUILayout.Height(37)))shift.Next(State,1);
                if(GUILayout.Button("150  |  OVERTIME PASS - 60 extra seconds",GUILayout.Height(37)))shift.Next(State,2);
                GUI.enabled=Hosting;if(GUILayout.Button("KEEP CREDITS AND CONTINUE",GUILayout.Height(32)))shift.Next(State,3);
            }else{
                GUILayout.Label(shift.Phase==ShiftPhase.Won?"Every order delivered. Your crew made it!":"The clock won this time. Change roles and try again.",body);
                GUILayout.Space(12);GUILayout.Label("RATING "+shift.Stars+" / 12    CREDITS "+shift.Credits,muted);
                GUILayout.Label("Delivered "+State.Score+"  /  Laser resets "+State.LaserResets+"  /  Knockdowns "+State.KnockEvents,body);
                GUILayout.Space(18);GUI.enabled=Hosting;
                if(GUILayout.Button("ANOTHER NIGHT / NEW LAYOUT",GUILayout.Height(42)))shift.NewRun(State,unchecked(shift.InitialSeed+104729));
            }
            GUI.enabled=true;
            if(!Hosting)GUILayout.Label("The host selects when the crew continues.",small);
            GUILayout.EndArea();
        }
        void DrawChat(float w, float h)
        {
            var rect = new Rect(w - 390, h - 265, 372, 247); Panel(rect);
            GUI.Label(new Rect(rect.x + 12, rect.y + 8, 345, 24), "CREW CHAT  /  T or ENTER", muted);
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 37, 348, 153));
            chatScroll = GUILayout.BeginScrollView(chatScroll);
            foreach (var line in chat) GUILayout.Label(line, chatStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (chatOpen)
            {
                GUI.SetNextControlName("ClawChat"); draft = GUI.TextField(new Rect(rect.x + 12, rect.y + 202, 275, 29), draft, 160);
                if (focusChat && Event.current.type == EventType.Repaint) { GUI.FocusControl("ClawChat"); focusChat = false; }
                if (GUI.Button(new Rect(rect.x + 292, rect.y + 202, 68, 29), "SEND")) { SendChat(draft); draft = ""; chatOpen = false; }
            }
            else GUI.Label(new Rect(rect.x + 12, rect.y + 207, 345, 25), "1/2 W/E  3/4 N/S  5/6 up/down  7 stop  8 drop", small);
        }
        static void Panel(Rect r)
        {
            var old = GUI.color; GUI.color = new Color(0.035f, 0.065f, 0.12f, 0.92f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
    }
}





















