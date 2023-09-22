using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using Windows.Foundation.Collections;

namespace NetworkChecker
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		#region Public Constructors

		public MainWindow()
		{
			this.NetworkChecker = new NetworkCheckerModule();

			this.InitNotifyIcon();

			this.StateChanged += Window_StateChanged;

			InitializeComponent();

			StatusArea.Text = string.Empty;

			this.NetworkChecker.StatusMessageUpdated += NetworkChecker_StatusMessageUpdated;
			this.NetworkChecker.WifiReset += NetworkChecker_WifiReset;
			this.NetworkChecker.NetworkStateChanged += NetworkChecker_NetworkStateChanged;
			this.NetworkChecker.NotificationSent += NetworkChecker_NotificationSent;

			this.Start();
		}

		#endregion Public Constructors

		#region Private Properties

		private NetworkCheckerModule NetworkChecker { get; set; }
		private NotifyIcon NotifyIcon { get; set; }

		#endregion Private Properties

		#region Private Methods

		private void AddConsolelineLine(string newLine)
		{
			this.StatusArea.Text += $"\n > {newLine}";
		}

		private void Clear()
		{
			this.StatusArea.Text = string.Empty;
			this.AddConsolelineLine("Cleared");
			this.AddConsolelineLine("Monitoring...");
		}

		private void ClearButton_Click(object sender, RoutedEventArgs e)
		{
			this.Clear();
		}

		private void CloseApplication()
		{
			// Clean up and close the application
			NotifyIcon.Visible = false;
			NotifyIcon.Dispose();
			System.Windows.Application.Current.Shutdown();
		}

		private void ExitMenuItem_Click(object? sender, EventArgs e)
		{
			// Handle the exit menu item click
			CloseApplication();
		}

		private void HandleToastNotification(string text)
		{
			new ToastContentBuilder()
				.AddText(text)
				.Show();
		}

		private void InitNotifyIcon()
		{
			NotifyIcon = new NotifyIcon();
			NotifyIcon.Icon = new System.Drawing.Icon("network_globe.ico");
			NotifyIcon.Visible = true;
			NotifyIcon.MouseClick += NotifyIcon_MouseClick;

			// Add a context menu (optional)
			var contextMenu = new ContextMenu();

			NotifyIcon.ContextMenuStrip = new ContextMenuStrip()
			{
				BackColor = System.Drawing.Color.Black,
				ForeColor = System.Drawing.Color.White,
				ShowImageMargin = false
			};

			NotifyIcon.ContextMenuStrip.Items.Add("Restart Wifi", null, RestartWifiMenuItem_Click);
			NotifyIcon.ContextMenuStrip.Items.Add("Exit", null, ExitMenuItem_Click);

			ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
		}

		private void NetworkChecker_NetworkStateChanged(object? sender, NetworkStateChangeEventArgs e)
		{
			this.Dispatcher.Invoke(new Action(() =>
			{
				var textColour = e.State switch
				{
					NetworkState.Connected => Colors.DarkGreen,
					NetworkState.NotConnected => Colors.Black,
					NetworkState.Restarting => Colors.CornflowerBlue,
					NetworkState.Unknown => Colors.Black,
					_ => Colors.Black
				};

				NetworkStatus.Text = e.StateText;
				NetworkStatus.Foreground = new SolidColorBrush(textColour);
			}));
		}

		private void NetworkChecker_NotificationSent(object? sender, NotificationEventArgs e)
		{
			this.HandleToastNotification(e.Message);
		}

		private void NetworkChecker_StatusMessageUpdated(object? sender, StatusMessageUpdateEventArgs e)
		{
			this.Dispatcher.Invoke(new Action(() =>
			{
				this.AddConsolelineLine(e.Message);
				this.StatusAreaScrollViewer.ScrollToBottom();
				NotifyIcon.Text = e.Message;
			}));
		}

		private void NetworkChecker_WifiReset(object? sender, WifiResetEventArgs e)
		{
			this.Dispatcher.Invoke(new Action(() =>
			{
				ResetCountText.Text = $"Reset Count: {e.Count}";
				this.AddConsolelineLine($"Monitoring...");
			}));
		}

		private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				this.RestoreWindow();
			}
		}

		private void RestartWifiMenuItem_Click(object? sender, EventArgs e)
		{
			this.NetworkChecker.ForceResetWifi();
		}

		private void RestoreWindow()
		{
			this.WindowState = WindowState.Normal;
			this.Show();
			this.Activate();
		}

		private void Start()
		{
			this.NetworkChecker.Start();
			StartButton.Visibility = Visibility.Collapsed;
			StopButton.Visibility = Visibility.Visible;
		}

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			this.Start();
		}

		private void Stop()
		{
			this.NetworkChecker.Stop();
			StopButton.Visibility = Visibility.Collapsed;
			StartButton.Visibility = Visibility.Visible;
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			this.Stop();
		}

		private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
		{
			// Obtain the arguments from the notification
			ToastArguments args = ToastArguments.Parse(toastArgs.Argument);

			// Obtain any user input (text boxes, menu selections) from the notification
			ValueSet userInput = toastArgs.UserInput;

			this.Dispatcher.Invoke(new Action(() =>
			{
				this.RestoreWindow();
			}));
		}

		private void Window_StateChanged(object? sender, EventArgs e)
		{
			if (this.WindowState == WindowState.Minimized)
			{
				this.Hide(); // Hide the main window
				NotifyIcon.Visible = true; // Show the tray icon
			}
		}

		#endregion Private Methods
	}
}