using UnityEngine;
using System.Collections.Generic;
namespace DeliveryDash
{
    // Lightweight articulated crowd rig. Visual parts are replaceable independently of navigation.
    public sealed class TownCitizenMotion : MonoBehaviour
    {
        Transform leftLeg,rightLeg,leftArm,rightArm;float phase;
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();readonly Dictionary<Transform,Quaternion> rest=new Dictionary<Transform,Quaternion>();
        void Awake(){foreach(var t in GetComponentsInChildren<Transform>()){if(t==transform)continue;bones[t.name]=t;rest[t]=t.localRotation;}leftLeg=transform.Find("Left leg");rightLeg=transform.Find("Right leg");leftArm=transform.Find("Left arm");rightArm=transform.Find("Right arm");}
        void Start(){Pose(0,1);}
        public void Pose(float speed,int mode)
        {
            if(bones.Count==0)Awake();phase+=Time.deltaTime*speed*5;
            if(bones.ContainsKey("thigh_l"))
            {
                foreach(var pair in rest)pair.Key.localRotation=pair.Value;
                float stride=speed>.1f?Mathf.Sin(phase)*26:0;
                bool seated=mode==1||mode==2||mode==3;
                Swing("thigh_l",seated?-70+stride*(mode==3?1:.25f):stride);Swing("thigh_r",seated?-70-stride*(mode==3?1:.25f):-stride);
                Swing("calf_l",seated?80+(mode==3?stride:0):Mathf.Max(0,-stride));Swing("calf_r",seated?80-(mode==3?stride:0):Mathf.Max(0,stride));
                if(mode==4){phase+=Time.deltaTime;Swing("upperarm_r",Mathf.Sin(phase)*12);}
                Arm("upperarm_l","lowerarm_l","hand_l",-1,mode,stride);Arm("upperarm_r","lowerarm_r","hand_r",1,mode,-stride);return;
            }
            if(leftLeg==null)return;
            float a=speed>.1f?Mathf.Sin(phase)*26:0;
            leftLeg.localRotation=Quaternion.Euler(mode>0?-65+a*.25f:a,0,0);rightLeg.localRotation=Quaternion.Euler(mode>0?-65-a*.25f:-a,0,0);
            leftArm.localRotation=Quaternion.Euler(mode>0?-55:-a*.7f,0,-7);rightArm.localRotation=Quaternion.Euler(mode>0?-55:a*.7f,0,7);
        }
        void Swing(string name,float angle){if(bones.TryGetValue(name,out var bone))bone.rotation=Quaternion.AngleAxis(angle,transform.right)*bone.rotation;}
        void Arm(string up,string low,string hand,int side,int mode,float stride)
        {
            if(!bones.TryGetValue(up,out var shoulder)||!bones.TryGetValue(low,out var elbow))return;
            var d=transform.TransformDirection(new Vector3(side*.12f,-1,mode>0&&mode<4?.7f:Mathf.Sin(stride*Mathf.Deg2Rad)*.4f));shoulder.rotation=Quaternion.FromToRotation(elbow.position-shoulder.position,d)*shoulder.rotation;
            if(mode>0&&mode<4&&bones.TryGetValue(hand,out var wrist))elbow.rotation=Quaternion.FromToRotation(wrist.position-elbow.position,transform.forward)*elbow.rotation;
        }
    }
}
