class A:
	def __init__(self):
		self.a = 1
		self.b = 2
		self.c = 3

	def printVar(self):
		print("a = ", self.a, " | b = ", self.b, " | c = ", self.c)

	def vars(self):
		varDict = self.__dict__

		keys_to_delete = []

		for key, value in varDict.items():
			if key == 'a':
				keys_to_delete.append(key)

		for key in keys_to_delete:
			varDict.pop(key, None)


if __name__ == "__main__":

	obj = A()
	obj.printVar()

	obj.vars()
	obj.printVar()