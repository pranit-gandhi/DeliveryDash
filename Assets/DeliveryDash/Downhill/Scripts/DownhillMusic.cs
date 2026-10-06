using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class DownhillMusic : MonoBehaviour
    {
        DownhillSession session;
        AudioSource menu, carting;
        public string Track => carting!=null&&carting.volume>0?"Carting":menu!=null&&menu.volume>0?"Menu":"";
        AudioSource Active => Track=="Carting"?carting:menu;
        public float TrackTime => Active!=null&&Active.clip!=null?Active.time:0;
        public float TrackLength => Active!=null&&Active.clip!=null?Active.clip.length:0;
        public bool Playing => Active!=null&&Active.isPlaying;
        void Start()
        {
            session=GetComponent<DownhillSession>();
            menu=Voice("DeliveryDashMusic/Menu");
            carting=Voice("DeliveryDashMusic/Carting");
        }
        AudioSource Voice(string path)
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.clip=Resources.Load<AudioClip>(path);
            source.loop=true;source.playOnAwake=false;source.volume=0;
            return source;
        }
        void Update()
        {
            // Listener pause freezes the existing voices. isPlaying can report
            // false while paused, so starting a voice here would reset its time.
            if(session==null||menu==null||carting==null||AudioListener.pause)return;
            bool road=session.State=="Running"||session.State=="Paused"||session.State=="Delivering";
            float menuTarget=road||session.State=="Crashing"?0:.22f;
            float roadTarget=road?.16f:0;
            Fade(menu,menuTarget);Fade(carting,roadTarget);
        }
        void Fade(AudioSource voice,float target)
        {
            voice.volume=Mathf.MoveTowards(voice.volume,target,Time.unscaledDeltaTime*.5f);
            if(target>0&&voice.clip!=null&&!voice.isPlaying)voice.Play();
            if(target==0&&voice.volume<=0&&voice.isPlaying)voice.Stop();
        }
    }
}
