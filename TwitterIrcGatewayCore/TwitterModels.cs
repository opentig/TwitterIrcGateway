using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// DirectMessage のセットを格納します。
    /// </summary>
    public class DirectMessages
    {
        [JsonProperty("direct_message")]
        public DirectMessage[] DirectMessage;
    }

    /// <summary>
    /// ダイレクトメッセージの情報を表します。
    /// </summary>
    public class DirectMessage
    {
        [JsonProperty("id")]
        public Int64 Id;
        [JsonProperty("text")]
        public String _textOriginal { get; set; }
        [JsonIgnore]
        private String _text { get; set; }
        [JsonIgnore]
        public String Text
        {
            get
            {
                if (!String.IsNullOrEmpty(_textOriginal) && _text == null)
                {
                    _text = Utility.UnescapeCharReference(_textOriginal);
                }

                return _text ?? "";
            }
            set
            {
                _text = value;
            }
        }
        [JsonProperty("sender_id")]
        public String SenderId;
        [JsonProperty("recipient_id")]
        public String RecipientId;
        [JsonProperty("created_at")]
        [JsonConverter(typeof(TwitterDateTimeConverter))]
        public DateTime CreatedAt;
        [JsonProperty("sender_screen_name")]
        public String SenderScreenName;
        [JsonProperty("recipient_screen_name")]
        public String RecipientScreenName;

        public override string ToString()
        {
            return String.Format("DirectMessage: {0} (ID:{1})", Text, Id.ToString());
        }
    }

    /// <summary>
    /// Statusのセットを格納します。
    /// </summary>
    public class Statuses
    {
        public Status[] Status;
    }

    /// <summary>
    /// Userのセットを格納します。
    /// </summary>
    public class UsersList
    {
        [JsonProperty("users")]
        public User[] Users { get; set; }

        [JsonProperty("next_cursor")]
        public Int64 NextCursor { get; set; }

        [JsonProperty("previous_cursor")]
        public Int64 PreviousCursor { get; set; }
    }

    /// <summary>
    /// ステータスを表します。
    /// </summary>
    public class Status
    {
        [JsonProperty("id")]
        public Int64 Id;
        [JsonProperty("in_reply_to_status_id")]
        public String InReplyToStatusId;
        [JsonProperty("in_reply_to_user_id")]
        public String InReplyToUserId;
        [JsonProperty("retweeted_status")]
        public Status RetweetedStatus;
        [JsonProperty("user")]
        public User User;
        [JsonProperty("source")]
        public String Source;
        [JsonProperty("favorited")]
        public String Favorited;

        [JsonProperty("truncated")]
        public Boolean Truncated { get; set; }

        [JsonProperty("retweet_count")]
        public String RetweetCount;

        [JsonProperty("retweeted")]
        public Boolean Retweeted { get; set; }

        [JsonProperty("entities")]
        public Entities Entities;

        [JsonProperty("text")]
        public String _textOriginal { get; set; }
        [JsonIgnore]
        private String _text { get; set; }
        [JsonIgnore]
        public String Text
        {
            get
            {
                if (!String.IsNullOrEmpty(_textOriginal) && _text == null)
                {
                    _text = Utility.UnescapeCharReference(_textOriginal);
                }

                return _text ?? "";
            }
            set
            {
                _text = value;
            }
        }

        [JsonProperty("created_at")]
        [JsonConverter(typeof(TwitterDateTimeConverter))]
        public DateTime CreatedAt { get; set; }

        public override int GetHashCode()
        {
            return (Int32)(this.Id - Int32.MaxValue);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Status))
                return false;

            Status status = obj as Status;
            return (status.Id == this.Id) && (status.Text == this.Text);
        }

        public override string ToString()
        {
            return String.Format("Status: {0} (ID:{1})", Text, Id.ToString());
        }
    }

    /// <summary>
    /// ユーザー情報を表すクラスです。
    /// </summary>
    public class User
    {
        [JsonProperty("id")]
        public Int32 Id;
        [JsonProperty("name")]
        public String Name;
        [JsonProperty("screen_name")]
        public String ScreenName;
        [JsonProperty("location")]
        public String Location;
        [JsonProperty("description")]
        public String Description;
        [JsonProperty("profile_image_url")]
        public String ProfileImageUrl;
        [JsonProperty("url")]
        public String Url;
        [JsonProperty("protected")]
        public Boolean Protected { get; set; }

        [JsonProperty("status")]
        public Status Status;
        [JsonIgnore]
        public Boolean Following { get { return FollowingOriginal ?? false; } set { FollowingOriginal = value; } }
        [JsonProperty("following")]
        public Boolean? FollowingOriginal;

        [JsonProperty("verified")]
        public Boolean Verified;

        public override string ToString()
        {
            return String.Format("User: {0} / {1} (ID:{2})", ScreenName, Name, Id.ToString());
        }
    }

    /// <summary>
    /// エンティティの情報を表します。
    /// </summary>
    public class Entities
    {
        //[XmlElement("media")]
        //public MediaEntity[] Media;
        [JsonProperty("urls")]
        public UrlEntity[] Urls;
        [JsonProperty("hashtags")]
        public HashtagEntity[] Hashtags;
    }

    /// <summary>
    /// URLエンティティの情報を表します。
    /// </summary>
    public class UrlEntity
    {
        //[JsonProperty("start")]
        //public Int32 Start;
        //[JsonProperty("end")]
        //public Int32 End;
        [JsonProperty("url")]
        public String Url;
        [JsonProperty("display_url")]
        public String DisplayUrl;
        [JsonProperty("expanded_url")]
        public String ExpandedUrl;
    }

    /// <summary>
    /// URLエンティティの情報を表します。
    /// </summary>
    public class HashtagEntity
    {
        //[JsonProperty("start")]
        //public Int32 Start;
        //[JsonProperty("end")]
        //public Int32 End;
        [JsonProperty("text")]
        public String Text;
    }

    internal class TwitterDateTimeConverter : IsoDateTimeConverter
    {
        public TwitterDateTimeConverter()
        {
            DateTimeFormat = "ddd MMM dd HH:mm:ss +0000 yyyy";
            DateTimeStyles = DateTimeStyles.AssumeUniversal;
            Culture = CultureInfo.GetCultureInfo("en-us");
        }
    }

    ///// <summary>
    ///// メディアエンティティの情報を表します。
    ///// </summary>
    //[XmlType("creative")]
    //public class MediaEntity
    //{
    //    [XmlElement("id")]
    //    public Int64 Id;
    //    [XmlElement("id_str")]
    //    public String IdStr;
    //    [XmlElement("media_url")]
    //    public String MediaUrl;
    //    [XmlElement("media_url_https")]
    //    public String MediaUrlHttps;
    //    [XmlElement("url")]
    //    public String Url;
    //    [XmlElement("display_url")]
    //    public String DisplayUrl;
    //    [XmlElement("expanded_url")]
    //    public String ExpandedUrl;
    //    [XmlElement("sizes")]
    //    public MediaEntity.EntitySizes Sizes;
    //    [XmlElement("type")]
    //    public String Type;
    //    [XmlAttribute("start")]
    //    public Int32 Start;
    //    [XmlAttribute("end")]
    //    public Int32 End;

    //    [XmlType("size")]
    //    public class EntitySize
    //    {
    //        [XmlElement("resize")]
    //        public String Resize;
    //        [XmlElement("w")]
    //        public Int32 W;
    //        [XmlElement("h")]
    //        public Int32 H;
    //    }

    //    [XmlType("size")]
    //    public class EntitySizes
    //    {
    //        [XmlElement("large")]
    //        public EntitySize Large;
    //        [XmlElement("medium")]
    //        public EntitySize Medium;
    //        [XmlElement("small")]
    //        public EntitySize Small;
    //        [XmlElement("thumb")]
    //        public EntitySize Thumb;
    //    }
    //}
}
