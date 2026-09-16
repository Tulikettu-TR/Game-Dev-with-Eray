using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Clawbles
{
    public sealed class ClawWorld : MonoBehaviour
    {
        public ClawSession Session;
        Transform root, layoutRoot, claw, duck, cable, trolley;
        Camera view;
        ClawControlRoom controlRoom;Transform scoopModel,magnetModel,hookModel,missionRing;
        readonly List<Mesh> plushChunks = new List<Mesh>();
        Mesh[] plushMeshes;Mesh scoopRamp;
        Mesh[] heroMeshes;
        Transform loadSkin;
        Renderer loadRenderer;
        Transform goalMarker;
        int lastStage=-1;
        Material[] plushMaterials;
        Material glass, chrome, white, plushPink, plushMint, plushYellow, plushCream, plushOrange;
        Texture2D fabric;
        Transform carriage;
        readonly List<Transform> fingers = new List<Transform>();
        Transform hands, laserRoot;
        readonly List<Transform> laserVisuals=new List<Transform>();
        readonly List<Transform> laserHeads=new List<Transform>();
        ClawLayout builtLayout;
        int builtSeed = int.MinValue;
        bool hasSeed, cameraPlaced;
        readonly Dictionary<ulong, ClawToy> toys = new Dictionary<ulong, ClawToy>();
        readonly List<ulong> removed = new List<ulong>();
        readonly List<Material> materials = new List<Material>();
        Material mint, navy, gold, coral, cream, metal, floorMat, laserMat;
        Material[] paints;
        Texture2D floorTexture, wallTexture;
        Material wallMat, trimMat, lightMat;
        readonly List<Mesh> surfaceMeshes=new List<Mesh>();
        void Start()
        {
            root = new GameObject("CLAWBLES Arcade Cabinet").transform; root.SetParent(transform, false);
            mint = Mat("Mint", new Color(0.2f, 0.68f, 0.56f)); navy = Mat("Ink", new Color(0.075f, 0.105f, 0.16f));
            gold = Mat("Duck yellow", new Color(0.97f, 0.64f, 0.1f)); coral = Mat("Coral", new Color(0.83f, 0.25f, 0.28f));
            cream = Mat("Warm white", new Color(0.77f, 0.78f, 0.7f)); metal = Mat("Steel", new Color(0.35f, 0.42f, 0.48f));
            laserMat = Mat("Laser beam", new Color(1,.025f,.12f)); laserMat.EnableKeyword("_EMISSION"); laserMat.SetColor("_EmissionColor",new Color(1,.02f,.1f)*2);
            floorMat = Mat("Mint terrazzo tiles", Color.white);floorMat.SetFloat("_Smoothness",.24f);
            floorTexture=ClawSurfaceArt.Texture(false);floorMat.mainTexture=floorTexture;
            wallTexture=ClawSurfaceArt.Texture(true);wallMat=Mat("Printed enamel panels",Color.white);wallMat.mainTexture=wallTexture;wallMat.SetFloat("_Smoothness",.36f);
            trimMat=Mat("Deep teal framing",new Color(.10f,.29f,.31f));trimMat.SetFloat("_Smoothness",.42f);
            lightMat=Mat("Warm cabinet light",new Color(1,.85f,.55f));lightMat.EnableKeyword("_EMISSION");lightMat.SetColor("_EmissionColor",new Color(1,.65f,.25f)*.7f);
            paints = new[] { coral, mint, Mat("Lilac", new Color(0.48f, 0.36f, 0.69f)), gold };
            chrome=Mat("Polished claw steel",new Color(.66f,.73f,.78f));chrome.SetFloat("_Metallic",.85f);chrome.SetFloat("_Smoothness",.82f);
            white=Mat("Ivory enamel",new Color(.92f,.91f,.85f));white.SetFloat("_Smoothness",.45f);
            glass=new Material(Resources.Load<Material>("CabinetGlass"));materials.Add(glass);
            fabric=new Texture2D(64,64,TextureFormat.RGB24,true);fabric.name="Woven plush fabric";
            var weave=new Color[4096];var textile=new System.Random(43);
            for(int i=0;i<weave.Length;i++){float c=.83f+(float)textile.NextDouble()*.17f;if((i%64)%3==0)c*=.93f;weave[i]=new Color(c,c,c);}
            fabric.SetPixels(weave);fabric.Apply();fabric.wrapMode=TextureWrapMode.Repeat;
            plushPink=Cloth("Rose teddy",new Color(.89f,.47f,.59f));plushMint=Cloth("Mint bunny",new Color(.38f,.73f,.67f));
            plushYellow=Cloth("Honey duck",new Color(.98f,.75f,.25f));plushCream=Cloth("Cream stitching",new Color(.98f,.9f,.75f));plushOrange=Cloth("Soft orange beak",new Color(.98f,.43f,.19f));
            plushMaterials=new Material[9];for(int kind=0;kind<3;kind++)for(int part=0;part<3;part++)plushMaterials[kind*3+part]=Cloth(ClawFigureStyle.Name(kind)+" "+part,ClawFigureStyle.ColorFor(kind,part));
            plushMeshes=new[]{ClawPlushMesh.Create(0),ClawPlushMesh.Create(1),ClawPlushMesh.Create(2)};
            claw = new GameObject("Three finger claw assembly").transform; claw.SetParent(root,false);
            Part("Solenoid housing",PrimitiveType.Cylinder,new Vector3(0,.18f,0),new Vector3(.64f,.26f,.64f),chrome,claw);
            Part("Rubber cable socket",PrimitiveType.Cylinder,new Vector3(0,.54f,0),new Vector3(.2f,.12f,.2f),navy,claw);
            Part("Actuator collar",PrimitiveType.Cylinder,new Vector3(0,-.05f,0),new Vector3(.8f,.055f,.8f),chrome,claw);
            for(int i=0;i<3;i++){
                var radial=new GameObject("Finger hinge "+i).transform;radial.SetParent(claw,false);radial.localRotation=Quaternion.Euler(0,i*120,0);
                var hinge=new GameObject("Articulated steel finger").transform;hinge.SetParent(radial,false);hinge.localPosition=new Vector3(.28f,-.07f,0);fingers.Add(hinge);
                var pivot=Part("Hinge pin",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.15f,.1f,.15f),navy,hinge);pivot.localRotation=Quaternion.Euler(90,0,0);
                Rod("Upper finger",new Vector3(0,0,0),new Vector3(.42f,-.43f,0),.095f,chrome,hinge);
                Rod("Curved finger",new Vector3(.42f,-.43f,0),new Vector3(.32f,-.84f,0),.085f,chrome,hinge);
                Rod("Inward scoop tip",new Vector3(.32f,-.84f,0),new Vector3(-.12f,-1.04f,0),.08f,chrome,hinge);
                Part("Rubber grip pad",PrimitiveType.Sphere,new Vector3(-.12f,-1.04f,0),new Vector3(.13f,.09f,.15f),navy,hinge);
            }
            cable=Part("Suspension cable",PrimitiveType.Cylinder,Vector3.zero,Vector3.one,navy,root);
            trolley=new GameObject("Moving dual cross rails").transform;trolley.SetParent(root,false);
            foreach(float z in new[]{-.4f,.4f})Part("Cross beam",PrimitiveType.Cube,new Vector3(0,0,z),new Vector3(61,.2f,.17f),chrome,trolley);
            carriage=new GameObject("X axis motor carriage").transform;carriage.SetParent(root,false);
            Part("Motor casing",PrimitiveType.Cube,new Vector3(0,.05f,0),new Vector3(1.1f,.45f,1.2f),coral,carriage);
            Part("Cable spool",PrimitiveType.Cylinder,new Vector3(0,-.1f,0),new Vector3(.6f,.27f,.6f),chrome,carriage);
            foreach(float side in new[]{-1f,1f}){var wheel=Part("Carriage roller",PrimitiveType.Cylinder,new Vector3(side*.55f,0,0),new Vector3(.25f,.12f,.25f),navy,carriage);wheel.localRotation=Quaternion.Euler(0,0,90);}
            duck=new GameObject("Interactive sewn duck prize").transform;duck.SetParent(root,false);
            var duckSkin=new GameObject("Plush shell");duckSkin.transform.SetParent(duck,false);duckSkin.transform.localPosition=Vector3.down*.55f;
            heroMeshes=new[]{ClawPlushMesh.Create(2,20),ClawPlushMesh.Create(0,20),ClawPlushMesh.Create(1,20)};
            loadSkin=duckSkin.transform;duckSkin.AddComponent<MeshFilter>().sharedMesh=heroMeshes[0];loadRenderer=duckSkin.AddComponent<MeshRenderer>();loadRenderer.sharedMaterials=FigureMaterials(2);
            foreach(float x in new[]{-1.35f,1.35f})Part("Fabric carry loop",PrimitiveType.Capsule,new Vector3(x,0,0),new Vector3(.12f,.09f,.45f),mint,duck);
            Part("Hook target",PrimitiveType.Cylinder,new Vector3(0,.8f,0),new Vector3(.25f,.08f,.25f),mint,duck);
            goalMarker=Label("MISSION LOAD",Vector3.zero,.045f,mint.color,root);
            var cam = new GameObject("First person camera"); cam.transform.SetParent(root, false);
            view = cam.AddComponent<Camera>(); cam.AddComponent<AudioListener>(); cam.tag = "MainCamera";
            view.orthographic = false; view.fieldOfView = 78; view.nearClipPlane = 0.055f; view.farClipPlane = 160;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(0.1f, 0.14f, 0.2f); view.allowHDR = false;
            hands = new GameObject("First person sleeves and mittens").transform; hands.SetParent(view.transform,false);
            foreach(float side in new[]{-1f,1f})
            {
                var arm=Part("Sleeve",PrimitiveType.Capsule,new Vector3(side*.31f,-.32f,.48f),new Vector3(.15f,.23f,.15f),coral,hands);
                arm.localRotation=Quaternion.Euler(65,side*-10,0);
                Part("Mitten",PrimitiveType.Sphere,new Vector3(side*.27f,-.22f,.69f),new Vector3(.18f,.16f,.21f),navy,hands);
            }
            var sun = new GameObject("Key light").AddComponent<Light>(); sun.transform.SetParent(root, false);
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(52, -32, 0); sun.intensity = 1.05f; sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.57f, 0.61f, 0.68f); RenderSettings.fog = false;
            controlRoom=gameObject.AddComponent<ClawControlRoom>();controlRoom.Session=Session;BuildToolModels();
            BuildLayout();
        }
        void BuildToolModels()
        {
            scoopModel=new GameObject("Scoop basket").transform;scoopModel.SetParent(claw,false);
            foreach(var panel in ClawScoopShape.Panels)Part("Scoop panel",PrimitiveType.Cube,panel.center,panel.size,chrome,scoopModel);
            var rampObject=new GameObject("Scoop entry ramp");rampObject.transform.SetParent(scoopModel,false);var rampMesh=ClawScoopShape.Ramp();scoopRamp=rampMesh;rampObject.AddComponent<MeshFilter>().sharedMesh=rampMesh;rampObject.AddComponent<MeshRenderer>().sharedMaterial=chrome;
            foreach(float x in new[]{-1.5f,1.5f})foreach(float z in new[]{-1.1f,1.1f})Rod("Basket suspension strut",new Vector3(0,0,0),new Vector3(x,-.95f,z),.06f,chrome,scoopModel);
            magnetModel=new GameObject("Polarity magnet").transform;magnetModel.SetParent(claw,false);
            Part("Magnet core",PrimitiveType.Cylinder,new Vector3(0,-.4f,0),new Vector3(1.3f,.2f,1.3f),chrome,magnetModel);
            Part("North pole",PrimitiveType.Cube,new Vector3(-.35f,-.65f,0),new Vector3(.6f,.25f,.8f),coral,magnetModel);
            Part("South pole",PrimitiveType.Cube,new Vector3(.35f,-.65f,0),new Vector3(.6f,.25f,.8f),mint,magnetModel);
            hookModel=new GameObject("J hook").transform;hookModel.SetParent(claw,false);
            Rod("Hook shank",new Vector3(0,.65f,-.22f),ClawThreadPhysics.HookPoint(0),.11f,chrome,hookModel);
            for(int i=0;i<16;i++)Rod("Curved steel",ClawThreadPhysics.HookPoint(i),ClawThreadPhysics.HookPoint(i+1),.11f,chrome,hookModel);
            missionRing=new GameObject("Threading ring").transform;missionRing.SetParent(duck,false);Rod("Ring mount",new Vector3(0,-1.25f,0),new Vector3(0,-ClawThreadPhysics.RingRadius,0),ClawThreadPhysics.Wire*2,chrome,missionRing);
            for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;Rod("Ring",new Vector3(Mathf.Cos(a)*ClawThreadPhysics.RingRadius,Mathf.Sin(a)*ClawThreadPhysics.RingRadius,0),new Vector3(Mathf.Cos(b)*ClawThreadPhysics.RingRadius,Mathf.Sin(b)*ClawThreadPhysics.RingRadius,0),ClawThreadPhysics.Wire*2,chrome,missionRing);}
        }
        Material[] FigureMaterials(int kind){var result=new Material[3];System.Array.Copy(plushMaterials,kind*3,result,0,3);return result;}
        void BuildLayout()
        {
            var map = Session.State.Map; builtLayout=map; builtSeed = map.Seed; hasSeed = true;
            if (layoutRoot != null) Destroy(layoutRoot.gameObject);
            layoutRoot = new GameObject("Static layout / seed " + map.Seed).transform; layoutRoot.SetParent(root, false);
            foreach(var mesh in surfaceMeshes)Destroy(mesh);surfaceMeshes.Clear();
            foreach(var floor in map.Floors)Surface("Terrazzo floor",floor.center,floor.size,floorMat,true,4);
            foreach(var mesh in plushChunks)Destroy(mesh);plushChunks.Clear();
            BuildCabinet();
            int plushCount=ClawPlushMesh.Scatter(layoutRoot,map,plushMeshes,plushMaterials,plushChunks);
            Debug.Log("CLAWBLES_PLUSH_READY toys="+plushCount+" chunks="+plushChunks.Count);
            if(laserRoot!=null)Destroy(laserRoot.gameObject);
            laserRoot=new GameObject("Window laser carriages").transform;laserRoot.SetParent(root,false);
            laserVisuals.Clear();laserHeads.Clear();
            for(int i=0;i<map.Lasers.Count;i++){
                var beam=map.Lasers[i];var travel=map.LaserTravel(i);
                laserVisuals.Add(Part("Moving window beam",PrimitiveType.Cube,beam.center,beam.size,laserMat,laserRoot));
                foreach(float side in new[]{-1f,1f}){
                    bool acrossX=beam.size.x>beam.size.z;var edge=beam.center+(acrossX?Vector3.right*beam.extents.x:Vector3.forward*beam.extents.z)*side;
                    Box("Window laser track",new Vector3(edge.x,(travel.x+travel.y)/2,edge.z),acrossX?new Vector3(.12f,travel.y-travel.x,.38f):new Vector3(.38f,travel.y-travel.x,.12f),chrome);
                    laserHeads.Add(Part("Sliding emitter",PrimitiveType.Cube,edge,new Vector3(.25f,.28f,.35f),trimMat,laserRoot));
                }
            }
            Label("LOAD BAY / WINDOW EXIT",new Vector3(-16,8,9.6f),.065f,mint.color,layoutRoot);
            Label("SWING BAY / GANTRY STOPS AT X 30",new Vector3(30,9,15),.065f,gold.color,layoutRoot);
            Label("LASER = DUCK RESET",new Vector3(-16,7,9.6f),.065f,coral.color,layoutRoot);
            for (int i = 0; i < map.Obstacles.Count; i++)
            {
                var b = map.Obstacles[i];
                if(i<map.NoiseCount){
                    var collider=new GameObject("Static plush bundle collision");collider.transform.SetParent(layoutRoot,false);
                    collider.transform.position=b.center;collider.AddComponent<BoxCollider>().size=b.size;
                    Box("Plush display base",new Vector3(b.center.x,.1f,b.center.z),new Vector3(b.size.x,.2f,b.size.z),white);
                } else {
                    if(i>=map.Obstacles.Count-4)Box("Delivery rim",b.center,b.size,mint,true);
                    else{Surface("Printed obstacle panel",b.center,b.size,wallMat,true,4);FramePanel(b);}
                }
            }            foreach (float x in new[] { -30f, 30f }) Box("Crane rail", new Vector3(x, 14, 0), new Vector3(0.2f, 0.2f, 60), metal);
            var console = map.RoomCount>0?ClawLayout.PilotSeat+Vector3.forward*1.4f:ClawLayout.Console;
            Box("Console base", new Vector3(console.x, 0.55f, console.z), new Vector3(1.2f, 1.1f, 0.8f), white);
            Box("Console top", new Vector3(console.x, 1.15f, console.z), new Vector3(1, 0.15f, 0.8f), navy);
            Part("Joystick", PrimitiveType.Cylinder, new Vector3(console.x, 1.32f, console.z), new Vector3(0.07f, 0.18f, 0.07f), metal, layoutRoot);
            Part("Joystick ball", PrimitiveType.Sphere, new Vector3(console.x, 1.53f, console.z), Vector3.one * 0.22f, gold, layoutRoot);

            Part("Drop button bezel",PrimitiveType.Cylinder,new Vector3(console.x+.29f,1.26f,console.z+.13f),new Vector3(.27f,.045f,.27f),chrome,layoutRoot);
            Part("Drop button",PrimitiveType.Cylinder,new Vector3(console.x+.29f,1.31f,console.z+.13f),new Vector3(.2f,.03f,.2f),coral,layoutRoot);
            Box("Coin slot",new Vector3(console.x,.75f,console.z-.411f),new Vector3(.25f,.06f,.025f),navy);
            Label("1 CREDIT",new Vector3(console.x,.48f,console.z-.421f),.016f,coral.color,layoutRoot);
            Label("DELIVERY", ClawLayout.Delivery + new Vector3(0, 2, -3), 0.09f, mint.color, layoutRoot);
            Label("PILOT", new Vector3(-14, 2, -11.5f), 0.06f, mint.color, layoutRoot);
            Label("CLAWBLES", new Vector3(0, 11, 29.93f), 0.22f, cream.color, layoutRoot);
            Label("NORTH / PRIZE BAY", new Vector3(0, 9, 29.93f), 0.07f, mint.color, layoutRoot);
            // Lane markings remain static and use shared materials.
            for (int z = -12; z <= 12; z += 3) Box("Lane marking", new Vector3(0, 0.012f, z), new Vector3(0.1f, 0.015f, 1.2f), gold);
            var staticParts=new List<GameObject>();
            foreach(var filter in layoutRoot.GetComponentsInChildren<MeshFilter>()){
                var renderer=filter.GetComponent<MeshRenderer>();
                if(filter.sharedMesh!=null&&filter.sharedMesh.isReadable&&renderer!=null&&renderer.sharedMaterial!=null&&renderer.sharedMaterial.renderQueue<3000&&filter.GetComponent<TextMesh>()==null)
                    staticParts.Add(filter.gameObject);
            }
            StaticBatchingUtility.Combine(staticParts.ToArray(),layoutRoot.gameObject);
        }
        void LateUpdate()
        {
            if (claw == null) return;
            var s = Session.State;
            if(controlRoom!=null&&s.Map.RoomCount>0)controlRoom.EnsureMap(s.Map);
            if(scoopModel!=null){scoopModel.gameObject.SetActive(s.Tool==ClawTool.Scoop);magnetModel.gameObject.SetActive(s.Tool==ClawTool.Magnet);hookModel.gameObject.SetActive(s.Tool==ClawTool.Hook&&s.Map.RoomCount>0);missionRing.gameObject.SetActive(s.Tool==ClawTool.Hook&&s.Map.RoomCount>0);missionRing.localPosition=Vector3.up*s.MissionOffset;var oldTarget=duck.Find("Hook target");if(oldTarget!=null)oldTarget.gameObject.SetActive(!s.PhysicalHook);foreach(var finger in fingers)finger.gameObject.SetActive(s.Map.RoomCount==0||s.Tool==ClawTool.Claw);}
            goalMarker.position=s.Prize+Vector3.up*2;goalMarker.rotation=view.transform.rotation;goalMarker.gameObject.SetActive(Session.Connected && s.ResetTimer<=0);
            if(lastStage!=s.Shift.Stage){lastStage=s.Shift.Stage;loadSkin.GetComponent<MeshFilter>().sharedMesh=heroMeshes[lastStage==3?0:lastStage];var bounds=heroMeshes[lastStage==3?0:lastStage].bounds;float scale=s.CurrentExtents(0).y*2/bounds.size.y;loadSkin.localScale=Vector3.one*scale;loadSkin.localPosition=-bounds.center*scale;loadRenderer.sharedMaterials=FigureMaterials((lastStage==0||lastStage==3)?2:lastStage==1?0:1);goalMarker.GetComponent<TextMesh>().text=s.Shift.LoadName;coral.color=lastStage==0?new Color(.83f,.25f,.38f):lastStage==1?new Color(.25f,.42f,.75f):new Color(.53f,.28f,.65f);}
            if (!hasSeed || !ReferenceEquals(builtLayout,s.Map)) BuildLayout();
            for(int i=0;i<s.Map.Lasers.Count;i++){
                var beam=s.Map.Lasers[i];laserVisuals[i].position=beam.center;
                var axis=beam.size.x>beam.size.z?Vector3.right*beam.extents.x:Vector3.forward*beam.extents.z;
                laserHeads[i*2].position=beam.center-axis;laserHeads[i*2+1].position=beam.center+axis;
            }
            float t = 1 - Mathf.Exp(-20 * Time.deltaTime);
            claw.position = Vector3.Lerp(claw.position, s.Claw, t);
            duck.position = Vector3.Lerp(duck.position, s.Prize, t); duck.rotation = Quaternion.Slerp(duck.rotation, (s.PhysicalCargo?s.PrizeRotation:Quaternion.Euler(0, s.PrizeYaw, 0)), t);
            duck.localScale = Vector3.one * (s.ResetTimer > 0 ? Mathf.Max(0, s.ResetTimer / 2.5f) : 1);
            var suspension = new Vector3(s.Anchor.x, ClawLayout.Ceiling, s.Anchor.z);
            cable.position = (suspension + claw.position) * .5f;
            cable.up = (suspension - claw.position).normalized;
            cable.localScale = new Vector3(.055f, Mathf.Max(.025f,Vector3.Distance(suspension,claw.position)*.5f),.055f);
            carriage.position = suspension;
            foreach(var finger in fingers) finger.localRotation=Quaternion.Slerp(finger.localRotation,Quaternion.Euler(0,0,s.HasLoad?-28:12),t);
            trolley.position = new Vector3(0,ClawLayout.Ceiling,s.Anchor.z);
            claw.rotation = s.PhysicalHook?Quaternion.identity:s.RigRotation;
            if (Session.Connected && s.Players.TryGetValue(Session.LocalId, out var local))
            {
                hands.gameObject.SetActive(local.KnockTimer<=0);
                ClawGestures.Arms(local.Signal,local.Yaw,Time.time,out var leftGesture,out var rightGesture);
                for(int sideIndex=0;sideIndex<2;sideIndex++){
                    float side=sideIndex==0?-1:1;
                    var arm=hands.GetChild(sideIndex*2);var mitten=hands.GetChild(sideIndex*2+1);
                    var armTarget=new Vector3(side*.31f,-.32f,.48f);var mittenTarget=new Vector3(side*.27f,-.22f,.69f);
                    var armRotation=Quaternion.Euler(65,side*-10,0);
                    if(local.SignalTimer>0){
                        var direction=Quaternion.Euler(-Session.LookPitch,0,0)*(sideIndex==0?leftGesture:rightGesture);
                        var shoulder=new Vector3(side*.3f,-.35f,.35f);
                        armTarget=shoulder+direction*.18f;mittenTarget=shoulder+direction*.36f;
                        armRotation=Quaternion.FromToRotation(Vector3.up,-direction);
                    }
                    arm.localPosition=Vector3.Lerp(arm.localPosition,armTarget,t);arm.localRotation=Quaternion.Slerp(arm.localRotation,armRotation,t);
                    mitten.localPosition=Vector3.Lerp(mitten.localPosition,mittenTarget,t);
                }
                float bob=Mathf.Sin(Time.time*6)*.006f;
                hands.localPosition=new Vector3(0,bob,local.Attachment!=0?.08f:0);
                Vector3 target = local.Position + Vector3.up * (local.KnockTimer > 0 ? 0.28f : 0.93f);
                view.transform.position = cameraPlaced ? Vector3.Lerp(view.transform.position, target, t) : target; cameraPlaced = true;
                view.transform.rotation = Quaternion.Euler(Session.LookPitch, Session.LookYaw, local.KnockTimer > 0 ? 14 : 0);
            }
            else { hands.gameObject.SetActive(false); cameraPlaced = false; view.transform.position = new Vector3(-18, 5, -16); view.transform.LookAt(new Vector3(0,3,12)); }
            removed.Clear(); foreach (var id in toys.Keys) if (!s.Players.ContainsKey(id)) removed.Add(id);
            foreach (var id in removed) { Destroy(toys[id].gameObject); toys.Remove(id); }
            foreach (var p in s.Players.Values)
            {
                if (!toys.TryGetValue(p.Id, out var toy))
                {
                    toy = new GameObject("Crew " + (p.Slot + 1)).AddComponent<ClawToy>(); toy.transform.SetParent(root, false); toy.transform.position = p.Position;
                    toy.Build(paints[Mathf.Clamp(p.Slot, 0, 3)], navy, p.Slot); toys.Add(p.Id, toy);
                }
                toy.Present(p, p.Id == Session.LocalId, view, Session.Voice.Speaking(p.Id));
            }
        }
        Material Cloth(string name,Color color){var m=Mat(name,color);m.mainTexture=fabric;m.mainTextureScale=Vector2.one*6;m.SetFloat("_Smoothness",.02f);return m;}
        void BuildCabinet()
        {
            foreach(float x in new[]{-36f,36f}) {
                Box("Glass side",new Vector3(x,7,0),new Vector3(.12f,11,60),glass,true);
                foreach(float y in new[]{.7f,13.4f})Surface("Enamel side fascia",new Vector3(x,y,0),new Vector3(.6f,1.4f,61),wallMat,false,4);
                foreach(float z in new[]{-30f,0f,30f})Box("Chrome upright",new Vector3(x,7,z),new Vector3(.45f,14,.45f),chrome);
                Box("Side light strip",new Vector3(x-.15f*Mathf.Sign(x),12.55f,0),new Vector3(.08f,.14f,60),lightMat);
            }
            foreach(float z in new[]{-30f,30f}) {
                Box("Glass front or rear",new Vector3(0,7,z),new Vector3(72,11,.12f),glass,true);
                Surface("Printed lower fascia",new Vector3(0,.65f,z),new Vector3(72,1.3f,.6f),wallMat,false,4);
                Box("Marquee",new Vector3(0,13.5f,z),new Vector3(73,1.6f,.7f),coral);
                Box("Marquee lower light",new Vector3(0,12.65f,z),new Vector3(72,.14f,.16f),lightMat);
            }
            Label("CLAWBLES   /   PLUSH CLUB",new Vector3(0,13.5f,29.55f),.16f,Color.white,layoutRoot);
            Box("Prize hatch surround",ClawLayout.Delivery+new Vector3(0,-1.95f,0),new Vector3(5.1f,.3f,5.1f),navy);
            // Outside the glass: a quiet arcade backdrop, not an opaque warehouse wall.
            Box("Machine pedestal",new Vector3(0,-5.8f,0),new Vector3(73,5,61),coral);
            Box("Prize retrieval flap",new Vector3(29,-2.1f,-30.4f),new Vector3(5.3f,2.2f,.12f),navy);
            Box("Arcade floor",new Vector3(0,-8.4f,0),new Vector3(110,.3f,95),navy);
        }
        Transform Surface(string name,Vector3 p,Vector3 size,Material material,bool solid,float tile)
        {
            var box=Box(name,p,size,material,solid);var filter=box.GetComponent<MeshFilter>();
            var mesh=ClawSurfaceArt.WorldUV(filter.sharedMesh,p,size,tile);filter.sharedMesh=mesh;surfaceMeshes.Add(mesh);return box;
        }
        void FramePanel(Bounds b)
        {
            bool alongX=b.size.x>=b.size.z;
            Vector3 span=alongX?Vector3.right:Vector3.forward;
            Vector3 normal=alongX?Vector3.forward:Vector3.right;
            float width=alongX?b.size.x:b.size.z,depth=alongX?b.size.z:b.size.x;
            Vector3 Size(float w,float h,float d)=>alongX?new Vector3(w,h,d):new Vector3(d,h,w);
            // Shared material trims stay inside the collision silhouette; no new blockers.
            foreach(float face in new[]{-1f,1f}){
                var front=b.center+normal*face*(depth*.5f+.015f);
                foreach(float y in new[]{b.min.y+.07f,b.max.y-.07f})
                    Box("Panel edge molding",new Vector3(front.x,y,front.z),Size(width,.14f,.045f),trimMat);
                if(width>1&&b.size.y>1){
                    foreach(float side in new[]{-1f,1f})
                        Box("Panel side molding",front+span*side*(width*.5f-.07f),Size(.14f,b.size.y,.045f),trimMat);
                    for(float d=4;d<width-.4f;d+=4)
                        Box("Panel joint",front+span*(d-width*.5f),Size(.035f,b.size.y-.2f,.025f),trimMat);
                    if(b.min.y<.1f)
                        Box("Warm kick rail",new Vector3(front.x,.25f,front.z),Size(width-.2f,.09f,.06f),gold);
                }
            }
        }
        static void Rod(string name,Vector3 a,Vector3 b,float width,Material material,Transform parent)
        {
            var rod=Part(name,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b)*.5f,width),material,parent);
            rod.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        Material Mat(string name, Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            m.SetFloat("_Smoothness", 0.16f); materials.Add(m); return m;
        }
        Transform Box(string name, Vector3 p, Vector3 size, Material material, bool solid = false) => Part(name, PrimitiveType.Cube, p, size, material, layoutRoot, solid);
        static Transform Part(string name, PrimitiveType shape, Vector3 p, Vector3 scale, Material material, Transform parent, bool solid = false)
        {
            var g = GameObject.CreatePrimitive(shape); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = scale;
            if (!solid) { var c = g.GetComponent<Collider>(); c.enabled = false; Destroy(c); }
            var renderer = g.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return g.transform;
        }
        static Transform Label(string value, Vector3 p, float size, Color color, Transform parent)
        {
            var go = new GameObject(value); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            var text = go.AddComponent<TextMesh>(); text.text = value; text.fontSize = 48; text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.color = color;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material; return go.transform;
        }
        public void Capture(string path, bool overview = false, bool inspectImpact = false)
        {
            var target = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32); var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            bool hadHands = hands.gameObject.activeSelf;
            var previous = RenderTexture.active; var position = view.transform.position; var rotation = view.transform.rotation;
            try
            {
                if (overview) { view.transform.position = new Vector3(0, 66, -51); view.transform.LookAt(new Vector3(0, 0, 0)); }
                if (inspectImpact) foreach (var p in Session.State.Players.Values) if (p.KnockTimer > 0)
                { view.transform.position = p.Position + new Vector3(-3, 2.5f, -3); view.transform.LookAt(p.Position); break; }
                bool threadPreview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-clawThreadPreview")>=0;
                if(threadPreview){view.transform.position=Session.State.Prize+new Vector3(3,2,-3);view.transform.LookAt(Session.State.Prize+Vector3.up*1.1f);hands.gameObject.SetActive(false);}
                if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-clawToolPreview")>=0){view.transform.position=Session.State.Claw+new Vector3(4,1,-4);view.transform.LookAt(Session.State.Claw-Vector3.up*.7f);hands.gameObject.SetActive(false);}
                bool laserPreview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-clawLaserPreview")>=0;
                if(laserPreview){view.transform.position=new Vector3(16,4,-1);view.transform.LookAt(new Vector3(23,4.3f,7.3f));}
                bool environmentPreview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-clawEnvironmentPreview")>=0;
                if(environmentPreview){view.transform.position=new Vector3(-5,4.5f,-2);view.transform.LookAt(new Vector3(-16,3.2f,12));}
                bool figurePreview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-clawFigurePreview")>=0;
                if(figurePreview){var figure=Session.State.Map.Toys[0];view.transform.position=figure.Bounds.center+new Vector3(0,3,-1);view.transform.LookAt(figure.Bounds.center); }
                if (overview || inspectImpact || figurePreview || environmentPreview || laserPreview) hands.gameObject.SetActive(false);
                if(Session.State.Operator==Session.LocalId && Session.State.Players.TryGetValue(Session.LocalId,out var pilot)) Debug.Log("CLAWBLES_PILOT_CAMERA eye="+view.transform.position+" body="+pilot.Position);
                foreach(var field in GetComponentsInChildren<ClawToyField>())field.Render();
                target.Create(); RenderPipeline.SubmitRenderRequest(view, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG()); Debug.Log("CLAWBLES_CAMERA_CAPTURE_OK " + path);
            }
            finally { hands.gameObject.SetActive(hadHands); view.transform.SetPositionAndRotation(position, rotation); RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels); }
        }
        void OnDestroy() {if(scoopRamp!=null)Destroy(scoopRamp); foreach(var mesh in surfaceMeshes)if(mesh!=null)Destroy(mesh);if(wallTexture!=null)Destroy(wallTexture); if(heroMeshes!=null)foreach(var mesh in heroMeshes)if(mesh!=null)Destroy(mesh); foreach(var mesh in plushChunks) if(mesh!=null)Destroy(mesh); if(plushMeshes!=null)foreach(var mesh in plushMeshes)if(mesh!=null)Destroy(mesh);if(fabric!=null)Destroy(fabric); foreach (var m in materials) if (m != null) Destroy(m); if (floorTexture != null) Destroy(floorTexture); }
    }
}






















