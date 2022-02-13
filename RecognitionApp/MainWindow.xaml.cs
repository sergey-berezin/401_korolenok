using System.Windows;
using System.IO;
using System.Drawing;
using System.Windows.Media.Imaging;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Library;

namespace RecognitionApp
{
    public partial class MainWindow : Window
    {
        public string PathToImages { get; set; }
        public CancellationTokenSource CTS { get; set; }
        public DataBaseContext dataBase;
        public MainWindow()
        {
            InitializeComponent();
            dataBase = new DataBaseContext();
        }

        private void Choose_Folder(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK || result == System.Windows.Forms.DialogResult.Yes)
            {
                PathToImages = dialog.SelectedPath;
            }
        }

        private async void StartRecognition(object sender, RoutedEventArgs e)
        {
            Images_listBox.Items.Clear();

            CTS = new CancellationTokenSource();
            RecognitionModel model = new RecognitionModel(PathToImages, CTS);
            ConcurrentQueue<RecognitionResponse> responseQueue = new ConcurrentQueue<RecognitionResponse>();

            Task recognizeTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    model.Recognize(responseQueue);
                }
                catch (TaskCanceledException)
                {
                    System.Windows.MessageBox.Show("Recognition was cancelled.");
                }
            });

            Task responseTask = Task.Factory.StartNew(() =>
            {
                while (recognizeTask.Status == TaskStatus.Running)
                {
                    while (responseQueue.TryDequeue(out var res) && res != null)
                    {
                        try {
                            using (Graphics g = Graphics.FromImage(res.image))
                            {
                                byte[] byteArrayImage = ToByteArray(res.image);
                                int hash = DataBaseContext.Hash(byteArrayImage);
                                foreach (var recognizedObject in res.objects)
                                {
                                    int x0 = Convert.ToInt32(recognizedObject.borders[0]);
                                    int y0 = Convert.ToInt32(recognizedObject.borders[1]);
                                    int w = Convert.ToInt32(recognizedObject.borders[2] - recognizedObject.borders[0]);
                                    int h = Convert.ToInt32(recognizedObject.borders[3] - recognizedObject.borders[1]);
                                    g.DrawRectangle(Pens.Red, x0, y0, w, h);
                                    g.DrawString(recognizedObject.label, new Font("Calibri", 16), Brushes.Green, new PointF(x0, y0));

                                    RecognitionObject obj = new RecognitionObject { type = recognizedObject.label, x0 = x0, x1 = x0 + w, y0 = y0, y1 = y0 + h };
                                    Image img = new Image { image = byteArrayImage, ImageHash=hash, objects=new List<RecognitionObject>() };
                                    dataBase.AddObject(img, obj);
                                    dataBase.SaveChanges();
                                }
                            }
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                System.Windows.Controls.Image display = new System.Windows.Controls.Image
                                {
                                    Source = ToBitmapImage(res.image),
                                    Width = 400
                                };
                                Images_listBox.Items.Add(display);
                            }));
                        } catch (Exception) { }
                    }
                }
            });
            await Task.WhenAll(recognizeTask, responseTask);
            MessageBox.Show("Recognition finished");
        }

        private void CancelRecognition(object sender, RoutedEventArgs e)
        {
            CTS.Cancel();
            MessageBox.Show("Recognition interrupted");
        }
        public static byte[] ToByteArray(Bitmap image)
        {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                return memoryStream.ToArray();
            }
        }
        public static BitmapImage ToBitmapImage(Bitmap image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }
    }
}
