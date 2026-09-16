using System.Collections.Generic;
using UnityEngine;

namespace Clawbles
{
    // Six bodies are enabled only while knocked down. The host owns the root and recovery timer;
    // secondary limb motion is cosmetic, avoiding a stream of joint transforms over the network.
    public sealed class ClawToy : MonoBehaviour
    {
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<Collider> colliders = new List<Collider>();
        readonly List<Vector3> rest = new List<Vector3>();
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Renderer> details = new List<Renderer>();
        bool ragdoll;
        Vector3 previousPosition;
        float stride, gestureBlend;
        Transform label;
        public void Build(Material color, Material dark, int slot)
        {
            AddBody("Torso", new Vector3(0, 0, 0), new Vector3(0.62f, 0.7f, 0.45f), color, PrimitiveType.Sphere);
            AddBody("Head", new Vector3(0, 0.58f, 0), new Vector3(.64f,.60f,.56f), color, PrimitiveType.Sphere);
            AddBody("Left arm", new Vector3(-0.42f, -0.03f, 0), new Vector3(0.19f, 0.55f, 0.22f), color, PrimitiveType.Capsule);
            AddBody("Right arm", new Vector3(0.42f, -0.03f, 0), new Vector3(0.19f, 0.55f, 0.22f), color, PrimitiveType.Capsule);
            AddBody("Left leg", new Vector3(-0.17f, -0.37f, 0), new Vector3(0.23f, 0.55f, 0.25f), dark, PrimitiveType.Capsule);
            AddBody("Right leg", new Vector3(0.17f, -0.37f, 0), new Vector3(0.23f, 0.55f, 0.25f), dark, PrimitiveType.Capsule);
            foreach(float side in new[]{-1f,1f})
            {
                Detail("Big eye",bodies[1].transform,new Vector3(side*.21f,.05f,.45f),new Vector3(.25f,.32f,.13f),dark);
                Detail("Eye sparkle",bodies[1].transform,new Vector3(side*.21f+.04f,.12f,.52f),Vector3.one*.07f,color);
                int arm=side<0?2:3;
                Detail("Mitten",bodies[arm].transform,new Vector3(0,-.85f,0),new Vector3(1.35f,.6f,1.35f),dark);
            }
            Detail("Mouth",bodies[1].transform,new Vector3(0,-.27f,.47f),new Vector3(.3f,.065f,.08f),dark);
            Detail("Crew cap",bodies[1].transform,new Vector3(0,.45f,0),new Vector3(1.05f,.25f,1.05f),dark);
            for (int i = 1; i < bodies.Count; i++)
            {
                var joint = bodies[i].gameObject.AddComponent<CharacterJoint>(); joint.connectedBody = bodies[0];
                joint.axis = Vector3.right; joint.anchor = new Vector3(0, i == 1 ? -0.4f : 0.4f, 0);
                joint.lowTwistLimit = new SoftJointLimit { limit = -35 }; joint.highTwistLimit = new SoftJointLimit { limit = 35 };
                joint.swing1Limit = new SoftJointLimit { limit = 65 }; joint.swing2Limit = new SoftJointLimit { limit = 45 };
                joint.enableProjection = true; joint.projectionDistance = 0.1f;
            }
            for (int i = 0; i < colliders.Count; i++) for (int j = i + 1; j < colliders.Count; j++) Physics.IgnoreCollision(colliders[i], colliders[j]);
            var text = new GameObject("Crew label").AddComponent<TextMesh>(); label = text.transform; label.SetParent(transform, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 40; text.characterSize = 0.035f; text.anchor = TextAnchor.MiddleCenter; text.text = "P" + (slot + 1);
        }
        void Detail(string title,Transform parent,Vector3 p,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=title;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;
            var c=g.GetComponent<Collider>();c.enabled=false;
            var r=g.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;details.Add(r);
        }
        void AddBody(string name, Vector3 position, Vector3 size, Material material, PrimitiveType shape)
        {
            if (shape == PrimitiveType.Capsule) size.y *= 0.5f; // Unity's primitive capsule starts two units tall.
            var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(transform, false); go.transform.localPosition = position; go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderers.Add(renderer);
            var collider = go.GetComponent<Collider>(); collider.enabled = false; colliders.Add(collider);
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.mass = name == "Torso" ? 2 : 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.maxAngularVelocity = 12; body.solverIterations = 8;
            bodies.Add(body); rest.Add(position);
        }
        public void Present(ClawSimulation.Player p, bool own, Camera camera, bool speaking)
        {
            bool knocked = p.KnockTimer > 0;
            float movement = Vector3.Distance(p.Position, previousPosition); previousPosition = p.Position;
            stride += Mathf.Min(movement, .15f) * 10;
            if (knocked && !ragdoll)
            {
                transform.position = p.Position; transform.rotation = Quaternion.Euler(0, p.Yaw, 0);
                for (int i = 0; i < bodies.Count; i++)
                {
                    colliders[i].enabled = true; bodies[i].isKinematic = i == 0;
                    if (i > 0) { bodies[i].linearVelocity = p.KnockVelocity + Vector3.up; bodies[i].angularVelocity = new Vector3(2, 1, 3) * (i % 2 == 0 ? 1 : -1); }
                }
            }
            if (!knocked)
            {
                for (int i = 0; i < bodies.Count; i++)
                {
                    if (!bodies[i].isKinematic) { bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero; bodies[i].isKinematic = true; }
                    colliders[i].enabled = false; bodies[i].transform.localPosition = rest[i]; bodies[i].transform.localRotation = Quaternion.identity;
                }
                float step = movement > .002f ? Mathf.Sin(stride) * 24 : Mathf.Sin(Time.time*2) * 2;
                for(int i=2;i<6;i++) bodies[i].transform.localRotation=Quaternion.Euler(i<4 && p.Attachment!=0 ? -85 : step*(i%2==0?1:-1),0,0);
                gestureBlend=Mathf.MoveTowards(gestureBlend,p.SignalTimer>0?Mathf.Clamp01(p.SignalTimer/.2f):0,Time.deltaTime*8);
                if(gestureBlend>0){
                    ClawGestures.Arms(p.Signal,p.Yaw,Time.time,out var left,out var right);
                    for(int i=2;i<=3;i++){
                        var direction=i==2?left:right;var arm=bodies[i].transform;
                        arm.localPosition=Vector3.Lerp(rest[i],rest[i]+Vector3.up*.23f+direction*.23f,gestureBlend);
                        arm.localRotation=Quaternion.Slerp(arm.localRotation,Quaternion.FromToRotation(Vector3.down,direction),gestureBlend);
                    }
                }
                transform.position = Vector3.Lerp(transform.position, p.Position, 1 - Mathf.Exp(-20 * Time.deltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, p.Yaw, 0), 1 - Mathf.Exp(-16 * Time.deltaTime));
            }
            else { bodies[0].position = p.Position - Vector3.up * 0.25f; bodies[0].rotation = Quaternion.Euler(65, p.Yaw, 18); }
            ragdoll = knocked;
            for (int i = 0; i < renderers.Count; i++) renderers[i].enabled = !own || (knocked && i != 1);
            foreach(var detail in details) detail.enabled = !own;
            label.gameObject.SetActive(!own); label.position = p.Position + Vector3.up * 1.2f;
            label.rotation = camera.transform.rotation;
            label.GetComponent<TextMesh>().text="P"+(p.Slot+1)+(p.SignalTimer>0?"  "+ClawGestures.Label(p.Signal):"");
            label.GetComponent<TextMesh>().color = speaking ? new Color(0.25f, 1, 0.7f) : Color.white;
        }
    }
}



