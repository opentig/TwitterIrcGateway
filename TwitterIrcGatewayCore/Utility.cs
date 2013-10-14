using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// 様々な処理に便利な機能を提供します。
    /// </summary>
    public static class Utility
    {
        private const String noEscapeCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        static Utility()
        {
            InitializeCharEntityReferenceTable();
        }

        /// <summary>
        /// 日付の文字列をDateTime型に変換します。
        /// </summary>
        /// <param name="dateTimeString"></param>
        /// <returns></returns>
        public static DateTime ParseDateTime(String dateTimeString)
        {
            DateTime dateTime;
            if (!DateTime.TryParseExact(dateTimeString, "ddd MMM dd HH:mm:ss zz00 yyyy", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.None, out dateTime))
            {
                dateTime = DateTime.Now;
            }

            return dateTime;
        }

        /// <summary>
        /// 文中の TinyURL を展開します。
        /// タイムアウトするまでの時間は1秒です。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns></returns>
        public static String ResolveTinyUrlInMessage(String message)
        {
            return ResolveTinyUrlInMessage(message, 1000);
        }

        /// <summary>
        /// 文中の bitly を展開します。
        /// タイムアウトするまでの時間は1秒です。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns></returns>
        public static String ResolveShortUrlInMessage(String message)
        {
            return ResolveShortUrlInMessage(message, 1000);
        }

        /// <summary>
        /// 文中の TinyURL を展開します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public static String ResolveTinyUrlInMessage(String message, Int32 timeOut)
        {
            return Regex.Replace(message, @"http://tinyurl\.com/[A-Za-z0-9_/.;%&\-]+", delegate(Match m)
            {
                return ResolveRedirectUrl(m.Value, timeOut);
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 文中の htn.to, t.co, bit.ly, j.mp を展開します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public static String ResolveShortUrlInMessage(String message, Int32 timeOut)
        {
            // 改行ゴミがついてるのでついでに削除する
            return Regex.Replace(message, @"(http://(?:htn\.to|t\.co|bit\.ly|j\.mp)/[A-Za-z0-9_/.;%&\-]+)[\r\n]*", delegate(Match m)
            {
                return ResolveRedirectUrl(m.Groups[1].Value, timeOut);
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// TinyURLをリダイレクト先のURLに展開します。
        /// </summary>
        /// <param name="url">TinyURLのURL</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public static String ResolveRedirectUrl(String url, Int32 timeOut)
        {
            HttpWebResponse res = null;
            try
            {
                HttpWebRequest req = HttpWebRequest.Create(url) as HttpWebRequest;
                req.AllowAutoRedirect = false;
                req.Timeout = timeOut;
                req.Method = "HEAD";
                req.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1)";
                res = req.GetResponse() as HttpWebResponse;

                if (res.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    if (!String.IsNullOrEmpty(res.Headers["Location"]))
                    {
                        return res.Headers["Location"];
                    }
                }
                return url;
            }
            catch (WebException)
            {
                return url;
            }
            finally
            {
                if (res != null)
                {
                    res.Close();
                }
            }
        }

        /// <summary>
        /// 文中の URL を TinyURL に変換します。
        /// タイムアウトするまでの時間は1秒です。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns></returns>
        public static String UrlToTinyUrlInMessage(String message)
        {
            return UrlToTinyUrlInMessage(message, 1000);
        }

        /// <summary>
        /// 文中の URL を TinyURL に変換します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public static String UrlToTinyUrlInMessage(String message, Int32 timeOut)
        {
            return Regex.Replace(message, @"https?://[^ ]+", delegate(Match m)
            {
                return UrlToTinyUrl(m.Value, timeOut);
            }, RegexOptions.IgnoreCase);
        }
        /// <summary>
        /// URLをTinyURLに送信して短いURLに変換します。
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public static String UrlToTinyUrl(String url, Int32 timeOut)
        {
            HttpWebResponse res = null;
            try
            {
                HttpWebRequest req = HttpWebRequest.Create("http://tinyurl.com/api-create.php?url="+Utility.UrlEncode(url)) as HttpWebRequest;
                req.AllowAutoRedirect = false;
                req.Timeout = timeOut;
                req.Method = "GET";
                res = req.GetResponse() as HttpWebResponse;
                using (StreamReader sr = new StreamReader(res.GetResponseStream()))
                {
                    return sr.ReadLine();
                }
            }
            catch (WebException)
            {
                return url;
            }
            catch (IOException)
            {
                return url;
            }
            finally
            {
                if (res != null)
                {
                    res.Close();
                }
            }
        }
        /// <summary>
        /// 文字列をURLエンコードします。
        /// </summary>
        /// <param name="s">URLエンコードする文字列</param>
        /// <returns>URLエンコードされた文字列</returns>
        public static String UrlEncode(String s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Char c in s)
            {
                if (noEscapeCharacters.IndexOf(c) > -1)
                {
                    sb.Append(c);
                }
                else
                {
                    Byte[] bytes = Encoding.UTF8.GetBytes(c.ToString());
                    foreach (Byte b in bytes)
                    {
                        sb.AppendFormat("%{0:X2}", b);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 指定した文字列のSHA256 メッセージダイジェストを取得します。
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static String GetMesssageDigest(String s)
        {
            SHA256 sha256 = new SHA256Managed();
            return String.Join("", sha256.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")).ToArray());
        }

        /// <summary>
        /// HTML の数値文字参照・実態参照を元に戻します。
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static String UnescapeCharReference(String s)
        {
            return _regexEntityRef.Replace(s, new MatchEvaluator(ReplaceMatchEvaluator));
        }

        private static Regex _regexEntityRef = new Regex("&(#(?<dig>\\d+|x[0-9a-fA-F]+)|(?<char>[a-zA-Z0-9]{2,8}));", RegexOptions.Singleline);
        private static Dictionary<String, String> _entityReferenceTable;
        private static void InitializeCharEntityReferenceTable()
        {
            _entityReferenceTable = new Dictionary<string,string>(StringComparer.InvariantCultureIgnoreCase);
            _entityReferenceTable["nbsp"] = "\u00a0";
            _entityReferenceTable["iexcl"] = "\u00a1";
            _entityReferenceTable["cent"] = "\u00a2";
            _entityReferenceTable["pound"] = "\u00a3";
            _entityReferenceTable["curren"] = "\u00a4";
            _entityReferenceTable["yen"] = "\u00a5";
            _entityReferenceTable["brvbar"] = "\u00a6";
            _entityReferenceTable["sect"] = "\u00a7";
            _entityReferenceTable["uml"] = "\u00a8";
            //EntityReferenceTable["copy"] = "\u00a9";
            _entityReferenceTable["copy"] = "(C)";
            _entityReferenceTable["ordf"] = "\u00aa";
            _entityReferenceTable["laquo"] = "\u00ab";
            _entityReferenceTable["not"] = "\u00ac";
            _entityReferenceTable["shy"] = "\u00ad";
            //EntityReferenceTable["reg"] = "\u00ae";
            _entityReferenceTable["reg"] = "(R)";
            _entityReferenceTable["macr"] = "\u00af";
            _entityReferenceTable["deg"] = "\u00b0";
            _entityReferenceTable["plusmn"] = "\u00b1";
            _entityReferenceTable["sup2"] = "\u00b2";
            _entityReferenceTable["sup3"] = "\u00b3";
            _entityReferenceTable["acute"] = "\u00b4";
            _entityReferenceTable["micro"] = "\u00b5";
            _entityReferenceTable["para"] = "\u00b6";
            _entityReferenceTable["middot"] = "\u00b7";
            _entityReferenceTable["cedil"] = "\u00b8";
            _entityReferenceTable["sup1"] = "\u00b9";
            _entityReferenceTable["ordm"] = "\u00ba";
            _entityReferenceTable["raquo"] = "\u00bb";
            _entityReferenceTable["frac14"] = "\u00bc";
            _entityReferenceTable["frac12"] = "\u00bd";
            _entityReferenceTable["frac34"] = "\u00be";
            _entityReferenceTable["iquest"] = "\u00bf";
            _entityReferenceTable["Agrave"] = "\u00c0";
            _entityReferenceTable["Aacute"] = "\u00c1";
            _entityReferenceTable["Acirc"] = "\u00c2";
            _entityReferenceTable["Atilde"] = "\u00c3";
            _entityReferenceTable["Auml"] = "\u00c4";
            _entityReferenceTable["Aring"] = "\u00c5";
            _entityReferenceTable["AElig"] = "\u00c6";
            _entityReferenceTable["Ccedil"] = "\u00c7";
            _entityReferenceTable["Egrave"] = "\u00c8";
            _entityReferenceTable["Eacute"] = "\u00c9";
            _entityReferenceTable["Ecirc"] = "\u00ca";
            _entityReferenceTable["Euml"] = "\u00cb";
            _entityReferenceTable["Igrave"] = "\u00cc";
            _entityReferenceTable["Iacute"] = "\u00cd";
            _entityReferenceTable["Icirc"] = "\u00ce";
            _entityReferenceTable["Iuml"] = "\u00cf";
            _entityReferenceTable["ETH"] = "\u00d0";
            _entityReferenceTable["Ntilde"] = "\u00d1";
            _entityReferenceTable["Ograve"] = "\u00d2";
            _entityReferenceTable["Oacute"] = "\u00d3";
            _entityReferenceTable["Ocirc"] = "\u00d4";
            _entityReferenceTable["Otilde"] = "\u00d5";
            _entityReferenceTable["Ouml"] = "\u00d6";
            _entityReferenceTable["times"] = "\u00d7";
            _entityReferenceTable["Oslash"] = "\u00d8";
            _entityReferenceTable["Ugrave"] = "\u00d9";
            _entityReferenceTable["Uacute"] = "\u00da";
            _entityReferenceTable["Ucirc"] = "\u00db";
            _entityReferenceTable["Uuml"] = "\u00dc";
            _entityReferenceTable["Yacute"] = "\u00dd";
            _entityReferenceTable["THORN"] = "\u00de";
            _entityReferenceTable["szlig"] = "\u00df";
            _entityReferenceTable["agrave"] = "\u00e0";
            _entityReferenceTable["aacute"] = "\u00e1";
            _entityReferenceTable["acirc"] = "\u00e2";
            _entityReferenceTable["atilde"] = "\u00e3";
            _entityReferenceTable["auml"] = "\u00e4";
            _entityReferenceTable["aring"] = "\u00e5";
            _entityReferenceTable["aelig"] = "\u00e6";
            _entityReferenceTable["ccedil"] = "\u00e7";
            _entityReferenceTable["egrave"] = "\u00e8";
            _entityReferenceTable["eacute"] = "\u00e9";
            _entityReferenceTable["ecirc"] = "\u00ea";
            _entityReferenceTable["euml"] = "\u00eb";
            _entityReferenceTable["igrave"] = "\u00ec";
            _entityReferenceTable["iacute"] = "\u00ed";
            _entityReferenceTable["icirc"] = "\u00ee";
            _entityReferenceTable["iuml"] = "\u00ef";
            _entityReferenceTable["eth"] = "\u00f0";
            _entityReferenceTable["ntilde"] = "\u00f1";
            _entityReferenceTable["ograve"] = "\u00f2";
            _entityReferenceTable["oacute"] = "\u00f3";
            _entityReferenceTable["ocirc"] = "\u00f4";
            _entityReferenceTable["otilde"] = "\u00f5";
            _entityReferenceTable["ouml"] = "\u00f6";
            _entityReferenceTable["divide"] = "\u00f7";
            _entityReferenceTable["oslash"] = "\u00f8";
            _entityReferenceTable["ugrave"] = "\u00f9";
            _entityReferenceTable["uacute"] = "\u00fa";
            _entityReferenceTable["ucirc"] = "\u00fb";
            _entityReferenceTable["uuml"] = "\u00fc";
            _entityReferenceTable["yacute"] = "\u00fd";
            _entityReferenceTable["thorn"] = "\u00fe";
            _entityReferenceTable["yuml"] = "\u00ff";
            _entityReferenceTable["fnof"] = "\u0192";
            _entityReferenceTable["Alpha"] = "\u0391";
            _entityReferenceTable["Beta"] = "\u0392";
            _entityReferenceTable["Gamma"] = "\u0393";
            _entityReferenceTable["Delta"] = "\u0394";
            _entityReferenceTable["Epsilon"] = "\u0395";
            _entityReferenceTable["Zeta"] = "\u0396";
            _entityReferenceTable["Eta"] = "\u0397";
            _entityReferenceTable["Theta"] = "\u0398";
            _entityReferenceTable["Iota"] = "\u0399";
            _entityReferenceTable["Kappa"] = "\u039a";
            _entityReferenceTable["Lambda"] = "\u039b";
            _entityReferenceTable["Mu"] = "\u039c";
            _entityReferenceTable["Nu"] = "\u039d";
            _entityReferenceTable["Xi"] = "\u039e";
            _entityReferenceTable["Omicron"] = "\u039f";
            _entityReferenceTable["Pi"] = "\u03a0";
            _entityReferenceTable["Rho"] = "\u03a1";
            _entityReferenceTable["Sigma"] = "\u03a3";
            _entityReferenceTable["Tau"] = "\u03a4";
            _entityReferenceTable["Upsilon"] = "\u03a5";
            _entityReferenceTable["Phi"] = "\u03a6";
            _entityReferenceTable["Chi"] = "\u03a7";
            _entityReferenceTable["Psi"] = "\u03a8";
            _entityReferenceTable["Omega"] = "\u03a9";
            _entityReferenceTable["alpha"] = "\u03b1";
            _entityReferenceTable["beta"] = "\u03b2";
            _entityReferenceTable["gamma"] = "\u03b3";
            _entityReferenceTable["delta"] = "\u03b4";
            _entityReferenceTable["epsilon"] = "\u03b5";
            _entityReferenceTable["zeta"] = "\u03b6";
            _entityReferenceTable["eta"] = "\u03b7";
            _entityReferenceTable["theta"] = "\u03b8";
            _entityReferenceTable["iota"] = "\u03b9";
            _entityReferenceTable["kappa"] = "\u03ba";
            _entityReferenceTable["lambda"] = "\u03bb";
            _entityReferenceTable["mu"] = "\u03bc";
            _entityReferenceTable["nu"] = "\u03bd";
            _entityReferenceTable["xi"] = "\u03be";
            _entityReferenceTable["omicron"] = "\u03bf";
            _entityReferenceTable["pi"] = "\u03c0";
            _entityReferenceTable["rho"] = "\u03c1";
            _entityReferenceTable["sigmaf"] = "\u03c2";
            _entityReferenceTable["sigma"] = "\u03c3";
            _entityReferenceTable["tau"] = "\u03c4";
            _entityReferenceTable["upsilon"] = "\u03c5";
            _entityReferenceTable["phi"] = "\u03c6";
            _entityReferenceTable["chi"] = "\u03c7";
            _entityReferenceTable["psi"] = "\u03c8";
            _entityReferenceTable["omega"] = "\u03c9";
            _entityReferenceTable["thetasym"] = "\u03d1";
            _entityReferenceTable["upsih"] = "\u03d2";
            _entityReferenceTable["piv"] = "\u03d6";
            _entityReferenceTable["bull"] = "\u2022";
            _entityReferenceTable["hellip"] = "\u2026";
            _entityReferenceTable["prime"] = "\u2032";
            _entityReferenceTable["Prime"] = "\u2033";
            _entityReferenceTable["oline"] = "\u203e";
            _entityReferenceTable["frasl"] = "\u2044";
            _entityReferenceTable["weierp"] = "\u2118";
            _entityReferenceTable["image"] = "\u2111";
            _entityReferenceTable["real"] = "\u211c";
            //EntityReferenceTable["trade"] = "\u2122";
            _entityReferenceTable["trade"] = "TM";
            _entityReferenceTable["alefsym"] = "\u2135";
            _entityReferenceTable["larr"] = "\u2190";
            _entityReferenceTable["uarr"] = "\u2191";
            _entityReferenceTable["rarr"] = "\u2192";
            _entityReferenceTable["darr"] = "\u2193";
            _entityReferenceTable["harr"] = "\u2194";
            _entityReferenceTable["crarr"] = "\u21b5";
            _entityReferenceTable["lArr"] = "\u21d0";
            _entityReferenceTable["uArr"] = "\u21d1";
            _entityReferenceTable["rArr"] = "\u21d2";
            _entityReferenceTable["dArr"] = "\u21d3";
            _entityReferenceTable["hArr"] = "\u21d4";
            _entityReferenceTable["forall"] = "\u2200";
            _entityReferenceTable["part"] = "\u2202";
            _entityReferenceTable["exist"] = "\u2203";
            _entityReferenceTable["empty"] = "\u2205";
            _entityReferenceTable["nabla"] = "\u2207";
            _entityReferenceTable["isin"] = "\u2208";
            _entityReferenceTable["notin"] = "\u2209";
            _entityReferenceTable["ni"] = "\u220b";
            _entityReferenceTable["prod"] = "\u220f";
            _entityReferenceTable["sum"] = "\u2211";
            _entityReferenceTable["minus"] = "\u2212";
            _entityReferenceTable["lowast"] = "\u2217";
            _entityReferenceTable["radic"] = "\u221a";
            _entityReferenceTable["prop"] = "\u221d";
            _entityReferenceTable["infin"] = "\u221e";
            _entityReferenceTable["ang"] = "\u2220";
            _entityReferenceTable["and"] = "\u2227";
            _entityReferenceTable["or"] = "\u2228";
            _entityReferenceTable["cap"] = "\u2229";
            _entityReferenceTable["cup"] = "\u222a";
            _entityReferenceTable["int"] = "\u222b";
            _entityReferenceTable["there4"] = "\u2234";
            _entityReferenceTable["sim"] = "\u223c";
            _entityReferenceTable["cong"] = "\u2245";
            _entityReferenceTable["asymp"] = "\u2248";
            _entityReferenceTable["ne"] = "\u2260";
            _entityReferenceTable["equiv"] = "\u2261";
            _entityReferenceTable["le"] = "\u2264";
            _entityReferenceTable["ge"] = "\u2265";
            _entityReferenceTable["sub"] = "\u2282";
            _entityReferenceTable["sup"] = "\u2283";
            _entityReferenceTable["nsub"] = "\u2284";
            _entityReferenceTable["sube"] = "\u2286";
            _entityReferenceTable["supe"] = "\u2287";
            _entityReferenceTable["oplus"] = "\u2295";
            _entityReferenceTable["otimes"] = "\u2297";
            _entityReferenceTable["perp"] = "\u22a5";
            _entityReferenceTable["sdot"] = "\u22c5";
            _entityReferenceTable["lceil"] = "\u2308";
            _entityReferenceTable["rceil"] = "\u2309";
            _entityReferenceTable["lfloor"] = "\u230a";
            _entityReferenceTable["rfloor"] = "\u230b";
            _entityReferenceTable["lang"] = "\u2329";
            _entityReferenceTable["rang"] = "\u232a";
            _entityReferenceTable["loz"] = "\u25ca";
            _entityReferenceTable["spades"] = "\u2660";
            _entityReferenceTable["clubs"] = "\u2663";
            _entityReferenceTable["hearts"] = "\u2665";
            _entityReferenceTable["diams"] = "\u2666";
            _entityReferenceTable["quot"] = "\u0022";
            _entityReferenceTable["amp"] = "\u0026";
            _entityReferenceTable["lt"] = "\u003c";
            _entityReferenceTable["gt"] = "\u003e";
            _entityReferenceTable["OElig"] = "\u0152";
            _entityReferenceTable["oelig"] = "\u0153";
            _entityReferenceTable["Scaron"] = "\u0160";
            _entityReferenceTable["scaron"] = "\u0161";
            _entityReferenceTable["Yuml"] = "\u0178";
            _entityReferenceTable["circ"] = "\u02c6";
            _entityReferenceTable["tilde"] = "\u02dc";
            _entityReferenceTable["ensp"] = "\u2002";
            _entityReferenceTable["emsp"] = "\u2003";
            _entityReferenceTable["thinsp"] = "\u2009";
            _entityReferenceTable["zwnj"] = "\u200c";
            _entityReferenceTable["zwj"] = "\u200d";
            _entityReferenceTable["lrm"] = "\u200e";
            _entityReferenceTable["rlm"] = "\u200f";
            _entityReferenceTable["ndash"] = "\u2013";
            _entityReferenceTable["mdash"] = "\u2014";
            _entityReferenceTable["lsquo"] = "\u2018";
            _entityReferenceTable["rsquo"] = "\u2019";
            _entityReferenceTable["sbquo"] = "\u201a";
            _entityReferenceTable["ldquo"] = "\u201c";
            _entityReferenceTable["rdquo"] = "\u201d";
            _entityReferenceTable["bdquo"] = "\u201e";
            _entityReferenceTable["dagger"] = "\u2020";
            _entityReferenceTable["Dagger"] = "\u2021";
            _entityReferenceTable["permil"] = "\u2030";
            _entityReferenceTable["lsaquo"] = "\u2039";
            _entityReferenceTable["rsaquo"] = "\u203a";
            _entityReferenceTable["euro"] = "\u20ac";
        }
        private static String ReplaceMatchEvaluator(Match m)
        {
            if (m.Groups[1].Value[0] == '#')
            {
                // 数値参照
                try
                {
                    Char c;
                    if (m.Groups["dig"].Value[0] == 'x')
                    {
                        c = Convert.ToChar(Int32.Parse(m.Groups["dig"].Value.Substring(1), System.Globalization.NumberStyles.HexNumber));
                    }
                    else
                    {
                        c = Convert.ToChar(Int32.Parse(m.Groups["dig"].Value));
                    }
                    return c.ToString();
                }
                catch (OverflowException) {}
                catch (FormatException) {}

                return "?";
            }
            else
            {
                // 文字実体参照
                if (_entityReferenceTable.ContainsKey(m.Groups["char"].Value))
                {
                    return _entityReferenceTable[m.Groups["char"].Value];
                }
                else
                {
                    return "&" + m.Groups["char"].Value+";";
                }
            }
        }
    }
}
