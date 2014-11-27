using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using StrawhatNet.TwitterOAuth.PCL;

namespace StrawhatNet.TwitterOAuth.WPFSample
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODO: ConsumerKey, ConsumerSecret, OAuthCallbackUrlを設定
        public static readonly string ConsumerKey = "";
        public static readonly string ConsumerSecret = "";
        private static readonly string OAuthCallbackUrl = "http://example.net/api/TwitterOAuthSample";

        private TwitterOAuthClient twitterOAuth;
        private RequestTokenResponse requestToken;
        private RedirectToAuthResponse oauthVerifier;
        private AccessTokenResponse accessToken;

        public MainWindow()
        {
            InitializeComponent();

            this.twitterOAuth = new TwitterOAuthClient(new ConsumerInfo()
                {
                    OAuthCallbackUrl = OAuthCallbackUrl,
                    OAuthConsumerKey = ConsumerKey,
                    OAuthConsumerSecret = ConsumerSecret,
                });
            this.WebBrowser.Visibility = System.Windows.Visibility.Collapsed;
        }

        private async void GetRequestTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RequestTokenResponse response = await this.twitterOAuth.GetRequestToken();
                this.WriteLog(string.Format("ReqestToken: Token={0}, Secret={1}",
                    response.OAuthRequestToken, response.OAuthRequestTokenSecret));

                this.requestToken = response;
            }
            catch (Exception ex)
            {
                this.WriteLog(String.Format("ReqestToken: exception: {0}", ex.Message));
            }
            return;
        }

        private void RedirectToAuthButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.requestToken != null)
            {
                this.WebBrowser.Visibility = System.Windows.Visibility.Visible;

                var response = this.twitterOAuth.GetRedirectUserUrl(this.requestToken);
                this.WriteLog(String.Format("Redirect URI:{0}", response));

                this.WebBrowser.Navigate(response);
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

                return;
            }
        }

        private async void UpdateStateButton_Click(object sender, RoutedEventArgs e)
        {
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

        //WebBrowser_Navigated:https://api.twitter.com/oauth/authorize?oauth_token=jgokD0HJa8YYL4jk0HgGIp00h5fGusXBwlwmpGJu5s
        //WebBrowser_Navigated:https://api.twitter.com/oauth/authorize
        //WebBrowser_Navigated:http://app.strawhat.net/api/KumatterSimpleClient?oauth_token=jgokD0HJa8YYL4jk0HgGIp00h5fGusXBwlwmpGJu5s&oauth_verifier=3mjYKwm0oTD42E5VcZpQ7WnSyhkMzERdWxUTK4JS0S0
        private void WebBrowser_Navigated(object sender, NavigationEventArgs e)
        {
            this.WriteLog(String.Format("WebBrowser_Navigated:{0}", e.Uri.ToString()));

            if (e.Uri.AbsoluteUri.StartsWith(OAuthCallbackUrl))
            {
                var response = this.twitterOAuth.ParseRedirectToAuthResponse(e.Uri.AbsoluteUri);
                this.WriteLog(String.Format("OAuthVerifier:{0}", response.OAuthVerifier));
                this.oauthVerifier = response;

                this.WebBrowser.Visibility = System.Windows.Visibility.Collapsed;
            }

            return;
        }



    }
}
