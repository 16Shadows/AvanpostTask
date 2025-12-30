using System.Text.Json.Serialization;

namespace TR.Connector.Dto
{
	internal class CreateUserData : UserData
	{
		[JsonRequired, JsonPropertyName("password")]
		public required string Password { get; set; }
	}
}
