using System.Text.Json.Serialization;

namespace TR.Connector.ServiceMockup.Dto
{
	public class TokenData
	{
		[JsonRequired, JsonPropertyName("access_token")]
		public required string AccessToken { get; set; }
		
		[JsonRequired, JsonPropertyName("expires_in")]
		public required int ExpiresIn { get; set; }
	}
}
