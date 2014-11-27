using StrawhatNet.TwitterOAuth.WinRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    public sealed partial class MainPage : Page
    {
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

        private async void RedirectToAuthButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.requestToken != null)
            {
                var redirectUrl = this.twitterOAuth.GetRedirectAuthUrl(this.requestToken);
                this.WriteLog(String.Format("Redirect URI:{0}", redirectUrl));


                Uri StartUri = new Uri(redirectUrl);
                Uri EndUri = new Uri(TwitterOAuthConstants.OAuthCallbackUrl);
                var WebAuthenticationResult
                    = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, StartUri, EndUri);

                if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    var response = WebAuthenticationResult.ResponseData.ToString();

                    this.oauthVerifier = twitterOAuth.ParseRedirectToAuthResponse(response);

                    this.GetAccessTokenButton.IsEnabled = true;
                    this.WriteLog(String.Format("OAuthVerifier:{0}", this.oauthVerifier.OAuthVerifier));
                }
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


    }
}
