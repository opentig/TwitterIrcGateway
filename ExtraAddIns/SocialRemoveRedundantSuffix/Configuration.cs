using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.SocialRemoveRedundantSuffix
{
    public class Configuration : IConfiguration
    {
        public Configuration()
        {
            BlackListCache = "";
            BlackListUrl = "http://svn.coderepos.org/share/platform/twitterircgateway/suffixesblacklist.txt";
        }

        public String BlackListCache;
        public String BlackListUrl;
    }
}
