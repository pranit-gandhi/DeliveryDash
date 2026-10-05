using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    // Shared skeleton motion for imported likeness assets; no runtime mesh construction.
    public sealed class TownCustomerRig : MonoBehaviour
    {
        readonly Dictionary<string,Transform> bones = new Dictionary<string,Transform>();
        readonly Dictionary<Transform,Quaternion> rest = new Dictionary<Transform,Quaternion>();
        public void Initialize()
        {
            foreach(var t in GetComponentsInChildren<Transform>())
            { if(t==transform) continue; bones[t.name]=t; rest[t]=t.localRotation; }
            Pose(false);
        }
        public void Pose(bool walking)
        {
            foreach(var pair in rest) pair.Key.localRotation=pair.Value;
            float stride=walking ? Mathf.Sin(Time.time*9)*22 : 0;
            Swing("thigh_l",stride); Swing("thigh_r",-stride);
            Swing("calf_l",Mathf.Max(0,-stride)*.8f); Swing("calf_r",Mathf.Max(0,stride)*.8f);
            Arm("upperarm_l","lowerarm_l",-1,walking);
            Arm("upperarm_r","lowerarm_r",1,walking);
        }
        void Swing(string name,float angle)
        {
            if(bones.TryGetValue(name,out var bone)) bone.rotation=Quaternion.AngleAxis(angle,transform.right)*bone.rotation;
        }
        void Arm(string upper,string lower,int side,bool walking)
        {
            if(!bones.TryGetValue(upper,out var shoulder)||!bones.TryGetValue(lower,out var elbow))return;
            Vector3 direction=transform.TransformDirection(new Vector3(side*.22f,-1,.12f));
            shoulder.rotation=Quaternion.FromToRotation(elbow.position-shoulder.position,direction)*shoulder.rotation;
            if(bones.TryGetValue(side<0?"hand_l":"hand_r",out var hand))
            {
                Vector3 forearm=transform.TransformDirection(new Vector3(-side*.1f,-.3f,.9f));
                elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,forearm)*elbow.rotation;
            }
        }
    }
}
