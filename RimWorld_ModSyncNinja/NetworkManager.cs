using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace RimWorld_ModSyncNinja
{
    static class NetworkManager
    {
        public const string URL = "http://www.modsync.ninja";
#if DEBUG_CLIENT
        public const string API = "http://localhost:29671/api/SyncMods";
#else
        public const string API = "http://www.modsync.ninja/api/SyncMods";
#endif

        public delegate void CheckForInternetConnectionAsyncCallback(Dialog_ModSync_Window.InternetConnectivity clientInternetConnectivity);

        public static void CheckForInternetConnectionAsync(
            Dialog_ModSync_Window.InternetConnectivity clientInternetConnectivity, 
            CheckForInternetConnectionAsyncCallback callback)
        {
            new Thread(() =>
            {
                MSLog.Log("Checking for network async", MSLog.Level.All);
                try
                {
                    //user.OnNetworkConnectionTestCompleted();

                    var request = WebRequest.Create("http://www.google.com");
                    request.Timeout = 5000;

                    request.BeginGetResponse((IAsyncResult result) =>
                    {
                        MSLog.Log("CheckForInternetConnectionAsyncCallback:", MSLog.Level.All);
                        var httpWebRequest = result.AsyncState as HttpWebRequest;
                        try
                        {
                            // Request finished
                            clientInternetConnectivity = (httpWebRequest == null || !httpWebRequest.HaveResponse)
                                ? Dialog_ModSync_Window.InternetConnectivity.Offline
                                : Dialog_ModSync_Window.InternetConnectivity.Online;
                            // Perform callback
                            callback(clientInternetConnectivity);
                        }
                        catch
                        {
                            MSLog.Log("failed to assign network check response, client probably closed window", MSLog.Level.All);
                        }
                    }, request);

                    // assume time out
                    try
                    {
                        Thread.Sleep(5000);
                        request.Abort();
                        MSLog.Log("Connection test request closed with connectivity state: " + clientInternetConnectivity, MSLog.Level.All);
                        if (clientInternetConnectivity != Dialog_ModSync_Window.InternetConnectivity.Online)
                        {
                            clientInternetConnectivity = Dialog_ModSync_Window.InternetConnectivity.Offline;
                            // Perform callback
                            callback(clientInternetConnectivity);
                        }
                    }
                    catch
                    {
                        MSLog.Log("network check timed out", MSLog.Level.All);
                    }
                }
                catch
                {
                    MSLog.Log("network check failed", MSLog.Level.All);
                }
            }).Start();
        }

        public static bool CheckForInternetConnectionSync()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static void OpenModSyncUrl()
        {
            Application.OpenURL(URL);
        }

        public static string GenerateServerRequestString(string modDirectoryName, string modVersion)
        {
            StringBuilder json = new StringBuilder("[");
            PlayerModAndVersion pmv = new PlayerModAndVersion();
            pmv.MF = modDirectoryName.ToUpper();
            pmv.V = modVersion;
            json.Append(JsonUtility.ToJson(pmv));
            json.Append("]");
            return json.ToString();
        }

        public static string GenerateServerRequestString(List<ModSyncModMetaData> mods)
        {
            StringBuilder json = new StringBuilder();
            if (mods.Count > 0) json.Append("[");
            bool isFirst = true;
            foreach (var modSyncModMetaData in mods)
            {
                if (!isFirst) json.Append(",");
                isFirst = false;
                PlayerModAndVersion pmv = new PlayerModAndVersion();
                pmv.MF = modSyncModMetaData.ModDirName.ToUpper();
                pmv.V = modSyncModMetaData.LocalModData.Version;
                json.Append(JsonUtility.ToJson(pmv));
            }
            if (mods.Count > 0) json.Append("]");
            return json.ToString();
        }

        public static void CreateUpdateModHttpRequest(string userRequestStr, Action successCallback, Action failureCallback)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                HttpWebResponse response = null;
                try
                {
                    // Create a request using a URL that can receive a post. 
                    WebRequest request = WebRequest.Create(API + "/VerifyOwnership");
                    // Set the Method property of the request to POST.
                    request.Method = "POST";
                    request.Timeout = 20000;

                    // Create POST data and convert it to a byte array.
                    string postData = userRequestStr;
                    MSLog.Log("Sending request to server: " + postData);
                    byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                    // Set the ContentType property of the WebRequest.
                    request.ContentType = "application/json; charset=utf-8";
                    // Set the ContentLength property of the WebRequest.
                    request.ContentLength = byteArray.Length;

                    // Get the request stream.
                    Stream dataStream = request.GetRequestStream();
                    // Write the data to the request stream.
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    // Close the Stream object.
                    dataStream.Close();

                    // Get the response.
                    response = (HttpWebResponse)request.GetResponse();

                    dataStream = response.GetResponseStream();


                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    responseFromServer = responseFromServer.Trim('\"');
                    reader.Close();
                    dataStream.Close();
                    MSLog.Log("Connection closed with response:" + responseFromServer);
                    // 3 is the length of our error codes, since base64 length is base 4, no valid response will have a length of 3
                    if (!String.IsNullOrEmpty(responseFromServer) && responseFromServer.Length.Equals(3))
                    {
                        MSLog.Log("ModSync Ninja has encountered an error with your request if this error repeats, feel free to submit the error below to the forums", MSLog.Level.User);
                        MSLog.Log("Your request: " + userRequestStr, MSLog.Level.User);
                        MSLog.Log("Response Error: " + responseFromServer, MSLog.Level.User);
                        if (failureCallback != null)
                            failureCallback.Invoke();
                    }
                    else if (successCallback != null)
                        successCallback.Invoke();
                }
                catch (WebException e)
                {
                    bool showErrorWindow = true;
                    if (e.Status == WebExceptionStatus.ConnectFailure ||
                        e.Status == WebExceptionStatus.Timeout ||
                        e.Status == WebExceptionStatus.ConnectionClosed)
                    {
                        showErrorWindow = false;
                    }
                    MSLog.Log("ModSync Ninja has encountered an error with your request if this error repeats, feel free to submit the error below to the forums", MSLog.Level.User);
                    MSLog.Log("Your request: " + userRequestStr, MSLog.Level.User);
                    if (e.Status == WebExceptionStatus.ProtocolError)
                    {
                        response = (HttpWebResponse)e.Response;
                        MSLog.Log("Response Errorcode: " + (int)response.StatusCode, MSLog.Level.User);
                    }
                    else
                    {
                        MSLog.Log("Response error: " + e.Status, MSLog.Level.User);
                    }
                    if (showErrorWindow)
                        MSLog.ShowErrorScreen();
                    if (failureCallback != null)
                        failureCallback.Invoke();
                }
                finally
                {
                    if (response != null)
                    {
                        response.Close();
                    }
                }
            }).Start();
        }

        public delegate void CreateHttpRequestSyncStateCallback(Dialog_ModSync_Window.CurrentSyncState syncState);
        public delegate void CreateHttpRequestFinishedCallback(string responseStr, bool success, string errorCode = "");
        public static void CreateRequestHttpRequest(string userRequestStr, CreateHttpRequestSyncStateCallback syncStateCallback, CreateHttpRequestFinishedCallback finishedCallback)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                HttpWebResponse response = null;
                try
                {
                    if (syncStateCallback != null)
                        syncStateCallback.Invoke(Dialog_ModSync_Window.CurrentSyncState.RequestStarted);

                    // Create a request using a URL that can receive a post. 
                    WebRequest request = WebRequest.Create(API);
                    // Set the Method property of the request to POST.
                    request.Method = "POST";
                    request.Timeout = 20000;

                    // Create POST data and convert it to a byte array.
                    string postData = userRequestStr;
                    MSLog.Log("Sending request to server: " + postData);
                    byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                    // Set the ContentType property of the WebRequest.
                    request.ContentType = "application/json; charset=utf-8";
                    // Set the ContentLength property of the WebRequest.
                    request.ContentLength = byteArray.Length;

                    // Get the request stream.
                    Stream dataStream = request.GetRequestStream();
                    // Write the data to the request stream.
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    // Close the Stream object.
                    dataStream.Close();

                    // Get the response.
                    response = (HttpWebResponse)request.GetResponse();

                    dataStream = response.GetResponseStream();


                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    responseFromServer = responseFromServer.Trim('\"');
                    reader.Close();
                    dataStream.Close();
                    MSLog.Log("Connection closed with response:" + responseFromServer);
                    // 3 is the length of our error codes, since base64 length is base 4, no valid response will have a length of 3
                    if (!String.IsNullOrEmpty(responseFromServer) && responseFromServer.Length.Equals(3))
                    {
                        MSLog.Log("ModSync Ninja has encountered an error with your request if this error repeats, feel free to submit the error below to the forums", MSLog.Level.User);
                        MSLog.Log("Your request: " + userRequestStr, MSLog.Level.User);
                        MSLog.Log("Response Error: " + responseFromServer, MSLog.Level.User);
                        if (finishedCallback != null)
                            finishedCallback.Invoke(String.Empty, false,responseFromServer);
                    }
                    else if (finishedCallback != null)
                        finishedCallback.Invoke(responseFromServer, true);
                }
                catch (WebException e)
                {
                    bool showErrorWindow = true;
                    if (e.Status == WebExceptionStatus.ConnectFailure ||
                        e.Status == WebExceptionStatus.Timeout ||
                        e.Status == WebExceptionStatus.ConnectionClosed)
                    {
                        syncStateCallback(Dialog_ModSync_Window.CurrentSyncState.ModSyncOffline);
                        showErrorWindow = false;
                    }
                    MSLog.Log("ModSync Ninja has encountered an error with your request if this error repeats, feel free to submit the error below to the forums", MSLog.Level.User);
                    MSLog.Log("Your request: " + userRequestStr, MSLog.Level.User);
                    if (e.Status == WebExceptionStatus.ProtocolError)
                    {
                        response = (HttpWebResponse)e.Response;
                        MSLog.Log("Response Errorcode: " + (int)response.StatusCode, MSLog.Level.User);
                        if (finishedCallback != null)
                            finishedCallback.Invoke(String.Empty, false, response.StatusCode.ToString());
                    }
                    else
                    {
                        MSLog.Log("Response error: " + e.Status, MSLog.Level.User);
                        if (finishedCallback != null)
                            finishedCallback.Invoke(String.Empty, false,e.Status.ToString());
                    }
                    if(showErrorWindow) MSLog.ShowErrorScreen();
                }
                finally
                {
                    if (response != null)
                    {
                        response.Close();
                    }
                }
            }).Start();
        }
    }
}
