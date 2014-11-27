using System;
using System.Collections.Generic;
using System.Linq; 
using System.Text;
using System.Threading.Tasks;

namespace StrawhatNet.TwitterOAuth.WinRT
{
    public static class StringUtil
    {
        // EscapeDataStringのツイート、RFC3986、RFC2396あたりが関係
        public static string EscapeDataStringRFC3986(this string src)
        {
            return Uri.EscapeDataString(src)
                .Replace("!", "%21")
                .Replace("*", "%2A")
                .Replace("'", "%27")
                .Replace("(", "%28")
                .Replace(")", "%29");
        }
    }
}
