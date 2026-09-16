using UnityEngine;
namespace Clawbles
{
    // Original synthesized arcade cues. Voice routing remains entirely separate.
    public sealed class ClawFeedback:MonoBehaviour
    {
        public ClawSession Session;
        AudioSource effects,motor;
        AudioClip delivery,fail,grip,win,hum;
        int lastScore,lastLaser,lastKnock;bool held;Vector3 previous;
        ShiftPhase phase;
        public float Volume=.5f;
        void Start()
        {
            Volume=PlayerPrefs.GetFloat("EffectsVolume",.5f);
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-clawSocialSmoke")>=0)Volume=0;
            effects=gameObject.AddComponent<AudioSource>();effects.playOnAwake=false;effects.spatialBlend=0;
            var engine=new GameObject("Crane motor sound");engine.transform.SetParent(transform,false);
            motor=engine.AddComponent<AudioSource>();motor.playOnAwake=false;motor.loop=true;motor.spatialBlend=1;motor.minDistance=4;motor.maxDistance=65;motor.rolloffMode=AudioRolloffMode.Linear;
            delivery=Tone("Delivery chime",new[]{523f,659f,784f,1046f},.12f);
            fail=Tone("Soft fail",new[]{220f,165f,110f},.1f);grip=Tone("Claw latch",new[]{300f,450f},.045f);
            win=Tone("Shift complete",new[]{523f,659f,784f,659f,784f,1046f},.16f);
            hum=Tone("Motor",new[]{65f},1);motor.clip=hum;motor.Play();
        }
        static AudioClip Tone(string name,float[] notes,float beat)
        {
            const int rate=22050;var data=new float[Mathf.CeilToInt(rate*beat*notes.Length)];
            for(int i=0;i<data.Length;i++){
                float time=i/(float)rate;int note=Mathf.Min(notes.Length-1,(int)(time/beat));float p=(time-note*beat)/beat;
                float envelope=Mathf.Min(1,p*40)*Mathf.Pow(1-p,2);
                data[i]=(Mathf.Sin(time*notes[note]*Mathf.PI*2)+.18f*Mathf.Sin(time*notes[note]*Mathf.PI*4))*envelope*.16f;
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void Update()
        {
            if(effects==null)return;var s=Session.State;effects.volume=Volume;
            if(Session.Connected){
                if(s.Score>lastScore)effects.PlayOneShot(delivery);
                if(s.LaserResets>lastLaser||s.KnockEvents>lastKnock)effects.PlayOneShot(fail,.7f);
                if(s.Gripped&&!held)effects.PlayOneShot(grip);
                if(s.Shift.Phase!=phase&&(s.Shift.Phase==ShiftPhase.Won||s.Shift.Phase==ShiftPhase.Upgrade))effects.PlayOneShot(win);
            }
            float speed=Vector3.Distance(s.Anchor,previous)/Mathf.Max(.001f,Time.deltaTime);
            motor.transform.position=new Vector3(s.Anchor.x,ClawLayout.Ceiling,s.Anchor.z);motor.volume=Session.Connected?Mathf.Min(.3f,speed*.04f)*Volume:0;
            motor.pitch=.7f+Mathf.Min(1,speed*.07f);
            previous=s.Anchor;lastScore=s.Score;lastLaser=s.LaserResets;lastKnock=s.KnockEvents;held=s.Gripped;phase=s.Shift.Phase;
        }
        void OnDestroy(){foreach(var c in new[]{delivery,fail,grip,win,hum})if(c!=null)Destroy(c);}
    }
}


