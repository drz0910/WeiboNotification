using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeiboNotification
{
    public partial class Form : System.Windows.Forms.Form
    {
        DateTime baseDate;
        private HashSet<long> idSet;
        private Queue<DateTime> dateQueue;
        private Queue<string> blogQueue;
        private Thread piThread;
        private string siteURL = @"http://m.weibo.cn";
        private string tagPattern = @"\<a.*?\<\/a\>\s*";

        public Form()
        {
            InitializeComponent();
            baseDate = new DateTime(1970, 1, 1, 8, 0, 0, 0);
            idSet = new HashSet<long>();
            dateQueue = new Queue<DateTime>(21);
            blogQueue = new Queue<string>(21);
        }

        private void SaveNotification(DateTime date, string blog)
        {
            dateQueue.Enqueue(date);
            blogQueue.Enqueue(blog);
            if (blogQueue.Count == 20)
            {
                dateQueue.Dequeue();
                blogQueue.Dequeue();
            }
        }

        private String expandArticle(String text)
        {
            Match match = Regex.Match(text, "href=\\\".*?\\\"");
            String expandURL = siteURL + text.Substring(match.Index, match.Length).Remove(0, 5).Replace("\"", "");
            while (true)
            {
                System.Net.HttpWebRequest request;
                // Create HTTP request
                request = (System.Net.HttpWebRequest)WebRequest.Create(expandURL);
                System.Net.HttpWebResponse response;
                try
                {
                    response = (System.Net.HttpWebResponse)request.GetResponse();

                }
                catch (Exception)
                {
                    SaveNotification(DateTime.Now, "连接错误");
                    Thread.Sleep(30000);
                    continue;
                }
                System.IO.StreamReader myreader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                string responseText = myreader.ReadToEnd();
                // scan from right
                match = Regex.Match(responseText, @"class=.*?weibo-text.*?\<\/div\>", RegexOptions.RightToLeft);
                responseText = responseText.Substring(match.Index);
                // scan from left
                match = Regex.Match(responseText, @"\<\/div\>");
                responseText = responseText.Substring(0, match.Index);
                // remove tags
                if (Regex.IsMatch(responseText, tagPattern))
                    responseText = Regex.Replace(responseText, tagPattern, "");
                // remove header
                return Regex.Replace(responseText, @"class.*\>", "").Trim();
            }
        }

        private void RefreshNotification()
        {
            while (true)
            {
                
                string mainURL = siteURL + @"/page/card?itemid=1005051821993884_-_WEIBO_INDEX_PROFILE_WEIBO_GROUP_OBJ&callback=_1456298670085_5";
                String expandPattern = @".?\<a.*?...全文.*?\<\/a\>\s*";
                System.Net.HttpWebRequest request;
                // Create HTTP request
                request = (System.Net.HttpWebRequest)WebRequest.Create(mainURL);
                System.Net.HttpWebResponse response;
                try
                {
                    response = (System.Net.HttpWebResponse)request.GetResponse();
                   
                }
                catch(Exception)
                {
                    SaveNotification(DateTime.Now, "连接错误");
                    Thread.Sleep(30000);
                    continue;
                }
                System.IO.StreamReader myreader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                string responseText = myreader.ReadToEnd().Replace("_1456298670085_5(", "").Replace(")", "");
                myreader.Close();
                JObject obj = JObject.Parse(responseText);
                if(obj.Count < 5)
                {
                    SaveNotification(DateTime.Now, "解析错误");
                    Thread.Sleep(30000);
                    continue;
                }
                IList<JToken> list = obj["card_group"].Children().ToList();
                long id;
                DateTime date;
                string str;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    JToken blog = list[i]["mblog"];
                    if (blog != null && blog["isTop"] == null)
                    {
                        id = long.Parse(blog["id"].ToString());
                        if (!idSet.Contains(id))
                        {
                            idSet.Add(id);
                            date = baseDate.AddSeconds(long.Parse(blog["created_timestamp"].ToString()));
                            str = blog["text"].ToString();
                            // check if need to expand
                            if (Regex.IsMatch(str, expandPattern))
                                str = expandArticle(str.Substring(Regex.Match(str, expandPattern, RegexOptions.RightToLeft).Index));
                            // remove tags
                            else if (Regex.IsMatch(str, tagPattern))
                                str = Regex.Replace(str, tagPattern, "");

                            SaveNotification(date, str);
                            notifyIcon.ShowBalloonTip(20000, date.ToShortTimeString(), str, ToolTipIcon.Info);
                        }
                    }
                }
                if (InvokeRequired)
                {
                    Invoke(new InvokeDelegate(RefreshView));
                }

                DateTime now = DateTime.Now;
                if(now.Hour < 8 && now.Hour > 15)
                {
                    Thread.Sleep(3000000);
                }else
                {
                    Thread.Sleep(60000);
                }
                
            }
        }
        private delegate void InvokeDelegate();
        private void RefreshView()
        {
            Queue<DateTime>.Enumerator dateEnum = dateQueue.GetEnumerator();
            Queue<string>.Enumerator blogEnum = blogQueue.GetEnumerator();

            listViewMessage.Items.Clear();
            while (dateEnum.MoveNext() && blogEnum.MoveNext())
            {
                String dateStr = dateEnum.Current.ToShortTimeString() + ' ' + dateEnum.Current.ToShortDateString();
                String blogStr = blogEnum.Current;
                int wrap = 35;
                int index = wrap;
                if(blogStr.Length < index)
                {
                    listViewMessage.Items.Add(new ListViewItem(new string[] { dateStr, blogStr }));
                }
                else
                {
                    listViewMessage.Items.Add(new ListViewItem(new string[] { dateStr, blogStr.Substring(0, wrap) }));
                    while (blogStr.Length > index + wrap)
                    {
                        listViewMessage.Items.Add(new ListViewItem(new string[] { "", blogStr.Substring(index, wrap) }));
                        index += wrap;
                    }
                    
                    listViewMessage.Items.Add(new ListViewItem(new string[] { "", blogStr.Substring(index) }));
                }
               
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            piThread = new Thread(new ThreadStart(RefreshNotification));
            piThread.Start();
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible)
            {
                hideToolStripMenuItem_Click(sender, e);
            }
            else
            {
                openToolStripMenuItem_Click(sender, e);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void listViewMessage_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://weibo.com/u/1821993884?is_all=1");
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            piThread.Abort();
        }
    }
}
