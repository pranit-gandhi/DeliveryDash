using UnityEngine;

namespace DeliveryDash
{
    // Local coordinates keep the performance stable through streamed-world rebases.
    public sealed class TownShopOwner : MonoBehaviour
    {
        Transform actor, leftLeg, rightLeg, parcel;
        TownCustomerRig detailedRig;
        Vector3 CarryPosition => detailedRig != null ? new Vector3(0,1.12f,.4f) : new Vector3(0,1.35f,.59f);
        Vector3 doorway, curb, parcelStart;
        CartFeelController cart;
        float elapsed, walkSeconds;
        bool serving, returning, customer;
        public static readonly string[] CustomerNames = { "Dario Amodei", "Donald Trump", "Elon Musk", "Sam Altman", "Jensen Huang" };
        Vector3 CartParcelPosition=>cart.transform.TransformPoint(cart.GetComponent<TownRideSelector>()?.Selected>0?new Vector3(0,.95f,-.93f):new Vector3(0,1.1f,.42f));
        public bool HasTransferred { get; private set; }
        public bool IsOutside => actor != null && Vector3.Distance(actor.localPosition, doorway) > .2f;
        public float HoldSeconds => walkSeconds + 1.25f;

        public void Configure(Vector3 door, Vector3 bay, int side, Material white, Material skin, Material red, Material dark, Material cardboard, int customerStyle = -1, GameObject modelTemplate = null)
        {
            customer = customerStyle >= 0;
            doorway = door; curb = bay + Vector3.right * side * 1.3f;
            walkSeconds = Vector3.Distance(doorway, curb) / 3f;
            if(customer && modelTemplate != null)
            {
                actor=Instantiate(modelTemplate,transform).transform;
                actor.name=CustomerNames[customerStyle]+" photo-fitted character";
                actor.localPosition=doorway; actor.localRotation=Quaternion.LookRotation(Vector3.left*side);
                detailedRig=actor.gameObject.AddComponent<TownCustomerRig>(); detailedRig.Initialize();
            }
            else
            {
            actor = new GameObject(customer ? CustomerNames[customerStyle] + " caricature" : "Chef in apron").transform; actor.SetParent(transform, false);
            actor.localPosition = doorway; actor.localRotation = Quaternion.LookRotation(Vector3.left * side);
            Part(actor, "Chef jacket", PrimitiveType.Capsule, new Vector3(0, 1.28f, 0), new Vector3(.65f,.48f,.4f), customer ? dark : white);
            if (!customer) Part(actor, "Red apron", PrimitiveType.Cube, new Vector3(0,1.08f,.22f), new Vector3(.49f,.72f,.06f), red);
            Part(actor, "Head", PrimitiveType.Sphere, new Vector3(0,1.97f,0), new Vector3(.42f,.5f,.42f), skin);
            Part(actor, "Nose", PrimitiveType.Sphere, new Vector3(0,1.97f,.22f), Vector3.one*.1f, skin);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(actor, "Eye", PrimitiveType.Sphere, new Vector3(s*.085f,2.03f,.193f), Vector3.one*.045f, dark);
                Part(actor, "Sleeve", PrimitiveType.Capsule, new Vector3(s*.39f,1.4f,.13f), new Vector3(.2f,.23f,.2f), customer ? dark : white).localRotation = Quaternion.Euler(45,0,s*15);
                Part(actor, "Hand", PrimitiveType.Sphere, new Vector3(s*.32f,1.27f,.48f), Vector3.one*.17f, skin);
                Transform leg = new GameObject("Walking leg").transform; leg.SetParent(actor,false); leg.localPosition = new Vector3(s*.17f,.88f,0);
                Part(leg,"Trousers",PrimitiveType.Capsule,new Vector3(0,-.34f,0),new Vector3(.24f,.34f,.25f),dark);
                Part(leg,"Shoe",PrimitiveType.Cube,new Vector3(0,-.77f,.075f),new Vector3(.27f,.15f,.43f),dark);
                if (s < 0) leftLeg = leg; else rightLeg = leg;
            }
            if (!customer)
            {
            Part(actor,"Chef hat band",PrimitiveType.Cylinder,new Vector3(0,2.25f,0),new Vector3(.47f,.09f,.47f),white);
            for (int i=0;i<3;i++) Part(actor,"Chef hat puff",PrimitiveType.Sphere,new Vector3((i-1)*.15f,2.43f,0),new Vector3(.32f,.34f,.4f),white);
            }
            else
            {
                // Interchangeable caricature pieces, rather than a photoreal likeness.
                Material hair = customerStyle==1 ? cardboard : (customerStyle==4 ? white : dark);
                Part(actor,"Hair",PrimitiveType.Sphere,new Vector3(0,2.17f,-.025f),new Vector3(.44f,.22f,.43f),hair);
                if(customerStyle==1)
                {
                    Part(actor,"Swept blond fringe",PrimitiveType.Sphere,new Vector3(-.08f,2.19f,.13f),new Vector3(.42f,.13f,.24f),hair);
                    Part(actor,"White shirt",PrimitiveType.Cube,new Vector3(0,1.43f,.207f),new Vector3(.22f,.45f,.03f),white);
                    Part(actor,"Long red tie",PrimitiveType.Cube,new Vector3(0,1.35f,.235f),new Vector3(.07f,.5f,.035f),red);
                }
                if(customerStyle==4)
                {
                    for(int sideEye=-1;sideEye<=1;sideEye+=2)
                        Part(actor,"Glasses",PrimitiveType.Cube,new Vector3(sideEye*.105f,2.03f,.219f),new Vector3(.18f,.105f,.04f),dark);
                    Part(actor,"Leather jacket zipper",PrimitiveType.Cube,new Vector3(0,1.29f,.212f),new Vector3(.025f,.62f,.035f),white);
                }
                if(customerStyle==0)
                {
                    for(int curl=-1;curl<=1;curl++) Part(actor,"Wavy fringe",PrimitiveType.Sphere,new Vector3(curl*.12f,2.18f,.16f),Vector3.one*.17f,hair);
                    Part(actor,"Shirt collar",PrimitiveType.Cube,new Vector3(0,1.67f,.18f),new Vector3(.3f,.09f,.1f),white);
                }
                if(customerStyle==2) Part(actor,"T-shirt emblem",PrimitiveType.Cube,new Vector3(0,1.45f,.22f),new Vector3(.16f,.16f,.025f),white);
                if(customerStyle==3) Part(actor,"Sweater neckline",PrimitiveType.Cube,new Vector3(0,1.68f,.15f),new Vector3(.26f,.06f,.1f),white);
            }
            }
            parcel = new GameObject("Pizza boxes carried by owner").transform; parcel.SetParent(actor,false);
            parcel.localPosition = CarryPosition;
            for(int i=0;i<3;i++)
            {
                Part(parcel,"Pizza box",PrimitiveType.Cube,new Vector3(0,i*.12f,0),new Vector3(.72f,.105f,.72f),cardboard);
                Part(parcel,"Red box seal",PrimitiveType.Cube,new Vector3(0,i*.12f+.055f,0),new Vector3(.13f,.01f,.73f),red);
            }
            parcel.gameObject.SetActive(false);
            actor.gameObject.AddComponent<TownDistanceDetail>();
        }
        public void Begin(CartFeelController runner, int count)
        {
            cart = runner; elapsed = 0; serving = true; returning = false; HasTransferred = false;
            actor.localPosition = doorway;
            parcel.SetParent(actor,false); parcel.localPosition = CarryPosition; parcel.localRotation = Quaternion.identity;
            for(int i=0;i<parcel.childCount;i++) parcel.GetChild(i).gameObject.SetActive(i/2 < count);
            parcel.gameObject.SetActive(!customer);
        }
        public void Finish()
        {
            serving = false; returning = true;
            if(customer && HasTransferred) { parcel.SetParent(actor,false); parcel.localPosition = CarryPosition; }
            else parcel.gameObject.SetActive(false);
        }
        void Update()
        {
            if (serving && cart != null && cart.IsHandingOff)
            {
                elapsed += Time.deltaTime;
                actor.localPosition = Vector3.Lerp(doorway,curb,Mathf.Clamp01(elapsed/walkSeconds));
                actor.localRotation = Quaternion.LookRotation((curb-doorway).normalized);
                Step(elapsed < walkSeconds);
                if(elapsed >= walkSeconds)
                {
                    if(parcel.parent == actor)
                    {
                        parcel.SetParent(transform,true); parcelStart = parcel.localPosition;
                        if(customer) parcelStart = transform.InverseTransformPoint(CartParcelPosition);
                        parcel.gameObject.SetActive(true);
                    }
                    float t = Mathf.Clamp01((elapsed-walkSeconds)/.75f);
                    Vector3 destination = transform.InverseTransformPoint(CartParcelPosition);
                    if(customer) destination = transform.InverseTransformPoint(actor.TransformPoint(CarryPosition));
                    parcel.localPosition = Vector3.Lerp(parcelStart,destination,t) + Vector3.up * Mathf.Sin(t*Mathf.PI)*.25f;
                    if(t >= 1) HasTransferred = true;
                }
            }
            else if(returning)
            {
                actor.localRotation = Quaternion.LookRotation((doorway-curb).normalized);
                actor.localPosition = Vector3.MoveTowards(actor.localPosition,doorway,Time.deltaTime*3);
                bool moving = Vector3.Distance(actor.localPosition,doorway) > .01f; Step(moving);
                if(!moving) { returning=false; parcel.gameObject.SetActive(false); actor.localRotation = Quaternion.LookRotation((curb-doorway).normalized); }
            }
        }
        void Step(bool moving)
        {
            if(detailedRig != null) { detailedRig.Pose(moving); return; }
            float angle = moving ? Mathf.Sin(Time.time*12)*24 : 0;
            leftLeg.localRotation = Quaternion.Euler(angle,0,0); rightLeg.localRotation = Quaternion.Euler(-angle,0,0);
        }
        internal static Transform Part(Transform parent,string label,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(shape); go.name=label; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
            go.GetComponent<Collider>().enabled=false; Destroy(go.GetComponent<Collider>()); return go.transform;
        }
    }
}
