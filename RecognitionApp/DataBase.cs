using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;


namespace RecognitionApp
{
    public class Image
    {
        public int ImageID { get; set; }
        public int ImageHash { get; set; }
        public byte[] image { get; set; }
        virtual public List<RecognitionObject> objects { get; set; }
    }

    public class RecognitionObject
    {
        public int RecognitionObjectID { get; set; }
        public String type { get; set; }
        public int x0 { get; set; }
        public int y0 { get; set; }
        public int x1 { get; set; }
        public int y1 { get; set; }

        public bool Equals(RecognitionObject other)
        {
            return type == other.type && x0 == other.x0 && x1 == other.x1 && y0 == other.y0 && y1 == other.y1; 
        }
    }

    public class DataBaseContext : DbContext
    {
        public string dataBasePath { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<RecognitionObject> Objects { get; set; }
    
        public DataBaseContext()
        {
            var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dataBasePath = System.IO.Path.Join(folderPath, "recognition_database.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder o)
        => o.UseLazyLoadingProxies().UseSqlite($"Data Source={dataBasePath}");

        public static bool compareItems(Image first, Image second)
        {
            /* returns True if elements are equal and False otherwise */
            return first.ImageHash == second.ImageHash && first.image.SequenceEqual(second.image);
            //first.image == second.image;
        }
        public static int Hash(byte[] image)
        {
            int x = 0;
            const int div = 1234567891, k = 198347, b = 756912;
            for (int i = 0; i < image.Length; i++)
            {
                x <<= 1;
                x += image[i];
                x %= div;
            }
            return (k * x + b) % div;
        }

        public bool ImageInDataBase(Image image)
        {
            var query = Images;
            foreach (var database_image in query)
            {
                if (compareItems(image, database_image))
                    return true;
            }
            return false;
        }

        public void AddObject(Image image, RecognitionObject recognitionObject)
        {
            if (!ImageInDataBase(image))
            {
                image.objects.Add(recognitionObject);
                Add(image);
            }
            else
            {
                image.objects.Add(recognitionObject);
                var database_image = Images.Where(o => o.ImageHash == image.ImageHash && o.image.SequenceEqual(image.image)).First();
                if (database_image.objects.Where(o => recognitionObject.Equals(o)).Count() == 0)
                {
                    database_image.objects.Add(recognitionObject);
                }
            }
        }
    }
}