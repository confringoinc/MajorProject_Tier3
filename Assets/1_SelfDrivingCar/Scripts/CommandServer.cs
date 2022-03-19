using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Dpoch.SocketIO;

using UnityStandardAssets.Vehicles.Car;
using System;
using System.Security.AccessControl;
using UnityEngine.SceneManagement;

[Serializable]
class communicationPoint{
	public bool done;	
	public float[] observation;
	public float reward;

	public communicationPoint(){
		observation = null;
		reward = 0f;
		done = false;
	}

	public communicationPoint(float[] obs, int len){
		this.observation = new float[len];
		
		for(int i = 0; i<len; i++)
			this.observation[i] = obs[i];
	}
	public communicationPoint(float[] obs, int len, float reward, bool done){
		this.observation = new float[len];
		
		for(int i = 0; i<len; i++)
			this.observation[i] = obs[i];

		this.reward = reward;
		this.done = done;
	}
}

class steerHere{
	public float acceleration;
	public float steering_angle;
	steerHere(){
		steering_angle = 0f;
		acceleration = 0f;
	}
}

public class CommandServer : MonoBehaviour
{
	public GameObject Car;
	public Camera FrontFacingCamera;
	// private SocketIOComponent _socket;
	private CarController _carController;
	private CarAIControl _carAIController;
	private perfect_controller point_path;
	private CarTraffic car_traffic;
	private communicationPoint data;

	public List<float> sensorData;

	private float curr_SteeringAngle;
	private float prev_SteeringAngle;

	private Vector3 init_position;
	private Quaternion init_rotation;


	// Convert angle (degrees) from Unity orientation to 
	//            90
	//
	//  180                   0/360
	//
	//            270
	//
	// This is the standard format used in mathematical functions.
	float convertAngle(float psi) {
		if (psi >= 0 && psi <= 90) {
			return 90 - psi;
		}
		else if (psi > 90 && psi <= 180) {
			return 90 + 270 - (psi - 90);
		}
		else if (psi > 180 && psi <= 270) {
			return 180 + 90 - (psi - 180);
		}
		return 270 - 90 - (psi - 270);
	}

	// Use this for initialization
	void Start()
	{	
		//Initial Parameters
		init_position = Car.transform.position;
		init_rotation = Car.transform.rotation;

		point_path = Car.GetComponent<perfect_controller>();
		car_traffic = Car.GetComponent<CarTraffic>();
		_carAIController = Car.GetComponent<CarAIControl>();
		_carController = Car.GetComponent<CarController>();

		var socket = new SocketIO("ws://127.0.0.1:3000/socket.io/?EIO=4&transport=websocket");
		Debug.LogError("Starting listening to server");

		socket.OnOpen += () => {
			Debug.LogError("Socket open!");
		};

		socket.OnConnectFailed += () => Debug.LogError("Socket failed to connect!");
		socket.OnClose += () => {
			Debug.LogError("Socket closed!");
			Time.timeScale = 0f;
		};
		socket.OnError += (err) => Debug.LogError("Socket Error: " + err);
		
		socket.Connect();

		socket.On("step", (ev) => {
			// Debug.Log("STEP -> Done: " + isDone);
			steerHere action = ev.Data[0].ToObject<steerHere>();

			// Debug.LogError("Accelerator: " + action.acceleration + " | SteeringAngle: " + action.steering_angle);
			_carController.Move(action.steering_angle, action.acceleration, action.acceleration, 0f);
			curr_SteeringAngle = action.steering_angle;

			_carController.performFixedUpdate(Time.deltaTime);
			//_carController.performUpdate(Time.deltaTime);
			car_traffic.performUpdate(Time.deltaTime);
			car_traffic.performFixedUpdate(Time.deltaTime);
			_carAIController.performFixedUpdate(Time.deltaTime);

			// isDoneMethod();
			freeze();

			sensorData = _carController.getSensors();
			
			sensorData.Add(_carController.CurrentSteerAngle);
			sensorData.Add(_carController.AccelInput);


			communicationPoint data = new communicationPoint(sensorData.ToArray(), sensorData.Count, calcReward(), CarController.isDone);
			string strJson = Newtonsoft.Json.JsonConvert.SerializeObject(data);
			
			//	 Send acknowledgment
			if(ev.IsAcknowledgable) {
				ev.Acknowledge(strJson);
			}
		});

		socket.On("reset", (ev) => {
			//Debug.Log("RESET -> Done: " + isDone);
			if(CarController.isDone){
				resetEnv();
			}
			_carController.performFixedUpdate(Time.deltaTime);
			//_carController.performUpdate(Time.deltaTime);
			car_traffic.performUpdate(Time.deltaTime);
			car_traffic.performFixedUpdate(Time.deltaTime);
			_carAIController.performFixedUpdate(Time.deltaTime);

			// isDoneMethod();

			freeze();
			// prev_SteeringAngle = _carController.CurrentSteerAngle;
			curr_SteeringAngle = 0f;
			prev_SteeringAngle = 0f;
			List<float> sensorData = _carController.getSensors();
			
			sensorData.Add(_carController.CurrentSteerAngle);
			sensorData.Add(_carController.AccelInput);

			communicationPoint data = new communicationPoint(sensorData.ToArray(), sensorData.Count);
			string strJson = Newtonsoft.Json.JsonConvert.SerializeObject(data);

			if(ev.IsAcknowledgable){
				ev.Acknowledge(strJson);
			}		
			// Debug.Log("RESET-END -> Done: " + isDone);
		});
	}
	void resetEnv(){
		CarController.isDone = false;
		// Debug.Log("RESET ENV -> Done: " + isDone);
		Car.transform.position = init_position;
		Car.transform.rotation = init_rotation;
		Car.GetComponent<Rigidbody>().velocity = Vector3.zero;

		// Debug.LogError("Car AI List: " + car_traffic.CarList.Count);
		for(int i = 0; i < car_traffic.CarList.Count; i++){
			car_traffic.CarList[i].removeCar();
		}
		
		car_traffic.CarList.Clear();
		_carController.Start();
	}

	void freeze(){
		foreach(CarAIControl car in car_traffic.CarList){
			// car.m_Rigidbody.constraints = RigidbodyConstraints.FreezePosition;
			car.performFixedUpdate(Time.fixedDeltaTime);
		}
  	}

	// Method that indicates the episode has been ended
	// void isDoneMethod(){
	// 	if(isDone){
	// 		Debug.Log("End Episode | Done: " + isDone);
	// 	}
	// }

	float calcReward(){
		float Rtb1, Rtb; //Rtb1 = 1st part of reward, Rtb = Reward (Throttle/Brake)
		float Rsa1, Rsa; //Rsa1 = 2nd part of reward, Rsa = Reward (Steering Angle)
		float Rtotal = 0; //Total Reward
		float Eq1 = 0f, Eq2 = 0f; //Threshold value
		float x = 0f; //Minimum distance between Main car and AI Car either from front | back sensor
		
		//Constants
		float rho = 1; 
		float beta = 1;
		float delta1 = 10;
		float delta2 = 4;
		float lambda = 1;
		float theta = 0.05f;

		//Increment 'rho' value
		Rtb1 = rho * Mathf.Abs(_carController.Jerk);
		
		//sensorData[0] = front Sesnor, sensorData[7] = Back Sensor
		if(sensorData[0] < delta1){
			Eq1 = beta * (sensorData[0] - delta1);
		}else if(sensorData[7] < delta1){
			Eq1 = beta * (sensorData[7] - delta1);
		}else{
			Eq1 = theta * Mathf.Min(sensorData[0], sensorData[7]);
		}
		

		Rtb = Eq1 - Rtb1;

		Rsa1 = lambda * Mathf.Abs(curr_SteeringAngle - prev_SteeringAngle);
		prev_SteeringAngle = curr_SteeringAngle;

		//sensorData[3] = Right middle sensor, sensorData[4] = Left middle sensor
		if(sensorData[3] < delta2){
			Eq2 = beta * (sensorData[3] - delta2);
		}else if(sensorData[4] < delta2){
			Eq2 = beta * (sensorData[4] - delta2);
		}else{
			Eq2 = theta * Mathf.Min(sensorData[3], sensorData[4]);
		}

		Rsa = Eq2 - Rsa1;

		Rtotal = Rsa + Rtb;
		
		Debug.LogError("Rtb: Rtb1-> " + Rtb1 + " Eq1-> " + Eq1 + " | Rsa: Rsa1-> " + Rsa1 + " Eq2-> " + Eq2 + " | Total Reward: " + Rtotal);
		return Rtotal;
	}
	
	// void OnOpen(SocketIOEvent obj)
	// {
	// 	Debug.LogError("Connection Open:- " + obj);
	// 	point_path.OpenScript();
	// 	EmitTelemetry(obj);
	// }
	// void OnClose(SocketIOEvent obj)
	// {
	// 	Debug.Log("Connection Closed");
	// 	point_path.CloseScript ();

	// }

	// 
	// void onManual(SocketIOEvent obj)
	// {
	// 	EmitTelemetry (obj);
	// }

	// void Control(SocketIOEvent obj)
	// {
	// 	JSONObject jsonObject = obj.data;

	// 	//Debug.Log ("sending control");


	// 	var next_x = jsonObject.GetField ("next_x");
	// 	var next_y = jsonObject.GetField ("next_y");
	// 	List<float> my_next_x = new List<float> ();
	// 	List<float> my_next_y = new List<float> ();

	// 	for (int i = 0; i < next_x.Count; i++) 
	// 	{
	// 		my_next_x.Add (float.Parse((next_x [i]).ToString()));
	// 		my_next_y.Add (float.Parse((next_y [i]).ToString()));
	// 	}

	// 	point_path.setControlPath (my_next_x, my_next_y);
	// 	//point_path.ProgressPath ();

	// 	point_path.setSimulatorProcess();

	// 	EmitTelemetry (obj);
	// }
		

	// void EmitTelemetry(SocketIOEvent obj)
	// {
	// 	UnityMainThreadDispatcher.Instance().Enqueue(() =>
	// 	{

	// 		//print("Attempting to Send...");
	// 		// send only if it's not being manually driven
	// 		if ( !point_path.isServerProcess() ) {
	// 			// Debug.LogError("Manual Driving");
	// 			_socket.Emit("telemetry", new JSONObject());
	// 		}
	// 		else {

	// 			point_path.ServerPause();
				
	// 			// Collect Data from the Car
	// 			Dictionary<string, JSONObject> data = new Dictionary<string, JSONObject>();

	// 			// localization of car
	// 			data["x"] = new JSONObject(Car.transform.position.x);
	// 			data["y"] = new JSONObject(Car.transform.position.z);
	// 			data["yaw"] = new JSONObject (convertAngle(Car.transform.rotation.eulerAngles.y));
	// 			data["speed"] = new JSONObject(_carController.CurrentSpeed);

	// 			CarAIControl carAI = (CarAIControl) Car.GetComponent(typeof(CarAIControl));

	// 			List<float> frenet_values = carAI.getThisFrenetFrame();

	// 			data["s"] = new JSONObject(frenet_values[0]);
	// 			data["d"] = new JSONObject(frenet_values[1]);

	// 			// Previous Path data
	// 			JSONObject arr_x = new JSONObject(JSONObject.Type.ARRAY);
	// 			JSONObject arr_y = new JSONObject(JSONObject.Type.ARRAY);
	// 			var previous_path_x = point_path.previous_path_x();
	// 			var previous_path_y = point_path.previous_path_y();

	// 			for( int i = 0; i < previous_path_x.Count; i++)
	// 			{
	// 					arr_x.Add(previous_path_x[i]);
	// 					arr_y.Add(previous_path_y[i]);
	// 			}

	// 			var previous_y = JsonUtility.ToJson(point_path.previous_path_y());
	// 			data["previous_path_x"] = arr_x;
	// 			data["previous_path_y"] = arr_y;
				
	// 			var end_path_s = 0.0f;
	// 			var end_path_d = 0.0f;

	// 			if(previous_path_x.Count > 0)
	// 			{
	// 				List<float> frenet_values_others = carAI.getFrenetFrame(previous_path_x[previous_path_x.Count-1],previous_path_y[previous_path_y.Count-1]);
	// 				end_path_s = frenet_values_others[0];
	// 				end_path_d = frenet_values_others[1];
	// 			}

	// 			//End path S and D values
	// 			data["end_path_s"] = new JSONObject(end_path_s);
	// 			data["end_path_d"] = new JSONObject(end_path_d);

				
	// 			//data["v_x"] = new JSONObject((Car.GetComponent<Rigidbody>().velocity.x));  
	// 			//data["v_y"] = new JSONObject((Car.GetComponent<Rigidbody>().velocity.z));
	// 			//Vector3 vdir = Car.GetComponent<Rigidbody>().velocity;
	// 			//data["v_yaw"] = new JSONObject((float)convertAngle(Mathf.Atan2(vdir.x,vdir.z)*Mathf.Rad2Deg));
	// 			//data["a_x"] = new JSONObject(_carController.SenseAcc().x);
	// 			//data["a_y"] = new JSONObject(_carController.SenseAcc().z);
	// 			//Vector3 adir = _carController.SenseAcc();
	// 			//data["a_yaw"] = new JSONObject(((float)convertAngle(Mathf.Atan2(adir.x,adir.z)*Mathf.Rad2Deg)));

	// 			CarTraffic cars = (CarTraffic) Car.GetComponent(typeof(CarTraffic));
	// 			data["sensor_fusion"] = new JSONObject(cars.example_sensor_fusion());
	// 			data["temp_data"] = new JSONObject("temp_varibale");
				

	// 			//data["steering_angle"] = new JSONObject(_carController.CurrentSteerAngle);
	// 			//data["throttle"] = new JSONObject(_carController.AccelInput);
	// 			//data["speed"] = new JSONObject(_carController.CurrentSpeed);
	// 			_socket.Emit("telemetry", new JSONObject(data), (callback) => {
	// 				Debug.LogError("Callback was called");
	// 			} );
	// 		}
	// 	});

		//    UnityMainThreadDispatcher.Instance().Enqueue(() =>
		//    {
		//      	
		//      
		//
		//		// send only if it's not being manually driven
		//		if ((Input.GetKey(KeyCode.W)) || (Input.GetKey(KeyCode.S))) {
		//			_socket.Emit("telemetry", new JSONObject());
		//		}
		//		else {
		//			// Collect Data from the Car
		//			Dictionary<string, string> data = new Dictionary<string, string>();
		//			data["steering_angle"] = _carController.CurrentSteerAngle.ToString("N4");
		//			data["throttle"] = _carController.AccelInput.ToString("N4");
		//			data["speed"] = _carController.CurrentSpeed.ToString("N4");
		//			data["image"] = Convert.ToBase64String(CameraHelper.CaptureFrame(FrontFacingCamera));
		//			_socket.Emit("telemetry", new JSONObject(data));
		//		}
		//      
		////      
		//    });
	// }
}