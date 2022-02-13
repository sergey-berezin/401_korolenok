using System.Collections.Generic;

namespace Library
{
    public class RecognitionResponse
    {
        public Dictionary<string, int> ObjectsDict { get; set; }
        public int progress { get; set; }
        public List<ImageObject> objects { get; set; }
        public System.Drawing.Bitmap image { get; set; }
        public RecognitionResponse(System.Drawing.Bitmap img)
        {
            ObjectsDict = new Dictionary<string, int>();
            objects = new List<ImageObject>();
            progress = 0;
            image = img;
        }
    }

    public class ImageObject
    {
        public string label { get; set; }
        public List<float> borders { get; set; }
        public ImageObject(string name, List<float> coords)
        {
            this.borders = new List<float>(coords);
            this.label = new string(name);
        }
    }
}