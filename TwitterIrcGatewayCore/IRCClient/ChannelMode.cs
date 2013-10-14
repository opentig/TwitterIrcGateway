// $Id: ChannelMode.cs 399 2008-05-31 03:55:39Z tomoyo $
using System;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace Misuzilla.Net.Irc
{
    public class ChannelMode
    {
        public static ChannelMode[] Parse(String modes)
        {
            Debug.WriteLine(String.Format("parse-mode: {0}", modes));

            List<ChannelMode> channelModes = new List<ChannelMode>();
            String[] param = modes.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (Int32 i = 0; i < param.Length; i++)
            {
                String s = param[i];
                if (s[0] == '+' || s[0] == '-')
                {
                    Boolean addFlag = true;
                    Int32 paramPosition = 0;
                    Boolean hasParam = false;
                    for (Int32 j = 0; j < s.Length; j++)
                    {
                        switch (s[j])
                        {
                            case '+':
                                addFlag = true;
                                continue;
                            case '-':
                                addFlag = false;
                                continue;

                            // パラメータあり mode
                            case 'O':
                            case 'o':
                            case 'b':
                            case 'e':
                            case 'I':
                            case 'l':
                            case 'v':
                            case 'k':
                                if (s[j] == 'l' && !addFlag)
                                {
                                    // -l だけパラメータを取らない。
                                    hasParam = false;
                                }
                                else
                                {
                                    paramPosition++;
                                    hasParam = true;
                                }
                                break;
                            // パラメータなし mode
                            default:
                                //
                                hasParam = false;
                                break;
                        }

                        if (!Enum.IsDefined(typeof(ChannelModeTypes), (Int32)s[j]))
                        {
                            Debug.WriteLine(String.Format("Unknown mode: {0}", s[j]));
                            continue;
                        }

                        // パラメータがあるはずなのにない場合
                        if (hasParam && (i + paramPosition) > (param.Length - 1))
                        {
                            // throw new ChannelModeParseException();
                            Debug.WriteLine(String.Format("Channel mode: {0} / Invalid Parameters", s[j]));
                            continue;
                        }
                        
                        ChannelMode cmode = new ChannelMode()
                        {
                            Mode      = (ChannelModeTypes)s[j],
                            Parameter = (hasParam ? param[i + paramPosition] : String.Empty),
                            IsRemove  = !addFlag
                        };
                        channelModes.Add(cmode);

                        Debug.WriteLine(String.Format("  --> {0}", cmode.ToString()));
                    }
                }
            }

            return channelModes.ToArray();
        }

        public ChannelModeTypes Mode { get; set; }

        public Boolean IsRemove { get; set; }

        public String Parameter { get; set; }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(IsRemove ? "-" : "+");
            sb.Append((Convert.ToChar(Mode)).ToString());
            //sb.Append("(");
            //sb.Append(Mode.ToString());
            //sb.Append(")");
            if (Parameter != String.Empty)
            {
                sb.Append(" ");
                sb.Append(Parameter);
            }

            return sb.ToString();
        }
    }
}