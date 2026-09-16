using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Clawbles
{
    public sealed class ClawControlRoom:MonoBehaviour
    {
        public ClawSession Session;
        ClawLayout map;GameObject roomRoot;RenderTexture feed;Material screenMaterial,wall,ink;
        readonly List<Camera> cameras=new List<Camera>();
        readonly List<TextMesh> poles=new List<TextMesh>();
        TextMesh caption,magnetLabel,targetLabel;int room,angle;float nextFrame;
        public int CameraCount=>cameras.Count;
        public void EnsureMap(ClawLayout layout)
        {
            if(ReferenceEquals(map,layout))return;map=layout;
            if(roomRoot!=null)Destroy(roomRoot);
            foreach(var cam in cameras)if(cam!=null)Destroy(cam.gameObject);cameras.Clear();poles.Clear();room=0;
            if(feed==null){feed=new RenderTexture(640,360,16);feed.name="Live CCTV";feed.Create();}
            if(wall==null){wall=new Material(Resources.Load<Material>("RuntimeLit"));wall.color=new Color(.12f,.19f,.23f);ink=new Material(wall);ink.color=new Color(.025f,.03f,.035f);}
            if(screenMaterial==null){screenMaterial=new Material(Resources.Load<Material>("CCTVScreen"));screenMaterial.SetTexture("_BaseMap",feed);}
            roomRoot=new GameObject("Isolated pilot booth and CCTV fixtures");roomRoot.transform.SetParent(transform,false);
            var seat=ClawLayout.PilotSeat;
            Box("Booth floor",seat+new Vector3(0,-.8f,0),new Vector3(6,.3f,6),wall);
            foreach(float sign in new[]{-1f,1f}){
                Box("Opaque booth wall",seat+new Vector3(sign*3,1,0),new Vector3(.3f,4,6),wall);
                Box("Opaque booth wall",seat+new Vector3(0,1,sign*3),new Vector3(6,4,.3f),wall);
            }
            Box("Booth ceiling",seat+Vector3.up*3,new Vector3(6,.25f,6),wall);
            Box("Computer desk",seat+new Vector3(0,.2f,1.55f),new Vector3(2.5f,.16f,1.1f),wall);
            Box("Monitor bezel",seat+new Vector3(0,1.03f,1.55f),new Vector3(2.08f,1.22f,.16f),ink);
            var screen=GameObject.CreatePrimitive(PrimitiveType.Quad);screen.name="Physical CCTV screen";screen.transform.SetParent(roomRoot.transform);
            screen.transform.position=seat+new Vector3(0,1.03f,1.455f);screen.transform.localScale=new Vector3(1.94f,1.09f,1);
            Destroy(screen.GetComponent<Collider>());screen.GetComponent<Renderer>().sharedMaterial=screenMaterial;
            caption=Text("CCTV",seat+new Vector3(0,.31f,1.40f),.025f);
            magnetLabel=Text("MAGNET POLE / E",Vector3.zero,.02f);targetLabel=Text("",Vector3.zero,.025f);
            for(int i=0;i<map.Rooms.Count;i++){
                var bounds=map.Rooms[i];
                for(int j=0;j<2;j++){
                    var p=new Vector3(j==0?bounds.min.x+1:bounds.max.x-1,7.7f,j==0?bounds.max.z-1:bounds.min.z+1);
                    var go=new GameObject("Room "+(i+1)+" CCTV "+(j+1));go.transform.SetParent(roomRoot.transform);go.transform.position=p;
                    go.transform.LookAt(new Vector3(bounds.center.x,1.5f,bounds.center.z));
                    var cam=go.AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=85;cam.nearClipPlane=.08f;cam.farClipPlane=90;cam.allowHDR=false;cam.backgroundColor=new Color(.08f,.1f,.14f);cam.clearFlags=CameraClearFlags.SolidColor;cameras.Add(cam);
                    Box("Security camera housing",p,new Vector3(.45f,.25f,.6f),wall);
                }
                var label=Text("ROOM "+(i+1),new Vector3(bounds.center.x,5.7f,bounds.max.z-.25f),.06f);
            }
            foreach(var toy in map.Toys)poles.Add(Text("",toy.Target,.018f));
        }
        void LateUpdate()
        {
            if(map==null||Session==null)return;
            bool magnetic=Session.State.Tool==ClawTool.Magnet; magnetLabel.gameObject.SetActive(magnetic);targetLabel.gameObject.SetActive(magnetic);magnetLabel.text="MAGNET "+(Session.State.MagnetPole>0?"N":"S")+" / E";targetLabel.text="TARGET "+(Session.State.PrizePole>0?"N":"S")+" / E";targetLabel.transform.position=Session.State.Prize+Vector3.up*(Session.State.MissionOffset+.3f);
            magnetLabel.transform.position=Session.State.Claw+Vector3.up*.8f;
            var labelCamera=Session.State.Operator==Session.LocalId?cameras[room*2+angle]:Camera.main; if(labelCamera!=null){targetLabel.transform.rotation=labelCamera.transform.rotation;magnetLabel.transform.rotation=labelCamera.transform.rotation;}
            for(int i=0;i<poles.Count;i++){
                poles[i].gameObject.SetActive(Session.State.Tool==ClawTool.Magnet);
                poles[i].transform.position=map.Toys[i].Target;
                poles[i].text=(map.Toys[i].Pole>0?"N":"S")+" / E";poles[i].color=map.Toys[i].Pole>0?Color.red:Color.cyan;
                if(labelCamera!=null)poles[i].transform.rotation=labelCamera.transform.rotation;
            }
            if(!Session.Connected||Session.State.Operator!=Session.LocalId)return;
            var kb=Keyboard.current;
            if(Session.InputActive&&kb!=null){
                if(kb.zKey.wasPressedThisFrame)room=(room+map.RoomCount-1)%map.RoomCount;
                if(kb.xKey.wasPressedThisFrame)room=(room+1)%map.RoomCount;
                if(kb.cKey.wasPressedThisFrame)angle=1-angle;
            }
            caption.text="ROOM "+(room+1)+" / "+map.RoomCount+"   CAM "+(angle+1)+"\nZ / X : ROOM    C : CAMERA";
            if(Time.unscaledTime<nextFrame)return;nextFrame=Time.unscaledTime+.0833f;
            foreach(var field in GetComponentsInChildren<ClawToyField>())field.Render();
            RenderPipeline.SubmitRenderRequest(cameras[room*2+angle],new UniversalRenderPipeline.SingleCameraRequest{destination=feed});
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(roomRoot.transform);go.transform.position=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());return go;
        }
        TextMesh Text(string value,Vector3 p,float size)
        {
            var text=new GameObject("CCTV label").AddComponent<TextMesh>();text.transform.SetParent(roomRoot.transform);text.transform.position=p;
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.GetComponent<Renderer>().sharedMaterial=text.font.material;text.fontSize=48;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.text=value;return text;
        }
        void OnDestroy(){if(feed!=null){feed.Release();Destroy(feed);}if(screenMaterial!=null)Destroy(screenMaterial);if(wall!=null)Destroy(wall);if(ink!=null)Destroy(ink);}
    }
}
