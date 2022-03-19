using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityStandardAssets.Vehicles.Car
{
public class ButtonToggle : MonoBehaviour {

	public Text myText;
	public Text sf;
	public Text srf;
	public Text slf;
	public Text srm;
	public Text slm;
	public Text srb;
	public Text slb;
	public Text sb;
	//public Text gps;

	public Text fpsText;
	
	private float _deltaTime;
	public GameObject car;
	//the state, true is off, false is on
	private bool state;

	[HideInInspector]
	public float front;
	[HideInInspector]
	public float rightf;
	[HideInInspector]
	public float leftf;
	[HideInInspector]
	public float rightm;
	[HideInInspector]
	public float leftm;
	[HideInInspector]
	public float rightb;
	[HideInInspector]
	public float leftb;
	[HideInInspector]
	public float back;



	// Use this for initialization
	void Start () {
		state = false;
		myText.text = "Sensor: View";
		sf.text = "";
		srf.text = "";
		slf.text = "";
		srm.text = "";
		slm.text = "";
		srb.text = "";
		slb.text = "";
		sb.text = "";
		//gps.text = "";

	}

	void Update(){
		
		if(!state)
		{
			CarController m_Car = (CarController) car.GetComponent(typeof(CarController));
			List<float> sensors = m_Car.getSensors ();
			//List<int> mgps = m_Car.getGPS ();

			if (sensors.Count != 0) {
				front = (float) sensors [0];
				sf.text = "Front: "+front.ToString ("N2");
				rightf = (float) sensors [1];
				srf.text = "Right_F: "+rightf .ToString ("N2");
				leftf = (float) sensors [2];
				slf.text = "Left_F: "+rightf .ToString ("N2");
				rightm = (float) sensors [3];
				srm.text = "Right_M: "+rightm .ToString ("N2");
				leftm = (float) sensors [4];
				slm.text = "Left_M: "+leftm .ToString ("N2");
				rightb = (float) sensors [5];
				srb.text = "Right_B: "+rightb .ToString ("N2");
				leftb = sensors [6];
				slb.text = "Left_B: "+leftb .ToString ("N2");
				back = sensors [7];
				sb.text = "Back: "+back .ToString ("N2");

				//gps.text = "GPS: "+mgps [0].ToString ("N0") + " , " + mgps [1].ToString ("N0");
			}
		}

		_deltaTime += (Time.deltaTime - _deltaTime) * 0.1f;
		float fps = 1.0f/_deltaTime;
		fpsText.text = "Status: " + (Mathf.Ceil(fps).ToString()) + " fps"; 
	}
	
	// Update is called once per frame
	public void Toggle(){
		state = !state;
		if(state){
			myText.text = "Sensor: View";
			sf.text = "";
			srf.text = "";
			slf.text = "";
			srm.text = "";
			slm.text = "";
			srb.text = "";
			slb.text = "";
			sb.text = "";
			//gps.text = "";
		}
		else{
			myText.text = "Sensor: Hide";
		}
	}
}
}
