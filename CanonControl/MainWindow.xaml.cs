using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using LitJson;
using EOSDigital.API;
using EDSDKLib.API.Base;

namespace CanonControl
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread threadWatch = null;
        Socket Mysocket = null;
        byte[] arrMsgRec = new byte[1024 * 1024 * 2];
        static Socket policy = null;
        Dictionary<string, Socket> SoketList = new Dictionary<string, Socket>();
        Thread threadRece = null;
        JsonData jsonData;
        string order;
        CameraManager cameraManager = new CameraManager();
        List<Camera> cameraList = new List<Camera>();

        public MainWindow()
        {
            InitializeComponent();
            cameraManager.sendToUnity += send;
            InitServer();
            this.Visibility = Visibility.Hidden;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //cameraManager.CloseCamera();
        }
        public void DisConnect()
        {

        }


        private void InitServer()
        {
            Mysocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress address = IPAddress.Parse("127.0.0.1");
            IPEndPoint endpoint = new IPEndPoint(address, int.Parse("4502"));
            Mysocket.Bind(endpoint);
            Mysocket.Listen(10);
            threadWatch = new Thread(WatchConnection);
            threadWatch.IsBackground = true;
            threadWatch.Start();
            Console.Write("safdasfdas");
        }

        private void WatchConnection()
        {
            while (true)
            {
                policy = Mysocket.Accept();
                Socket policynew = policy;
                send("{\"info\":\"连接成功\"}");
                SoketList.Add(policynew.RemoteEndPoint.ToString(), policy);
                if (threadRece == null)
                {
                    threadRece = new Thread(Recivce);
                    threadRece.IsBackground = true;
                    threadRece.Start();
                }
            }
        }

        private void Recivce()
        {
            while (true)
            {
                int strlong = policy.Receive(arrMsgRec);

                string strMsgRec = System.Text.Encoding.UTF8.GetString(arrMsgRec, 0, strlong);
                ShowText(strMsgRec);
            }
        }

        public static void send(string s)
        {
            policy.Send(Encoding.UTF8.GetBytes(s));
        }

        private void ShowText(string text)
        {
            jsonData = JsonMapper.ToObject(text);
            foreach (string k in jsonData.Keys)
            {
                order = k;
            }
            switch (order)
            {
                case "Initial":
                    cameraManager.Initial();
                    break;
                case "ConnectCamera":
                    cameraList = cameraManager.GetCameraList();
                    if (cameraList.Count == 0)
                    {
                        send("{\"error\":\"相机没有连接\"}");
                    }
                    else
                    {
                        cameraManager.ConnectCamera(cameraList[0]);
                    }
                    //try
                    //{
                    //}
                    //catch (Exception e)
                    //{
                    //    send(e.Message);
                    //    send("{\"error\":\"相机没有连接或处于关机状态\"}");
                    //}
                    break;
                case "DisConnectCamera":
                    try
                    {
                        cameraList.Clear();
                        cameraManager.CloseCamera();
                    }
                    catch (Exception e)
                    {
                        send(e.Message);
                        send("{\"error\":\"相机没有连接或处于关机状态\"}");
                    }
                    break;
                case "Path":
                    string normalBase64= ((string)jsonData["Path"]).Replace("-", "+").Replace("_", "/");
                    byte[] stringData = System.Convert.FromBase64String(normalBase64);
                    string path= Encoding.UTF8.GetString(stringData);
                    cameraManager.path = path;
                    break;
                case "TakePhoto":
                    cameraManager.TakePhoto();
                    break;
                case "DisConnect":
                    DisConnect();
                    break;
            }
        }
    }
}
