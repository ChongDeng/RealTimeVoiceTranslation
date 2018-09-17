using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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

        static void Main(string[] args)
        {
            //get real-time transcipt from pcm audio file 
            SpeechToText(PCM_AUDIO_FILE);

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

                /* This is id of iflytek App: When you create an app from iflytek console, 
                 * you will get an app id */
                String AppId = "XXX";

                /* This is the secret key of iflytek realt-time transcript feature. After you create the above AppId, 
                 * there is no SECRET_KEY. So, you need to use this AppId to apply for realt-time transcript. Then, 
                 * after your application is approved, you will have this SECRET_KEY then.
                 */
                String SECRET_KEY = "YYY";

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
                    Console.WriteLine(DateTime.Now.ToString("ss.fff") + "\t result: " + getTranscript(Response.data));
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

        }
    }
}
