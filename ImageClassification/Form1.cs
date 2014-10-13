using Accord.Imaging.Converters;
using Accord.Math;
using Accord.Neuro;
using Accord.Neuro.ActivationFunctions;
using Accord.Neuro.Learning;
using Accord.Neuro.Networks;
using Accord.Statistics;
using AForge.Neuro.Learning;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageClassification
{
    public partial class Form1 : Form
    {
        private static string PREFIX = "../../data/101_ObjectCategories";
        private static int NUM_CATEGORIES = 5; // can be up to 101
        private static int UNSUPERVISED_EPOCHS = 200; // originally 200
        private static int SUPERVISED_EPOCHS = 300; // originally 500
        private static int NUM_EXAMPLES = 30; // must be <= 31
        private static int NUM_TRAIN = 20; // must be < NUM_EXAMPLES to have something to test
        private static int WIDTH = 30; // standard width for images used here
        private static int HEIGHT = 20; // standard height for images used here
        private static int[] LAYERS = new int[] { 600, 400, NUM_CATEGORIES, NUM_CATEGORIES }; // architecture of the net

        private static ImageToArray _itoa = new ImageToArray(min: 0, max: 1);
        private static ArrayToImage _atoi = new ArrayToImage(WIDTH, HEIGHT, min: 0.0, max: 1.0);
        private DeepBeliefNetwork _network;
        private Bitmap _imageToClassify;
        private string[] _categories;

        public Form1()
        {
            InitializeComponent();

            UpdateLayerDescription();
            txtUnsupervised.Text = UNSUPERVISED_EPOCHS.ToString();
            txtSupervised.Text = SUPERVISED_EPOCHS.ToString();
            txtCategories.Text = NUM_CATEGORIES.ToString();
        }

        private void UpdateLayerDescription(){
            label3.Text += "\n";
            for (int i = 0; i < LAYERS.Length; i++)
            {
                label3.Text += "Layer " + i + " has " + LAYERS[i] + " neurons.\n";
            }
            label3.Refresh();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void txtUnsupervised_Changed(object sender, EventArgs e)
        {
            UNSUPERVISED_EPOCHS = int.Parse(txtUnsupervised.Text);
            UpdateLayerDescription();
        }

        private void txtSupervised_Changed(object sender, EventArgs e)
        {
            SUPERVISED_EPOCHS = int.Parse(txtSupervised.Text);
            UpdateLayerDescription();
        }

        private void txtCategories_Changed(object sender, EventArgs e)
        {
            NUM_CATEGORIES = int.Parse(txtCategories.Text);
            // TODO: need to update LAYERS
            UpdateLayerDescription();
        }

        private void Classify(object sender, EventArgs e)
        {
            if (_imageToClassify == null)
            {
                label1.Text = "You didn't choose an image!\n";
                label1.Refresh();
                return;
            }

            double[] input;
            _itoa.Convert(_imageToClassify, out input);
            double[] output = _network.Compute(input);
            label1.Text = "Prediction: " + _categories[GetResult(output)];
            label1.Refresh();
        }

        private void Recall(bool reconstruct)
        {
            string[] sp = textBox1.Text.Split(',');
            if (sp.Length != 2) {
                label1.Text = "You need to enter <neuron>,<layer>!";
                label1.Refresh();
                return;
            }
            try
            {
                int neuron = int.Parse(sp[0]);
                int layer = int.Parse(sp[1]);
                string c = (layer == LAYERS.Length - 1) ? listBox1.Items[neuron].ToString() : "(not a category)";
                
                double[] a = (reconstruct) ? new double[LAYERS[layer]] : new double[NUM_CATEGORIES];
                a[neuron] = 1;

                double[] r = (reconstruct) ? _network.Reconstruct(a, layer) : _network.GenerateInput(a);
                Bitmap bm;
                _atoi.Convert(r, out bm);

                label1.Text = "Reconstructing " + c + ", length of reconstruction: " + r.Length;
                label1.Refresh();

                pictureBox1.Image = bm;
                pictureBox1.Refresh();
            } 
            catch (Exception ex)
            {
                label1.Text = ex.Message + "\n" + ex.StackTrace + "\n";
                label1.Text += "Reconstruction input params invalid. neuron should be < size of layer.";
                label1.Refresh();
            }
        }

        private void reconstructButton_Click(object sender, EventArgs e)
        {
            Recall(true);
        }

        private void generateInputButton_Clicked(object sender, EventArgs e)
        {
            Recall(false);
        }

        private void getDataButton_Clicked(object sender, EventArgs e)
        {
            double[][] inputs;
            double[][] outputs;
            double[][] testInputs;
            double[][] testOutputs;
            GetData(out inputs, out outputs, out testInputs, out testOutputs);
        }

        private void train_Click(object sender, EventArgs e)
        {
            double[][] inputs;
            double[][] outputs;
            double[][] testInputs;
            double[][] testOutputs;
            GetData(out inputs, out outputs, out testInputs, out testOutputs);

            Stopwatch sw = Stopwatch.StartNew();

            // Setup the deep belief network and initialize with random weights.
            _network = new DeepBeliefNetwork(inputs.First().Length, LAYERS);
            new GaussianWeights(_network, 0.1).Randomize();
            _network.UpdateVisibleWeights();

            // Setup the learning algorithm.
            DeepBeliefNetworkLearning teacher = new DeepBeliefNetworkLearning(_network)
            {
                Algorithm = (h, v, i) => new ContrastiveDivergenceLearning(h, v)
                {
                    LearningRate = 0.1,
                    Momentum = 0.5,
                    Decay = 0.001,
                }
            };

            // Setup batches of input for learning.
            int batchCount = Math.Max(1, inputs.Length / 100);
            // Create mini-batches to speed learning.
            int[] groups = Accord.Statistics.Tools.RandomGroups(inputs.Length, batchCount);
            double[][][] batches = inputs.Subgroups(groups);
            // Learning data for the specified layer.
            double[][][] layerData;

            // Unsupervised learning on each hidden layer, except for the output layer.
            for (int layerIndex = 0; layerIndex < _network.Machines.Count - 1; layerIndex++)
            {
                teacher.LayerIndex = layerIndex;
                layerData = teacher.GetLayerInput(batches);
                for (int i = 0; i < UNSUPERVISED_EPOCHS; i++)
                {
                    double error = teacher.RunEpoch(layerData) / inputs.Length;
                    if (i % 10 == 0)
                    {
                        label1.Text = "Layer: " + layerIndex + " Epoch: " + i + ", Error: " + error;
                        label1.Refresh();
                    }
                }
            }

            // Supervised learning on entire network, to provide output classification.
            var teacher2 = new BackPropagationLearning(_network)
            {
                LearningRate = 0.1,
                Momentum = 0.5
            };

            // Run supervised learning.
            for (int i = 0; i < SUPERVISED_EPOCHS; i++)
            {
                double error = teacher2.RunEpoch(inputs, outputs) / inputs.Length;
                if (i % 10 == 0)
                {
                    label1.Text = "Supervised: " + i + ", Error = " + error;
                    label1.Refresh();
                }
            }

            // Test the resulting accuracy.
            label1.Text = "";
            int correct = 0;
            for (int i = 0; i < testInputs.Length; i++)
            {
                double[] outputValues = _network.Compute(testInputs[i]);
                int y = GetResult(outputValues);
                int t = GetResult(testOutputs[i]);
                label1.Text += "predicted: " + y + " actual: " + t + "\n";
                label1.Refresh();
                if (y == t)
                {
                    correct++;
                }
            }
            sw.Stop();

            label1.Text = "Correct " + correct + "/" + testInputs.Length + ", " + Math.Round(((double)correct / (double)testInputs.Length * 100), 2) + "%";
            label1.Text += "\nElapsed train+test time: " + sw.Elapsed;
            label1.Refresh();
        }

        int GetResult(double[] output)
        {
            return output.ToList().IndexOf(output.Max());
        }

        private void chooseImage_Click(object sender, EventArgs e)
        {
            // Show the Open File dialog. If the user clicks OK, load the 
            // picture that the user chose. 
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //double[] array;
                Bitmap image = (Bitmap)Bitmap.FromFile(openFileDialog1.FileName, true);
                _imageToClassify = ShrinkImage(image);
                // itoa.Convert(image, out array);

                //Bitmap im2;
                //atoi.Convert(array, out im2);

                //ImageBox.Show(array, image.Width, image.Height, PictureBoxSizeMode.Zoom);
                pictureBox1.Load(openFileDialog1.FileName);
                //pictureBox1.Image = im2;
                //pictureBox1.Refresh();
            }
        }

        private void saveButton_Clicked(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Stream st;
                if ((st = saveFileDialog1.OpenFile()) != null)
                {
                    _network.Save(st);
                    st.Close();
                }
            }
        }

        private void loadButton_Clicked(object sender, EventArgs e)
        {
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                _network = DeepBeliefNetwork.Load(openFileDialog2.FileName);
            }
        }

        static Bitmap ShrinkImage(Bitmap bmp)
        {
            Bitmap bmp2 = new Bitmap(WIDTH, HEIGHT);
            Graphics g = Graphics.FromImage(bmp2);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, 0, 0, bmp2.Width, bmp2.Height);
            g.Dispose();
            return bmp2;
        }

        void GetData(out double[][] r_inputs, out double[][] r_outputs, out double[][] test_inputs, out double[][] test_outputs)
        {
            //ImageToArray conv = new ImageToArray(min: 0, max: 1);

            List<double[]> inputs = new List<double[]>();
            List<double[]> outputs = new List<double[]>();
            List<double[]> t_inputs = new List<double[]>();
            List<double[]> t_outputs = new List<double[]>();
            List<string> cat_idx = new List<string>();
            List<string> short_cats = new List<string>();

            string prefix = PREFIX;
            string[] categories = Directory.GetDirectories(prefix);
            int min = 10000;
            double[] input;
            label1.Text = "";
            for (int i = 0; i < NUM_CATEGORIES; i++)
            {
                string c = categories[i];
                cat_idx.Add(c);

                string[] split = c.Split('\\');
                short_cats.Add(split.Last());
                string[] files = Directory.GetFiles(c, "*.jpg");
                label1.Text += c + " => " + files.Length + " files.\n";
                label1.Refresh();
                if (files.Length < min) min = files.Length;

                int added = 0;
                foreach (string f in files)
                {
                    Bitmap image = (Bitmap)Bitmap.FromFile(f, true);
                    if (image.Width < 300 || image.Height < 180) continue;

                    // crop the image
                    image = image.Clone(new Rectangle(0, 0, 300, Math.Min(180,200)), image.PixelFormat);

                    // downsample the image to save memory
                    Bitmap small_image = ShrinkImage(image);
                    image.Dispose();

                    _itoa.Convert(small_image, out input);
                    small_image.Dispose();

                    //Console.WriteLine("Length of input: " + input.Length);

                    double[] output = new double[NUM_CATEGORIES];
                    output[i] = 1;

                    added++;

                    if (added <= NUM_TRAIN)
                    {
                        inputs.Add(input);
                        outputs.Add(output);
                    }
                    else
                    {
                        t_inputs.Add(input);
                        t_outputs.Add(output);
                    }
                    if (added >= NUM_EXAMPLES) break;
                }
            }
            label1.Text += "Number of categories: " + categories.Length + " min files: " + min + " number of short cats: " + short_cats.Count();

            listBox1.Items.Clear();
            for (int i = 0; i < short_cats.Count; i++)
                listBox1.Items.Add(short_cats[i] + ", " + i);

            _categories = short_cats.ToArray();
            r_inputs = inputs.ToArray();
            r_outputs = outputs.ToArray();
            test_inputs = t_inputs.ToArray();
            test_outputs = t_outputs.ToArray();
        }
    }
}
