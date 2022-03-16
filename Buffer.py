import numpy as np
import os
import json

class ReplayBuffer(object):
	"""Replay Buffer to store transition for training the SAC network from past experiences"""
	def __init__(self, max_exp_size, history_len, input_dim, n_action, dir_name='./BufferData'):
		"""	
		Arguments:
			max_exp_size: Integer, Maximum number of experiences to store
			history_len: Intenger, Number of obs to stack to create a stack for agent
			input_dim: Tupple, Define the shape of the observation
			n_action: Integer, Stating the number of action an agent can take

		"""
		self.max_exp_size = max_exp_size
		
		self.input_dim = input_dim
		self.n_action = n_action
		self.history_len = history_len
		self.dir_name = dir_name

		if not os.path.isdir(self.dir_name):
			os.mkdir(self.dir_name)

		self.state = np.empty((self.max_exp_size, *self.input_dim), dtype=np.float32)
		self.action = np.empty((self.max_exp_size, self.n_action), dtype=np.float32)
		self.reward = np.empty(self.max_exp_size, dtype=np.float32)
		self.done = np.empty(self.max_exp_size, dtype=np.float32)

		self.count = 0
		self.current = 0

	def store_transition(self, s, a, r, d):
		"""Store an experience for a timestamp
		Arguments:
			s: An array of observation obtained from environment
			a: An array of size (n_action,) where each value is in range (-1, 1)
			r: A float specifying the perfomance for taking an action
			d: A bool indicating the termination of an episode

		"""
		self.state[self.current] = s
		self.action[self.current] = a
		self.reward[self.current] = r
		self.done[self.current] = d

		self.current = (self.current+1) % self.max_exp_size
		self.count = max(self.count, self.current)

	def getBatchData(self, batch_size):
		"""Returns a batch for training the agent
		Arguments:
			batch_size: Integer, specifying the size of batch to train Agent on

		Returns:
			A tupple containing state, action, reward, done, next_state
		"""
		# index = []

		# #	Loop untill the index list if filled
		# while len(index) < batch_size:
		# 	idx = np.random.randint(self.history_len, self.count)

		# 	# if index coverlaps current episode then continue
		# 	# if idx > self.current and idx-self.history_len <= self.current:
		# 	# 	continue

		# 	#	take a batch of single episode at a time
		# 	if self.done[idx-self.history_len:idx].any():
		# 		continue

		# 	index.append(idx)


		# states = []
		# next_states = []

		# #	Shape of state and next_state will be (batch_Size, hist_len, obs_size)
		# for idx in index:
		# 	states.append(self.state[idx-self.history_len:idx])
		# 	next_states.append(self.state[idx-self.history_len+1:idx+1])

		# states = np.array(states)
		# next_states = np.array(next_states)

		batch = np.random.choice(self.count, size=batch_size)
		s = self.state[batch]
		a = self.action[batch]
		r = self.reward[batch]
		d = self.done[batch]
		s_ = self.state[batch+1]
		
		return s, a, r, d, s_

	def save(self):
		"""Save the buffer to specified folder"""
		np.save(self.dir_name + "/state.npy", self.state)
		np.save(self.dir_name + "/action.npy", self.action)
		np.save(self.dir_name + "/reward.npy", self.reward)
		np.save(self.dir_name + "/done.npy", self.done)

		varDir = {
			'count': self.count,
			'current': self.current
		}

		with open(os.path.join(self.dir_name, 'vars.json'), 'w') as jfile:
			json.dump(varDir, jfile)


	def load(self, folder_name=None):
		"""Load the buffer from a specified folder
		
		Argument:
			folder_name: a string telling the folder where buffer are store. If None then
						 default folder will be used to find buffer
		"""
		if folder_name is not None:
			self.dir_name = folder_name

		self.state = np.load(self.dir_name + "/state.npy")
		self.action = np.load(self.dir_name + "/action.npy")
		self.reward = np.load(self.dir_name + "/reward.npy")
		self.done = np.load(self.dir_name + "/done.npy")

		f = open(os.path.join(self.dir_name, 'vars.json'), 'r')
		data = json.load(f)
		self.count = data['count']
		self.current = data['current']

		print("<---- Buffer Loaded ---->")