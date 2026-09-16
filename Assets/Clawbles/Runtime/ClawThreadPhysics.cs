using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Clawbles {
 // Host-only isolated PhysX world. There is deliberately no grab joint or proximity latch.
 public sealed class ClawThreadPhysics : IDisposable {
 readonly Scene scene; readonly PhysicsScene physics; readonly Rigidbody hook,load;
 readonly List<Rigidbody> looseBodies=new List<Rigidbody>();readonly bool isScoop;
 readonly List<Transform> toys=new List<Transform>(); readonly ClawLayout map;
 Mesh ramp; PhysicsMaterial scoopMaterial;
 Vector3 lastPosition; Quaternion lastRotation; bool initialized;
 public static Vector3 HookPoint(int i) {float a=Mathf.Lerp(Mathf.PI,Mathf.PI*2.15f,i/16f);return new Vector3(0,Mathf.Sin(a)*.22f,Mathf.Cos(a)*.22f);}
 public const float RingRadius=.26f, Wire=.035f;
 public ClawThreadPhysics(ClawLayout layout,Vector3 ext,float ringHeight,bool scoop=false){
 isScoop=scoop;map=layout;scene=SceneManager.CreateScene("Host hook contacts "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();
 foreach(var b in map.Obstacles)Box(b);foreach(var b in map.Floors)Box(b);
 foreach(var toy in map.Toys){var go=Box(toy.Bounds);toys.Add(go.transform);if(scoop){var rb=go.AddComponent<Rigidbody>();rb.mass=.7f;rb.isKinematic=true;rb.useGravity=false;rb.constraints=RigidbodyConstraints.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;looseBodies.Add(rb);}}
 var h=Object("Steel hook");hook=h.AddComponent<Rigidbody>();hook.isKinematic=true;hook.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
 if(scoop){
 scoopMaterial=new PhysicsMaterial("Sliding steel"){dynamicFriction=.12f,staticFriction=.16f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum};
 foreach(var panel in ClawScoopShape.Panels){var go=new GameObject("Scoop panel");go.transform.SetParent(h.transform,false);go.transform.localPosition=panel.center;var c=go.AddComponent<BoxCollider>();c.size=panel.size;c.contactOffset=.002f;c.sharedMaterial=scoopMaterial;}
 var lip=new GameObject("Scoop ramp");lip.transform.SetParent(h.transform,false);ramp=ClawScoopShape.Ramp();var wedge=lip.AddComponent<MeshCollider>();wedge.sharedMesh=ramp;wedge.convex=true;wedge.contactOffset=.001f;wedge.sharedMaterial=scoopMaterial;
 }else{
 Capsule(h.transform,new Vector3(0,.65f,-.22f),HookPoint(0),.055f);
 for(int i=0;i<16;i++)Capsule(h.transform,HookPoint(i),HookPoint(i+1),.055f);
 }
 var l=Object("Ring and mission load");load=l.AddComponent<Rigidbody>();load.mass=2;load.useGravity=false;load.linearDamping=.18f;load.angularDamping=.7f;load.solverIterations=20;load.solverVelocityIterations=12;load.maxAngularVelocity=8;load.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
 var body=l.AddComponent<BoxCollider>();body.size=ext*2;body.contactOffset=.001f;
 if(!scoop){Capsule(l.transform,new Vector3(0,ext.y,0),new Vector3(0,ringHeight-RingRadius,0),Wire);
 // XY ring plane is perpendicular to the YZ hook curve.
 for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;Capsule(l.transform,new Vector3(Mathf.Cos(a)*RingRadius,ringHeight+Mathf.Sin(a)*RingRadius,0),new Vector3(Mathf.Cos(b)*RingRadius,ringHeight+Mathf.Sin(b)*RingRadius,0),Wire);}
 }
 }
 GameObject Object(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
 GameObject Box(Bounds b){var go=Object("Course collider");go.transform.position=b.center;go.AddComponent<BoxCollider>().size=b.size;return go;}
 static void Capsule(Transform parent,Vector3 a,Vector3 b,float radius){var go=new GameObject("Contact segment");go.transform.SetParent(parent,false);go.transform.localPosition=(a+b)*.5f;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);var c=go.AddComponent<CapsuleCollider>();c.radius=radius;c.height=Vector3.Distance(a,b)+2*radius;c.contactOffset=.002f;}
 public void Step(Vector3 start,Vector3 end,ref Vector3 position,ref Vector3 velocity,ref Quaternion rotation,float dt,Quaternion? toolRotation=null){
 if(!initialized||(position-lastPosition).sqrMagnitude>.000001f||Quaternion.Angle(rotation,lastRotation)>.1f){load.position=position;load.rotation=rotation;load.linearVelocity=velocity;if(!initialized)load.angularVelocity=Vector3.zero;}
 initialized=true;hook.position=start;var fromRotation=hook.rotation;var toRotation=toolRotation??Quaternion.identity;
 int awake=0;foreach(var toy in map.Toys)if(toy.PhysicsManaged)awake++;
 for(int i=0;i<toys.Count;i++){
 var toy=map.Toys[i];
 if(isScoop){var rb=looseBodies[i];if(!toy.PhysicsManaged&&awake<12&&Vector3.Distance(toy.Bounds.center,end)<4.5f){toy.PhysicsManaged=true;rb.isKinematic=false;rb.linearVelocity=toy.Velocity;awake++;}
 if(toy.PhysicsManaged){if((rb.position-toy.Bounds.center).sqrMagnitude>.000001f){rb.position=toy.Bounds.center;rb.linearVelocity=toy.Velocity;}continue;}}
 toys[i].position=toy.Bounds.center;
 }
 Physics.SyncTransforms();
 int steps=Mathf.Max(8,Mathf.CeilToInt(Vector3.Distance(start,end)/.015f));steps=Mathf.Min(steps,64);
 for(int i=0;i<steps;i++){hook.MovePosition(Vector3.Lerp(start,end,(i+1f)/steps));hook.MoveRotation(Quaternion.Slerp(fromRotation,toRotation,(i+1f)/steps));load.AddForce(Vector3.down*12,ForceMode.Acceleration);if(isScoop)foreach(var rb in looseBodies)if(!rb.isKinematic)rb.AddForce(Vector3.down*12,ForceMode.Acceleration);physics.Simulate(dt/steps);}
 if(isScoop)for(int i=0;i<looseBodies.Count;i++){var toy=map.Toys[i];if(!toy.PhysicsManaged)continue;var rb=looseBodies[i];var old=toy.Bounds.center;toy.SetCenter(rb.position);toy.Velocity=rb.linearVelocity;toy.Moving=true;
 if(map.LaserHit(old,rb.position,toy.Bounds.extents)||(map.InsideHole(rb.position,toy.Bounds.extents)&&rb.position.y<-.5f)){toy.Reset();rb.position=toy.Bounds.center;rb.linearVelocity=Vector3.zero;}
 if(Vector3.Distance(rb.position,end)>8&&rb.linearVelocity.sqrMagnitude<.01f){toy.PhysicsManaged=false;rb.isKinematic=true;}}
 position=load.position;velocity=load.linearVelocity;rotation=load.rotation;lastPosition=position;lastRotation=rotation;
 }
 public void Dispose(){foreach(var toy in map.Toys)toy.PhysicsManaged=false;if(ramp!=null)UnityEngine.Object.Destroy(ramp);if(scoopMaterial!=null)UnityEngine.Object.Destroy(scoopMaterial);if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);}
 }
}
