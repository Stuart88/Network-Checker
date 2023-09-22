namespace NetworkChecker
{
	internal class DnsServer
	{
		#region Public Constructors

		public DnsServer(string name, string dns)
		{
			Name = name;
			Dns = dns;
		}

		#endregion Public Constructors

		#region Public Properties

		public string Dns { get; set; }
		public string Name { get; set; }

		#endregion Public Properties

		#region Public Methods

		public override string ToString()
		{
			return $"{Name} ({Dns})";
		}

		#endregion Public Methods
	}
}