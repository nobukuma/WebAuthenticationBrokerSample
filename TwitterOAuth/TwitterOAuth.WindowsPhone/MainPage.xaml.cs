using StrawhatNet.TwitterOAuth.WinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Web;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace StrawhatNet.TwitterOAuth
{
    /// <summary>
    /// Frame 内へナビゲートするために利用する空欄ページ。
    /// </summary>
    public sealed partial class MainPage : Page, IWebAuthenticationContinuable
    {
        //private static readonly string OAuthVerifierParameter = "oauth_verifier";
        //private static readonly string StatusParameter = "status";
        //private static readonly string OAuthTokenParameter = "oauth_token";

        private TwitterOAuthClient twitterOAuth;
        private RequestTokenResponse requestToken;
        private RedirectToAuthResponse oauthVerifier;
        private AccessTokenResponse accessToken;

        public MainPage()
        {
            this.InitializeComponent();

            this.twitterOAuth = new TwitterOAuthClient(new ConsumerInfo()
            {
                OAuthCallbackUrl = TwitterOAuthConstants.OAuthCallbackUrl,
                OAuthConsumerKey = TwitterOAuthConstants.ConsumerKey,
                OAuthConsumerSecret = TwitterOAuthConstants.ConsumerSecret,
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            this.RedirectToAuthButton.IsEnabled = false;
            this.UpdateStateButton.IsEnabled = false;
            this.GetAccessTokenButton.IsEnabled = false;

            // XXX
            // {ms-app://s-1-15-2-4268621673-3455213802-466465341-2795654515-3951074169-3820925526-636053919/}
            var uri = WebAuthenticationBroker.GetCurrentApplicationCallbackUri();

            return;
        }

        private async void GetRequestTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RequestTokenResponse response = await this.twitterOAuth.GetRequestToken();
                this.WriteLog(string.Format("GetRequestToken: Token={0}, Secret={1}",
                    response.OAuthRequestToken, response.OAuthRequestTokenSecret));

                this.requestToken = response;
                this.RedirectToAuthButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                this.WriteLog(String.Format("ReqestToken: exception: {0}", ex.Message));
            }
            return;
        }


        // http://msdn.microsoft.com/ja-jp/library/dn631755.aspx
        // http://dotnet.dzone.com/articles/how-use
        // http://www.cloudidentity.com/blog/2014/04/16/calling-office365-api-from-a-windows-phone-8-1-app/
        // http://msicc.net/?p=4054
        private void RedirectToAuthButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.requestToken != null)
            {
                var redirectUrl = this.twitterOAuth.GetRedirectAuthUrl(this.requestToken);
                this.WriteLog(String.Format("Redirect URI:{0}", redirectUrl));

                Uri StartUri = new Uri(redirectUrl);
                Uri EndUri = new Uri(TwitterOAuthConstants.OAuthCallbackUrl);
                //WebAuthenticationBroker.AuthenticateAndContinue(StartUri, EndUri, null, WebAuthenticationOptions.None);

                WebAuthenticationBroker.AuthenticateAndContinue(StartUri, EndUri);
            }
        }

        private async void GetAccessTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.oauthVerifier != null)
            {
                var response = await this.twitterOAuth.GetAccessToken(this.requestToken, this.oauthVerifier);
                this.WriteLog(String.Format("AccessToken: Token={0}, Secret={1}, ScreenName={2}, UserId={3}",
                    response.OAuthAccessToken, response.OAuthAccessTokenSecret,
                    response.ScreenName, response.UserId));

                this.accessToken = response;
                this.UpdateStateButton.IsEnabled = true;
            }             
        }

        private async void UpdateStateButton_Click(object sender, RoutedEventArgs e)
        {
            //// for test
            //this.accessToken = new AccessTokenResponse()
            //{
            //    OAuthAccessToken = TwitterOAuthConstants.AccessToken,
            //    OAuthAccessTokenSecret = TwitterOAuthConstants.AccessTokenSecret,
            //};

            DateTime now = DateTime.Now;
            int randomNumber = new Random().Next();
            var text = String.Format("test tweet at {0} {1} ({2})",
                now.ToString("yyyy/MM/dd"), now.ToString("HH:mm:ss.fff"),
                randomNumber);

            string result = await this.twitterOAuth.UpdateStatus(this.accessToken, text);
            this.WriteLog(String.Format("UpdateStatus:{0}", result));

            return;
        }

        private void WriteLog(string log)
        {
            this.LogTextBox.Text += log + "\r";
        }

        /*
         * Signature base string 
POST&https%3A%2F%2Fapi.twitter.com%2F1.1%2Fstatuses%2Fupdate.json&oauth_consumer_key%3DLK2akZl6e6BMEAPDMxGueajSp%26oauth_nonce%3D768ae59f0653f5b289fe5305c1a55033%26oauth_signature_method%3DHMAC-SHA1%26oauth_timestamp%3D1405193441%26oauth_token%3D15244042-LXQPX5ILb2TpOvXc5gBgXsIFvMQo9Zvw0DyA7i2ve%26oauth_version%3D1.0%26status%3DMaybe%2520he%2527ll%2520finally%2520find%2520his%2520keys.%2520%2523peterfalk 
 
Authorization header 
Authorization: OAuth oauth_consumer_key="LK2akZl6e6BMEAPDMxGueajSp", oauth_nonce="768ae59f0653f5b289fe5305c1a55033", oauth_signature="2VcjU41Ce7LaSHTW3iWjv%2FvLKhE%3D", oauth_signature_method="HMAC-SHA1", oauth_timestamp="1405193441", oauth_token="15244042-LXQPX5ILb2TpOvXc5gBgXsIFvMQo9Zvw0DyA7i2ve", oauth_version="1.0" 
 

         */

        // http://d.hatena.ne.jp/kaorun/20140518/1400392292
        // http://peterfoot.net/2014/04/14/webauthenticationbroker-wp8/
        // http://blogs.msdn.com/b/wsdevsol/archive/2014/05/08/using-the-andcontinue-methods-in-windows-phone-silverlight-8-1-apps.aspx

        // http://msdn.microsoft.com/ja-jp/library/dn631755.aspx
        // http://msdn.microsoft.com/ja-jp/library/dn596094.aspx
        public void ContinueWebAuthentication(WebAuthenticationBrokerContinuationEventArgs args)
        {
            WebAuthenticationResult result = args.WebAuthenticationResult;

            // Process the authentication result	
            switch (result.ResponseStatus)
            {
                case WebAuthenticationStatus.Success:
                    {
                        var response = result.ResponseData.ToString();

                        this.oauthVerifier = twitterOAuth.ParseRedirectToAuthResponse(response);

                        this.GetAccessTokenButton.IsEnabled = true;
                        this.WriteLog(String.Format("OAuthVerifier:{0}", this.oauthVerifier.OAuthVerifier));

                        break;
                    }
                case WebAuthenticationStatus.UserCancel:
                    break;
                case WebAuthenticationStatus.ErrorHttp:
                    {
                        var response = result.ResponseErrorDetail.ToString();
                        this.WriteLog(String.Format("Error:ContinueWebAuthentication:{0}", response));
                        break;
                    }
                default:
                    break;
            }
        }
    }
}
