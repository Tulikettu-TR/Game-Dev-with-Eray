using UnityEditor;
using UnityEngine;
using System.IO;
namespace Clawbles.Editor
{
    public static class ClawArt
    {
        [MenuItem("Clawbles/Generate Crew Art")]
        public static void Generate()
        {
            const string folder="Assets/Clawbles/Art";
            Directory.CreateDirectory(folder);
            var colors=new[]{new Color(.83f,.25f,.28f),new Color(.2f,.68f,.56f),new Color(.48f,.36f,.69f),new Color(.97f,.64f,.1f)};
            var dark=Material(folder+"/Ink.mat",new Color(.075f,.105f,.16f));
            var idle=Clip(folder+"/Idle.anim",false);
            var walk=Clip(folder+"/Walk.anim",true);
            for(int i=0;i<4;i++)
            {
                var paint=Material(folder+"/Crew"+(i+1)+".mat",colors[i]);
                var root=new GameObject("Crew"+(i+1));
                var toy=root.AddComponent<ClawToy>();toy.Build(paint,dark,i);
                Object.DestroyImmediate(toy);
                var animation=root.AddComponent<Animation>();animation.AddClip(idle,"Idle");animation.AddClip(walk,"Walk");animation.clip=idle;
                PrefabUtility.SaveAsPrefabAsset(root,folder+"/Crew"+(i+1)+".prefab");
                Object.DestroyImmediate(root);
            }
            GeneratePlush(folder);
            AssetDatabase.SaveAssets();
            Debug.Log("CLAWBLES_ART_READY");
        }
        static void GeneratePlush(string folder)
        {
            string[] names={"Teddy","Bunny","Duck"};
            var colors=new[]{new Color(.89f,.47f,.59f),new Color(.38f,.73f,.67f),new Color(.98f,.75f,.25f)};
            for(int i=0;i<3;i++){
                string meshPath=folder+"/Plush"+names[i]+".asset";
                var generated=ClawPlushMesh.Create(i,20);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null){mesh=generated;AssetDatabase.CreateAsset(mesh,meshPath);}else{EditorUtility.CopySerialized(generated,mesh);Object.DestroyImmediate(generated);}
                var body=Material(folder+"/Plush"+names[i]+".mat",colors[i]);
                var trim=Material(folder+(i==2?"/PlushBeak.mat":"/PlushCream.mat"),new Color(.98f,.9f,.75f));
                var root=new GameObject("Plush "+names[i]);root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var palette=new Material[3];for(int part=0;part<3;part++)palette[part]=Material(folder+"/"+names[i]+part+".mat",ClawFigureStyle.ColorFor(i,part));root.AddComponent<MeshRenderer>().sharedMaterials=palette;
                PrefabUtility.SaveAsPrefabAsset(root,folder+"/Plush"+names[i]+".prefab");Object.DestroyImmediate(root);
            }
        }
        static Material Material(string path,Color color)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(m);return m;
        }
        static AnimationClip Clip(string path,bool moving)
        {
            var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(c==null){c=new AnimationClip();AssetDatabase.CreateAsset(c,path);}
            c.legacy=true;c.wrapMode=WrapMode.Loop;
            string[] parts={"Left arm","Right arm","Left leg","Right leg"};
            for(int i=0;i<4;i++)
            {
                float amount=(moving?24:2)*(i%2==0?1:-1);
                c.SetCurve(parts[i],typeof(Transform),"localEulerAnglesRaw.x",new AnimationCurve(new Keyframe(0,0),new Keyframe(.25f,amount),new Keyframe(.5f,0),new Keyframe(.75f,-amount),new Keyframe(1,0)));
            }
            EditorUtility.SetDirty(c);return c;
        }
    }
}





