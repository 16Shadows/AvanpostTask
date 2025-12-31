using System.Text.Json.Serialization;

namespace TR.Connector.ServiceMockup.Dto
{
	public class CreateUserData : UserData
	{
		[JsonRequired, JsonPropertyName("password")]
		public required string Password { get; set; }
	}
}
