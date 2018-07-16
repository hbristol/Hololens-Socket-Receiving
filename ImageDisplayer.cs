using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Receiving
{
    public class ImageDisplayer : MonoBehaviour
    {

        // test elements
        //private static int ImgWidth;
        //private static int ImgHeight;

        // private static byte[,,] ImgRGB; // image matrix (RGB)
        // private static byte[,] ImgBand; // image matrix (single band)
        // private int frame; // to be seen outside of Update method

        // the important class variables
        private Texture2D ImgTex; // texture to be populated by byte array
        private Color32[] ColorArr; // color array used as intermediate between byte array and texture

        public ImageReceiver receiver;
        public TextMesh DebugText;

        int updateCount = 0;

        // Use this for initialization
        async void Start()
        {
            //DebugText = this.GetComponent<TextMesh>();
            Debug.Log("Displayer DebugText: " + (DebugText != null));
            InitTexture();
            GetComponent<Renderer>().material.mainTexture = ImgTex;
            Debug.Log("Set Renderer");
            ImgTex.Apply();

            await System.Threading.Tasks.Task.Delay(5000);

            //ImgTex.SetPixels32(ColorArr);

            ImgTex.LoadRawTextureData(receiver.Get_ImageData1D());
            ImgTex.Apply();
            /*
            //var fileBytes = System.IO.File.ReadAllBytes(@"C:/Users/henry/Downloads/VandyLogo.jpg");
            WWW www = new WWW(@"C:/Users/henry/Downloads/VandyLogo.jpg");
            //System.Threading.Tasks.Task.Delay(8000);
            imageBytes = www.bytes;
            Debug.Log("Filled imageBytes array. Length: " + imageBytes.Length);
            ImgTex.LoadImage(imageBytes);
            Debug.Log("Loaded Into Texture");

            imgSprite = Sprite.Create(ImgTex, new Rect(0, 0, ImgTex.width, ImgTex.height), new Vector2(0.5f, 0.5f));
            sr.sprite = imgSprite;
            */
        }

        private void InitTexture()
        {
            ImgTex = new Texture2D(800, 800, TextureFormat.RGBA32, false);
            for (int y = 0; y < ImgTex.height; y++)
            {
                for (int x = 0; x < ImgTex.width; x++)
                {

                    ImgTex.SetPixel(x, y, Color.blue); // new Color32(0x00, 0xff, 0x00, 0xff));

                }
            }
            GetComponent<Renderer>().material.mainTexture = ImgTex;
            ImgTex.Apply();
        }


        // called once every frame
        public void Update()
        {
            DebugText.text = receiver.Get_Message();
            if (++updateCount % 60 == 0)
            {
                if (receiver.CheckNewImage())
                {
                    //ImgTex.LoadImage(receiver.Get_ImageData());
                    ImgTex = new Texture2D(receiver.Get_ImageWidth(), receiver.Get_ImageHeight(), TextureFormat.RGBA32, false);
                    byte[] arr1d = receiver.Get_ImageData1D();
                    //ImgTex.SetPixels32(ColorArr);
                    ImgTex.LoadRawTextureData(arr1d);
                    ImgTex.Apply();
                }
            }            
        }
        /* 
        private void UpdateColorArr(byte[,,] img)
        {
            for (int x = 0; x < img.GetLength(0); x++)
            {
                for (int y = 0; y < img.GetLength(1); y++)
                {
                    ColorArr[x + y * img.GetLength(0)] = 
                        new Color32(img[x, y,0], img[x,y,1], img[x,y,2], 0xff);
                }
            }
        }
        private void FlattenInColorArr(byte[,] data2d)
        {
            if (ColorArr == null || ColorArr.Length != data2d.Length)
            {
                InitializeArr(data2d.GetLength(0), data2d.GetLength(1));
            }

            int width = data2d.GetLength(0);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < data2d.GetLength(1); y++)
                {
                    ColorArr[x + y * width] = new Color32(data2d[x, y], 0x00, 0x00, 0xff);
                }
            }
        }
        */
        private void InitializeArr(int width, int height)
        {
            ColorArr = new Color32[width * height];
            ImgTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            GetComponent<Renderer>().material.mainTexture = ImgTex;
        }

        public void DisplayText(string message, bool add = false)
        {
            if (add)
                DebugText.text += message;
            else
                DebugText.text = message;
        }

        public void DisplayJPG(byte[] image)
        {
            ImgTex.LoadImage(image);
            ImgTex.Apply();
        }

        // Display RGB image from MxNx3 byte array representing RGB data
        public void DisplayRGB(byte[] image, int width, int height)
        {
            if (ColorArr == null || ColorArr.Length != image.Length ||
                ImgTex == null || ImgTex.height != height || ImgTex.width != width)
                InitializeArr(width, height);

            for (int i = 0; i < image.Length; i += 3)
            {
                ColorArr[i] = new Color32(image[i + 0], image[i + 1], image[i + 2], 0xff);
            }

            ImgTex.SetPixels32(ColorArr);
            ImgTex.Apply();
        }

        // Display One Band Image
        public void DisplayBand(byte[] image, int width, int height, string colorscheme = "grey")
        {
            // if the class structures are the wrong size 
            if (ColorArr == null || ColorArr.Length != image.Length || ImgTex == null ||
                ImgTex.height != height || ImgTex.width != width)
                InitializeArr(width, height);

            for (int i = 0; i < image.Length; ++i)
            {
                ColorArr[i] = GetFalseColor(image[i], colorscheme);
            }

            ImgTex.SetPixels32(ColorArr);
            ImgTex.Apply();
        }

        private Color32 GetFalseColor(byte val, string colorscheme, float ClampMin = Byte.MinValue, float ClampMax = Byte.MaxValue)
        {
            float hue = 0, s = 1.0f, v = 0.8f; // false coloring is done by hue, s & v are constants
            float value = (float)val;
            if (value > ClampMax)
                value = ClampMax;
            else if (value < ClampMin)
                value = ClampMin;

            switch (colorscheme)
            {
                case "grey-inv":
                    return new Color32(val, val, val, 0xff);
                case "grey":
                    val = (byte)(Byte.MaxValue - (int)val);
                    return new Color32(val, val, val, 0xff);
                case "BGYR": // blue -> green -> yellow -> red :: 160->0 hue
                    hue = Interp1D(value, ClampMin, ClampMax, 160, 0);
                    return HSV_ToColor32((float)hue, s, v, 0xff);
                case "RYGB": //red -> yellow -> green -> blue :: 0->160 hue
                    hue = Interp1D(value, ClampMin, ClampMax, 0, 160);
                    return HSV_ToColor32(hue, s, v, 0xff);
                case "BPR": // blue -> magenta -> red :: 160->360 hue
                    hue = Interp1D(value, ClampMin, ClampMax, 160, 360);
                    return HSV_ToColor32(hue, s, v, 0xff);
                case "RPB": // blue -> magenta -> red :: 160->360 hue
                    hue = Interp1D(value, ClampMin, ClampMax, 360, 160);
                    return HSV_ToColor32(hue, s, v, 0xff);
                default:
                    return new Color32(val, val, val, 0xff);
            }
        }

        public static float Interp1D(float v, float x0, float x1, float y0, float y1)
        {
            if ((x1 - x0) == 0)
                return (y0 + y1) / 2;
            else if (v == x0)
                return y0;
            else if (v == x1)
                return y1;
            return y0 + (v - x0) * (y1 - y0) / (x1 - x0);
        }

        public static Color32 HSV_ToColor32(float H, float S, float V, byte A)
        {
            float r = 0, g = 0, b = 0;

            if (S == 0)
                return new Color32((byte)(V * 255), (byte)(V * 255), (byte)(V * 255), A);
            // else 
            int i;
            float f, p, q, t;

            if (H == 360)
                H = 0;
            else
                H = H / 60;

            i = (int)Math.Truncate(H);
            f = H - i;

            p = V * (1.0f - S);
            q = V * (1.0f - (S * f));
            t = V * (1.0f - (S * (1.0f - f)));

            switch (i)
            {
                case 0:
                    r = V;
                    g = t;
                    b = p;
                    break;
                case 1:
                    r = q;
                    g = V;
                    b = p;
                    break;
                case 2:
                    r = p;
                    g = V;
                    b = t;
                    break;
                case 3:
                    r = p;
                    g = q;
                    b = V;
                    break;
                case 4:
                    r = t;
                    g = p;
                    b = V;
                    break;
                default:
                    r = V;
                    g = p;
                    b = q;
                    break;
            }

            // return the color associated
            return new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), A);
        }

        /* private void Testing_Start()
        {
            frame = 0;
            ImgRGB = new byte[TestWidth, TestHeight, 3];
            ImgBand = new byte[TestWidth, TestHeight];
            InitializeArr(ImgRGB);

            for (int i = 0; i < TestWidth; i++)
            {
                for (int j = 0; j < TestHeight; j++)
                {
                    if ((i & j) != 0)
                    {
                        ImgRGB[i, j, 0] = (byte)255;
                        ImgRGB[i, j, 1] = (byte)255;
                        ImgRGB[i, j, 2] = (byte)255;
                    }
                    else
                    {
                        ImgRGB[i, j, 0] = (byte)0;
                        ImgRGB[i, j, 1] = (byte)0;
                        ImgRGB[i, j, 2] = (byte)255;
                    }

                    ImgBand[i, j] = Convert.ToByte(Interp1D(i + j, 0, TestWidth + TestHeight, Byte.MinValue, Byte.MaxValue));
                }
            }

            //DisplayRGB(ImgRGB);
            DisplayBand(ImgBand, "BGYR");
        }

        private void Testing_Update()
        {
            for (int i = 0; i < ImgTex.width; i++)
            {
                if ((int)(frame / ImgTex.height) % 2 == 0)
                {
                    ImgRGB[i, frame % ImgTex.height, 0] = 0x00;
                    ImgRGB[i, frame % ImgTex.height, 1] = 0xff;
                    ImgRGB[i, frame % ImgTex.height, 2] = 0x00;
                }
                else
                {
                    ImgRGB[i, frame % ImgTex.height, 0] = 0xff;
                    ImgRGB[i, frame % ImgTex.height, 1] = 0x00;
                    ImgRGB[i, frame % ImgTex.height, 2] = 0x00;
                }
            }
            frame++;
            if (frame % 10 == 0) DisplayRGB(ImgRGB);
        } */
    }
}
