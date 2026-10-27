using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CyborgVitals
{
    sealed partial class App : Application
    {
        // Each Game Bar view retains its own connection for that window's lifetime.
        private XboxGameBarWidget widget;
        public App() { InitializeComponent(); }
        private Frame TransparentFrame() => new Frame { Background = new SolidColorBrush(Colors.Transparent) };
        protected override void OnActivated(IActivatedEventArgs args)
        {
            var activation = args as XboxGameBarWidgetActivatedEventArgs;
            if (activation == null || !activation.IsLaunchActivation) return;
            if (activation.AppExtensionId != "Vitals") return;
            var frame = TransparentFrame();
            Window.Current.Content = frame;
            var instance = new XboxGameBarWidget(activation, Window.Current.CoreWindow, frame);
            frame.Background = new SolidColorBrush(Colors.Transparent);
            widget = instance;
            frame.Navigate(typeof(VitalsPage), instance);
            Window.Current.Closed += (s, e) => {
                if (object.ReferenceEquals(widget, instance)) widget = null;
            };
            Window.Current.Activate();
        }
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (args.PrelaunchActivated) return;
            var frame = Window.Current.Content as Frame;
            if (frame == null) { frame = TransparentFrame(); Window.Current.Content = frame; }
            frame.Background = new SolidColorBrush(Colors.Transparent);
            if (frame.Content == null) frame.Navigate(typeof(VitalsPage));
            Window.Current.Activate();
        }
    }
}
