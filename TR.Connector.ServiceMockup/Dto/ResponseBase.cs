using System.Text.Json.Serialization;

namespace TR.Connector.ServiceMockup.Dto
{
	public class ResponseBase
	{
		[JsonRequired, JsonPropertyName("success")]
		public bool Success { get; set; }

		[JsonRequired, JsonPropertyName("errorText")]
		public string? ErrorText { get; set; }
	}
}
