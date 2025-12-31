namespace TR.Connector
{
	internal class ConnectorApiException : Exception
	{
		public ConnectorApiException(string message) : base(message) { }
		public ConnectorApiException(string message, Exception innerException) : base(message, innerException) { }
	}
}
