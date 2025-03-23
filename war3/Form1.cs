using Microsoft.ML;
using Microsoft.ML.Transforms.Image;
using System;
using System.Drawing.Imaging;
using System.Windows.Forms;
using war3.Models;

namespace war3
{
    public partial class Form1 : Form
    {

        public const int rowCount = 13, columnCount = 13;

        public const int featuresPerBox = 5;

        private static readonly (float x, float y)[] boxAnchors = { (0.573f, 0.677f), (1.87f, 2.06f), (3.34f, 5.47f), (7.88f, 3.53f), (9.77f, 9.17f) };

        private PredictionEngine<BugInput, BugPrediction> _predictionEngine;
        public Form1()
        {
            InitializeComponent();

            // Make it so that the predicted picture is not visible at first
            picPrediction.Visible = false;

            // Create a ML context for the model
            var mlContext = new MLContext();

            // Define an empty dataset using BugInput
            var placeholderData = new List<BugInput>();

            // Converts the empty dataset into a format that ML.NET understands
            //this line
            var inputData = mlContext.Data.LoadFromEnumerable(placeholderData);

            // Constructs an image processing pipeline
            var imageProcessingPipeline = mlContext.Transforms
                .ResizeImages(
                    resizing: ImageResizingEstimator.ResizingKind.Fill,
                    outputColumnName: "ProcessedImage",
                    imageWidth: ImageSettings.imageWidth,
                    imageHeight: ImageSettings.imageHeight,
                    inputColumnName: nameof(BugInput.Image))
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "ProcessedImage"))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    modelFile: @".MLModel\model.onnx",
                    outputColumnName: "grid",
                    inputColumnName: "ProcessedImage"));

            // Trains the model using the empty dataset
            var trainedModel = imageProcessingPipeline.Fit(inputData);

            _predictionEngine = mlContext.Model.CreatePredictionEngine<BugInput, BugPrediction>(trainedModel);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                // Loads the selected image
                var selectedImage = new Bitmap(openFileDialog1.FileName);

                // Performs the prediction onto the selected image
                var predictionResult = _predictionEngine.Predict(new BugInput());

                // Loads the labels for classification
                var classLabels = File.ReadAllLines("./MLModel/labels.txt");

                // Processes the models outputs into a bounding box
                var detectedBoxes = ParseOutputs(predictionResult.BugType, classLabels);

                // Grabs the dimensions of the original image
                var imgWidth = selectedImage.Width;
                var imgHeight = selectedImage.Height;

                if (detectedBoxes.Count > 1)
                {
                    // Selects the bounding box with the highest confidence
                    detectedBoxes = new List<BoundingBox>
        {
            detectedBoxes.OrderByDescending(b => b.Confidence).First()
        };
                }
                else if (!detectedBoxes.Any())
                {
                    MessageBox.Show("No prediction available for this image.");
                    return;
                }

                // Draws the bounding box onto the image
                using (var graphics = Graphics.FromImage(selectedImage))
                {
                    foreach (var box in detectedBoxes)
                    {
                        float x = Math.Max(box.Dimensions.X, 0);
                        float y = Math.Max(box.Dimensions.Y, 0);
                        float width = Math.Min(imgWidth - x, box.Dimensions.Width);
                        float height = Math.Min(imgHeight - y, box.Dimensions.Height);

                        // Adjusts the bounding box so it matches the scale of the chosen image
                        x = imgWidth * x / ImageSettings.imageWidth;
                        y = imgHeight * y / ImageSettings.imageHeight;
                        width = imgWidth * width / ImageSettings.imageWidth;
                        height = imgHeight * height / ImageSettings.imageHeight;

                        // Draws the bounding box and adds the label
                        using (var pen = new Pen(Color.Red, 3))
                        {
                            graphics.DrawRectangle(pen, x, y, width, height);
                        }

                        using (var font = new Font(FontFamily.Families[0], 30f))
                        {
                            graphics.DrawString(box.Description, font, Brushes.Red, x + 5, y + 5);
                        }
                    }
                }

                // Outputs the imagw
                picPrediction.Image = selectedImage;
                picPrediction.SizeMode = PictureBoxSizeMode.AutoSize;
                picPrediction.Visible = true;
            }

        }

        public static List<BoundingBox> ParseOutputs(float[] modelOutput, string[] labels, float probabilityThreshold = .5f)
        {
            var detectedBoxes = new List<BoundingBox>();

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                for (int colIndex = 0; colIndex < columnCount; colIndex++)
                {
                    for (int anchorIndex = 0; anchorIndex < boxAnchors.Length; anchorIndex++)
                    {
                        int channelOffset = anchorIndex * (labels.Length + featuresPerBox);

                        var predictedBox = ExtractBoundingBoxPrediction(modelOutput, rowIndex, colIndex, channelOffset);
                        var mappedBox = MapBoundingBoxToCell(rowIndex, colIndex, anchorIndex, predictedBox);

                        if (predictedBox.Confidence < probabilityThreshold)
                            continue;

                        float[] classScores = ExtractClassProbabilities(
                            modelOutput, rowIndex, colIndex, channelOffset, predictedBox.Confidence, labels);

                        var bestMatch = classScores
                            .Select((score, index) => new { Score = score, Index = index })
                            .OrderByDescending(item => item.Score)
                            .FirstOrDefault();

                        if (bestMatch == null || bestMatch.Score < probabilityThreshold)
                            continue;

                        detectedBoxes.Add(new BoundingBox
                        {
                            Dimensions = mappedBox,
                            Confidence = bestMatch.Score,
                            Label = labels[bestMatch.Index]
                        });
                    }
                }
            }

            return detectedBoxes;
        }

        private static BoundingBoxDimensions MapBoundingBoxToCell(int row, int column, int box, BoundingBoxPrediction boxDimensions)
        {
            // Calculates the sizes of the grid cells
            const float gridCellWidth = ImageSettings.imageWidth / columnCount;
            const float gridCellHeight = ImageSettings.imageHeight / rowCount;

            // Adjusts the dimensions of the bounding box
            var adjustedBoundingBox = new BoundingBoxDimensions
            {
                X = (row + Sigmoid(boxDimensions.X)) * gridCellWidth,
                Y = (column + Sigmoid(boxDimensions.Y)) * gridCellHeight,
                Width = MathF.Exp(boxDimensions.Width) * gridCellWidth * boxAnchors[box].x,
                Height = MathF.Exp(boxDimensions.Height) * gridCellHeight * boxAnchors[box].y,
            };

            // Centres the bounding box around the midpoint
            adjustedBoundingBox.X -= adjustedBoundingBox.Width / 2;
            adjustedBoundingBox.Y -= adjustedBoundingBox.Height / 2;

            return adjustedBoundingBox;
        }

        private static BoundingBoxPrediction ExtractBoundingBoxPrediction(float[] modelOutput, int row, int column, int channel)
        {
            return new BoundingBoxPrediction
            {
                X = modelOutput[GetOffset(row, column, channel++)],
                Y = modelOutput[GetOffset(row, column, channel++)],
                Width = modelOutput[GetOffset(row, column, channel++)],
                Height = modelOutput[GetOffset(row, column, channel++)],
                Confidence = Sigmoid(modelOutput[GetOffset(row, column, channel++)])
            };
        }

        public static float[] ExtractClassProbabilities(float[] modelOutput, int row, int column, int channel, float confidence, string[] labels)
        {
            // Get the starting offset for class probabilities
            int probabilityStartIndex = channel + featuresPerBox;

            // Extracts the class probabilities from model output
            float[] extractedProbabilities = labels
                .Select((_, index) => modelOutput[GetOffset(row, column, probabilityStartIndex + index)])
                .ToArray();

            // Apply Softmax and scale by confidence score
            return Softmax(extractedProbabilities)
                .Select(probability => probability * confidence)
                .ToArray();
        }

        private static float Sigmoid(float value)
        {
            var k = (float)Math.Exp(value);
            return k / (1.0f + k);
        }

        private static float[] Softmax(float[] classProbabilities)
        {
            var max = classProbabilities.Max();
            var exp = classProbabilities.Select(v => Math.Exp(v - max));
            var sum = exp.Sum();
            return exp.Select(v => (float)v / (float)sum).ToArray();
        }

        private static int GetOffset(int row, int column, int channel)
        {
            const int channelStride = rowCount * columnCount;
            return (channel * channelStride) + (column * columnCount) + row;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
    }

    class BoundingBoxPrediction : BoundingBoxDimensions
    {
        public float Confidence { get; set; }
    }
}
