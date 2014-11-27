using StrawhatNet.TwitterOAuth.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

#if WINDOWS_PHONE_APP
using Windows.Phone.UI.Input;
#endif

// 空のアプリケーション テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234227 を参照してください

namespace StrawhatNet.TwitterOAuth
{
    /// <summary>
    /// 既定の Application クラスに対してアプリケーション独自の動作を実装します。
    /// </summary>
    public sealed partial class App : Application
    {
#if WINDOWS_PHONE_APP
        private TransitionCollection transitions;
        public static ContinuationManager ContinuationManager { get; private set; }
#endif

        /// <summary>
        /// 単一アプリケーション オブジェクトを初期化します。これは、実行される作成したコードの
        /// 最初の行であり、main() または WinMain() と論理的に等価です。
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
#if WINDOWS_PHONE_APP
            ContinuationManager = new ContinuationManager();
#endif
        }

        /// <summary>
        /// アプリケーションがエンド ユーザーによって正常に起動されたときに呼び出されます。他のエントリ ポイントは、
        /// アプリケーションが特定のファイルを開くために呼び出されたときに
        /// 検索結果やその他の情報を表示するために使用されます。
        /// </summary>
        /// <param name="e">起動要求とプロセスの詳細を表示します。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            this.CreateRootFrame();

            //// ウィンドウに既にコンテンツが表示されている場合は、アプリケーションの初期化を繰り返さずに、
            //// ウィンドウがアクティブであることだけを確認してください
            //if (rootFrame == null)
            //{
            //    // ナビゲーション コンテキストとして動作するフレームを作成し、最初のページに移動します
            //    rootFrame = new Frame();

            //    // TODO: この値をアプリケーションに適切なキャッシュ サイズに変更します
            //    rootFrame.CacheSize = 1;

            //    if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
            //    {
            //        //TODO: 以前中断したアプリケーションから状態を読み込みます。
            //    }

            //    // フレームを現在のウィンドウに配置します
            //    Window.Current.Content = rootFrame;
            //}

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame.Content == null)
            {
#if WINDOWS_PHONE_APP
                // スタートアップのターンスタイル ナビゲーションを削除します。
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;
#endif

                // ナビゲーションの履歴スタックが復元されていない場合、最初のページに移動します。
                // このとき、必要な情報をナビゲーション パラメーターとして渡して、新しいページを
                // 作成します
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // 現在のウィンドウがアクティブであることを確認します
            Window.Current.Activate();
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        /// アプリを起動した後のコンテンツの移行を復元します。
        /// </summary>
        /// <param name="sender">ハンドラーがアタッチされたオブジェクト。</param>
        /// <param name="e">ナビゲーション イベントの詳細。</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }
#endif

        /// <summary>
        /// アプリケーションの実行が中断されたときに呼び出されます。アプリケーションの状態は、
        /// アプリケーションが終了されるのか、メモリの内容がそのままで再開されるのか
        /// わからない状態で保存されます。
        /// </summary>
        /// <param name="sender">中断要求の送信元。</param>
        /// <param name="e">中断要求の詳細。</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            await SuspensionManager.SaveAsync();
#if WINDOWS_PHONE_APP
            ContinuationManager.MarkAsStale();
#endif

            deferral.Complete();
        }

        private void CreateRootFrame()
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame != null)
                return;

            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            //Associate the frame with a SuspensionManager key                                
            SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

            // Set the default language
            //rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

            // Place the frame in the current Window
            Window.Current.Content = rootFrame;
        }

 #if WINDOWS_PHONE_APP
        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            Frame frame = Window.Current.Content as Frame;
            if (frame == null)
            {
                return;
            }

            if (frame.CanGoBack)
            {
                frame.GoBack();
                e.Handled = true;
            }
        }

        protected async override void OnActivated(IActivatedEventArgs e)
        {
            base.OnActivated(e);

            CreateRootFrame();

            // Restore the saved session state only when appropriate
            if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
            {
                try
                {
                    await SuspensionManager.RestoreAsync();
                }
                catch (SuspensionManagerException)
                {
                    //Something went wrong restoring state.
                    //Assume there is no state and continue
                }
            }

            //Check if this is a continuation
            var continuationEventArgs = e as IContinuationActivatedEventArgs;
            if (continuationEventArgs != null)
            {
                ContinuationManager.Continue(continuationEventArgs);
            }

            Window.Current.Activate();
        }
#endif
    }
}