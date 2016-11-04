using LitJson;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace QiniuBackupAndM3u8
{
    class Program
    {
        /// <summary>
        /// 配置
        /// </summary>
        static NameValueCollection Config = null;

        //m3u8分片的存放
        static List<string> listTSname = new List<string>();
      


        /// <summary>
        /// 程序入口
        /// </summary>
        static void Main(string[] args)
        {
            // 读取配置文件
            Config = ConfigurationManager.AppSettings;

            // 全局异常捕捉
            try
            {
                Console.WriteLine("================我是漂亮的分割线========================");
                Console.WriteLine("这里是备份与m3u8列表删除工具");
                Console.WriteLine("数据无价!!!!!!请先确保配置文件中各项配置填写正确");
                Console.WriteLine("@author xingwen 博客(小马哥nice) http://blog.csdn.net/qq_14997169");
                Console.WriteLine(">>>>>> 退出程序可以在程序运行时  请按任意键");
                Console.WriteLine("================我是漂亮的分割线========================");

                Console.WriteLine("请输入您要的操作  1为执行删除指定m3u8列表的操作    2为备份空间操作 .");
                Console.WriteLine("请输入1 或者 2.");

                string str = Console.ReadLine();

                if (str == "1")
                {
                    //删除指定的m3u8
                    ExecuteDelete();
                }
                else if (str == "2")
                {
                    //备份
                    ExecuteBackUp();
                }
                while (true)
                {
                    Console.ReadKey(false);
                }

              
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine("===官方api===错误码参考");
                Console.WriteLine("400  管理凭证无效 ");
                Console.WriteLine("401  请求报文格式错误 ");
                Console.WriteLine("599  服务端操作失败 ");
                Console.WriteLine("612  待删除资源不存在 ");
  
                while (true)
                {
                    Console.ReadKey(false);
                }
            }
        }

        /// <summary>
        /// 运行指定删除
        /// </summary>
        static void ExecuteDelete()
        {

            GetM3U8ts();

            Console.WriteLine("请选择执行以下操作");
            Console.WriteLine("--备份ts后执行云ts删除以上检索到的ts文件操作  请输入y --");
            Console.WriteLine("--直接删除以上检索到的ts文件操作  请输入d --");
            Console.WriteLine("--程序终止 请输入n --");
           string str = Console.ReadLine();

            if (str != "y" && str != "d")
            {
                Console.WriteLine("=======程序终止========");
                return;
            }
           

            //遍历该 m3u8文件存在的ts内容
            for (int i = 0; i < listTSname.Count; i++)
            {
                Console.WriteLine(listTSname[i]);
                if (str == "y")
                {
                    DeleteThis("", Config["BucketM3U8"], listTSname[i], "y");//备份ts后删除
                }
                else if (str == "d")
                {
                    DeleteThis("", Config["BucketM3U8"], listTSname[i], "n");//不备份ts 直接删除
                }
            }

            // 删除完成
            Console.WriteLine("================我是漂亮的分割线========================");
            Console.WriteLine();
            Console.WriteLine("若需要继续操作请退出后重新进入");
            Console.ReadKey(true);
        }

        /// <summary>
        /// 运行备份
        /// </summary>
        static void ExecuteBackUp()
        {

           // if (Console.ReadKey(true).Key != ConsoleKey.Enter) return;

            // 私有空间下载签名的过期时间
            DateTime expired = DateTime.Now.AddMonths(1);

            // 每次扫描列表的数量
            int limit = 100;


            // 读取次数，成功下载次数，跳过次数
            int times = 0, success = 0, skip = 0;

            // 循环扫描文件列表
            string marker = "";
            do
            {
                Log("开始扫描第 " + (limit * times) + " 至 " + (limit * ++times) + " 个文件");

                // 扫描文件，取得扫描结果
                JsonData result = List(Config["Prefix"], limit, marker);
                JsonData items = result["items"];
                marker = result.Keys.Contains("marker") ? (string)result["marker"] : "";

                // 遍历文件列表，下载文件
                Log("扫描到 " + items.Count + " 个文件，开始下载");
                foreach (JsonData item in items)
                {
                    // 资源名，文件名
                    string filename = (string)item["key"];
                    string savepath = Config["SaveAs"] + filename.Replace('/', '\\');

                    // 如果文件存在，覆盖则删除，不覆盖则跳过
                    if (File.Exists(savepath))
                    {
                        if (Convert.ToBoolean(Config["OverWrite"])) File.Delete(savepath);
                        else
                        {
                            skip++;
                            continue;
                        }
                    }

                    // 检查并创建文件夹
                    CheckPath(savepath);

                    // 下载地址，如果为私有空间则追加签名
                    string url = Config["Domain"] + filename;
                    if (Convert.ToBoolean(Config["Private"])) url = url + "?" + DownloadToken(url, expired);

                    // 下载资源
                    Log("开始下载：" + filename);
                    WebClient web = new WebClient();
                    web.DownloadFile(url, savepath);
                    success++;
                }

            } while (marker != "");

            // 下载完成
            Console.WriteLine("========================================");
            Log("下载完成，成功下载 " + success + " 个文件，跳过 " + skip + " 个文件！");
            Console.WriteLine();
            Console.WriteLine("按任意键完成并退出");
            Console.ReadKey(true);
        }


        /// <summary>
        /// 输出时间和日志
        /// </summar>y
        static void Log(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        }


        /// <summary>
        /// 检查路径，创建目录
        /// </summary>
        static void CheckPath(string path)
        {
            path = path.Substring(0, path.LastIndexOf('\\'));
            if (Directory.Exists(path) == false) Directory.CreateDirectory(path);
        }

        /// <summary>
        /// 删除指定文件前先备份
        /// </summary>
        static void toBackBeforeDelete(string fileKey)
        {
            // 私有空间下载签名的过期时间
            DateTime expired = DateTime.Now.AddMonths(1);

            string savepath = Config["SaveAs"] + fileKey.Replace('/', '\\');

            // 检查并创建文件夹
            CheckPath(savepath);


            Console.WriteLine(">>>>>备份到"+savepath);
            // 下载地址，如果为私有空间则追加签名
            string url = Config["Domain"] + fileKey;
             url = url + "?" + DownloadToken(url, expired);

             Console.WriteLine(savepath);

            // 下载资源
            Log("开始下载：" + fileKey);
            WebClient web = new WebClient();
            web.DownloadFile(url, savepath);
            Log("删除前先备份完成文件：" + fileKey);

        }

        /// <summary>
        /// 读取资源列表
        /// </summary>
        static JsonData List(string prefix = "", int limit = 100, string marker = "")
        {
            string uri = string.Format("/list?bucket={0}&marker={1}&limit={2}&prefix={3}",
                HttpUtility.UrlEncode(Config["Bucket"]),
                HttpUtility.UrlEncode(marker),
                HttpUtility.UrlEncode(limit.ToString()),
                HttpUtility.UrlEncode(prefix)
                );

            WebClient web = new WebClient();
            web.Encoding = Encoding.UTF8;
            web.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
            web.Headers.Add(HttpRequestHeader.Authorization, "QBox " + AccessToken(uri));


            string result = web.DownloadString("http://rsf.qbox.me" + uri);

            return JsonMapper.ToObject(result);
        }

      
        static string strFileName = "";

        //根据m3u8文件获得ts分片
        static bool LoadTSNameList(string intListNum)
        {
            

            strFileName = "M3U8File/" + intListNum.ToString() + ".m3u8";

            try
            {
                if (!File.Exists(strFileName))
                {
                    Console.WriteLine("---文件在本地不存在---");
                    return false;
                }
                FileStream myFileStream = new FileStream(strFileName, FileMode.Open);
                StreamReader myStreamReader = new StreamReader(myFileStream, Encoding.GetEncoding("GB2312"));
                string strLine = myStreamReader.ReadLine();

                //检索符合的字符串操作
                while (strLine.Trim() != "#EXT-X-ENDLIST")
                {
                    if(strLine.Trim().IndexOf(".ts")>0){
                        Console.WriteLine(">>>>>>检索到" + strLine.Substring(1));
                        listTSname.Add(strLine.Substring(1));

                    }
                 // Console.WriteLine(">>>>>>检索到"+strLine);
                    strLine = myStreamReader.ReadLine();
                }
                myStreamReader.Close();
                myFileStream.Close();
                strFileName = "";
            }
            catch (Exception ex)
            {
                throw new Exception("解析m3u8文件失败" + ex.ToString());
            }
            return true;
        }


         
        /// <summary>
        ///获得指定的m3u8的分片资源
        /// </summary>
        static string GetM3U8ts()
        {
            string str;

            if (listTSname == null)
            {
                listTSname = new List<string>();
            }
            listTSname.Clear();

            Console.WriteLine("----------请确保文件放在exe同级目录的M3U8File 文件夹中--------");
            Console.WriteLine("--请输入指定的m3u8的文件的序号: 譬如 xingwen.m3u8文件 则输入 xingwen回车即可--");

            str = Console.ReadLine();

            Console.WriteLine("搜索文件: " + str + ".m3u8");
            if (!LoadTSNameList(str))
            {//假如获取本地文件失败，则中断
                Console.WriteLine("=======程序终止========");
                return null;
            }
            
           
            return "";

        }

        /// <summary>
        ///执行写入文件操作
        /// </summary>
        static void WriteLogInfo(string LogStr="")
        {
              // 创建文件
            FileStream fs = new FileStream("log.txt", FileMode.Append); //可以指定盘符，也可以指定任意文件名
            StreamWriter sw = new StreamWriter(fs);
           sw.WriteLine(LogStr); // 写入
           sw.Close(); //关闭文件
        }

        /// <summary>
        /// 删除指定的资源
        /// </summary>
        static string DeleteThis(string prefix = "", string Bucket = "", string key = "",string issave="")
        {
           
            WebClient web = new WebClient();
            web.Encoding = Encoding.UTF8;
            web.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
            Console.WriteLine("发送Post到七牛:");
            Console.WriteLine("/delete/" + Base64Encode(Bucket + ":" + key));
            string uri = "/delete/" + Base64Encode(Bucket + ":" + key);
            Console.WriteLine("发送Authorization到七牛:");
            String s= "QBox " + AccessToken(uri);
           
            Console.WriteLine(s);
            web.Headers.Add(HttpRequestHeader.Authorization, s);

            Console.WriteLine("----------当前删除的文件为" + key);


            if (issave == "y")
            {
                //删除前备份m3u8 ts分片操作
                //先执行下载到本地文件夹
                Console.WriteLine(">>>从私有空间开始备份 " + key + "  到 M3U8File/backup中");
                //执行下载操作
                toBackBeforeDelete(key);
            }

            //保存的日志文件中log.txt
            if (web.DownloadString("http://rs.qiniu.com" + uri)!=null)
            {
                WriteLogInfo("------>>>>>>>>>>>>>>>>>>>>>---------删除完成" + key);
                Console.WriteLine("------>>>>>>>>>>>>>>>>>>>>>---------删除完成" + key);
            }


            return "success";
        }


        /// <summary>
        /// URL 安全的 Base64 编码
        /// </summary>
        static string Base64Encode(string content)
        {
            return string.IsNullOrEmpty(content) 
                ? "" 
                : Base64Encode(Encoding.UTF8.GetBytes(content));
        }
        static string Base64Encode(byte[] content)
        {
            if (content == null || content.Length == 0) return "";
            string encode = Convert.ToBase64String(content);
            return encode.Replace('+', '-').Replace('/', '_');
        }


        /// <summary>
        /// 使用 HMAC-SHA1 对内容签名
        /// </summary>
        static string Sign(string content)
        {
            byte[] key = Encoding.UTF8.GetBytes(Config["SecretKey"]);
            HMACSHA1 hmac = new HMACSHA1(key);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
            string encode = Base64Encode(hash);
            return Config["AccessKey"] + ":" + encode;
        }


        /// <summary>
        /// 管理凭证
        /// </summary>
        static string AccessToken(string uri, string body = "")
        {
            return Sign(uri + "\n" + body);
        }


        /// <summary>
        /// 下载凭证
        /// </summary>
        static string DownloadToken(string url, DateTime expired)
        {
            DateTime time = new DateTime(1970, 1, 1);
            string e = ((int)(expired - time).TotalSeconds).ToString();
            return string.Format("e={0}&token={1}", e, Sign(url + "?e=" + e));
        }
    }
}
