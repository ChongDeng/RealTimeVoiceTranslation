using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeTranscript
{
    // This class represents the websocket response from iflyetk server.
    [DataContract]
    public class ServerResponse
    {
        [DataMember]
        public String action; //"started": handshake; "result": transcript result; "error": websocket connnection error

        [DataMember]
        public String code;

        [DataMember]
        public String data;

        [DataMember]
        public String desc;

        [DataMember]
        public String sid;
    }

    class Program
    {
        //PCM Audo file path. This pcm audio sample is from the offical java demo code project
        private static String PCM_AUDIO_FILE = "./test_1.pcm";

        //The recommened size of data to be sent from client. This value is recommended by iflytek api tutorial.
        private static int CHUNCKED_SIZE = 1280;

        /* This is id of iflytek App: When you create an app from iflytek console, 
                 * you will get an app id */
        private static String AppId = "XXX";

        /* This is the secret key of iflytek realt-time transcript feature. After you create the above AppId, 
         * there is no SECRET_KEY. So, you need to use this AppId to apply for realt-time transcript. Then, 
         * after your application is approved, you will have this SECRET_KEY then.
         */
        private static String SECRET_KEY = "YYY";

        private static String Translate_API_Key = "ZZZ";

        static void Main(string[] args)
        {
            //get real-time transcipt from pcm audio file 
            SpeechToText(PCM_AUDIO_FILE);

            //Translate("床前明月光");            
            
            Console.ReadLine();
        }

        //to get real-time transcipt from pcm audio file
        static void SpeechToText(String PcmAudioFile)
        {
            try
            {
                if (!File.Exists(PcmAudioFile))
                {
                    Console.WriteLine("audio file " + PcmAudioFile + " does not exist!");
                    return;
                }

                //Part url of iflytek websocket server uri
                String HOST = "rtasr.xfyun.cn/v1/ws";
                String BASE_URL = "ws://" + HOST;    

                //Create handshake paramerter for the following iflytek server websocket connection
                String HandShakePara = EncryptUtil.getHandShakeParams(AppId, SECRET_KEY);

                //to connect websocket
                ClientWebSocket WebSocket = Connect(BASE_URL + HandShakePara);

                //set up websocket receiver thread
                Thread WebSocketReceiverTh = new Thread(() =>
                {
                    ReceiveWebSocketData(WebSocket);
                });
                WebSocketReceiverTh.Start();

                //send pcm audo data via websocket to iflytek server
                SendAudioData(WebSocket, PcmAudioFile);

            }
            catch (Exception ex)
            {
                Console.WriteLine("function SpeechToText get excetpion: " + ex.Message);
            }            
        }

        //to parse the iflytek server resoponse data
        public static void Parse(String ServerResponseData)
        {
            if (String.IsNullOrEmpty(ServerResponseData))
            {
                return;
            }

            try
            {
                ServerResponse Response = new ServerResponse();
                MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(ServerResponseData));
                DataContractJsonSerializer djs = new DataContractJsonSerializer(Response.GetType());
                Response = djs.ReadObject(ms) as ServerResponse;
                ms.Close();
                
                //1 if Response["action"] == "started":  means handshake successful.
                if (Response.action.Equals("started"))
                {
                    Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t handshake successful!");
                }

                //2 if Response["action"] == "result":  means this response includes transcript data.
                else if (Response.action.Equals("result"))
                {
                    //print transcript Chinese text
                    String Transcript = getTranscript(Response.data);
                    Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t result: " + Transcript);

                    //print translate result
                    Translate(Transcript);
                }

                //3 if Response["action"] == "error":  means server found websocket connection has got error.
                else if (Response.action.Equals("error"))
                {
                    Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t Connection Error!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("function Parse get excetpion: " + ex.Message);
            }
        }

        // to get transcript Chinese text that we want
        public static String getTranscript(String ResponseData)
        {
            String res = "";

            try
            {
                JToken messageObj = JObject.Parse(ResponseData);
                JToken cn = messageObj.SelectToken("cn");
                JToken st = cn.SelectToken("st");
                
                IEnumerable<JToken> rtArr = st.SelectTokens("rt");
                foreach (JToken rtArrItem in rtArr)
                {
                    foreach (JToken rtArrObj in rtArrItem)
                    {
                        IEnumerable<JToken> wsArr = rtArrObj.SelectTokens("ws");
                        foreach (JToken wsArrObjItem in wsArr)
                        {
                            foreach (JToken wsArrObj in wsArrObjItem)
                            {
                                IEnumerable<JToken> cwArr = wsArrObj.SelectTokens("cw");
                                foreach (JToken cwArrItem in cwArr)
                                {
                                    foreach (JToken cwArrObj in cwArrItem)
                                    {
                                        String wStr = (String)cwArrObj.SelectToken("w");
                                        res += wStr;
                                    }
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("function getContent(String Data) got exception: " + e.Message);
                return ResponseData;
            }

            return res;
        }

        //to receive the socket response from iflytek server
        public static async void ReceiveWebSocketData(ClientWebSocket webSocket)
        {
            int ReceiveChunkSize = 5000;
            byte[] Buffer = new byte[ReceiveChunkSize];
            ArraySegment<byte> ar = new ArraySegment<byte>(Buffer);

            while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                WebSocketReceiveResult res = null;
                MemoryStream MemStream = new MemoryStream();

                do
                {
                    try
                    {
                        res = await webSocket.ReceiveAsync(new ArraySegment<byte>(Buffer),
                            CancellationToken.None);

                        if (res.Count > 0)
                        {
                            MemStream = new MemoryStream();
                            MemStream.Write(ar.Array, 0, res.Count);
                        }

                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                string.Empty, CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t Websocket disconnected now: server closes websocket after sending all results.");
                    }
                } while (res != null && !res.EndOfMessage);

                byte[] ResBuffer = MemStream.GetBuffer();
                String ServerResponse = Encoding.UTF8.GetString(ResBuffer, 0, (int)MemStream.Length);

                //parse the iflytek server resoponse data
                Parse(ServerResponse);
            }
        }

         /* 
         *    This function is to send pcm audio data to websocket server, inorder to get transcript text.
         *    Please keep in mind that iflytek requires the audo format as folows:
         *          (1) sampling frequency is 16K
         *          (2) sampling depth is 16bits
         *          (3) audio must be pcm_s16le
         */
        public static void SendAudioData(ClientWebSocket websocket, String PcmAudioFile)
        {
            byte[] Bytes = new byte[CHUNCKED_SIZE];
            using (FileStream fs = new FileStream(PcmAudioFile, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNCKED_SIZE, FileOptions.RandomAccess))
            {
                int len = -1;
                //long lastTs = 0;              
                while ((len = fs.Read(Bytes, 0, Bytes.Length)) > 0)
                {                   
                    if (len < CHUNCKED_SIZE)
                    {
                        websocket.SendAsync(new ArraySegment<byte>(Bytes.Take(len).ToArray()),
                            WebSocketMessageType.Binary, true, CancellationToken.None);
                        //Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t send finish");
                        break;
                    }

                    //long curTs = (long)(DateTime.UtcNow - EncryptUtil.Jan1st1970).TotalMilliseconds;
                    //if (lastTs == 0)
                    //{
                    //    lastTs = (long)(DateTime.UtcNow - EncryptUtil.Jan1st1970).TotalMilliseconds;                      
                    //}
                    //else
                    //{
                    //    long s = curTs - lastTs;
                    //    if (s < 40)
                    //    {
                    //        Console.WriteLine("error time interval: " + s + " ms");
                    //    }
                    //}
                   
                    websocket.SendAsync(new ArraySegment<byte>(Bytes),
                            WebSocketMessageType.Binary, true, CancellationToken.None);
                    //Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t send");

                    /* Send data every 40 ms. This is recommended by iflytek api tutorial.
                     * iflytek indicates that: If send data too fast, its transcript engine may get error.
                     */
                    Thread.Sleep(40);
                }
            }

            Thread.Sleep(40);

            // Send terminate flag: since api tutorial requires to send end message after all audio data has been sent. 
            String EndData = "{\"end\": true}";         
            byte[] EndBytes = Encoding.ASCII.GetBytes(EndData);
            websocket.SendAsync(new ArraySegment<byte>(EndBytes),
                            WebSocketMessageType.Binary, true, CancellationToken.None);   
                  
            Console.WriteLine(DateTime.Now.ToString("hh:mm:ss.fff") + "\t Client finishes sending game over flag");
        }

        //this is to connect websocket
        public static ClientWebSocket Connect(String Uri)
        {
            ClientWebSocket WebSocket = null;
            try
            {
                WebSocket = new ClientWebSocket();
                Task Conn = WebSocket.ConnectAsync(new Uri(Uri), CancellationToken.None);
                Conn.Wait();
                return WebSocket;
            }
            catch (Exception ex)
            {
                Console.WriteLine("function Connect get exception: " + ex.Message);
            }
            return null;
        }

        public static void Translate(String Text)
        {
            String src = "cn";
            String to = "en";
            String url = "http://openapi.openspeech.cn";
            String APIKey = "98e4db853a0be99eb213ac01dd8b2991";

            String x_param = Base64Encode("appid=" + AppId);

            //String Signa = EncryptUtil.CreateMD5(Text + x_param + APIKey);
            String Signa = "";

            using (MD5 md5 = MD5.Create())
            {
                byte[] InputBytes = Encoding.UTF8.GetBytes(Text + x_param + Translate_API_Key);
                byte[] HashBytes = md5.ComputeHash(InputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < HashBytes.Length; i++)
                {
                    sb.Append(HashBytes[i].ToString("x2"));
                }
                Signa = sb.ToString();
            }

            String Uri = "http://openapi.openspeech.cn/webapi/its.do?svc=its&token=its&from=" + src + "&to=" + to + "&q=" + Text + "&sign=" + Signa;
            WebRequest(Uri, Text, x_param);

        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private static void WebRequest(String WEBSERVICE_URL, String Text = "", String X_Para = "")
        {            
            try
            {
                var webRequest = System.Net.WebRequest.Create(WEBSERVICE_URL);
                if (webRequest != null)
                {
                    webRequest.Method = "GET";
                    //webRequest.Timeout = 12000;
                    //webRequest.ContentType = "application/json";
                    //webRequest.Headers.Add("Authorization", "Basic dchZ2VudDM6cGFdGVzC5zc3dvmQ=");
                    webRequest.Headers.Add("X-Par", X_Para);
                    webRequest.Headers.Add("Ver", "1.0");

                    using (System.IO.Stream s = webRequest.GetResponse().GetResponseStream())
                    {
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(s))
                        {
                            String Response = Base64Decode(sr.ReadToEnd());
                            //Console.WriteLine(String.Format("Response: {0}", Response));
                            try
                            {
                                JToken MessageObj = JObject.Parse(Response);
                                int ret = (int)MessageObj.SelectToken("ret");
                                if (ret == 0)
                                {
                                    JToken TransResultObj = MessageObj.SelectToken("trans_result");
                                    String Result = (String) TransResultObj.SelectToken("dst");
                                    Console.WriteLine(String.Format("\t\t\t\t" + Text + " -> " + Result));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("json parsing get exception: " + ex.Message);
                            }
                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
