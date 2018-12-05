using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using EOSDigital.API;
using EOSDigital.SDK;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EDSDKLib.API.Base
{
    public class CameraManager
    {
        #region Variables

        CanonAPI APIHandler;
        Camera MainCamera;
        List<Camera> CamList=new List<Camera>();
        bool IsInit = false;
        string _path;
        public string path { get { return _path; } set { _path = value; } }

        int ErrCount;
        object ErrLock = new object();
        //public Action<int> CameraProgress;
        public SaveTo saveTo = SaveTo.Host;
        public Action<string> sendToUnity;
        private string photoName;
        #endregion

        public void Initial()
        {
            try
            {
                APIHandler = new CanonAPI();
                APIHandler.CameraAdded += APIHandler_CameraAdded;
                ErrorHandler.SevereErrorHappened += ErrorHandler_SevereErrorHappened;
                ErrorHandler.NonSevereErrorHappened += ErrorHandler_NonSevereErrorHappened;
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RemotePhoto");
                GetCameraList();
                IsInit = true;
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"initial\":\"true\"}");
            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); }
            catch (Exception ex) {
                ReportError(ex.Message, true);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"initial\":\"false\"}");
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                IsInit = false;
                MainCamera?.Dispose();
                APIHandler?.Dispose();
            }
            catch (Exception ex) {
                ReportError(ex.Message, false);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"error\":" + ex.Message + "}");
            }
        }

        #region API Events

        private void APIHandler_CameraAdded(CanonAPI sender)
        {
            if (sendToUnity != null)
                sendToUnity("{\"CanmeraAdded\":" + sender.ToString() + "}");
        }

        private void ErrorHandler_NonSevereErrorHappened(object sender, ErrorCode ex)
        {
            ReportError($"SDK Error code: {ex} ({((int)ex).ToString("X")})", false);
        }

        private void ErrorHandler_SevereErrorHappened(object sender, Exception ex)
        {
            ReportError(ex.Message, true);
            if (sendToUnity != null)
                sendToUnity.Invoke("{\"error\":"+ex.Message+"}");
        }

        #endregion

        #region Session

        public void ConnectCamera(Camera targetCamera)
        {
            try
            {
                if (MainCamera?.SessionOpen == true) CloseCamera();
                else
                {
                    OpenSession(targetCamera);
                    SetPictureSavePath();
                    if (sendToUnity != null)
                        sendToUnity.Invoke("{\"CameraState\":\"ConnectSucceed\"}");
                } 
            }
            catch (Exception ex) {
                ReportError(ex.Message, false);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"CameraState\":\"ConnectFailed\"}");
            }
        }

        #endregion

        #region Settings


        public void TakePhoto()
        {
            try
            {
              MainCamera.TakePhotoAsync();
            }
            catch (Exception ex) {
                ReportError(ex.Message, false);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"error\":" + ex.Message + "}");
            }
        }

        private void SetPictureSavePath()
        {
            try
            {
                if (IsInit)
                {
                    if (saveTo==SaveTo.Camera)
                    {
                        MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Camera);
                    }
                    else
                    {
                        if (saveTo==SaveTo.Host) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                        else if (saveTo==SaveTo.Both) MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Both);

                        MainCamera.SetCapacity(4096, int.MaxValue);
                    }
                }
            }
            catch (Exception ex) {
                ReportError(ex.Message, false);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"error\":" + ex.Message + "}");
            }
        }

        #endregion


        #region Subroutines

        public void CloseCamera()
        {
            try { MainCamera.CloseSession(); } catch (Exception e) {
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"error\":" + e.Message + "}");
            }
        }

        public List<Camera> GetCameraList()
        {
            CamList.Clear();
            //try
            //{
            //    CamList = APIHandler.GetCameraList();
            //}
            //catch (Exception ex)
            //{
            //    ReportError(ex.Message, false);
            //}
            CamList = APIHandler.GetCameraList();
            return CamList;
            
        }

        private void OpenSession(Camera c)
        {
            MainCamera = c;
            MainCamera.OpenSession();
            MainCamera.ProgressChanged += MainCamera_ProgressChanged;
            MainCamera.StateChanged += MainCamera_StateChanged;
            MainCamera.DownloadReady += MainCamera_DownloadReady;
            MainCamera.PropertyChanged += MainCamera_PropertyChanged;
        }
      

        private void MainCamera_StateChanged(Camera sender, StateEventID eventID, int parameter)
        {
            if (sendToUnity != null)
                sendToUnity.Invoke("{\"CameraState\":\"" + eventID.ToString() + "\"}");
            try
            {
                //if (eventID == StateEventID.WillSoonShutDown && IsInit)
                //{
                //    CloseCamera();
                //}

            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_ProgressChanged(object sender, int progress)
        {
            if (sendToUnity != null)
                sendToUnity.Invoke("{\"progress\":" + progress + "}");
        }


        private void MainCamera_DownloadReady(Camera sender, DownloadInfo Info)
        {

            try
            {
                sender.DownloadFile(Info, path);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"DownLoaded\":\"" + Info.FileName + "\"}");
                photoName = Info.FileName;

            }
            catch (Exception ex)
            {
                ReportError(ex.Message, false);
                if (sendToUnity != null)
                    sendToUnity.Invoke("{\"error\":" + ex.Message + "}");
            }
        }
        private void MainCamera_PropertyChanged(Camera sender, PropertyEventID eventID, PropertyID propID, int parameter)
        {
            if (sendToUnity != null)
                sendToUnity.Invoke("{\""+eventID+"\":\"" + propID + "\"}");
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; }

            if (errc < 4) sendToUnity(message);
            else if (errc == 4) sendToUnity("Many errors happened!");

            lock (ErrLock) { ErrCount--; }
        }

        #endregion
    }
    }
