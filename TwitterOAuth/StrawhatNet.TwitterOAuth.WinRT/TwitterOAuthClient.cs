using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace StrawhatNet.TwitterOAuth.WinRT
{
    public class ConsumerInfo
    {
        public string OAuthConsumerKey { get; set; }
        public string OAuthConsumerSecret { get; set; }
        public string OAuthCallbackUrl { get; set; }
    }

    public class RequestTokenResponse
    {
        public string OAuthRequestToken { get; set; }
        public string OAuthRequestTokenSecret { get; set; }
        public bool Confirmed { get; set; }
    }

    public class RedirectToAuthResponse
    {
        public string OAuthVerifier { get; set; }
    }

    public class AccessTokenResponse
    {
        public string OAuthAccessToken { get; set; }
        public string OAuthAccessTokenSecret { get; set; }
        public string ScreenName { get; set; }
        public string UserId { get; set; }

        public AccessTokenResponse()
        {

        }

        public AccessTokenResponse(
            string oauthAccessToken,
            string oauthAccessTokenSecret)
        {
            this.OAuthAccessToken = oauthAccessToken;
            this.OAuthAccessTokenSecret = oauthAccessTokenSecret;
        }

    }

    public class TwitterOAuthClient
    {
        private static readonly string OAuthVerifierParameter = "oauth_verifier";
        private static readonly string StatusParameter = "status";
        private static readonly string OAuthTokenParameter = "oauth_token";

        private static readonly string RequestTokenUrl = "https://api.twitter.com/oauth/request_token";
        //private static readonly string AuthorizeUrl = "https://api.twitter.com/oauth/authorize";
        private static readonly string AuthenticateUrl = "https://api.twitter.com/oauth/authenticate";
        private static readonly string AccessTokenUrl = "https://api.twitter.com/oauth/access_token";
        private static readonly string UpdateStatusUrl = "https://api.twitter.com/1.1/statuses/update.json";

        private ConsumerInfo consumerInfo;

        public TwitterOAuthClient(
            ConsumerInfo consumerInfo)
        {
            this.consumerInfo = consumerInfo;
        }

        private string CreateRequestString(SortedDictionary<string, string> requestParameters)
        {
            return string.Join("&",
                requestParameters.Select(p => string.Format("{0}={1}", p.Key, p.Value.EscapeDataStringRFC3986())));
        }

        // https://dev.twitter.com/docs/auth/authorizing-request
        private async Task<string> PostRequest(
            string requestUrl,
            SortedDictionary<string, string> requestParams,
            IHttpFilter handler)
        {
            string response = String.Empty;

            using (var client = new HttpClient(handler))
            {
                IHttpContent content = new HttpFormUrlEncodedContent(requestParams);

                // var contentType = "application/x-www-form-urlencoded";
                // var content = new HttpStringContent(this.CreateRequestString(requestParams), Windows.Storage.Streams.UnicodeEncoding.Utf8, contentType);

                var httpResponse = await client.PostAsync(new Uri(requestUrl), content);

                response = httpResponse.Content.ToString();
            }

            return response;
        }


        // https://dev.twitter.com/docs/api/1/post/oauth/request_token
        public async Task<RequestTokenResponse> GetRequestToken()
        {
            SortedDictionary<string, string> requestParams = new SortedDictionary<string, string>();
            var handler = new AuthHeaderFilter(this.consumerInfo, RequestTokenUrl, requestParams);
            string response = await PostRequest(RequestTokenUrl, requestParams, handler);

            // result:
            // oauth_token=UC9NrLRmLge4dBFExQOQ4UGuv6ytvSfpdpgfkoTaBU&oauth_token_secret=TmlhJukIcjPbIsLNRw4vQUFVYjot5jaXABFHiR18&oauth_callback_confirmed=true
            MatchCollection mc = Regex.Matches(response,
                @"^oauth_token=(?<token>[^&]+?)&oauth_token_secret=(?<secret>[^&]+?)&oauth_callback_confirmed=(?<confirmed>.+?)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            RequestTokenResponse result;
            if (mc.Count == 1)
            {
                Match m = mc[0];
                var requestToken = m.Groups["token"].Value;
                var requestTokenSecret = m.Groups["secret"].Value;
                var confirmed = m.Groups["confirmed"].Value;

                result = new RequestTokenResponse()
                {
                    OAuthRequestToken = requestToken,
                    OAuthRequestTokenSecret = requestTokenSecret,
                    Confirmed = Boolean.Parse(confirmed),
                };
            }
            else
            {
                throw new TwitterOAuthException(String.Format("invalid response: {0}", response));
            }

            return result;
        }

        // geturl
        public string GetRedirectAuthUrl(RequestTokenResponse requestToken)
        {
            string redirectUrl
                = String.Format("{0}?{1}={2}", AuthenticateUrl, OAuthTokenParameter, requestToken.OAuthRequestToken);
            return redirectUrl;
        }

        // http://app.strawhat.net/api/KumatterSimpleClient?oauth_token=6AEcvWTJ4AmCkelRF7gaIsdMHCqE8P3zfkyUsZGNWcg&oauth_verifier=RL1q0iQCwYIFN50tRwz4brYUrbsaFKyLh47xdB4mg
        public RedirectToAuthResponse ParseRedirectToAuthResponse(string result)
        {
            MatchCollection mc = Regex.Matches(result,
                @"(?<url>[^?]+?)?oauth_token=(?<token>[^&]+?)&oauth_verifier=(?<verifier>.+?)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (mc.Count == 1)
            {
                Match m = mc[0];
                return new RedirectToAuthResponse()
                {
                    OAuthVerifier = mc[0].Groups["verifier"].Value,
                };
            }
            else
            {
                throw new TwitterOAuthException(String.Format("invalid response: {0}", result));
            }
        }

        public async Task<AccessTokenResponse> GetAccessToken(
            RequestTokenResponse requestToken,
            RedirectToAuthResponse redirectToAuth)
        {
            SortedDictionary<string, string> requestParams = new SortedDictionary<string, string>();
            requestParams.Add(OAuthVerifierParameter, redirectToAuth.OAuthVerifier);

            var handler = new AuthHeaderFilter(this.consumerInfo, requestToken, AccessTokenUrl, requestParams);
            var response = await PostRequest(AccessTokenUrl, requestParams, handler);

            // result:
            // oauth_token=15244042-e0MDgWWgvAsJDVGTrwTbjKJYUOsnUdhNpqccnhBX8&oauth_token_secret=atrv7CXDUJymKxwVyanAVOTAXz0VyDtAx2aXNo9VVfq4i&user_id=15244042&screen_name=kumar0001
            MatchCollection mc = Regex.Matches(response,
                @"^oauth_token=(?<token>[^&]+?)&oauth_token_secret=(?<secret>[^&]+?)&user_id=(?<userid>[^&]+?)&screen_name=(?<screenname>.+?)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (mc.Count == 1)
            {
                Match m = mc[0];

                return new AccessTokenResponse()
                {
                    OAuthAccessToken = m.Groups["token"].Value,
                    OAuthAccessTokenSecret = m.Groups["secret"].Value,
                    UserId = m.Groups["userid"].Value,
                    ScreenName = m.Groups["screenname"].Value,
                };
            }
            else
            {
                throw new TwitterOAuthException(String.Format("invalid response: {0}", response));
            }
        }

        // https://dev.twitter.com/docs/api/1.1/post/statuses/update
        public async Task<string> UpdateStatus(
            AccessTokenResponse oAuthAcessToken,
            string tweetString)
        {
            string response = String.Empty;

            SortedDictionary<string, string> requestParams = new SortedDictionary<string, string>();
            requestParams.Add(StatusParameter, tweetString);

            var handler = new AuthHeaderFilter(this.consumerInfo, oAuthAcessToken, UpdateStatusUrl, requestParams);
            response = await PostRequest(UpdateStatusUrl, requestParams, handler);

            // result
            // {"created_at":"Sun Jun 29 00:44:49 +0000 2014","id":483048408681152513,"id_str":"483048408681152513","text":"\u30c6\u30b9\u30c8PG\u304b\u3089Tweet at 2014\/06\/29 09:44:39.061 (1509971026)","source":"\u003ca href=\"http:\/\/app.strawhat.net\/TwitterSimpleClient\" rel=\"nofollow\"\u003eTwitterSimpleClient\u003c\/a\u003e","truncated":false,"in_reply_to_status_id":null,"in_reply_to_status_id_str":null,"in_reply_to_user_id":null,"in_reply_to_user_id_str":null,"in_reply_to_screen_name":null,"user":{"id":15244042,"id_str":"15244042","name":"\u306e\u3076@\u3054\u6ce8\u6587\u306f\u3072\u3050\u307e\u3067\u3059\u304b","screen_name":"kumar0001","location":"Japan\/Nagoya","description":"\u30c6\u30c7\u30a3\u30d9\u30a2\u5236\u4f5c\uff08\u8da3\u5473\uff09\u3068SE\uff08\u672c\u696d\uff09\u3084\u3063\u3066\u308b\u4e09\u5341\u8def\u7537\u3067\u3059\u3002\u30c4\u30a4\u30fc\u30c8\u306e\u30b8\u30e3\u30f3\u30eb\u304c\u3044\u308d\u3044\u308d\u306a\u306e\u3067\u3054\u6ce8\u610f\u3092\u3000\u300a\u30ad\u30fc\u30ef\u30fc\u30c9\u300b\u30c6\u30c7\u30a3\u30d9\u30a2\u5236\u4f5c, \u718a(\u30b0\u30ea\u30ba\u30ea\u30fc\u30fb\u30a2\u30e1\u30ea\u30ab\u30af\u30ed\u30af\u30de)\uff0f\u30d6\u30bf, A\u30c1\u30e3\u30f3\u30cd\u30eb, IT(C#\/WP8\/Silverlight\/WPF), \u30ed\u30fc\u30bc\u30f3\u30e1\u30a4\u30c7\u30f3, \u72fc\u3068\u9999\u8f9b\u6599","url":"http:\/\/t.co\/kZjLv4nMsC","entities":{"url":{"urls":[{"url":"http:\/\/t.co\/kZjLv4nMsC","expanded_url":"http:\/\/www.strawhat.net\/","display_url":"strawhat.net","indices":[0,22]}]},"description":{"urls":[]}},"protected":false,"followers_count":658,"friends_count":749,"listed_count":48,"created_at":"Thu Jun 26 13:50:28 +0000 2008","favourites_count":6947,"utc_offset":32400,"time_zone":"Tokyo","geo_enabled":false,"verified":false,"statuses_count":87359,"lang":"ja","contributors_enabled":false,"is_translator":false,"is_translation_enabled":false,"profile_background_color":"9AE4E8","profile_background_image_url":"http:\/\/abs.twimg.com\/images\/themes\/theme16\/bg.gif","profile_background_image_url_https":"https:\/\/abs.twimg.com\/images\/themes\/theme16\/bg.gif","profile_background_tile":false,"profile_image_url":"http:\/\/pbs.twimg.com\/profile_images\/461630005156392960\/BARks8-s_normal.jpeg","profile_image_url_https":"https:\/\/pbs.twimg.com\/profile_images\/461630005156392960\/BARks8-s_normal.jpeg","profile_banner_url":"https:\/\/pbs.twimg.com\/profile_banners\/15244042\/1398896172","profile_link_color":"0084B4","profile_sidebar_border_color":"BDDCAD","profile_sidebar_fill_color":"DDFFCC","profile_text_color":"333333","profile_use_background_image":true,"default_profile":false,"default_profile_image":false,"following":false,"follow_request_sent":false,"notifications":false},"geo":null,"coordinates":null,"place":null,"contributors":null,"retweet_count":0,"favorite_count":0,"entities":{"hashtags":[],"symbols":[],"urls":[],"user_mentions":[]},"favorited":false,"retweeted":false,"lang":"ja"}

            return response;
        }
    }
}
