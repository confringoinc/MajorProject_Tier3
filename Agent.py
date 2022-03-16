import tensorflow as tf
from tensorflow.keras.optimizers import Adam
import tensorflow_probability as tfp
from Networks import CriticNetwork, ValueNetwork, ActorNetwork
from Buffer import ReplayBuffer
import os
import json

class Agent:
	"""Agent will be ressponsible for being a mediatro between the models and env"""
	def __init__(self, input_dims, n_action, history_len=10,
		alpha=0.0001, beta=0.0001, gamma=0.99, tau=0.005, max_exp_size=10_00_000, noise=1e-6,
		fc_dims=[256, 100, 256], batch_size=256, reward_scale=2, chkpt_dir="./checkpoints"):
		"""
		Arguments:
			input_dims: Integer, Dimentions of observations
			n_action: Integer, Number of actions performed in env
			history_len: Integer, Number of observation to stack, to consider as one
			alpha: float, Leaning rate of actor network
			beta: float, Leaning rate of critic and value network
			gamma: float, Discount factor for determining importance of past reward
			tau: float, percentage of weight transfer from online net -> offline net
			max_exp_size: Integer, Maximum number of experience to store
			noise: float, Random noise to prevent log from getting to 0
			fc_dims: List of Integer, Dimension for network
			batch_size: Integer, Batch size to obtain for training nets
			reward_scale: float, Moving network towards more rewarding action
			chkpt_dir: String, Path to checkpoint dir to store data
		"""

		self.input_dims = input_dims
		self.n_action = n_action
		self.history_len = history_len
		self.alpha = alpha
		self.beta = beta
		self.gamma = gamma
		self.tau = tau
		self.max_exp_size = max_exp_size
		self.noise = noise
		self.fc_dims = fc_dims
		self.batch_size = batch_size
		self.scale = reward_scale
		self.chkpt_dir = chkpt_dir
		self.model_dir = os.path.join(self.chkpt_dir, "Models")
		self.buffer_dir = os.path.join(self.chkpt_dir, "Buffer")

		#	Create all necessary directories if not exists
		if not os.path.isdir(self.chkpt_dir):
			os.mkdir(self.chkpt_dir)
			print("Checkpoint dir created")

		if not os.path.isdir(self.model_dir):
			os.mkdir(self.model_dir)

		if not os.path.isdir(self.buffer_dir):
			os.mkdir(self.buffer_dir)

		#	Create a buffer to store info
		self.buffer = ReplayBuffer(self.max_exp_size, self.history_len,
								   self.input_dims, self.n_action, self.buffer_dir)

		#	Create networks
		self.actor = ActorNetwork(self.fc_dims, self.n_action)
		self.critic_1 = CriticNetwork(self.fc_dims)
		self.critic_2 = CriticNetwork(self.fc_dims)
		self.value = ValueNetwork(self.fc_dims)
		self.target_value = ValueNetwork(self.fc_dims)

		#	Compile Networks
		self.actor.compile(optimizer=Adam(learning_rate=self.alpha))
		self.critic_1.compile(optimizer=Adam(learning_rate=self.beta))
		self.critic_2.compile(optimizer=Adam(learning_rate=self.beta))
		self.value.compile(optimizer=Adam(learning_rate=self.beta))
		self.target_value.compile(optimizer=Adam(learning_rate=self.beta))

		self.update_target_params(tau=1.)

	def update_target_params(self, tau=None):
		"""Update the weights of target network by a factor of tau
		Arguments:
			tau: float, Updation value
		"""
		if tau is None:
			tau = self.tau

		weights = []
		for main_W, target_W in zip(self.value.get_weights(), self.target_value.get_weights()):
			weights.append(main_W*tau + target_W*(1-tau))

		self.target_value.set_weights(weights)

	def sample_normal(self, state):
		"""Provide the log probabilities of the actions along with action value
		Arguments:
			state: N-D Array, Array of observation with state of [obs_len, 1]
		Returns:
			a tensor tuple of action and log prob for action
		"""
		mu, sigma = self.actor(state)

		probabilities = tfp.distributions.Normal(mu, sigma, allow_nan_stats=False)

		#	Unsquased actions
		actions = probabilities.sample()
		log_prob = probabilities.log_prob(actions)

		#	Squash the actions in a desired range
		actions = tf.math.tanh(actions)
		
		log_prob -= tf.math.log(1-tf.math.pow(actions, 2)+self.noise)
		log_prob = tf.math.reduce_sum(log_prob, axis=1, keepdims=True)

		return actions, log_prob

	def get_action(self, state):
		"""Provide the log probabilities of the action with actions with action value
		Arguments:
			state: a N-D array of observation

		returns:
			a tuple of action and log_prob of action in a numpy version
		"""

		state = tf.convert_to_tensor([state], dtype=tf.float32)

		action, log_prob = self.sample_normal(state)

		return action[0].numpy(), log_prob[0].numpy()

	def remember(self, s, a, r, d):
		"""Method to store current step data to buffer to use it for training nets
		Arguments:
			s: N-D array of observation
			a: 1-D array of float specifying action
			reward: float, reward for taking the action
			done: bool, indicting the end of episode
		"""
					
		self.buffer.store_transition(s, a, r, d)

	def learn(self):
		"""Training the nets from past experience to improve later"""

		if self.buffer.count < self.batch_size:
			return

		state, action, reward, done, next_state = self.buffer.getBatchData(self.batch_size)
	
		state = tf.convert_to_tensor(state, dtype=tf.float32)
		action = tf.convert_to_tensor(action, dtype=tf.float32)
		reward = tf.convert_to_tensor(reward, dtype=tf.float32)
		done = tf.convert_to_tensor(done, dtype=tf.float32)
		next_state = tf.convert_to_tensor(next_state, dtype=tf.float32)

		#	Train Value Network
		with tf.GradientTape() as value_tape:
			value = self.value(state)
			current_policy_action, log_prob = self.sample_normal(state)
			log_prob = tf.squeeze(log_prob, axis=1)

			q1_new_policy = self.critic_1(state, current_policy_action)
			q2_new_policy = self.critic_2(state, current_policy_action)

			critic_value = tf.squeeze(tf.math.minimum(q1_new_policy, q2_new_policy),
									axis=1)

			#	value target is the resultant value
			value_target = critic_value - log_prob
			value_loss = 0.5 * tf.keras.losses.MSE(value_target, value)

		value_grad = value_tape.gradient(value_loss, self.value.trainable_variables)
		self.value.optimizer.apply_gradients(zip(value_grad, self.value.trainable_variables))

		#	Train actor network
		with tf.GradientTape() as actor_tape:
			new_policy_action, log_prob = self.sample_normal(state)
			log_prob = tf.squeeze(log_prob, axis=1)

			q1_new_policy = self.critic_1(state, new_policy_action)
			q2_new_policy = self.critic_2(state, new_policy_action)

			critic_value = tf.squeeze(tf.math.minimum(q1_new_policy, q2_new_policy),
										axis=1)
			actor_loss = log_prob - critic_value
			actor_loss = tf.math.reduce_mean(actor_loss)

		actor_grad = actor_tape.gradient(actor_loss, self.actor.trainable_variables)
		self.actor.optimizer.apply_gradients(zip(actor_grad, self.actor.trainable_variables))

		#	Train both critic Networks
		with tf.GradientTape(persistent=True) as critic_tape:
			value_ = self.target_value(next_state)

			q_hat = self.scale*reward + (self.gamma * value_ * (1-done))
			
			q1_old_policy = self.critic_1(state, action)
			q2_old_policy = self.critic_2(state, action)

			critic_1_loss = 0.5 * tf.keras.losses.MSE(q_hat, q1_old_policy)
			critic_2_loss = 0.5 * tf.keras.losses.MSE(q_hat, q2_old_policy)

		critic_1_grad = critic_tape.gradient(critic_1_loss, self.critic_1.trainable_variables)
		self.critic_1.optimizer.apply_gradients(zip(critic_1_grad, self.critic_1.trainable_variables))

		critic_2_grad = critic_tape.gradient(critic_2_loss, self.critic_2.trainable_variables)
		self.critic_2.optimizer.apply_gradients(zip(critic_2_grad, self.critic_2.trainable_variables))

		#	After traing all networks update target net
		self.update_target_params()

	def save_models(self):
		"""Save all models to checkpoint dir in models folder"""

		if self.buffer.count > self.batch_size:
			self.actor.save(self.model_dir + "/actor", save_format="tf")
			self.critic_1.save(self.model_dir+"/critic_1", save_format="tf")
			self.critic_2.save(self.model_dir+"/critic_2", save_format="tf" )
			self.value.save(self.model_dir+"/value", save_format="tf")
			self.target_value.save(self.model_dir+"/target_value", save_format="tf")

	def load_models(self):
		"""Laod all models"""
		self.actor = tf.keras.models.load_model(self.model_dir + "/actor")
		self.critic_1 = tf.keras.models.load_model(self.model_dir+"/critic_1")
		self.critic_2 = tf.keras.models.load_model(self.model_dir+"/critic_2")
		self.value = tf.keras.models.load_model(self.model_dir+"/value")
		self.target = tf.keras.models.load_model(self.model_dir+"/target_value")

	def __isJsonSerialiable(self, obj):
		try:
			json.dumps(obj)
			return True
		except:
			return False

	def saveVars(self):
		
		varsDict = []
				
		#	Add only those object that are serialiable
		for key, value in self.__dict__.items():
			if self.__isJsonSerialiable(value):
				varsDict[key] = value

		with open(os.path.join(self.chkpt_dir,'agent.json'), 'w') as agent:
			json.dump(varsDict, agent)

	def loadVars(self):
		f = open(os.path.join(self.chkpt_dir, 'agent.json'), 'r')
		data = json.load(f)

		for key, value in data.items():
			if key in self.__dict__:
				self.__dict__[key] = value


	def save(self):
		self.save_models()	
		self.buffer.save()

		self.saveVars()

		print("<---- Data Saved ---->")

	def load(self):
		self.load_model()
		self.buffer.load()
		self.loadVars()

		print("<---- Data Loaded ---->")
