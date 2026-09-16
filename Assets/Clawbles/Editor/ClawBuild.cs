using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Clawbles.Editor
{
    public static class ClawBuild
    {
        public const string ScenePath = "Assets/Clawbles/Scenes/Clawbles.unity";
        [MenuItem("Clawbles/Create Prototype Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("CLAWBLES / Prototype").AddComponent<ClawSession>();
            Directory.CreateDirectory("Assets/Clawbles/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "CLAWBLES";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            // Keep runtime-created primitive meshes and the Lit shader available in builds.
            Directory.CreateDirectory("Assets/Clawbles/Resources");
            string matPath = "Assets/Clawbles/Resources/RuntimeLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")), matPath);
            var runtimeLit=AssetDatabase.LoadAssetAtPath<Material>(matPath);runtimeLit.enableInstancing=true;EditorUtility.SetDirty(runtimeLit);
            const string cctvPath="Assets/Clawbles/Resources/CCTVScreen.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(cctvPath)==null){var screen=new Material(Shader.Find("Universal Render Pipeline/Unlit"));screen.SetFloat("_Cull",0);AssetDatabase.CreateAsset(screen,cctvPath);}
            const string glassPath="Assets/Clawbles/Resources/CabinetGlass.mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
            if(glass==null){glass=new Material(Shader.Find("Clawbles/CabinetGlass"));AssetDatabase.CreateAsset(glass,glassPath);}
            glass.SetColor("_Tint",new Color(.5f,.85f,.9f,.075f));EditorUtility.SetDirty(glass);
            string rpPath = "Assets/Clawbles/ClawblesLow.asset";
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
            if (rp == null)
            {
                AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset", rpPath);
                rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
            }
            if (rp != null)
            {
                string rendererPath = "Assets/Clawbles/ClawblesRenderer.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                renderer.renderingMode = RenderingMode.Forward;
                renderer.postProcessData = null;
                renderer.rendererFeatures.Clear();
                EditorUtility.SetDirty(renderer);
                var serializedPipeline = new SerializedObject(rp);
                var renderers = serializedPipeline.FindProperty("m_RendererDataList");
                renderers.arraySize = 1; renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
                rp.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                rp.supportsHDR = false; rp.msaaSampleCount = 2; rp.renderScale = 1;
                rp.supportsCameraDepthTexture = false; rp.supportsCameraOpaqueTexture = false; rp.shadowDistance = 0;
                GraphicsSettings.defaultRenderPipeline = rp; QualitySettings.renderPipeline = rp;
                EditorUtility.SetDirty(rp);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CLAWBLES_SCENE_READY " + ScenePath);
        }
        [MenuItem("Clawbles/Build Windows Prototype")]
        public static void Build()
        {
            CreateScene();
            string output = Environment.GetEnvironmentVariable("CLAWBLES_BUILD_PATH");
            if (string.IsNullOrEmpty(output)) output = "Builds/Windows/CLAWBLES.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("CLAWBLES build failed: " + report.summary.result);
            Debug.Log("CLAWBLES_BUILD_OK " + output);
        }
        public static void Validate()
        {
            var a = new ClawLayout(7241); var b = new ClawLayout(7241); var other = new ClawLayout(9918);
            Require(a.Obstacles.Count > 10 && a.Obstacles.Count < 160, "Bounded procedural obstacle budget");
            Require(a.Obstacles.Count == b.Obstacles.Count, "Seed repeatability");
            for (int i = 0; i < a.Obstacles.Count; i++) Require(a.Obstacles[i] == b.Obstacles[i], "Identical obstacle geometry");
            bool changed = a.Obstacles.Count != other.Obstacles.Count;
            for (int i = 0; !changed && i < a.Obstacles.Count; i++) changed |= a.Obstacles[i] != other.Obstacles[i];
            Require(changed, "Different seeds vary the map");
            Require(a.Overlaps(new Vector3(0, 6, -12), Vector3.one*.45f), "Partition requires a window crossing");
            Require(!a.Overlaps(ClawLayout.Console, Vector3.one * 0.4f), "Console access");
            var s = new ClawSimulation(); s.Add(1); s.Add(2); s.Add(3); s.Add(4);
            Require(s.Add(5) == null, "Four-player capacity");
            s.Input(1, Vector2.zero, 0, 1, 2, 0); s.Step(0.02f, 0); Require(s.Operator == 1, "Console claim");
            s.Remove(1); Require(s.Operator == ClawSimulation.Nobody, "Pilot disconnect releases console");
            s.Prize = ClawLayout.Delivery + Vector3.up * 0.86f; for(int drop=0;drop<100;drop++) s.Step(.02f,drop*.02f); Require(s.Score == 1, "Delivery scores after falling inside hole");
            for (int i = 0; i < 20; i++) s.Step(0.02f, i * 0.02f);
            Require(s.Score == 1, "Single score per delivery");
            s.Input(2, new Vector2(float.NaN, 0), 0, 2, 0, 1); s.Step(0.02f, 1);
            Require(float.IsFinite(s.Players[2].Position.x), "Invalid input rejected");
            var g = new ClawSimulation(); g.Add(11); var helper = g.Add(12);
            g.Input(11, Vector2.zero, 0, 1, 2, 0); g.Step(0.02f, 0);
            g.Anchor = g.Claw = g.Prize + Vector3.up * 0.8f;
            g.Input(11, Vector2.zero, 0, 2, 2, 0); g.Step(0.02f, 0);
            Require(g.Gripped && g.GripQuality >= 0.85f, "Centered hook is secure");
            helper.Position = g.Prize + Vector3.left;
            g.Input(12, Vector2.zero, 0, 1, 2, 0); g.Step(0.02f, 0); Require(helper.Attachment == 1, "Helper attaches");
            g.GripQuality = 0.5f;
            g.Input(12, Vector2.zero, 0, 1, 0, 0, 0, 1, true); g.Step(0.1f, 0);
            Require(g.PrizeYaw > 0 && g.GripQuality > 0.5f, "Helper turns and secures load");
            g.Input(12, Vector2.zero, 0, 2, 1, 0); g.Step(0.02f, 0); Require(helper.Attachment == 0, "Jump detaches");
            g.Input(11, Vector2.zero, 0, 3, 2, 0); g.Step(0.02f, 0); Require(!g.Gripped, "Release works");
            var prior = helper.Position;
            g.Input(12, Vector2.right, 0, 2, 0, 0); g.Step(0.02f, 1);
            Require(Mathf.Approximately(prior.x, helper.Position.x), "Stale input stops movement");
            var loose = new ClawSimulation(); loose.Add(1); loose.Operator = 1;
            loose.Prize = new Vector3(0, 4, 9); loose.Anchor = loose.Claw = loose.Prize + Vector3.up * 0.8f + Vector3.right;
            loose.Input(1, Vector2.zero, 0, 1, 2, 0); loose.Step(0.02f, 0);
            Require(loose.Gripped && loose.GripQuality < 0.85f, "Off-center hook is weak");
            for (int i = 1; i < 250 && loose.Gripped; i++) { loose.Input(1, Vector2.right, 0, 1, 0, i * 0.02f); loose.Step(0.02f, i * 0.02f); }
            Require(!loose.Gripped && loose.SlipEvents == 1, "Weak load slips");
            var impact = new ClawSimulation(); var victim = impact.Add(1); victim.Position = new Vector3(0, ClawSimulation.Floor, 0);
            impact.Prize = new Vector3(0, 5, 0); impact.PrizeVelocity = Vector3.down * 6;
            float clock = 0;
            for (int i = 0; i < 100 && impact.KnockEvents == 0; i++) { clock += 0.02f; impact.Step(0.02f, clock); }
            Require(impact.KnockEvents == 1 && victim.KnockTimer > 0, "Falling load knocks down victim");
            for (int i = 0; i < 150; i++) { clock += 0.02f; impact.Step(0.02f, clock); }
            Require(victim.KnockTimer == 0, "Ragdoll recovery timer");
            float before = victim.Position.x; impact.Input(1, Vector2.right, 0, 1, 0, clock); impact.Step(0.05f, clock);
            Require(victim.Position.x > before, "Control returns after knockdown");
            Require(ClawVoice.InRange(Vector3.zero, Vector3.right * 10) && !ClawVoice.InRange(Vector3.zero, Vector3.right * (ClawVoice.Range + 1)), "Voice proximity routing");
            for (int i = -100; i <= 100; i++) Require(Mathf.Abs(ClawVoice.Decode(ClawVoice.Encode(i / 100f)) - i / 100f) < 0.04f, "Voice codec round-trip");
            Require(ClawSession.CleanChat(new string('x', 200)).Length == 160, "Chat size bound");
            Require(ClawSession.CleanChat(" a\nb\t ") == "ab", "Chat control characters filtered");
            var laser=new ClawSimulation(); laser.Prize=new Vector3(-16,3.3f,10); laser.Step(.02f,0);
            Require(laser.LaserResets==1 && laser.ResetTimer>0 && laser.Score==0,"Laser resets without scoring");
            Require(a.LaserHit(new Vector3(-16,3.3f,8),new Vector3(-16,3.3f,12),Vector3.one*.2f),"Swept laser contact");
            Require(!a.InsideHole(ClawLayout.Delivery+Vector3.right*2,Vector3.one),"Rim does not count as delivery");
            Require(a.LandingHeight(ClawLayout.Delivery+Vector3.up,ClawLayout.Delivery,Vector3.one*.5f)<0,"Actual floor opening");
            var swing=new ClawSimulation(); swing.Add(1); swing.Operator=1;
            for(int i=0;i<40;i++){ swing.Input(1,Vector2.right,0,1,0,i*.02f);swing.Step(.02f,i*.02f); }
            Require(Mathf.Abs(swing.Claw.x-swing.Anchor.x)>.2f,"Load lags behind moving carriage");
            var stop=swing.Claw;
            for(int i=40;i<55;i++){swing.Input(1,Vector2.zero,0,1,0,i*.02f);swing.Step(.02f,i*.02f);}
            Require(Vector3.Distance(stop,swing.Claw)>.1f,"Pendulum continues after stopping");
            Require(a.Overlaps(new Vector3(30,6,15),ClawSimulation.PrizeExtents(0)),"Gantry alone cannot pass side window");
            Require(!a.Overlaps(new Vector3(32,6,15),ClawSimulation.PrizeExtents(0)),"Swing offset fits side window");
            Require(a.Overlaps(new Vector3(32,1,24),ClawSimulation.PrizeExtents(90)),"Duck cannot use pedestrian doorway");
            Require(!a.Overlaps(new Vector3(32,.65f,24),new Vector3(.3f,.64f,.3f)),"Crew can use pedestrian doorway");
            var grounded=new ClawSimulation();var operatorPlayer=grounded.Add(1);grounded.Operator=1;var consolePosition=operatorPlayer.Position;
            for(int i=0;i<60;i++){grounded.Input(1,Vector2.right,1,1,0,i*.02f);grounded.Step(.02f,i*.02f);}
            Require(operatorPlayer.Position==consolePosition,"Operator stays on ground while crane moves");
            Require(grounded.Anchor.x>1 && grounded.Anchor.y>6,"Grounded operator still moves crane");
            var run=new ClawSimulation();run.Add(1);run.Shift.NewRun(run,7241);
            var start=run.Players[1].Position;run.Input(1,Vector2.right,0,1,0,0);run.Step(.02f,0);
            Require(run.Players[1].Position==start && run.Shift.Seconds==600,"Briefing freezes simulation and timer");
            Require(run.Shift.Start(),"Start first shift");run.Step(1,0);Require(run.Shift.Seconds==599,"Active clock counts down");
            Require(!run.Shift.Next(run,0),"Cannot buy gear during active play");
            for(int stage=0;stage<4;stage++){
                if(stage>0)Require(run.Shift.Start(),"Start next shift");
                int credits=run.Shift.Credits;
                for(int delivered=0;delivered<run.Shift.Quota;delivered++){
                    run.Prize=ClawLayout.Delivery+Vector3.down*.6f;run.Gripped=false;run.ResetTimer=0;run.PrizeVelocity=Vector3.zero;
                    run.Step(.02f,0);run.Step(.02f,0);
                }
                Require(run.Shift.Credits>credits,"Delivered prizes award credits");
                if(stage<3){
                    Require(run.Shift.Phase==ShiftPhase.Upgrade,"Quota opens upgrade screen");
                    Require(!run.Shift.Next(run,-1),"Invalid upgrade rejected");
                    Require(run.Shift.Next(run,stage),"Buy upgrade and advance");
                    Require(run.Shift.Phase==ShiftPhase.Briefing && run.Operator==ClawSimulation.Nobody,"Next shift resets roles");
                }
            }
            Require(run.Shift.Phase==ShiftPhase.Won && run.Shift.Stars==12,"Complete four-shift run");
            int previousRun=run.Shift.RunId;run.Shift.NewRun(run,9901);
            Require(run.Shift.RunId==previousRun+1 && run.Score==0 && run.Shift.Credits==0,"Replay resets economy and scores");
            run.Shift.Start();run.Shift.Seconds=.01f;run.Step(.02f,0);Require(run.Shift.Phase==ShiftPhase.Lost,"Timer failure is terminal");
            var noMoney=new ClawSimulation();noMoney.Shift.NewRun(noMoney,1);noMoney.Shift.Phase=ShiftPhase.Upgrade;noMoney.Shift.Credits=149;
            Require(!noMoney.Shift.Next(noMoney,0)&&noMoney.Shift.Credits==149,"Cannot overspend");
            Require(noMoney.Shift.Next(noMoney,3),"Free continuation remains available");
            var rescue=new ClawSimulation();var crew=rescue.Add(1);
            crew.Position=rescue.Map.Obstacles[0].center;rescue.Step(.02f,0);
            Require(!rescue.Map.Overlaps(crew.Position,new Vector3(.3f,.64f,.3f)),"Embedded crew automatically separates from obstacle");
            crew.Position=new Vector3(10,ClawSimulation.Floor,0);
            rescue.Input(1,Vector2.zero,0,1,8,0);rescue.Step(.02f,0);
            Require(Vector3.Distance(crew.Position,ClawLayout.Console)<2,"R recovers crew to console");
            rescue.Operator=1;crew.RecoveryCooldown=0;rescue.Gripped=true;rescue.GripQuality=1;
            rescue.Claw=rescue.Anchor=new Vector3(29,6,15);rescue.Prize=rescue.Claw-Vector3.up*.8f;
            var pilotGround=crew.Position;rescue.Input(1,Vector2.zero,0,2,8,0);rescue.Step(.02f,0);
            Require(!rescue.Gripped && rescue.Claw.x==0 && crew.Position==pilotGround,"Crane rescue clears jam without moving operator");
            var slideMap=new ClawLayout(7241);var frame=slideMap.Obstacles[0];
            var ext=new Vector3(.3f,.64f,.3f);var alongside=new Vector3(frame.min.x-.31f,frame.center.y,frame.center.z);
            var slid=slideMap.Slide(alongside,alongside+new Vector3(.5f,0,.5f),ext);
            Require(slid.z>alongside.z && !slideMap.Overlaps(slid,ext),"Blocked axis preserves movement along wall");
            var crossed=slideMap.Slide(new Vector3(frame.min.x-1,frame.center.y,frame.center.z),new Vector3(frame.max.x+1,frame.center.y,frame.center.z),ext);
            Require(crossed.x<frame.min.x,"Swept movement cannot tunnel through obstacle");
            var reversing=new ClawSimulation();reversing.Add(1);reversing.Operator=1;
            var wall=reversing.Map.Obstacles[0];reversing.Claw=reversing.Anchor=new Vector3(wall.min.x-.6f,wall.center.y,wall.center.z);
            for(int i=0;i<65;i++){reversing.Input(1,Vector2.right,0,1,0,i*.02f);reversing.Step(.02f,i*.02f);}
            for(int i=65;i<230;i++){reversing.Input(1,Vector2.left,0,1,0,i*.02f);reversing.Step(.02f,i*.02f);}
            Require(reversing.Claw.x<wall.min.x-.5f && !reversing.Map.Overlaps(reversing.Claw,Vector3.one*.45f),"Crane reverses out of sustained wall contact");
            var dragged=new ClawSimulation();var holder=dragged.Add(1);wall=dragged.Map.Obstacles[0];
            holder.Position=new Vector3(wall.min.x-1,wall.center.y,wall.center.z);holder.Attachment=1;
            dragged.Prize=new Vector3(wall.max.x+2,wall.center.y,wall.center.z);dragged.Step(.02f,0);
            Require(holder.Attachment==0&&!dragged.Map.Overlaps(holder.Position,new Vector3(.3f,.64f,.3f)),"Attached crew cannot be pulled through wall");
            var paid=new ClawSimulation();paid.Add(1);paid.Shift.NewRun(paid,7241);paid.Shift.Start();
            paid.Input(1,Vector2.zero,0,1,8,0);paid.Step(.02f,0);
            paid.Input(1,Vector2.zero,0,2,8,.02f);paid.Step(.02f,.02f);
            Require(paid.Shift.Seconds>589 && paid.Shift.Seconds<590,"Rescue costs ten seconds and cooldown blocks repeat charge");
            Debug.Log("CLAWBLES_RECOVERY_CHECKS_OK");
        }
                public static void ValidateToyCourse()
        {
            var map=new ClawLayout(7241);var repeat=new ClawLayout(7241);
            Require(map.Toys.Count>80 && map.Toys.Count<=198,"Bounded plush population");
            for(int i=0;i<map.Toys.Count;i++){
                Require(map.Toys[i].Bounds==repeat.Toys[i].Bounds && map.Toys[i].Rotation==repeat.Toys[i].Rotation,"Deterministic plush placement");
                Require(Mathf.Abs(map.Toys[i].Bounds.min.y-.002f)<.001f,"Every plush touches the floor");
                Require(!map.OverlapsStructure(map.Toys[i].Bounds.center,map.Toys[i].Bounds.extents),"Plush clear of walls");
            }
            Require(!map.Overlaps(map.PrizeSpawn(0),ClawSimulation.PrizeExtents(0)),"Mission spawn has clearance inside bay");
            Require(map.Overlaps(new Vector3(-20,ClawLayout.HookMax,10),Vector3.one*.45f),"Claw cannot pass over bay wall");
            Require(map.Overlaps(new Vector3(0,ClawLayout.HookMax,-12),Vector3.one*.45f),"Claw cannot fly over delivery partition");
            for(int stage=0;stage<3;stage++){
                var route=new ClawSimulation();route.Shift.Stage=stage;
                var ext=route.CurrentExtents(0);
                var start=new Vector3(-16,4.7f,12);var end=new Vector3(-16,4.7f,8);
                Require(Vector3.Distance(map.Slide(start,end,ext),end)<.01f&&!map.LaserHit(start,end,ext),"Every mission cargo fits laser window");
                start=new Vector3(map.SouthGateX,4.2f,-10);end=new Vector3(map.SouthGateX,4.2f,-14);
                Require(Vector3.Distance(map.Slide(start,end,ext),end)<.01f&&!map.LaserHit(start,end,ext),"Delivery window remains traversable");
            }
            var ceiling=new ClawSimulation();ceiling.Add(1);ceiling.Operator=1;
            for(int i=0;i<300;i++){ceiling.Input(1,Vector2.right,1,0,0,i*.02f);ceiling.Step(.02f,i*.02f);Require(ceiling.Claw.y<=ClawLayout.HookMax+.001f&&ceiling.Anchor.y<=ClawLayout.HookMax+.001f,"Height cap includes swing lift");}
            var pick=new ClawSimulation();pick.Add(1);pick.Operator=1;
            var toy=pick.Map.Toys[0];pick.Anchor=pick.Claw=toy.Target;
            pick.Input(1,Vector2.zero,0,1,2,0);pick.Step(.02f,0);
            Require(pick.HeldToy==toy.Id&&!pick.Gripped,"Claw grabs ordinary plush");
            pick.Input(1,Vector2.zero,0,2,2,.02f);pick.Step(.02f,.02f);
            Require(pick.HeldToy==-1&&!pick.Gripped,"Wrong plush releases independently");
            toy.SetCenter(ClawLayout.Delivery+Vector3.down*.7f);toy.Velocity=Vector3.zero;toy.Moving=true;
            pick.Step(.02f,.04f);
            Require(pick.Score==0&&Vector3.Distance(toy.Bounds.center,toy.OriginalCenter)<.01f,"Wrong plush recycled without scoring: score="+pick.Score+" center="+toy.Bounds.center+" original="+toy.OriginalCenter+" ext="+toy.Bounds.extents);
            Require(map.Overlaps(map.Toys[0].Bounds.center,new Vector3(.3f,.64f,.3f)),"Players collide with plush geometry");
            var physics=new ClawSimulation();var walker=physics.Add(1);var figure=physics.Map.Toys[0];
            figure.SetCenter(new Vector3(0,figure.Bounds.extents.y+.002f,0));
            walker.Position=new Vector3(-figure.Bounds.extents.x-.34f,ClawSimulation.Floor,0);
            var original=figure.Bounds.center;
            for(int i=0;i<15;i++){physics.Input(1,Vector2.right,0,0,0,i*.02f);physics.Step(.02f,i*.02f);}
            Require(figure.Bounds.center.x>original.x+.1f,"Walking gently pushes a figure");
            for(int i=15;i<180;i++){physics.Input(1,Vector2.zero,0,0,0,i*.02f);physics.Step(.02f,i*.02f);}
            Require(!figure.Moving&&figure.Velocity.sqrMagnitude<.003f,"Ground friction puts a pushed figure to sleep");
            Require(figure.Bounds.min.y>=-.01f,"Pushed figure stays above floor");
            var jumping=new ClawSimulation();var jumper=jumping.Add(1);jumper.Position=new Vector3(0,ClawSimulation.Floor,0);
            jumping.Input(1,Vector2.zero,0,1,1,0);jumping.Step(.02f,0);
            Require(jumper.VerticalSpeed>0,"Ground jump starts");
            float apex=jumper.Position.y;
            for(int i=1;i<70;i++){jumping.Step(.02f,i*.02f);apex=Mathf.Max(apex,jumper.Position.y);}
            Require(apex>1.7f&&apex<2f&&Mathf.Abs(jumper.Position.y-ClawSimulation.Floor)<.02f,"Smooth jump height and landing");
            jumper.Position=Vector3.up*4;jumper.VerticalSpeed=0;jumper.GroundGrace=0;
            jumping.Input(1,Vector2.zero,0,2,1,2);jumping.Step(.02f,2);
            Require(jumper.VerticalSpeed<0,"No extra jump at airborne apex");
            jumper.Position=new Vector3(0,.72f,0);jumper.VerticalSpeed=-2;jumper.JumpBuffer=0;
            jumping.Input(1,Vector2.zero,0,3,1,2.02f);jumping.Step(.02f,2.02f);
            Require(jumper.VerticalSpeed>0,"Near landing jump input is accepted");
            Require(ClawGestures.Parse("[SIGNAL] RAISE THE HOOK")==5&&ClawGestures.Parse("[SIGNAL] fake")==0,"Only known arm signals accepted");
            for(int yaw=0;yaw<360;yaw+=45){
                ClawGestures.Arms(1,yaw,0,out var leftArm,out var rightArm);
                var pointing=leftArm==Vector3.down?rightArm:leftArm;
                Require(Vector3.Dot(Quaternion.Euler(0,yaw,0)*pointing,Vector3.left)>.99f,"West gesture keeps its world direction");
            }
            ClawGestures.Arms(5,0,0,out var upLeft,out var upRight);
            ClawGestures.Arms(6,0,0,out var downLeft,out var downRight);
            Require(upLeft.y>.8f&&upRight.y>.8f&&downLeft.y<-.8f&&downRight.y<-.8f,"Up and down have distinct two arm poses");
            var movingMap=new ClawLayout(7241);
            Require(movingMap.Lasers.Count==3,"Only framed window lasers remain");
            var staticBeam=movingMap.Lasers[0];movingMap.UpdateLasers(0,8,true);
            Require(movingMap.Lasers[0]==staticBeam,"First shift lasers remain static");
            movingMap.UpdateLasers(2,0,true);movingMap.UpdateLasers(2,Mathf.PI/.75f);
            var stationary=new Vector3(-16,3.5f,10);
            Require(movingMap.LaserHit(stationary,stationary,Vector3.one*.1f),"Moving beam sweeps stationary cargo");
            for(int stage=1;stage<=2;stage++){
                var route=new ClawSimulation();route.Shift.Stage=stage;var ext=route.CurrentExtents(0);
                for(int gate=0;gate<3;gate++){
                    bool opening=false;float low=float.MaxValue,high=float.MinValue;
                    for(int tick=0;tick<80;tick++){
                        movingMap.UpdateLasers(stage,tick*.2f,true);var beam=movingMap.Lasers[gate];var travel=movingMap.LaserTravel(gate);
                        low=Mathf.Min(low,beam.center.y);high=Mathf.Max(high,beam.center.y);
                        Require(beam.min.y>travel.x&&beam.max.y<travel.y,"Moving beam stays inside frame");
                        for(float y=travel.x+ext.y+.05f;y<ClawLayout.HookMax-.8f;y+=.1f){
                            var p=new Vector3(beam.center.x,y,beam.center.z);
                            if(!movingMap.Overlaps(p,ext)&&!movingMap.Overlaps(p+Vector3.up*.8f,Vector3.one*.45f)&&!movingMap.LaserHit(p+Vector3.back,p+Vector3.forward,ext))opening=true;
                        }
                    }
                    Require(high-low>.5f,"Later shifts move every window laser");
                    Require(opening,"Every later window has a timed cargo passage");
                }
            }
            Debug.Log("CLAWBLES_MOVING_WINDOW_LASERS_OK");
            Debug.Log("CLAWBLES_FIGURE_PHYSICS_JUMP_OK");
            Debug.Log("CLAWBLES_TOY_COURSE_CHECKS_OK toys="+map.Toys.Count);
        }
        static void Require(bool value, string name) { if (!value) throw new Exception("Simulation check failed: " + name); }
        public static void ValidateAndBuild() { SessionState.SetBool("ThreadBuild",true); ClawThreadChecks.Play(); }
    }
}



















