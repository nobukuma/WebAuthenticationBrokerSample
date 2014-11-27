using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;

namespace StrawhatNet.TwitterOAuth.PCL
{
    public class AuthHeaderFilter : DelegatingHandler
    {
        private ConsumerInfo consumerInfo;
        private RequestTokenResponse requestToken;
        private AccessTokenResponse accessToken;

        private string requestUrl;
        private SortedDictionary<string, string> requestParameters;

        private static readonly string OAuthConsumerKeyParameter = "oauth_consumer_key";
        private static readonly string OAuthNounceParameter = "oauth_nonce";
        private static readonly string OAuthSignatureMethodParameter = "oauth_signature_method";
        private static readonly string OAuthTimestampParameter = "oauth_timestamp";
        private static readonly string OAuthVersionParameter = "oauth_version";
        private static readonly string OAuthSignatureParameter = "oauth_signature";
        private static readonly string OAuthTokenParameter = "oauth_token";

        private static readonly string OAuthSignatureMethod = "HMAC-SHA1";
        private static readonly string OAuthVersion = "1.0";
        private static readonly string OAuthCallbackParameter = "oauth_callback";

        private static readonly string AuthorizationHeader = "Authorization";

        public AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            string requestUrl,
            SortedDictionary<string, string> requestParameters)
            : this(consumerInfo, null, null, requestUrl, requestParameters, new HttpClientHandler())
        {
        }

        //public AuthHeaderFilter(
        //    ConsumerInfo consumerInfo,
        //    string requestUrl,
        //    SortedDictionary<string, string> requestParameters,
        //    HttpMessageHandler handler)
        //    : this(consumerInfo, null, null, requestUrl, requestParameters, handler)
        //{
        //}

        public AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            RequestTokenResponse requestToken,
            string requestUrl,
            SortedDictionary<string, string> requestParameters)
            : this(consumerInfo, requestToken, null, requestUrl, requestParameters, new HttpClientHandler())
        {
        }

        //public AuthHeaderFilter(
        //    ConsumerInfo consumerInfo,
        //    RequestTokenResponse requestToken,
        //    string requestUrl,
        //    SortedDictionary<string, string> requestParameters,
        //    HttpMessageHandler handler)
        //    : this(consumerInfo, requestToken, null, requestUrl, requestParameters, handler)
        //{
        //}

        public AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            AccessTokenResponse accessToken,
            string requestUrl,
            SortedDictionary<string, string> requestParameters)
            : this(consumerInfo, null, accessToken, requestUrl, requestParameters, new HttpClientHandler())
        {
        }

        //public AuthHeaderFilter(
        //    ConsumerInfo consumerInfo,
        //    AccessTokenResponse accessToken,
        //    string requestUrl,
        //    SortedDictionary<string, string> requestParameters,
        //    HttpMessageHandler handler)
        //    : this(consumerInfo, null, accessToken, requestUrl, requestParameters, handler)
        //{
        //}

        private AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            RequestTokenResponse requestToken,
            AccessTokenResponse accessToken,
            string requestUrl,
            SortedDictionary<string, string> requestParameters,
            HttpMessageHandler handler)
            : base(handler)
        {
            this.consumerInfo = consumerInfo;
            this.requestToken = requestToken;
            this.accessToken = accessToken;
            this.requestUrl = requestUrl;
            this.requestParameters = requestParameters;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SortedDictionary<string, string> authorizationParameters = new SortedDictionary<string, string>();

            // 認証用パラメーターの追加
            AddPercentEncodedItem(authorizationParameters, OAuthConsumerKeyParameter, this.consumerInfo.OAuthConsumerKey);
            AddPercentEncodedItem(authorizationParameters, OAuthNounceParameter, GenerateNonce());
            AddPercentEncodedItem(authorizationParameters, OAuthSignatureMethodParameter, OAuthSignatureMethod);
            AddPercentEncodedItem(authorizationParameters, OAuthTimestampParameter, GenerateTimeStamp());
            AddPercentEncodedItem(authorizationParameters, OAuthVersionParameter, OAuthVersion);

            if (this.accessToken != null && this.accessToken.OAuthAccessToken != null)
            {
                AddPercentEncodedItem(authorizationParameters, OAuthTokenParameter, this.accessToken.OAuthAccessToken);
            }
            else if (this.requestToken != null && this.requestToken.OAuthRequestToken != null)
            {
                AddPercentEncodedItem(authorizationParameters, OAuthTokenParameter, this.requestToken.OAuthRequestToken);
            }
            else
            {
                AddPercentEncodedItem(authorizationParameters, OAuthCallbackParameter, this.consumerInfo.OAuthCallbackUrl);
            }

            // Signatureの計算では認証用パラメータを使う
            SortedDictionary<string, string> signatureParameters = new SortedDictionary<string, string>();
            foreach (var kvp in authorizationParameters)
            {
                signatureParameters.Add(kvp.Key, kvp.Value);
            }

            // Signatureの計算ではさらにリクエストパラメータも使う
            foreach (var kvp in requestParameters)
            {
                AddPercentEncodedItem(signatureParameters, kvp.Key, kvp.Value);
            }

            // Signature用パラメータからシグネチャを生成して認証用パラメータに追加
            string signature = GenerateSignature(signatureParameters, this.requestUrl, this.consumerInfo.OAuthConsumerSecret,
                this.accessToken != null ? this.accessToken.OAuthAccessTokenSecret : null);
            AddPercentEncodedItem(authorizationParameters, OAuthSignatureParameter, signature);

            // 認証用パラメータからリクエストヘッダーを生成
            string header = GenerateAuthorizationHeader(authorizationParameters);
            request.Headers.Add(AuthorizationHeader, header);

            // 元々の SendAsync を呼び出す
            var response = base.SendAsync(request, cancellationToken);
            return response;
        }

        // https://dev.twitter.com/docs/auth/implementing-sign-twitter
        private void AddPercentEncodedItem(SortedDictionary<string, string> dictionary, string key, string keyValue)
        {
            dictionary.Add(key.EscapeDataStringRFC3986(), keyValue.EscapeDataStringRFC3986());
        }

        // https://dev.twitter.com/docs/auth/creating-signature
        private string GenerateSignature(SortedDictionary<string, string> paramDictionary,
            string reqestUrl,
            string oAuthConsumerSecret,
            string oAuthTokenSecret = null)
        {
            //パラメータディクショナリ内の要素を結合しシグネチャのベースとなる文字列を生成
            string baseStrParams = String.Empty;
            foreach (var kvp in paramDictionary.OrderBy(kvp => kvp.Key))
            {
                baseStrParams += (baseStrParams.Length > 0 ? "&" : String.Empty) + kvp.Key + "=" + kvp.Value;
            }
            string baseStr = "POST&" + reqestUrl.EscapeDataStringRFC3986() + "&" + baseStrParams.EscapeDataStringRFC3986();

            //デジタル署名用キーを生成するためのキー文字列を生成
            string stringKey = oAuthConsumerSecret.EscapeDataStringRFC3986() + "&";
            if (!String.IsNullOrEmpty(oAuthTokenSecret))
            {
                stringKey += oAuthTokenSecret.EscapeDataStringRFC3986();
            }

            byte[] keyBytes = Encoding.UTF8.GetBytes(stringKey);
            HMACSHA1 hmacsha1 = new HMACSHA1(keyBytes);

            byte[] inDataBytes = Encoding.UTF8.GetBytes(baseStr);
            MemoryStream ms = new MemoryStream(inDataBytes);


            byte[] outDataBytes = hmacsha1.ComputeHash(inDataBytes);
   
            //Base64エンコードにてシグネチャを取得
            string signature = Convert.ToBase64String(outDataBytes, 0, outDataBytes.Length);

            return signature;
        }

        private string GenerateNonce()
        {
            var rand = new Random();
            return (rand.Next(1000000000)).ToString();
        }

        private string GenerateTimeStamp()
        {
            return (Math.Round((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds)).ToString();
        }

        private string GenerateAuthorizationHeader(SortedDictionary<string, string> paramDictionary)
        {
            string header = string.Empty;

            string headerParams = String.Empty;
            foreach (var kvp in paramDictionary.OrderBy(kvp => kvp.Key))
            {
                headerParams += (headerParams.Length > 0 ? ", " : string.Empty) + kvp.Key + "=\"" + kvp.Value + "\"";
            }
            header = "OAuth " + headerParams;

            return header;
        }
    }
}
