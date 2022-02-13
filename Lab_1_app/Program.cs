using Library;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace App
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Enter a path to the images' directory:");
            string dir = Console.ReadLine();
            var cts = new System.Threading.CancellationTokenSource();
            RecognitionModel model = new RecognitionModel(dir, cts);
            int n_images = Directory.GetFiles(dir).Length;
            var responseQueue = new ConcurrentQueue<RecognitionResponse>();
            var task = Task.Run(() =>
            {
                if (!cts.Token.IsCancellationRequested)
                {
                    for (int i = 0; i < n_images; i++)
                    {
                        while (responseQueue.Count == 0) { }
                        responseQueue.TryDequeue(out var res);
                        Console.Write($"\n{res.progress}% done. ");
                        foreach (var obj in res.ObjectsDict)
                            Console.Write($"{obj.Key}: {obj.Value}, ");
                    }
                }
            }, cts.Token);
            model.Recognize(responseQueue);
            task.Wait();
        }
    }
}