using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace StrawhatNet.TwitterOAuth.WinRT
{
    public sealed class AuthHeaderFilter : IHttpFilter
    {
        private ConsumerInfo consumerInfo;
        private RequestTokenResponse requestToken;
        private AccessTokenResponse accessToken;

        private string requestUrl;
        private IDictionary<string, string> requestParameters;

        private IHttpFilter filter;

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
            IDictionary<string, string> requestParameters)
            : this(consumerInfo, null, null, requestUrl, requestParameters, new HttpBaseProtocolFilter())
        {
        }

        public AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            RequestTokenResponse requestToken,
            string requestUrl,
            IDictionary<string, string> requestParameters)
            : this(consumerInfo, requestToken, null, requestUrl, requestParameters, new HttpBaseProtocolFilter())
        {
        }

        public AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            AccessTokenResponse accessToken,
            string requestUrl,
            IDictionary<string, string> requestParameters)
            : this(consumerInfo, null, accessToken, requestUrl, requestParameters, new HttpBaseProtocolFilter())
        {
        }

        private AuthHeaderFilter(
            ConsumerInfo consumerInfo,
            RequestTokenResponse requestToken,
            AccessTokenResponse accessToken,
            string requestUrl,
            IDictionary<string, string> requestParameters,
            IHttpFilter filter)
        {
            this.consumerInfo = consumerInfo;
            this.requestToken = requestToken;
            this.accessToken = accessToken;
            this.requestUrl = requestUrl;
            this.requestParameters = requestParameters;
            this.filter = filter;
        }

        public IAsyncOperationWithProgress<HttpResponseMessage, HttpProgress> SendRequestAsync(HttpRequestMessage request)
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
            string signature = GenerateSignature(signatureParameters, this.requestUrl,
                this.consumerInfo.OAuthConsumerSecret,
                this.accessToken != null ? this.accessToken.OAuthAccessTokenSecret : null);
            AddPercentEncodedItem(authorizationParameters, OAuthSignatureParameter, signature);

            // 認証用パラメータからリクエストヘッダーを生成
            string header = GenerateAuthorizationHeader(authorizationParameters);
            request.Headers[AuthorizationHeader] = header;

            return this.filter.SendRequestAsync(request);
        }

        public void Dispose()
        {
            return;
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
            string signature = String.Empty;

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

            //キー文字列をバッファに変換
            IBuffer KeyMaterial = CryptographicBuffer.ConvertStringToBinary(stringKey, BinaryStringEncoding.Utf8);

            //MACアルゴリズムを指定
            MacAlgorithmProvider macAlgorithm = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha1);

            //デジタル署名用キーの生成
            CryptographicKey MacKey = macAlgorithm.CreateKey(KeyMaterial);

            //ベース文字列をバッファに変換
            IBuffer DataToBeSigned = CryptographicBuffer.ConvertStringToBinary(baseStr, BinaryStringEncoding.Utf8);

            //ベース文字列をデジタル署名
            IBuffer SignatureBuffer = CryptographicEngine.Sign(MacKey, DataToBeSigned);

            //Base64エンコードにてシグネチャを取得
            signature = CryptographicBuffer.EncodeToBase64String(SignatureBuffer);

            return signature;
        }

        private string GenerateNonce()
        {
            var rand = new Random();
            return (rand.Next(1000000000)).ToString();
        }

        private string GenerateTimeStamp()
        {
            var value = Math.Round((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds);
            return value.ToString();
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
