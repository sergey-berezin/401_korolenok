using Microsoft.ML;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace Library
{
    public class RecognitionModel
    {
        static readonly ConcurrentQueue<Bitmap> bitmapQueue = new ConcurrentQueue<Bitmap>();
        public CancellationTokenSource cts {get; set;}
        public string DirPath { get; set; }
        private static readonly FileInfo _dataRoot = new FileInfo(typeof(RecognitionModel).Assembly.Location);
        private static readonly string modelPath = Path.Combine(_dataRoot.Directory.FullName, @"C:\Users\Alex\Desktop\Учеба\4 курс\ConsoleApp1\yolov4.onnx");
        private static readonly string[] classes = new string[] { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear",
            "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat",
            "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
            "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet",
            "tvmonitor", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase",
            "scissors", "teddy bear", "hair drier", "toothbrush" };

        public RecognitionModel(string dir, CancellationTokenSource cancellationTokenSource)
        {
            DirPath = dir;
            cts = cancellationTokenSource;
        }

        public void Recognize(ConcurrentQueue<RecognitionResponse> responseQueue)
        {
            MLContext mlContext = new MLContext();
            var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0", scaleImage: 1f / 255f, interleavePixelColors: true))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelPath, recursionLimit: 100));
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));
            var predictionEngine = mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);

            int n_images = Directory.GetFiles(DirPath).Length;
            int cnt = 0;
            
            List<Task> tasks = new List<Task>();
            var locker = new Object();

            foreach (string imagePath in Directory.GetFiles(DirPath))
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        bitmapQueue.Enqueue(new Bitmap(Image.FromFile(imagePath)));
                    }
                }, cts.Token));
            }
            for (int j = 0; j < n_images; ++j)
            {
                while (bitmapQueue.Count == 0) { }
                bitmapQueue.TryDequeue(out Bitmap bitmap);

                var predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    RecognitionResponse response = new RecognitionResponse(bitmap);
                    Dictionary<string, int> detected = new Dictionary<string, int>();
                    if (!cts.Token.IsCancellationRequested)
                    {
                        var results = predict.GetResults(classes, 0.3f, 0.7f);
                        foreach (var res in results)
                        {
                            if (detected.ContainsKey(res.Label))
                                detected[res.Label]++;
                            else
                                detected.Add(res.Label, 1);

                            var coords = new List<float> { res.BBox[0], res.BBox[1], res.BBox[2], res.BBox[3] };
                            response.objects.Add(new ImageObject(res.Label, coords));
                        }
                        response.ObjectsDict = detected;

                        lock (locker)
                        {
                            response.progress = Convert.ToInt32(100 * ++cnt / n_images);
                        }
                        responseQueue.Enqueue(response);
                    }
                    return response;
                }, cts.Token));
            }
            Task.WaitAll(tasks.ToArray());
        }
    }
}