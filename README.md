image-classification-dbn
========================

Image classification using a Deep Belief Network with multiple layers of Restricted Boltzmann Machines.

Written in C# and uses the Accord.NET machine learning library.

Lazily threw together some code to create a deep net where weights are initialized via unsupervised training in the hidden layers and then trained further using backpropagation.

The data is included, I used Caltech 101.

There are a handful of parameters you can configure. Number of epochs you can control through the UI, but the rest, which would affect the architecture of the net, I leave in the code. They are all in one place though (member variables of the main class) so they should be easy to change.

- Number of categories - I set it to 5 currently, but it can go up to all 101 categories. Will take much longer to train all 101.
- Number of examples - To balance the labels used for training I make it so that you can only use up to 31 samples (the min number of images for a category).
- Number of training samples - Should be something less than total number of examples, so there is something to test on.
- Width and height - By default I downsample the image by 10 in both directions. This sped up training a reasonable amount for development purposes, but you could increase this later given you have the computational power to do so. Also, it would help greatly with reconstructing images later to see what the network has learned.
- Layers - Define the architecture of the network by specifying how many neurons are in each layer. There must be NUM_CATEGORIES in the last layer.

Performance:
- Despite downsampling the image by 10x10, we still get very good performance. With only 2 categories I consistently achieve a perfect score on the test set. With 5 categories I get > 80% and with 10 categories I get > 66%, which are all significantly better than randomly guessing, which would give 1/N.

Usage:
- Training: Click the train button.
- Prediction: Click "choose image" to choose a test image (should be approx. 3x2 aspect ratio, and obviously the object should be something similar compared to that seen in the data). Then click the "Classify" button.
- "Get Data" was more for debugging purposes, it just loads up the data and produces the categories in the lower left list box.
- Because training takes so long, I provide you with the "Save DBN" and "Load DBN" buttons to save and load a trained DBN.
- To reconstruct an image given a label, or by activating a particular neuron, enter the neuron index and layer index beside the "reconstruct" button and then click the button. Accord.NET also lets you probabilistically generate inputs given an output, so I included a button for that too. You still have to enter neuron and layer but it only uses the neuron.
- Of course, reconstructing something from a hidden layer there are actually 2^H possible reconstructions where H=number of nodes in the layer. Another thing that could be done is to randomly reconstruct an image for a layer given a random choice of the 2^H activations. This might lead to more meaningful pictures.
