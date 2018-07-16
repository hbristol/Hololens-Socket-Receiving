using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net;

#if !UNITY_EDITOR
using Windows.Networking.Sockets;
using Windows.Networking.Connectivity;
using Windows.Networking;
using Windows.Graphics.Imaging; // for BitmapDecoder
using Windows.Storage.Streams;
#endif

namespace Receiving
{
    public class ImageReceiver : MonoBehaviour
    {
        private int imageCount = 0;

        static readonly string RemoteIP = "10.67.119.87";
        static readonly string RemotePort = "5000";
        static readonly string ServerPort = "5001";

        private const int PACKET_SIZE = 1000;
        private const int NORMAL_PACKET_INDEX_BYTES = 3;
        private const int START_PACKET_SIZE = 3;

        private const char START_RGB = 'M';
        private const char START_ONE_BAND = 'I';

        private const int AWAITING_IMAGE = 0;
        private const int RECEIVING_RGB = 1;
        private const int RECEIVING_ONE_BAND = 2;

        private const int UNCOMPRESSED_RGB = RECEIVING_RGB;
        private const int UNCOMPRESSED_BAND = RECEIVING_ONE_BAND;
        private const int COMPRESSED_JPEG = 3;

        private int ReceivingStatus = AWAITING_IMAGE;
        private byte[] ImageBuffer = null;
        private bool ImgReceiving = false;

        private byte[] ID_ImageData1D;
        private int ID_ImageType;
        private int ID_ImageWidth;
        private int ID_ImageHeight;
        private string ID_Message = "testing";
        private bool ID_NewData = false;

        private readonly ConcurrentQueue<Action> ExecuteOnMainThread = new ConcurrentQueue<Action>();
        //private readonly Queue<Action> ExecuteOnMainThread = new Queue<Action>();

        // The singleton instance
        private static ImageReceiver sReceiver;
        public static ImageReceiver GetInstance()
        {
            if (sReceiver == null)
            {
                sReceiver = new ImageReceiver();
            }
            return sReceiver;
        }

        //get methods
        public string Get_Message()
        {
            return ID_Message;
        }
        public byte[] Get_ImageData1D()
        {
            ID_NewData = false;
            return ID_ImageData1D;
        }
        public int Get_ImageType()
        {
            return ID_ImageType;
        }
        public int Get_ImageWidth()
        {
            return ID_ImageWidth;
        }
        public int Get_ImageHeight()
        {
            return ID_ImageHeight;
        }
        public bool CheckNewImage()
        {
            return ID_NewData;
        }

#if !UNITY_EDITOR
        DatagramSocket ServerSocket;

        async void Start()
        {
            Init_Test();
            ServerSocket = new DatagramSocket();
            Debug.Log("Waiting for Connection...");
            ServerSocket.MessageReceived += ServerSocket_MessageReceived;
            Debug.Log("Received Event Added\n");
            
            try
            {
                await ServerSocket.BindServiceNameAsync(ServerPort);
                Debug.Log("Connected to " + RemoteIP + ":" + RemotePort + "\n");
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                Debug.Log(SocketError.GetStatus(e.HResult).ToString());
                return;
            }

            await System.Threading.Tasks.Task.Delay(2000);

            await SendBroadcast();
            Debug.Log("Exit Start");
        }

        private async System.Threading.Tasks.Task SendBroadcast()
        {
            try  // send out a message, otherwise receiving does not work ?!
            {
                Debug.Log("\nSending broadcast1");
                var outputStream = await ServerSocket.GetOutputStreamAsync(new HostName(RemoteIP), RemotePort);
                Debug.Log("2");
                DataWriter writer = new DataWriter(outputStream);
                Debug.Log("3");
                writer.WriteString("Hello World!");
                Debug.Log("4");
                await writer.StoreAsync();
                Debug.Log("5");
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
                Debug.Log(SocketError.GetStatus(ex.HResult).ToString());
                return;
            }
        }

        private void Init_Test()
        {
            ID_ImageWidth = 800;
            ID_ImageHeight = 800;
            ID_ImageData1D = new byte[ID_ImageWidth * ID_ImageHeight * 4];
            for (int i = 0; i < ID_ImageData1D.Length; i += 4)
            {
                ID_ImageData1D[i + 0] = 0x00; // r
                ID_ImageData1D[i + 1] = 0xff; // g
                ID_ImageData1D[i + 2] = 0x00; // b
                ID_ImageData1D[i + 3] = 0xff; // a
            }
            Debug.Log("Width: " + ID_ImageWidth + ", Height: " + ID_ImageHeight + ", Length: " + ID_ImageData1D.Length);
        }

        // Update is called once per frame
        void Update()
        {
            // execute everything queued
            while (ExecuteOnMainThread.Count > 0)
            {
                ExecuteOnMainThread.Dequeue().Invoke();
            }
            
        }

        //async 
        private async void ServerSocket_MessageReceived(
            DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                Stream streamIn = args.GetDataStream().AsStreamForRead();
                MemoryStream ms = ToMemoryStream(streamIn);
                byte[] data = ms.ToArray();
                Debug.Log("Read in byte array of length " + data.Length);

                // enqueue all, continually stack // used for managing events, creates a group of actions
                Debug.Log("Queue Count: " + ExecuteOnMainThread.Count);

                //if ((data.Length == START_PACKET_SIZE) && (ExecuteOnMainThread.Count == 0))
                //{
                if (data.Length == START_PACKET_SIZE && ExecuteOnMainThread.Count == 0)
                {
                    ImgReceiving = true;
                }
                if (ImgReceiving)
                {
                    ExecuteOnMainThread.Enqueue(() =>
                    {
                        ProcessPacket(data);
                    });
                }
                //}
                //else if()
            });
        }

        public void Dispose()
        {
            if (ServerSocket != null)
            {
                ServerSocket.Dispose();
                ServerSocket = null;
            }
        }

        //process the packet of data we get
        private void ProcessPacket(byte[] data)
        {
            /*if (data.Length >= 3) // read in string packet  with '___' prefix- this is for testing
            {
                if ((char)data[0] == '_' && (char)data[1] == '_' && (char)data[2] == '_')
                {
                    string message = Encoding.UTF8.GetString(data);
                    Debug.Log("String: " + message);
                    ID_Message = message;
                    Debug.Log("Set ID_Message variable");
                    return;
                }
            }*/

            switch (data.Length)
            {
                case (0): //  last packet
                    // Handle image here 
                    // ServerSocket.MessageReceived -= ServerSocket_MessageReceived;
                    ImgReceiving = false; // testing to receive only one image
                    byte[] jpegBuffer = ImageBuffer;
                    ImageBuffer = null;
                    int imageType = ReceivingStatus;
                    ReceivingStatus = AWAITING_IMAGE;

                    // Cant launch DisplayImage in a new thread because graphics has to happen on main
                    Debug.Log("Last Packet - Bytes received: " + jpegBuffer.Length);        
                    ProcessImageArr(jpegBuffer, imageType); // for uncompressed data transmission
                    ++imageCount;
                    Debug.Log("Image Count: " + imageCount);
                    break;

                case (START_PACKET_SIZE): // first packet
                    // Byte 1: For the first udp packet of an image, the first byte is either 
                    // an 'M' for RGB, or an 'I' for one band.
                    // Byte 2-3: The next two bytes indicate how many udp packets make up the image.
                    char nextByte = (char)data[0];
                    UInt16 num_packets = data[1];
                    num_packets |= (UInt16)((data[2] & 0xff) << 8);

                    if (nextByte == START_RGB)
                    {
                        ReceivingStatus = RECEIVING_RGB;
                    }
                    else if (nextByte == START_ONE_BAND)
                    {
                        ReceivingStatus = RECEIVING_ONE_BAND;
                    }
                    else
                    {
                        Debug.Log("Unrecognized Data Identifier");
                        return;
                    }
                    Debug.Log("Packet (1 of " + num_packets + ")");
                    ImageBuffer = new byte[PACKET_SIZE * num_packets];
                    break;
                default: // rest of packets
                    int ind = data[0];
                    ind |= ((data[1] & 0xff) << 8);
                    ind |= ((data[2] & 0xff) << 16);
                    for (int i = 0; i < data.Length - NORMAL_PACKET_INDEX_BYTES; ++i)
                    {
                        ImageBuffer[(ind * PACKET_SIZE) + i] = data[i + NORMAL_PACKET_INDEX_BYTES];
                    }
                    break;
            }
        }

        // display image when last packet arrives
        private async void ProcessImageArr(byte[] img, int imgType)
        {
            try
            {
                // Decode the JPEG
                MemoryStream stream = new MemoryStream(img);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
                //SoftwareBitmap sftwareBmp = await decoder.GetSoftwareBitmapAsync();
                //SoftwareBitmap displayableImage = SoftwareBitmap.Convert(sftwareBmp, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
                ID_ImageData1D = pixelData.DetachPixelData();

                ID_ImageWidth = (int)decoder.PixelWidth;
                ID_ImageHeight = (int)decoder.PixelHeight;
                Debug.Log("Length Check -- " + ID_ImageData1D.Length + ": " + ID_ImageWidth * ID_ImageHeight);

                //RGB1D_ToImageData2D(ref ID_ImageData1D, width, height);
                //RGB1D_ToImageData3D(ref ID_ImageData1D, width, height);
                ID_ImageType = imgType;
                ID_NewData = true;
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
            }
        }
#endif
        /* private void RGB1D_ToImageData2D(ref byte[] Arr1D, int width, int height)
        {
            ID_ImagaData2D = new byte[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    byte r = Arr1D[0 + 4*x + 4*width * y];
                    byte g = Arr1D[1 + 4 * x + 4 * width * y];
                    byte b = Arr1D[2 + 4 * x + 4 * width * y];
                    byte a = Arr1D[4 + 4 * x + 4 * width * y];
                    ID_ImagaData2D[x, y] = r;
                }
            }
        }
        private void RGB1D_ToImageData3D(ref byte[] Arr1D, int width, int height)
        {
            ID_ImagaData2D = new byte[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        ID_ImagaData3D[x, y, b] = Arr1D[b + 3*x + 3*width*y];
                    }
                }
            }
        } */

        /* our code
        public async void StartServer()
        {
            serverDatagramSocket = new DatagramSocket();
            DebugText.text += "1";

            // The ConnectionReceived event is raised when connections are received.
            serverDatagramSocket.MessageReceived += ServerDatagramSocket_MessageReceived;
            DebugText.text += "2";
            // Start listening for incoming TCP connections on the specified port. You can specify any port that's not currently in use.
            try
            {
                await serverDatagramSocket.BindServiceNameAsync(ServerServerPortStringing);
                //Debug.Log("Listening on 0.0.0.0:" + ServerServerPortStringing);
                DebugText.text += "3; Listening on 0.0.0.0:" + ServerServerPortStringing + "\n";
                /*
                var outputStream = await serverDatagramSocket.GetOutputStreamAsync(
                    new HostName("192.168.199.189"), ServerServerPortStringing);
                DebugText.text += "4";
                Debug.Log("4");
                DataWriter writer = new DataWriter(outputStream);
                DebugText.text += "5";
                Debug.Log("5");
                writer.WriteString("Hello World!");
                DebugText.text += "6";
                Debug.Log("6");
                //
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
                Debug.LogError(SocketError.GetStatus(ex.HResult).ToString());
                DebugText.text += " - Failed to bind: " + ex.ToString() + "\n";
                DebugText.text += SocketError.GetStatus(ex.HResult).ToString();
                serverDatagramSocket.Dispose();
                serverDatagramSocket = null;
                return;
            }
        } 

        #if !UNITY_EDITOR

                //private async void, use with await System.Threading.Tasks.Task.Run()
                private async void ServerDatagramSocket_MessageReceived(
                    Windows.Networking.Sockets.DatagramSocket sender,
                    Windows.Networking.Sockets.DatagramSocketMessageReceivedEventArgs args)
                {
                    DebugText.text += "Received Packet";
                    Debug.Log("Received Packet");

                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        Stream streamIn = args.GetDataStream().AsStreamForRead();
                        MemoryStream ms = ToMemoryStream(streamIn);
                        byte[] data = ms.ToArray();
                        // ProcessPacket(data);
                    });
                    DebugText.text += "; Processed packet \n";
                    Debug.Log("Processed Packet");
                }

                private void ProcessPacket(byte[] data)
                { 
                    switch (data.Length)
                    {
                        case (0): //  last packet
                            // Handle image here 
                            byte[] jpegBuffer = ImageBuffer;
                            ImageBuffer = null;
                            int imageType = ReceivingStatus;
                            ReceivingStatus = AWAITING_IMAGE;
                            // TODO: Launch DisplayImage in a new thread
                            // DisplayImage(jpegBuffer, imageType);
                            break;
                        case (START_PACKET_SIZE): // first packet
                            // For the first udp packet of an image, the first byte is either 
                            //an 'M' for RGB, or an 'I' for one band.
                            //The next two bytes indicate how many udp packets make up the image.
                            char nextByte = (char)data[0];
                            UInt16 num_packets = data[1];
                            num_packets |= (UInt16)((data[2] & 0xff) << 8);
                            if (nextByte == START_RGB)
                            {
                                ReceivingStatus = RECEIVING_RGB;
                            }
                            else if (nextByte == START_ONE_BAND)
                            {
                                ReceivingStatus = RECEIVING_ONE_BAND;
                            }
                            else
                            {
                                DebugText.text += "Band/RGB Identifier is not 'M' or 'I'";
                                return;
                            }
                            ImageBuffer = new byte[PACKET_SIZE * num_packets];
                            break;
                        default: // rest of packets
                            int ind = data[0];
                            ind |= ((data[1] & 0xff) << 8);
                            ind |= ((data[2] & 0xff) << 16);
                            for (int i = 0; i < data.Length - NORMAL_PACKET_INDEX_BYTES; ++i)
                            {
                                ImageBuffer[(ind * PACKET_SIZE) + i] = data[i + NORMAL_PACKET_INDEX_BYTES];
                            }
                            break;
                    }
                }
        #endif

                private async void DisplayImage(byte[] img, int imageType)
                {
        #if !UNITY_EDITOR
                    if (Displayer == null) return;
                    try
                    {
                        // Decode the JPEG
                        //MemoryStream stream = new MemoryStream(img);
                        //BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ToIRandomAccessStream(img));
                        PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
                        byte[] decompressedImage = pixelData.DetachPixelData();
                        uint width = decoder.PixelWidth;
                        uint height = decoder.PixelHeight;

                        if (imageType == RECEIVING_RGB)
                        {
                            // TODO: Make Display methods take the one dimensional decompressed byte array
                            byte[,,] decompressedRGBMatrix = ConvertDecompressedToRGBMatrix(decompressedImage, width, height);
                            Displayer.DisplayRGB(decompressedRGBMatrix);
                        }
                        else if (imageType == RECEIVING_ONE_BAND)
                        {
                            byte[,] decompressedBandMatrix = ConvertDecompressedToBandMatrix(decompressedImage, width, height);
                            Displayer.DisplayBand(decompressedBandMatrix, "grey-inv");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex.Message);
                        // DebugText.text += ex.Message;
                    }
        #endif
                }

                private static byte[,,] ConvertDecompressedToRGBMatrix(byte[] decompressed, uint width, uint height)
                {
                    byte[,,] rgbMatrix = new byte[width, height, 3];
                    for (int row = 0; row < height; ++row)
                    {
                        for (int col = 0; col < width; ++col)
                        {
                            for (int band = 0; band < 3; ++band)
                            {
                                rgbMatrix[row, col, band] = decompressed[band + (3 * col) + (3 * width * row)]; 
                            }
                        }
                    }
                    return rgbMatrix;
                }

                private static byte[,] ConvertDecompressedToBandMatrix(byte[] decompressed, uint width, uint height)
                {
                    byte[,] bandMatrix = new byte[width, height];
                    for (int row = 0; row < height; ++row)
                    {
                        for (int col = 0; col < width; ++col)
                        {
                            bandMatrix[row, col] = decompressed[col + row * width];
                        }
                    }
                    return bandMatrix;
                }

        #if !UNITY_EDITOR
                private static IRandomAccessStream ToIRandomAccessStream(byte[] arr)
                {
                    MemoryStream stream = new MemoryStream(arr);
                    return stream.AsRandomAccessStream();
                }
        #endif
                */
        private static MemoryStream ToMemoryStream(Stream input)
        {
            try
            {                                         // Read and write in
                byte[] block = new byte[0x1000];       // blocks of 4K.
                MemoryStream ms = new MemoryStream();
                while (true)
                {
                    int bytesRead = input.Read(block, 0, block.Length);
                    if (bytesRead == 0) return ms;
                    ms.Write(block, 0, bytesRead);
                }
            }
            finally { }
        }
    }
}
