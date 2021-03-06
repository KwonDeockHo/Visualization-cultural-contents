﻿using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class dino_manager : MonoBehaviour
{
	[Header("RESOURCES")]
	public Texture2D helpscreen; //help screen
	public Texture2D icons; //icons texture
	public AudioClip Wind, Underwater;
	[Tooltip("Add here the prefab of your dinosaurs and they will be available for spawning in game")]
	public List<GameObject> CollectionList; //Dino collection prefab
	[Header("CONTROLS")]
	[SerializeField] private bool InvertYAxis=false;
	[SerializeField] [Range(0.1f, 10.0f)] private float sensivity=2.5f;
	[Header("DINOSAURS")]
	public bool UseIK=true;
	[Tooltip("Dinosaurs will be active even if they are no longer visible. (performance may be affected).")]
	public bool RealtimeGame=false;
	[Tooltip("Allow dinosaurs to walk on all collider marked as ''walkable'' layer, otherwise they can only walk on Terrain collider. (performance may be affected)")]
	public bool UseRaycast=false;
	[Header("SCREEN")]
	[SerializeField] private bool ShowGUI=true;
	[SerializeField] private bool ShowFPS=true;
	[SerializeField] private bool Wireframe=false;
	[Header("UNDERWATER EFFECT")]
	public bool UnderwaterEffect=true;
	public Light DirectionalLight;
	public Color32 UnderWaterFog;
	public Color32 OuterWaterFog;
	public Texture[] LightCookie;
	private Vector3 StartLightDir;
	private bool InWater=false;
	private int i=0, j=0, h=0;

	[HideInInspector] public List<GameObject> dinoList; //list of all dino in game
	[HideInInspector] public int dino, CameraMode=1, message=0; //dino index, camera mode
	[HideInInspector] public Terrain terrain;
	private int Health, Food, Water, Sleep; //dino health bar
	private int toolBarTab=-1, addDinoTab=-2; //toolbar tab
	private Vector2 scroll1=Vector2.zero, scroll2=Vector2.zero; //Scroll position
	private float vx, vy, vz=25; //camera angle/zoom
	private float timer, frame, fps; //fps
	private Rigidbody body;
	private AudioSource[] source;
	private RaycastHit onscreenhit; //dino pickup ray
	private bool spawnAI, spawnRnd;

//*************************************************************************************************************************************************
// STARTUP
	void Start()
	{ 
		Cursor.visible = false; Cursor.lockState=CursorLockMode.Locked;
		StartLightDir = DirectionalLight.transform.forward; //start directional light dir 
		body=GetComponent<Rigidbody>();
		source=GetComponents<AudioSource>();
		//Get terrain
		if(Terrain.activeTerrain) terrain =Terrain.activeTerrain;
		//Find all dinos prefab in scene
		GameObject[] dinos= GameObject.FindGameObjectsWithTag("Dino");
		foreach (GameObject element in dinos)
		{ 
			if(!element.name.EndsWith("(Clone)")) dinoList.Add(element.gameObject); //Add to list
			else Destroy(element.gameObject); //Delete unwanted ghost object in hierarchy
		}
	}

//*************************************************************************************************************************************************
// CAMERA BEHAVIOR
	void Update()
	{
		//Fps counter
		frame += 1.0f; timer += Time.deltaTime; if(timer>1.0f) { fps = frame; timer = 0.0f; frame = 0.0f; } 

		//Lock/Unlock cursor
		if(Application.isEditor)
		{
			if(Input.GetKeyDown(KeyCode.Escape) && toolBarTab==-1) { Cursor.lockState=CursorLockMode.None; toolBarTab=1; }
			else if(Input.GetKeyDown(KeyCode.Escape) && toolBarTab!=-1) { Cursor.lockState=CursorLockMode.None; toolBarTab=-1; }
			else if(toolBarTab==-1) Cursor.lockState=CursorLockMode.Locked;
		}
		else
		{
			if(Cursor.lockState==CursorLockMode.None && Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState=CursorLockMode.Locked;
			else if(Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState=CursorLockMode.None;
		}

	}

	void FixedUpdate()
	{
		shared dinoscript=null;
		//If dino not found, switch to free camera mode
		if(dinoList.Count==0) CameraMode=0;
		else if(!dinoList[dino] | !dinoList[dino].activeInHierarchy) CameraMode=0;
		else dinoscript=dinoList[dino].GetComponent<shared>(); //Get dino script

		if(dinoscript)
		{
			//Dino select (Shortcut Key)
			if(Input.GetKeyDown(KeyCode.X)) { if(dino > 0) dino--; else dino=dinoList.Count-1; }
			else if(Input.GetKeyDown(KeyCode.Y)) { if(dino < dinoList.Count-1) dino++; else dino=0; }
			
			//Change View (Shortcut Key)
			if(Input.GetKeyDown(KeyCode.C))
			{ if(CameraMode==2) CameraMode=0; else CameraMode++; }
			
			//Use AI (Shortcut Key)
			if(Input.GetKeyDown(KeyCode.I))
			{ if(dinoscript.AI) dinoscript.AI=false; else dinoscript.AI=true; }
		}

		//Prevent camera from going into terrain 
		if(terrain && terrain.SampleHeight(transform.position)>transform.position.y-1.0f)
		{
			body.velocity = new Vector3(body.velocity.x, 0, body.velocity.z);
			transform.position=new Vector3(transform.position.x, terrain.SampleHeight(transform.position)+1.0f, transform.position.z);
		}

		switch(CameraMode)
		{
		//Free camera
		case 0:
			if(source[0].isPlaying) { if(!InWater) { source[0].volume=body.velocity.magnitude/128; source[0].pitch=body.velocity.magnitude/128; } else source[0].volume=0; }
			else if(!InWater) source[0].PlayOneShot(Wind);
			Vector3 dir=Vector3.zero; float y=0;
			if(Cursor.lockState==CursorLockMode.Locked | Input.GetKey(KeyCode.Mouse2))
			{
				if(Input.GetKey(KeyCode.Space)) y=1; else if(Input.GetKey(KeyCode.LeftControl)) y=-1; else y=0;
				if(Input.GetKey(KeyCode.LeftShift)) body.mass=0.025f; else body.mass=0.1f;  body.drag=1.0f;
				vx=vx+Input.GetAxis("Mouse X")*sensivity; //rotate cam X axe
				vy=Mathf.Clamp(InvertYAxis?vy+Input.GetAxis("Mouse Y")*sensivity:vy-Input.GetAxis("Mouse Y")*sensivity, -90f, 90f); //rotate cam Y axe
				transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(vx, Vector3.up)*Quaternion.AngleAxis(vy, Vector3.right), 0.1f);
			} else { body.mass=0.1f; body.drag=1.0f; }
			dir=transform.rotation*new Vector3(Input.GetAxis("Horizontal"), y, Input.GetAxis("Vertical")); //move
			body.AddForce(dir*(transform.position-(transform.position+dir)).magnitude);
		break;
		//Follow camera
		case 1:
			if(source[0].isPlaying) source[0].volume=0;
			body.mass=1.0f; body.drag=10.0f; float size = dinoscript.size;
			if(Cursor.lockState==CursorLockMode.Locked | Input.GetKey(KeyCode.Mouse2))
			{
				if(Input.GetKey(KeyCode.Mouse1))
				{
					vx=dinoList[dino].transform.eulerAngles.y; //lock camera to dino angle
					if(dinoscript.IsFlying)
					{ vy=Mathf.Clamp(Mathf.Lerp(vy, dinoscript.anm.GetFloat("Pitch")*90, 0.01f), -45f, 90f); }//pitch flying dino with camera axe
					 else
					{ vy=Mathf.Clamp(InvertYAxis?vy-Input.GetAxis("Mouse Y")*sensivity : vy+Input.GetAxis("Mouse Y")*sensivity, -90f, 90f); } //rotate cam Y axe
				}
				else if(!Input.GetKey(KeyCode.Mouse2) | Cursor.lockState!=CursorLockMode.Locked)
				{
					vx=vx+Input.GetAxis("Mouse X")*sensivity; //rotate cam X axe
					vy=Mathf.Clamp(InvertYAxis?vy-Input.GetAxis("Mouse Y")*sensivity:vy+Input.GetAxis("Mouse Y")*sensivity, -90f, 90f); //rotate cam Y axe
				}
			}
			vz=Mathf.Clamp(vz-Input.GetAxis("Mouse ScrollWheel")*10, size, size*32f); //zoom cam Z axe
			transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(vy, vx, 0.0f), 0.1f);
			Vector3 pos=((dinoList[dino].transform.position+Vector3.up*size*1.5f)-transform.position)-transform.forward*vz;
			body.AddForce(pos*128f);
		break;
		// POV camera
		case 2:
			if(source[0].isPlaying) source[0].volume=0; size = dinoscript.size;
			transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation((dinoList[dino].transform.position+Vector3.up*size*1.5f)-transform.position), 0.1f);
		break;
		default: CameraMode=0; break;
		}
	}
//*************************************************************************************************************************************************
//CHECK DINOSAURS COLLECTION
	void CheckCollection(string Specie, Rect rect)
	{
		if(!CollectionList.Exists(o => o.name == Specie)) { GUI.color=Color.gray; GUI.Button(rect, Specie); } else
		{ GUI.color=Color.white; if(GUI.Button (rect, Specie)) addDinoTab=CollectionList.FindIndex(o => o.name == Specie); }
	}
//*************************************************************************************************************************************************
// DRAW GUI
	void OnGUI ()
	{
		float sw =Screen.width, sh= Screen.height;

		shared dinoscript;
		if(dinoList.Count>0 && dinoList[dino] && dinoList[dino].activeInHierarchy)
		dinoscript=dinoList[dino].GetComponent<shared>();  //Get dino script

		//Show dino info on toolbar & Camera mode select
		GUIStyle style = new GUIStyle("box"); style.fontSize=16;
		if(ShowGUI)
		{
			if(onscreenhit.point!=Vector3.zero) GUI.Box(new Rect(0, 0, sw, 50), "Press any key to select this dino ", style);
			else if(dinoscript&&CameraMode!=0)
			{
				GUI.Box(new Rect(0, 0, sw, 50), dinoList[dino].name);
				GUI.color=Color.yellow; if(GUI.Button(new Rect(0,5, (sw/16)-4, 20), "Free")) CameraMode=0;
				if(CameraMode==1) GUI.color=Color.green; if(GUI.Button(new Rect((sw/16)*1.5f, 5, (sw/16)-4, 20), "Follow")) CameraMode=1; GUI.color=Color.yellow;
				if(CameraMode==2) GUI.color=Color.green; if(GUI.Button(new Rect((sw/16)*3.0f, 5, (sw/16)-4, 20), "POV")) CameraMode=2;
			}
			else
			{
				GUI.Box(new Rect(0, 0, sw, 50), "", style);
				if(dinoscript)
				{
					GUI.color=Color.green; GUI.Button(new Rect(0,5, (sw/16)-4, 20), "Free"); GUI.color=Color.yellow;
					if(GUI.Button(new Rect((sw/16)*1.5f, 5, (sw/16)-4, 20), "Follow")) CameraMode=1;
					if(GUI.Button(new Rect((sw/16)*3.0f, 5, (sw/16)-4, 20), "POV")) CameraMode=2; 
				}
			}
			GUI.color=Color.white;
		}

		if(Cursor.lockState==CursorLockMode.None)
		{
			Cursor.visible = true;
			//Toolbar tabs
			if(!ShowGUI) GUI.Box(new Rect(0, 0, sw, 50), ""); 
			string[] toolbarStrings = new string[] {"File", "Dinosaurs", "Options", "Help"};
			GUI.color=Color.yellow; toolBarTab = GUI.Toolbar(new Rect(0, 30, sw, 20), toolBarTab, toolbarStrings); GUI.color=Color.white;

			switch(toolBarTab)
			{
			//File
			case 0: GUI.Box (new Rect(sw/4, sh/4, sw/2, sh/2), "", style);
				if(GUI.Button(new Rect((sw/2)-60, (sh/2)-35, 120, 30), "Reset")) SceneManager.LoadScene(0); //Reset				
				if(GUI.Button (new Rect((sw/2)-60, (sh/2)+5, 120, 30), "Quit")) Application.Quit(); //Quit
			break;
			//Dinosaurs
			case 1:
			if(dinoscript)
			{
				//Delete
				if(GUI.Button (new Rect((sw*0.25f)/3, 75, (sw*0.25f)/3, 20), "Delete"))
				{
					Destroy(dinoList[dino].gameObject);  dinoList.RemoveAt(dino);
					if(dino>0) dino--; else if(dinoList.Count>0) dino=dinoList.Count-1; else return;
				}
				//Dino select
				if(GUI.Button (new Rect(0, 75,  (sw*0.25f)/3, 20), "<<")) 	{ if(dino>0) dino--; else dino=dinoList.Count-1; } 
				if(GUI.Button (new Rect(((sw*0.25f)/3)*2, 75, (sw*0.25f)/3, 20), ">>")) { if(dino < dinoList.Count-1) dino++; else dino=0; }
					GUI.Box (new Rect(0, 50, sw*0.25f, (sh*0.75f)-50)," ["+dino+"/"+(dinoList.Count-1)+"] "+dinoList[dino].name, style);
				scroll1 = GUI.BeginScrollView(new Rect(0, 110, sw*0.25f, (sh*0.75f)-120), scroll1, new Rect(0, 0, 0, 400));

				//AI
				GUI.Box(new Rect(18, 0, sw*0.22f, 25), "");
				dinoscript.AI= GUI.Toggle (new Rect(18, 0, sw*0.22f, 25), dinoscript.AI, " Use AI  : "+dinoscript.behavior);
				//Body skin
				int body= dinoscript.BodySkin.GetHashCode();
				if(GUI.Button (new Rect(18, 30, sw*0.22f, 25), "Body Skin : "+dinoscript.BodySkin))
				{ if(body<2) body++; else body=0; dinoscript.SetBodySkin(body); }
				//Eyes skin
				int eyes= dinoscript.EyesSkin.GetHashCode();
				if(GUI.Button (new Rect(18, 60, sw*0.22f, 25), "Eyes Skin : "+dinoscript.EyesSkin))
				{ if(eyes<15)eyes++; else eyes=0; dinoscript.SetEyesSkin(eyes); }
				//Model scale
				float Scale=dinoList[dino].transform.localScale.x;
				GUI.Box(new Rect(18, 90, sw*0.22f, 25), "Scale : "+Scale);
				Scale=GUI.HorizontalSlider(new Rect(18, 110, sw*0.22f, 25), Scale, 0.1f, 1.0f);
				if(Scale!=dinoList[dino].transform.localScale.x) dinoList[dino].SendMessage("SetScale", Mathf.Round(Scale*100)/100);
				//Animation speed
				float Speed= dinoscript.AnimSpeed;
				GUI.Box(new Rect(18, 125, sw*0.22f, 25), "Animation Speed : "+Mathf.Round(Speed*100)/100);
				dinoscript.AnimSpeed=GUI.HorizontalSlider(new Rect(18, 145, sw*0.22f, 25), Speed, 0.0f, 2.0f);
				//Health
				float Health=dinoscript.Health;
				GUI.Box(new Rect(18, 160, sw*0.22f, 25), "Health : "+Health);
				Health=GUI.HorizontalSlider(new Rect(18, 180, sw*0.22f, 25), dinoscript.Health, 0, 100); dinoscript.Health =Health;
				//Food
				float Food=dinoscript.Food;
				GUI.Box(new Rect(18, 200, sw*0.22f, 20), "Food : "+Food);
				Food=GUI.HorizontalSlider(new Rect(18, 220, sw*0.22f, 20), dinoscript.Food, 0, 100); dinoscript.Food =Food;
				//Water
				float Water=dinoscript.Water;
				GUI.Box(new Rect(18, 240, sw*0.22f, 20), "Water : "+Water);
				Water=GUI.HorizontalSlider(new Rect(18, 260, sw*0.22f, 20), dinoscript.Water, 0, 100); dinoscript.Water =Water;
				//Fatigue
				float Fatigue=dinoscript.Fatigue;
				GUI.Box(new Rect(18, 280, sw*0.22f, 20), "Fatigue : "+Fatigue);
				Fatigue=GUI.HorizontalSlider(new Rect(18, 300, sw*0.22f, 20), dinoscript.Fatigue, 0, 100); dinoscript.Fatigue =Fatigue;
				//Armor
				float Armor=dinoscript.Armor;
				GUI.Box(new Rect(18, 320, sw*0.22f, 20), "Armor : "+Armor);
				Armor=GUI.HorizontalSlider(new Rect(18, 340, sw*0.22f, 20), dinoscript.Armor, 0, 100); dinoscript.Armor =Armor;
				//Damage
				float Damage=dinoscript.Damage;
				GUI.Box(new Rect(18, 360, sw*0.22f, 20), "Damage : "+Damage);
				Damage=GUI.HorizontalSlider(new Rect(18, 380, sw*0.22f, 20), dinoscript.Damage, 0, 100); dinoscript.Damage =Damage;
				GUI.EndScrollView();
			} else GUI.Box (new Rect(0, 50, sw*0.25f, (sh*0.75f)-50), "None", style);

			//Add new dino
			if(addDinoTab==-2)
			{
				if(GUI.Button (new Rect(0, sh*0.75f, sw*0.25f, 25), "")) addDinoTab=-1;
				GUI.Box(new Rect(0, sh*0.75f, sw/4, sh/4), "New dino, Select specie", style);
			}
			else if(addDinoTab==-1)
			{
				if(GUI.Button (new Rect(0, sh*0.75f, sw*0.25f, 25), "")) addDinoTab=-2;
				GUI.Box(new Rect(0, sh*0.75f, sw/4, sh/4), "Cancel", style);
				GUI.Box(new Rect(sw/4, sh/4, sw*0.6f, sh/2), "Select a specie", style);
				scroll2 = GUI.BeginScrollView(new Rect(sw/4, (sh/4)+25, sw*0.6f, (sh/2)-25), scroll2, new Rect(0, 0, 740, 290));

				//Volume I
				GUI.color=Color.yellow; GUI.Box(new Rect(25, 0, 140, 25), "Vol. I", style);
					CheckCollection("Ankylosaurus", new Rect(25, 30, 140, 20));
					CheckCollection("Brachiosaurus", new Rect(25, 50, 140, 20));
					CheckCollection("Compsognathus", new Rect(25, 70, 140, 20));
					CheckCollection("Dilophosaurus", new Rect(25, 90, 140, 20));
					CheckCollection("Dimetrodon", new Rect(25, 110, 140, 20));
					CheckCollection("Oviraptor", new Rect(25, 130, 140, 20));
					CheckCollection("Parasaurolophus", new Rect(25, 150, 140, 20));
					CheckCollection("Pteranodon", new Rect(25, 170, 140, 20));
					CheckCollection("Spinosaurus", new Rect(25, 190, 140, 20));
					CheckCollection("Stegosaurus", new Rect(25, 210, 140, 20));
					CheckCollection("Triceratops", new Rect(25, 230, 140, 20));
					CheckCollection("Tyrannosaurus Rex", new Rect(25, 250, 140, 20));
					CheckCollection("Velociraptor", new Rect(25, 270, 140, 20));
				//Volume II
				GUI.color=Color.yellow; GUI.Box(new Rect(170, 0, 140, 25), "Vol. II", style);
					CheckCollection("Argentinosaurus", new Rect(170, 30, 140, 20));
					CheckCollection("Baryonyx", new Rect(170, 50, 140, 20));
					CheckCollection("Carnotaurus", new Rect(170, 70, 140, 20));
					CheckCollection("Dimorphodon", new Rect(170, 90, 140, 20));
					CheckCollection("Gallimimus", new Rect(170, 110, 140, 20));
					CheckCollection("Iguanodon", new Rect(170, 130, 140, 20));
					CheckCollection("Kentrosaurus", new Rect(170, 150, 140, 20));
					CheckCollection("Ouranosaurus", new Rect(170, 170, 140, 20));
					CheckCollection("Pachycephalosaurus", new Rect(170, 190, 140, 20));
					CheckCollection("Protoceratops", new Rect(170, 210, 140, 20));
					CheckCollection("Quetzalcoatlus", new Rect(170, 230, 140, 20));
					CheckCollection("Styracosaurus", new Rect(170, 250, 140, 20));
					CheckCollection("Troodon", new Rect(170, 270, 140, 20));
				//Volume III
				GUI.color=Color.yellow; GUI.Box(new Rect(315, 0, 140, 25), "Vol. III", style);
					CheckCollection("Acrocanthosaurus", new Rect(315, 30, 140, 20));
					CheckCollection("Allosaurus", new Rect(315, 50, 140, 20));
					CheckCollection("Amargasaurus", new Rect(315, 70, 140, 20));
					CheckCollection("Apatosaurus", new Rect(315, 90, 140, 20));
					CheckCollection("Archaeopteryx", new Rect(315, 110, 140, 20));
					CheckCollection("Ceratosaurus", new Rect(315, 130, 140, 20));
					CheckCollection("Corythosaurus", new Rect(315, 150, 140, 20));
					CheckCollection("Ornithocheirus", new Rect(315, 170, 140, 20));
					CheckCollection("Pachyrhinosaurus", new Rect(315, 190, 140, 20));
					CheckCollection("Postosuchus", new Rect(315, 210, 140, 20));
					CheckCollection("Proganochelys", new Rect(315, 230, 140, 20));
					CheckCollection("Therizinosaurus", new Rect(315, 250, 140, 20));
					CheckCollection("Titanoboa", new Rect(315, 270, 140, 20));
				//Volume IV
				GUI.color=Color.yellow; GUI.Box(new Rect(460, 0, 140, 25), "Vol. IV", style);
					CheckCollection("Ammonite", new Rect(460, 30, 140, 20));
					CheckCollection("Anomalocaris", new Rect(460, 50, 140, 20));
					CheckCollection("Archelon", new Rect(460, 70, 140, 20));
					CheckCollection("Dunkleosteus", new Rect(460, 90, 140, 20));
					CheckCollection("Giant Orthocone", new Rect(460, 110, 140, 20));
					CheckCollection("Helicoprion", new Rect(460, 130, 140, 20));
					CheckCollection("Ichthyosaur", new Rect(460, 150, 140, 20));
					CheckCollection("Leedsichthys", new Rect(460, 170, 140, 20));
					CheckCollection("Megalodon", new Rect(460, 190, 140, 20));
					CheckCollection("Mosasaurus", new Rect(460, 210, 140, 20));
					CheckCollection("Onchopristis", new Rect(460, 230, 140, 20));
					CheckCollection("Sarcosuchus", new Rect(460, 250, 140, 20));
					CheckCollection("Styxosaurus", new Rect(460, 270, 140, 20));
				//Volume V 
				GUI.color=Color.yellow; GUI.Box(new Rect(605, 0, 140, 25), "Vol. V", style);
					CheckCollection("Arthropleura", new Rect(605, 30, 140, 20));
					CheckCollection("Coelacanth", new Rect(605, 50, 140, 20));
					CheckCollection("Cynognathus", new Rect(605, 70, 140, 20));
					CheckCollection("Diplocaulus", new Rect(605, 90, 140, 20));
					CheckCollection("Euphoberia", new Rect(605, 110, 140, 20));
					CheckCollection("Koolasuchus", new Rect(605, 130, 140, 20));
					CheckCollection("Meganeuropsis", new Rect(605, 150, 140, 20));
					CheckCollection("Megazostrodon", new Rect(605, 170, 140, 20));
					CheckCollection("Nephila Jurassica", new Rect(605, 190, 140, 20));
					CheckCollection("Palaeocharinus", new Rect(605, 210, 140, 20));
					CheckCollection("Proceratocephala", new Rect(605, 230, 140, 20));
					CheckCollection("Pulmonoscorpius", new Rect(605, 250, 140, 20));
					CheckCollection("Stethacanthus", new Rect(605, 270, 140, 20));
				GUI.EndScrollView();
			}
			else
			{
				if(GUI.Button (new Rect(0, sh*0.75f, sw*0.25f, 25), "")) { addDinoTab=-1; return; }
				GUI.Box(new Rect(0, sh*0.75f, sw/4, sh/4), "New : "+CollectionList[addDinoTab].name, style);
				//AI
					GUI.Box(new Rect(18, (sh*0.75f)+30, sw*0.22f, 25), "");
					spawnAI= GUI.Toggle (new Rect(18, (sh*0.75f)+30, sw*0.22f, 25), spawnAI, " Use AI");
				//Randomize
					GUI.Box(new Rect(18, (sh*0.75f)+60, sw*0.22f, 25), "");
					spawnRnd= GUI.Toggle (new Rect(18, (sh*0.75f)+60, sw*0.22f, 25), spawnRnd, "Randomize skins and size");
				//Spawn new dino
				if(GUI.Button (new Rect(sw*0.09f, sh-30, 60, 30), "Spawn"))
				{
					GameObject spawndino = Instantiate(CollectionList[addDinoTab] ,transform.position+transform.forward*10, Quaternion.identity);
					if(spawnAI | spawnRnd)
					{
						shared script=spawndino.GetComponent<shared>(); script.AI=spawnAI;
						if(spawnRnd) { script.SetBodySkin(Random.Range(0, 3)); script.SetEyesSkin(Random.Range(0, 16)); script.SetScale(Random.Range(0.25f, 0.75f)); }
					}
					spawndino.name=CollectionList[addDinoTab].name;
					dinoList.Add(spawndino.gameObject); dino = dinoList.IndexOf(spawndino.gameObject); //add dino to dino list
				}
			}
			break;
			//Options
			case 2: 
			GUI.Box (new Rect(sw/4, sh/4, sw/2, sh/2), "Options", style);
			scroll1 = GUI.BeginScrollView(new Rect(sw/4, (sh/4)+20, sw/2, (sh/2)-20), scroll1, new Rect(0, 0, 640,  260));
			//Screen
			GUI.Box(new Rect(20, 20, 150, 220), "Screen", style);
			bool fullScreen=Screen.fullScreen; fullScreen= GUI.Toggle (new Rect(25, 60, 140, 20), fullScreen, " Fullscreen");
			if(fullScreen!=Screen.fullScreen) Screen.fullScreen=!Screen.fullScreen;
			Wireframe= GUI.Toggle (new Rect(25, 100, 140, 20), Wireframe, " Wireframe");
			ShowFPS= GUI.Toggle (new Rect(25, 140, 140, 20), ShowFPS, " Show Fps");
			ShowGUI = GUI.Toggle (new Rect(25, 180, 140, 20), ShowGUI, " Show GUI");
			//Controls
			GUI.Box(new Rect(245, 20, 150, 220), "Controls", style);
			InvertYAxis = GUI.Toggle (new Rect(250, 60, 140, 20), InvertYAxis, " Invert Y Axe");
			GUI.Label(new Rect(250, 100, 140, 20), "Sensivity");
			sensivity=GUI.HorizontalSlider(new Rect(250, 120, 140, 20), sensivity, 0.1f, 10.0f);
			//Dinosaurs
			GUI.Box(new Rect(470, 20, 150, 220), "Dinosaurs", style);
			UseIK= GUI.Toggle (new Rect(475, 60, 140, 20), UseIK, " Use IK");
			UseRaycast= GUI.Toggle (new Rect(475, 100, 140, 20), UseRaycast, " Use Raycast");
			RealtimeGame= GUI.Toggle (new Rect(475, 140, 140, 20), RealtimeGame, " Realtime Game");
			GUI.EndScrollView();
			break;
			//Help
			case 3: GUI.Box (new Rect(0, 50, sw, sh-50), "Controls", style);	
				GUI.DrawTexture(new Rect(0, 50, sw, sh-50), helpscreen); 
			break;
			}
		} else Cursor.visible = false;


		if(dinoscript)
		{
			if(ShowGUI)
			{
				// Health bar
				if(CameraMode==1)
				{
					Rect ico1 = new Rect(0, 0.5f, 0.5f, 0.5f), ico2 = new Rect(0.5f, 0.5f, 0.5f, 0.5f), ico3 = new Rect(0.5f, 0, 0.5f, 0.5f), ico4 =new Rect(0, 0, 0.5f, 0.5f), bar=new Rect(0, 0, 0.1f, 0.1f);
					GUI.color=Color.white; //Icons
					GUI.DrawTextureWithTexCoords(new Rect(sw/4, sh/1.1f, sw/48, sw/48), icons, ico1, true);  //health icon
					GUI.DrawTextureWithTexCoords(new Rect(sw/2, sh/1.1f, sw/48, sw/48), icons, ico2, true); //food icon
					GUI.DrawTextureWithTexCoords(new Rect(sw/2, sh/1.05f, sw/48, sw/48), icons, ico3, true); //water icon
					GUI.DrawTextureWithTexCoords(new Rect(sw/4, sh/1.05f, sw/48, sw/48), icons, ico4, true); //sleep icon
					GUI.color=Color.black; //bar background
					GUI.DrawTextureWithTexCoords(new Rect(sw/3.5f, sh/1.09f, (sw*0.002f)*100, sh/100), icons, bar, false);
					GUI.DrawTextureWithTexCoords(new Rect(sw/1.85f, sh/1.09f, (sw*0.002f)* 100, sh/100), icons, bar, false);
					GUI.DrawTextureWithTexCoords(new Rect(sw/1.85f, sh/1.04f, (sw*0.002f)*100, sh/100), icons, bar, false); 
					GUI.DrawTextureWithTexCoords(new Rect(sw/3.5f, sh/1.04f, (sw*0.002f)*100, sh/100), icons, bar, false);
					GUI.color=Color.green; //health bar
					GUI.DrawTextureWithTexCoords(new Rect(sw/3.5f, sh/1.09f, (sw*0.002f)*dinoscript.Health, sh/100), icons, bar, false);
					GUI.color=Color.yellow; //food bar
					GUI.DrawTextureWithTexCoords(new Rect(sw/1.85f, sh/1.09f, (sw*0.002f)*dinoscript.Food, sh/100), icons, bar, false);
					GUI.color=Color.cyan; //water bar
					GUI.DrawTextureWithTexCoords(new Rect(sw/1.85f, sh/1.04f, (sw*0.002f)*dinoscript.Water, sh/100), icons, bar, false);
					GUI.color=Color.gray; //sleep bar
					GUI.DrawTextureWithTexCoords(new Rect(sw/3.5f, sh/1.04f, (sw*0.002f)*dinoscript.Fatigue, sh/100), icons, bar, false);
				}
			}
		}

		//Fps
		GUI.color=Color.white;
		if(ShowFPS) GUI.Label(new Rect(sw-60, 1, 55, 20), "Fps : "+ fps);

		//Message
		if(message!=0)
		{
			h++;
			if(message==1) GUI.Box(new Rect((sw/2)-120, sh/2, 240, 25), "Nothing to eat or drink found...", style);
			else if(message==2)  GUI.Box(new Rect((sw/2)-200, sh/2, 400, 25), "AI and IK features require 'JP Script Extension Asset' ", style);
			if(h==512) { h=0; message=0; }
		}
	}

//*************************************************************************************************************************************************
// PICKUP DINO ON SCREEN
	void LateUpdate()
	{
		if((Cursor.lockState==CursorLockMode.None | CameraMode==0))
		{
			Ray ray = Camera.main.ScreenPointToRay( Input.mousePosition );
			if(Physics.Raycast(ray, out onscreenhit, 100))
			{
				if(onscreenhit.transform.tag.Equals("Dino") && onscreenhit.transform.gameObject!=dinoList[dino].gameObject)
				{
					if(Input.GetKey(KeyCode.Mouse0)) { dino = dinoList.IndexOf(onscreenhit.transform.gameObject); CameraMode = 1; }
				} else onscreenhit.point=Vector3.zero;
			} else onscreenhit.point=Vector3.zero;

		} else onscreenhit.point=Vector3.zero;

//*************************************************************************************************************************************************
//UNDERWATER EFFECT
		if(UnderwaterEffect)
		{
			if(InWater)
			{
				//Disable flare layer
				if(GetComponent<FlareLayer>()) GetComponent<FlareLayer>().enabled = false; 
				// Setup fog
				RenderSettings.fogColor = Color32.Lerp(RenderSettings.fogColor, UnderWaterFog, 1.0f);
				RenderSettings.fogStartDistance = 0f; RenderSettings.fogEndDistance = 100f; 
				//Setup camera background
				GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
				GetComponent<Camera>().backgroundColor = UnderWaterFog;
				//Animate light cookie and rotate light
				if(DirectionalLight && LightCookie.Length>0)
				{
					if(DirectionalLight.transform.forward==StartLightDir) DirectionalLight.transform.forward=-Vector3.up; 
					if(j>2) { if(i==LightCookie.Length) i=0; DirectionalLight.cookie=LightCookie[i]; i++; j=0; } else j++; 
				}
			}
			else
			{
				//Enable flare layer
				if(GetComponent<FlareLayer>()) GetComponent<FlareLayer>().enabled = true;
				// Restore fog
				RenderSettings.fogColor = Color32.Lerp(RenderSettings.fogColor, OuterWaterFog, 1.0f);
				RenderSettings.fogStartDistance = 0; RenderSettings.fogEndDistance = 750; 
				//Restore camera background
				if(!Wireframe) GetComponent<Camera>().clearFlags=CameraClearFlags.Skybox;
				else GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
				GetComponent<Camera>().backgroundColor = OuterWaterFog;
				//Disable light cookie and rotate light
				if(DirectionalLight && LightCookie.Length>0)
				{
					if(DirectionalLight.transform.forward==-Vector3.up) DirectionalLight.transform.forward=StartLightDir;
					Texture blank=null; if(DirectionalLight.cookie) DirectionalLight.cookie =blank;
				}
			}
		}
	}
	void OnTriggerStay(Collider col)
	{ if(col.gameObject.layer==4) { InWater = true; if(source[1].isPlaying) { source[1].volume =0.5f; source[1].pitch=0.5f; } else source[1].PlayOneShot(Underwater); } }
	void OnTriggerExit(Collider col)
	{ if(col.gameObject.layer==4) { InWater = false; source[1].volume =0; } }

//*************************************************************************************************************************************************
// WIREFRAME
	void OnPreRender() { 	if(Wireframe) GL.wireframe = true; }
	void OnPostRender() { GL.wireframe = false; }
}
