using UnityEngine;
namespace Clawbles {
 public static class ClawScoopShape {
 public static readonly Bounds[] Panels={
 new Bounds(new Vector3(0,-1.56f,0),new Vector3(3.4f,.16f,2.7f)),
 new Bounds(new Vector3(-1.65f,-1.205f,0),new Vector3(.1f,.55f,2.7f)),
 new Bounds(new Vector3(1.65f,-1.205f,0),new Vector3(.1f,.55f,2.7f)),
 new Bounds(new Vector3(0,-1.205f,1.3f),new Vector3(3.4f,.55f,.1f))};
 public static Mesh Ramp(){var m=new Mesh{name="Scoop entry wedge"};m.vertices=new[]{new Vector3(-1.7f,-1.70f,-2.1f),new Vector3(1.7f,-1.70f,-2.1f),new Vector3(-1.7f,-1.64f,-1.35f),new Vector3(1.7f,-1.64f,-1.35f),new Vector3(-1.7f,-1.69f,-2.1f),new Vector3(1.7f,-1.69f,-2.1f),new Vector3(-1.7f,-1.48f,-1.35f),new Vector3(1.7f,-1.48f,-1.35f)};m.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};m.RecalculateNormals();m.RecalculateBounds();return m;}
 }
}
