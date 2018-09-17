using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace RealTimeTranscript
{
    class EncryptUtil
    {
        public static readonly DateTime Jan1st1970 = new DateTime
            (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static long getCurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds / 1000;
        }
        
        public static string CreateMD5(string Input)
        {
            String res = "";

            try
            {
                // Use input string to calculate MD5 hash
                using (MD5 md5 = MD5.Create())
                {
                    byte[] InputBytes = Encoding.ASCII.GetBytes(Input);
                    byte[] HashBytes = md5.ComputeHash(InputBytes);

                    // Convert the byte array to hexadecimal string
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < HashBytes.Length; i++)
                    {
                        sb.Append(HashBytes[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateMD5 function get exception: " + ex.Message);
            }
            return res;
        }

        public static string HmacSHA1Encrypt(String Text, String Key)
        {
            String res = "";

            try
            {

                Encoding encode = Encoding.GetEncoding("utf-8");
                byte[] ByteData = encode.GetBytes(Text);
                byte[] ByteKey = encode.GetBytes(Key);
                HMACSHA1 Hmac = new HMACSHA1(ByteKey);
                CryptoStream cs = new CryptoStream(Stream.Null, Hmac, CryptoStreamMode.Write);
                cs.Write(ByteData, 0, ByteData.Length);
                cs.Close();
                return Convert.ToBase64String(Hmac.Hash);
            }
            catch (Exception ex)
            {
                Console.WriteLine("HmacSHA1Encrypt function get exception: " + ex.Message);
            }

            return res;
        }

        // to gnererate handshake paramerter for iflytek server websocket connection
        public static String getHandShakeParams(String appId, String secretKey)
        {
            String TimeStamp = getCurrentTimeMillis() + "";
           
            String signa = "";
            try
            {
                String MD5_Val = CreateMD5(appId + TimeStamp);
                signa = HttpUtility.UrlEncode(HmacSHA1Encrypt(MD5_Val, secretKey), Encoding.UTF8);
                return "?appid=" + appId + "&ts=" + TimeStamp + "&signa=" + signa;
            }
            catch (Exception ex)
            {
                Console.WriteLine("getHandShakeParams function get exception: " + ex.Message);
            }

            return "";
        }
    }
}
