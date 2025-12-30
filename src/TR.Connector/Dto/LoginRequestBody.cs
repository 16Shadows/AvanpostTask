using System.Text.Json.Serialization;

namespace TR.Connector.Dto
{
	internal class LoginRequestBody
	{
		public LoginRequestBody(string login, string password)
		{
			Login = login ?? throw new ArgumentNullException(nameof(login));
			Password = password ?? throw new ArgumentNullException(nameof(password));
		}

		[JsonRequired, JsonPropertyName("login")]
		public string Login { get; set; }

		[JsonRequired, JsonPropertyName("password")]
		public string Password { get; set; }
	}
}
