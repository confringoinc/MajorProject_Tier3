# General Moudles
import numpy as np
import time
import json

#	Environment Modules
import gym
from gym import Env, spaces

class CarSim(Env):
	def __init__(self, sio, history_len=10):
		super(CarSim, self).__init__()

		self.sio = sio
		self.history_len = history_len

		self.observation_space = spaces.Box(
									low = np.full((self.history_len*10), -1, dtype=np.float32),
									high = np.full((self.history_len*10), 100, dtype=np.float32),
									dtype = np.float32	
								)

		self.action_space = spaces.Box(
									low = np.full((2), -1, dtype=np.float32),
									high = np.full((2), 1, dtype=np.float32),
									dtype=np.float32
								)

		self.observation = np.empty(self.observation_space.shape[0]*self.history_len, dtype=np.float32)

	def __updateObservations(self, new_obs):
		
		self.observation[0:10] = self.observation[10:20]
		self.observation[10:20] = self.observation[20:30]
		self.observation[20:30] = self.observation[30:40]
		self.observation[30:40] = self.observation[40:50]
		self.observation[40:50] = self.observation[50:60]
		self.observation[50:60] = self.observation[60:70]
		self.observation[60:70] = self.observation[70:80]
		self.observation[70:80] = self.observation[80:90]
		self.observation[80:90] = self.observation[90:100]
		self.observation[90:100] = new_obs

	def step(self, action):
		"""
			Step function to perfom action
			parmas: 
				action: a list of array of 2 values
			returns:
				observation: a list of observation obtained after taking action
				reward: Reward obtained for taking action
				done: If episode ended due to action
				info: Additional info
		"""
		action = np.clip(action, -1., 1.)

		result = self.sio.call("step", data={'acceleration':action[0].__str__(), 'steering_angle':action[1].__str__()})
		result = json.loads(result)

		obs = result['observation'] # 10
		self.__updateObservations(obs)

		return self.observation, result['reward'], result['done']
	
	def reset(self):
		"""
			Reset environment after the episode has ended or going to start
			new episode.
			retruns:
				observation: Initial observation
		"""

		result = self.sio.call("reset", data={})
		result = json.loads(result)

		self.observation = np.tile(np.array(result['observation']), self.history_len) # 100
		
		return self.observation
		

