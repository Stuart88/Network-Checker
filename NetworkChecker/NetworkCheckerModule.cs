using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkChecker
{
	public enum NetworkState
	{
		Connected,
		NotConnected,
		Restarting,
		Unknown
	}

	public class NetworkStateChangeEventArgs : EventArgs
	{
		#region Public Properties

		public NetworkState State { get; set; } = NetworkState.Unknown;
		public string StateText { get; set; } = string.Empty;

		#endregion Public Properties
	}

	public class NotificationEventArgs : EventArgs
	{
		#region Public Properties

		public string Message { get; set; } = string.Empty;

		#endregion Public Properties
	}

	public class StatusMessageUpdateEventArgs : EventArgs
	{
		#region Public Properties

		public string Message { get; set; } = string.Empty;

		#endregion Public Properties
	}

	public class WifiResetEventArgs : EventArgs
	{
		#region Public Properties

		public int Count { get; set; } = 0;

		#endregion Public Properties
	}

	internal static class DnsServers
	{
		#region Public Fields

		public static readonly DnsServer Cloudflare = new DnsServer("Cloudflare", "1.1.1.1");
		public static readonly DnsServer ComodoSecureDNS = new DnsServer("Comodo Secure DNS", "8.26.56.26");
		public static readonly DnsServer Google = new DnsServer("Google", "8.8.8.8");
		public static readonly DnsServer Level3 = new DnsServer("Level3", "4.2.2.2");
		public static readonly DnsServer NortonConnectSafe = new DnsServer("Norton ConnectSafe", "199.85.126.10");
		public static readonly DnsServer OpenDNS = new DnsServer("OpenDNS", "208.67.222.222");
		public static readonly DnsServer Quad9 = new DnsServer("Quad9", "9.9.9.9");
		public static readonly DnsServer Verisign = new DnsServer("Verisign", "64.6.64.6");
		public static readonly DnsServer YandexDNS = new DnsServer("Yandex.DNS", "77.88.8.8");

		#endregion Public Fields
	}

	internal class NetworkCheckerModule
	{
		#region Private Fields

		private DnsServer[] _dnsServers =
		{
			DnsServers.Google,
			DnsServers.Cloudflare,
			DnsServers.OpenDNS,
			DnsServers.Quad9,
			DnsServers.Verisign,
			DnsServers.Level3,
			DnsServers.ComodoSecureDNS,
			DnsServers.NortonConnectSafe,
			DnsServers.YandexDNS
		};

		private int _resetCount = 0;
		private bool _running = false;

		#endregion Private Fields

		#region Public Events

		public event EventHandler<NetworkStateChangeEventArgs> NetworkStateChanged;

		public event EventHandler<NotificationEventArgs> NotificationSent;

		public event EventHandler<StatusMessageUpdateEventArgs> StatusMessageUpdated;

		public event EventHandler<WifiResetEventArgs> WifiReset;

		#endregion Public Events

		#region Public Methods

		public void ForceResetWifi()
		{
			ResetNetworkAdapter();
		}

		public void Start()
		{
			OnNotificationSent("Network Checker Started");

			_running = true;

			_ = Task.Run(async () =>
			{
				OnStatusMessageUpdated("Internet Connection Monitor - Started");

				bool isConnected = await CheckInternetConnectionAsync(_dnsServers);

				if (isConnected)
				{
					OnStatusMessageUpdated("Internet connection is up.");
					OnNetworkStateChanged("Connected", NetworkState.Connected);
					OnStatusMessageUpdated("Monitoring...");
				}

				while (_running)
				{
					isConnected = await CheckInternetConnectionAsync(_dnsServers);
					if (!isConnected)
					{
						OnStatusMessageUpdated("Internet connection is down.");
						OnNetworkStateChanged("Not Connected", NetworkState.NotConnected);
						CheckWiFiAdapterStatus();
						ResetNetworkAdapter();
					}

					Thread.Sleep(1000); // Wait for 1 second before checking again
				}
			});
		}

		public void Stop()
		{
			_running = false;
			OnStatusMessageUpdated("Internet Connection Monitor - Stopped");
			OnNetworkStateChanged("Stopped", NetworkState.Unknown);
			OnNotificationSent("Network Checker Stopped");
		}

		#endregion Public Methods

		#region Protected Methods

		protected virtual void OnNetworkStateChanged(string text, NetworkState state)
		{
			EventHandler<NetworkStateChangeEventArgs> handler = NetworkStateChanged;

			if (handler != null)
			{
				handler(this, new NetworkStateChangeEventArgs
				{
					StateText = text,
					State = state
				});
			}
		}

		protected virtual void OnNotificationSent(string message)
		{
			EventHandler<NotificationEventArgs> handler = NotificationSent;

			if (handler != null)
			{
				handler(this, new NotificationEventArgs
				{
					Message = message
				});
			}
		}

		protected virtual void OnStatusMessageUpdated(string message)
		{
			EventHandler<StatusMessageUpdateEventArgs> handler = StatusMessageUpdated;

			if (handler != null)
			{
				handler(this, new StatusMessageUpdateEventArgs
				{
					Message = message
				});
			}
		}

		protected virtual void OnWifiReset(int resetCount)
		{
			EventHandler<WifiResetEventArgs> handler = WifiReset;

			if (handler != null)
			{
				handler(this, new WifiResetEventArgs
				{
					Count = resetCount
				});
			}
		}

		#endregion Protected Methods

		#region Private Methods

		private async Task<bool> CheckInternetConnectionAsync(DnsServer[] dnsServers)
		{
			var cts = new CancellationTokenSource();
			var tasks = new Task<bool>[dnsServers.Length];

			for (int i = 0; i < dnsServers.Length; i++)
			{
				tasks[i] = PingDnsServerAsync(dnsServers[i], cts.Token);
			}

			while (tasks.Length > 0)
			{
				Task<bool> completedTask = await Task.WhenAny(tasks);
				tasks = tasks.Where(t => t != completedTask).ToArray(); // Remove completed task

				if (completedTask.Result)
				{
					cts.Cancel(); // Cancel all other tasks

					// At least one dns server connection succeeded, so return true
					OnNetworkStateChanged("Connected", NetworkState.Connected);
					return true;
				}
			}

			// Returns false if all tested dns servers fail to connect,
			// i.e. highly likely that wifi adapter has a problem!

			return false;
		}

		private void CheckWiFiAdapterStatus()
		{
			ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter");
			ManagementObjectCollection adapters = searcher.Get();

			foreach (ManagementObject adapter in adapters)
			{
				string? adapterName = adapter.Properties["Name"].Value as string;

				string? adapterStatus = adapter.Properties["NetConnectionStatus"].Value as string;

				if (!string.IsNullOrEmpty(adapterName) && adapterName.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase))
				{
					OnStatusMessageUpdated($"Wi-Fi Adapter: {adapterName}");
					OnStatusMessageUpdated($"Status: {adapterStatus ?? "Unknown"}");
					break;
				}
			}
		}

		private async Task<bool> PingDnsServerAsync(DnsServer dnsServer, CancellationToken cancellationToken)
		{
			using (var ping = new Ping())
			{
				try
				{
					PingReply reply = await ping.SendPingAsync(dnsServer.Dns);

					return reply.Status == IPStatus.Success;
				}
				catch (PingException pex)
				{
					// this.OnStatusMessageUpdated($"PingException\nServer: {dnsServer}\nException Message: {pex.Message}");
					return false;
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// Task was canceled
					return false;
				}
			}
		}

		private void ResetNetworkAdapter()
		{
			OnNotificationSent("Resetting Wifi Adapter");
			OnStatusMessageUpdated("Resetting adapter:");
			OnNetworkStateChanged("Restarting Adapter", NetworkState.Restarting);
			string powerShellCommand = "Restart-NetAdapter -Name \"*WiFi*\"";
			OnStatusMessageUpdated($" > > Powershell: {powerShellCommand}");

			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = "powershell.exe",
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				Verb = "runas"
			};

			// Create a new Process and start it
			Process process = new Process
			{
				StartInfo = processStartInfo
			};

			_ = process.Start();
			process.StandardInput.WriteLine(powerShellCommand);
			process.StandardInput.Close();
			string output = process.StandardOutput.ReadToEnd();
			string error = process.StandardError.ReadToEnd();

			process.WaitForExit();

			//if (!string.IsNullOrEmpty(output))
			//{
			//	OnStatusMessageUpdated($"Powershell Output:\n\n {output}\n");
			//}

			if (!string.IsNullOrEmpty(error))
			{
				OnStatusMessageUpdated($"Powershell Error:\n\n {error}");
			}

			process.Close();

			OnStatusMessageUpdated($"Adapter Reset complete");
			OnNotificationSent("Wifi Adapter Reset");
			OnWifiReset(++_resetCount);

			Thread.Sleep(10000); // Wait for 1 second before checking again
		}

		#endregion Private Methods
	}
}