using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace UsersnapCFTwitterService
{
    public class TweetStreamer
    {
        [DataContract]
        protected class DCUser {
            [DataMember(Name = "name")]
            public string name { get; set; }
            [DataMember(Name = "id")]
            public string id { get; set; }
            [DataMember(Name = "url")]
            public string url { get; set; }
        }

        [DataContract]
        protected class DCTweet
        {
            [DataMember(Name = "text")]
            public string text { get; set; }
            [DataMember(Name = "created_at")]
            public string created_at { get; set; }
            [DataMember(Name = "id")]
            public string id { get; set; }
            [DataMember(Name = "user")]
            public DCUser user { get; set; }
        }

        [DataContract]
        protected class DCMeta
        {
            [DataMember(Name = "refresh_url")]
            public string refresh_url { get; set; }
            
        }

        [DataContract]
        protected class DCResponse
        {
            [DataMember(Name = "statuses")]
            public DCTweet[] statuses { get; set; }
            [DataMember(Name = "search_metadata")]
            public DCMeta search_metadata { get; set; }
        }

        private static TweetStreamer instance = null;

        private string queryStr;
        private int cacheSize;
        List<Tweet> tweets;

        private string accessToken = null;

        private const string CONSUMER_KEY = "[PUT-YOUR-CONSUMER-KEY-HERE]";
        private const string CONSUMER_SECRET = "[PUT-YOUR-CONSUMER-SECRET-HERE]";

        private TweetStreamer(string query, int cs)
        {
            queryStr = query;
            cacheSize = cs;
            tweets = new List<Tweet>();
        }

        public static TweetStreamer getInstance(string baseQuery, int cacheSize)
        {
            if (TweetStreamer.instance == null)
            {
                TweetStreamer.instance = new TweetStreamer(baseQuery, cacheSize);
            }
            return TweetStreamer.instance;
        }

        public List<Tweet> getTweets(int fromIdx)
        {
            this.doRefresh();
            if (this.tweets.Count > this.cacheSize)
            {
                fromIdx -= (this.tweets.Count - this.cacheSize);
                this.tweets.RemoveRange(0, this.tweets.Count - this.cacheSize);
            }
            if (fromIdx < 0)
            {
                fromIdx = 0;
            }
            return this.tweets.GetRange(fromIdx, this.getLastIdx()-fromIdx);
        }

        static private string EncodeTo64(string toEncode)
        {
            byte[] toEncodeAsBytes
                  = System.Text.ASCIIEncoding.ASCII.GetBytes(toEncode);
            string returnValue
                  = System.Convert.ToBase64String(toEncodeAsBytes);
            return returnValue;
        }

        static private string DecodeFrom64(string encodedData)
        {
            byte[] encodedDataAsBytes
                = System.Convert.FromBase64String(encodedData);
            string returnValue =
               System.Text.ASCIIEncoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }

        public int getLastIdx()
        {
            return this.tweets.Count;
        }

        private void authenticate() { 
            WebClient c = new WebClient();
            c.Headers.Add("User-Agent", "Usersnap TFS Demo v0.1");
            c.Headers.Add("Content-Type", "application/x-www-form-urlencoded;charset=UTF-8");
            c.Headers.Add("Authorization", "Basic " + TweetStreamer.EncodeTo64(TweetStreamer.CONSUMER_KEY + ':' + TweetStreamer.CONSUMER_SECRET));
            string data = c.UploadString("https://api.twitter.com/oauth2/token", "grant_type=client_credentials");
            JavaScriptSerializer serial = new JavaScriptSerializer();
            var jsonObject = serial.DeserializeObject(data) as Dictionary<string, object>;
            this.accessToken = jsonObject["access_token"].ToString();
        }

        private void doRefresh()
        {
            if (this.accessToken == null)
            {
                this.authenticate();
            }
            WebClient c = new WebClient();
            c.Headers.Add("User-Agent", "Usersnap TFS Demo v0.1");
            c.Headers.Add("Authorization", "Bearer " + this.accessToken);
            string url = "https://api.twitter.com/1.1/search/tweets.json" + this.queryStr;
            string data = "";
            try
            {
                data = c.DownloadString(url);
                DCResponse jsonObject = JsonConvert.DeserializeObject<DCResponse>(data);

                List<Tweet> tmp = new List<Tweet>();
                Tweet t = null;
                TweetAuthor ta = null;
                foreach (DCTweet dct in jsonObject.statuses)
                {
                    t = new Tweet();
                    t.Id = dct.id;
                    t.Published = DateTime.ParseExact(dct.created_at, "ddd MMM dd H:mm:ss zzz yyyy", CultureInfo.InvariantCulture);
                    t.Text = dct.text;
                    ta = new TweetAuthor();
                    ta.Name = dct.user.name;
                    ta.Uri = dct.user.url;
                    t.Author = ta;
                    tmp.Add(t);
                }

                tmp.Reverse();
                this.tweets.AddRange(tmp);
                this.queryStr = jsonObject.search_metadata.refresh_url;
            } catch(Exception e) {
                this.authenticate();
            }
        }

        
    }
}